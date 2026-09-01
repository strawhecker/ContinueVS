using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IInstructionProcessorService.
    /// Processes debug instructions by calling LLM to generate ordered internal phases.
    /// </summary>
    public class InstructionProcessorService : IInstructionProcessorService
    {
        private readonly ILlmService _llmService;
        private readonly IBridgeLogger? _logger;

        /// <summary>
        /// Initializes a new instance of InstructionProcessorService.
        /// </summary>
        /// <param name="llmService">Service for LLM requests.</param>
        /// <param name="logger">Optional debug logger.</param>
        public InstructionProcessorService(ILlmService llmService, IBridgeLogger? logger = null)
        {
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _logger = logger;
        }

        /// <summary>
        /// Generates ordered internal phases from an execution instruction via LLM interpretation.
        /// </summary>
        public async Task<TestPlan> GenerateInternalPhasesAsync(ExecutionInstruction instruction, CancellationToken cancellationToken = default)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            if (string.IsNullOrWhiteSpace(instruction.Text))
                throw new ArgumentException("Instruction text cannot be empty.", nameof(instruction));

            if (_logger != null)
                await _logger.WriteDebugAsync($"InstructionProcessorService.GenerateInternalPhasesAsync: processing instruction '{instruction.Text}'");

            // Build the LLM prompt
            var prompt = BuildPrompt(instruction);
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = prompt }
            };

            // Stream LLM response and collect into single response string
            var responseBuilder = new StringBuilder();
            try
            {
                await foreach (var chunk in _llmService.StreamAsync(messages, null, cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    responseBuilder.Append(chunk.Content);
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstructionProcessorService.GenerateInternalPhasesAsync: LLM error: {ex.Message}");
                throw new InvalidOperationException("LLM interpretation failed.", ex);
            }

            var llmResponse = responseBuilder.ToString();
            if (string.IsNullOrWhiteSpace(llmResponse))
                throw new InvalidOperationException("LLM returned empty response.");

            // Log raw LLM response for debugging
            if (_logger != null)
                await _logger.WriteDebugAsync($"InstructionProcessorService.GenerateInternalPhasesAsync: Raw LLM response:\n{llmResponse}");

            // Parse the LLM response into phases
            var phases = ParsePhasesFromResponse(llmResponse);

            // Create and return TestPlan
            var testPlan = new TestPlan
            {
                Title = $"Debug Plan for: {instruction.Text.Substring(0, Math.Min(50, instruction.Text.Length))}",
                Phases = phases
            };

            if (_logger != null)
                await _logger.WriteDebugAsync($"InstructionProcessorService.GenerateInternalPhasesAsync: generated {phases.Count} phases");

            return testPlan;
        }

        /// <summary>
        /// Builds the LLM prompt for phase generation.
        /// </summary>
        private string BuildPrompt(ExecutionInstruction instruction)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a debugging assistant. The user has provided a debug request.");
            sb.AppendLine("Generate an ordered list of debug phases (strategy attempts) to investigate and resolve the issue.");
            sb.AppendLine();
            sb.AppendLine("Each phase should be one of these types: Analysis, Breakpoint, Instrumentation, Test, Observation.");
            sb.AppendLine("Analysis: inspect code, logs, runtime state to understand the problem.");
            sb.AppendLine("Breakpoint: set breakpoints and inspect runtime state.");
            sb.AppendLine("Instrumentation: add logging, monitoring, or diagnostic output.");
            sb.AppendLine("Test: run tests to validate or reproduce the issue.");
            sb.AppendLine("Observation: gather data without modifying code.");
            sb.AppendLine();
            sb.AppendLine("Format each phase as:");
            sb.AppendLine("- [TYPE]: [Description]");
            sb.AppendLine();
            sb.AppendLine("User Debug Request:");
            sb.AppendLine(instruction.Text);
            if (!string.IsNullOrWhiteSpace(instruction.Context))
            {
                sb.AppendLine();
                sb.AppendLine("Additional Context:");
                sb.AppendLine(instruction.Context);
            }
            sb.AppendLine();
            sb.AppendLine("Generate the phases:");
            return sb.ToString();
        }

        /// <summary>
        /// Parses phase descriptions from the LLM response.
        /// Expects format: "- [TYPE]: [Description]" but handles variations.
        /// </summary>
        private List<InternalPhase> ParsePhasesFromResponse(string response)
        {
            var phases = new List<InternalPhase>();

            // Match lines starting with "- " followed by a phase type and description
            var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("-"))
                    continue;

                // Try primary pattern: "- [TYPE]: Description" or "- TYPE: Description"
                var match = Regex.Match(trimmed, @"^-\s*\[?(\w+)\]?\s*:?\s*(.+)$", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    // Try fallback pattern for variations like "- TYPE – Description" or "- TYPE Description"
                    match = Regex.Match(trimmed, @"^-\s*(\w+)\s+(.+)$", RegexOptions.IgnoreCase);
                    if (!match.Success)
                        continue;
                }

                var typeStr = match.Groups[1].Value.Trim();
                var description = match.Groups[2].Value.Trim();

                // Try to parse the phase type
                if (!Enum.TryParse<InternalPhaseType>(typeStr, ignoreCase: true, out var phaseType))
                {
                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"InstructionProcessorService.ParsePhasesFromResponse: skipping invalid phase type '{typeStr}' in line '{trimmed}'");
                    continue; // Skip invalid phase types
                }

                var phase = new InternalPhase
                {
                    Type = phaseType,
                    Description = description
                };

                phases.Add(phase);

                if (_logger != null)
                    _ = _logger.WriteDebugAsync($"InstructionProcessorService.ParsePhasesFromResponse: parsed phase {phaseType}: {description}");
            }

            if (phases.Count == 0)
            {
                if (_logger != null)
                    _ = _logger.WriteDebugAsync("InstructionProcessorService.ParsePhasesFromResponse: no valid phases found in response");
                throw new InvalidOperationException("LLM response did not contain valid phases.");
            }

            return phases;
        }
    }
}
