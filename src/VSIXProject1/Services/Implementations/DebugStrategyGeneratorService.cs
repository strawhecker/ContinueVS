using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Generates instrumentation strategies via LLM interpretation of user instructions.
    /// Follows InstructionProcessorService pattern: async-first, regex parsing, trusts LLM output.
    /// </summary>
    public class DebugStrategyGeneratorService : IDebugStrategyGeneratorService
    {
        private readonly ILlmService _llmService;
        private readonly IBridgeLogger? _logger;

        public DebugStrategyGeneratorService(
            ILlmService llmService,
            IBridgeLogger? logger = null)
        {
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _logger = logger;
        }

        public async Task<InstrumentationStrategy?> GenerateStrategyAsync(
            string instruction,
            string? failureContext = null,
            string? targetFile = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(instruction))
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync("DebugStrategyGeneratorService: instruction is empty");
                return null;
            }

            try
            {
                // Build LLM prompt
                var prompt = BuildInstrumentationPrompt(instruction, failureContext, targetFile);

                if (_logger != null)
                    await _logger.WriteDebugAsync($"DebugStrategyGeneratorService: generating strategy for: {instruction.Substring(0, Math.Min(50, instruction.Length))}");

                // Call LLM via StreamAsync with ChatMessage format
                var messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.User, Content = prompt }
                };

                var strategyText = string.Empty;
                await foreach (var chunk in _llmService.StreamAsync(messages, null, cancellationToken))
                {
                    strategyText += chunk.Content;
                }

                // Parse strategy from LLM response
                var strategy = ParseStrategyFromResponse(strategyText, failureContext, targetFile);

                if (strategy != null)
                {
                    if (_logger != null)
                        await _logger.WriteDebugAsync($"DebugStrategyGeneratorService: strategy generated with {strategy.CodeSnippets.Count} snippets");
                }
                else
                {
                    if (_logger != null)
                        await _logger.WriteDebugAsync("DebugStrategyGeneratorService: failed to parse strategy from LLM response");
                }

                return strategy;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"DebugStrategyGeneratorService: exception during generation - {ex.Message}");
                return null;
            }
        }

        private string BuildInstrumentationPrompt(string instruction, string? failureContext, string? targetFile)
        {
            var prompt = $@"Given the following debug instruction, decide what instrumentation is needed:

Instruction: {instruction}";

            if (!string.IsNullOrEmpty(failureContext))
                prompt += $"\n\nFailure Context:\n{failureContext}";

            if (!string.IsNullOrEmpty(targetFile))
                prompt += $"\n\nTarget File: {targetFile}";

            prompt += @"

Respond with a JSON object in this format:
{
  ""description"": ""Brief description of the instrumentation strategy"",
  ""instrumentationType"": ""ConsoleLog|DebugAssert|NullCheck|TryCatchWrapper|LoggingStatement"",
  ""targetFile"": ""path/to/file.cs"",
  ""rationale"": ""Why this instrumentation is chosen"",
  ""snippets"": [
    {""lineNumber"": 42, ""code"": ""Console.WriteLine(...)"", ""reason"": ""Debug output""},
    {""lineNumber"": 50, ""code"": ""if (x == null) throw new ArgumentNullException(nameof(x));"", ""reason"": ""Null guard""}
  ]
}

Respond only with the JSON object.";

            return prompt;
        }

        private InstrumentationStrategy? ParseStrategyFromResponse(string response, string? failureContext, string? targetFile)
        {
            if (string.IsNullOrWhiteSpace(response))
                return null;

            try
            {
                // Simple regex extraction of JSON block
                var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}", RegexOptions.IgnoreCase);
                if (!jsonMatch.Success)
                    return null;

                var jsonText = jsonMatch.Value;

                // Parse JSON manually (avoiding external dependency on JSON libraries for .NET 4.7.2 compatibility)
                var strategy = new InstrumentationStrategy();

                // Extract fields
                ExtractStringField(jsonText, "description", out var description);
                strategy.Description = description ?? "Instrumentation";

                ExtractStringField(jsonText, "instrumentationType", out var typeStr);
                if (Enum.TryParse<InstrumentationType>(typeStr ?? "ConsoleLog", out var instrType))
                    strategy.InstrumentationType = instrType;

                ExtractStringField(jsonText, "targetFile", out var file);
                strategy.TargetFile = file ?? targetFile ?? "unknown.cs";

                ExtractStringField(jsonText, "rationale", out var rationale);
                strategy.Rationale = rationale ?? string.Empty;

                // Parse snippets array
                var snippets = ExtractSnippetsArray(jsonText);
                strategy.CodeSnippets = snippets;

                if (!strategy.IsValid())
                    return null;

                return strategy;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ExtractStringField(string json, string fieldName, out string? value)
        {
            value = null;
            var pattern = $@"""{fieldName}""\s*:\s*""([^""]*)""";
            var match = Regex.Match(json, pattern);
            if (match.Success && match.Groups.Count > 1)
                value = match.Groups[1].Value;
        }

        private List<InstrumentationSnippet> ExtractSnippetsArray(string json)
        {
            var snippets = new List<InstrumentationSnippet>();

            // Extract snippets array
            var arrayPattern = @"""snippets""\s*:\s*\[([\s\S]*?)\]";
            var arrayMatch = Regex.Match(json, arrayPattern);
            if (!arrayMatch.Success)
                return snippets;

            var arrayContent = arrayMatch.Groups[1].Value;

            // Split by objects (simplified: look for line number patterns)
            var objectPattern = @"\{[^}]*""lineNumber""\s*:\s*(\d+)[^}]*""code""\s*:\s*""([^""]*?)""[^}]*""reason""\s*:\s*""([^""]*)""[^}]*\}";
            var objectMatches = Regex.Matches(arrayContent, objectPattern);

            foreach (Match objMatch in objectMatches)
            {
                if (objMatch.Groups.Count >= 4)
                {
                    int.TryParse(objMatch.Groups[1].Value, out var lineNum);
                    var code = UnescapeString(objMatch.Groups[2].Value);
                    var reason = objMatch.Groups[3].Value;

                    snippets.Add(new InstrumentationSnippet
                    {
                        LineNumber = lineNum,
                        Code = code,
                        Reason = reason
                    });
                }
            }

            return snippets;
        }

        private string UnescapeString(string escaped)
        {
            if (string.IsNullOrEmpty(escaped))
                return escaped;

            return escaped
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
