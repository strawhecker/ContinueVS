using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                    _logger?.WriteDebug("DebugStrategyGeneratorService: instruction is empty");
                return null;
            }

            try
            {
                // Build LLM prompt
                var prompt = BuildInstrumentationPrompt(instruction, failureContext, targetFile);

                if (_logger != null)
                    _logger?.WriteDebug($"DebugStrategyGeneratorService: generating strategy for: {instruction.Substring(0, Math.Min(50, instruction.Length))}");

                // Call LLM via StreamAsync with ChatMessage format
                // SuppressTools: this generator expects structured TEXT strategy back, not
                // tool calls. Advertising tools caused the model to emit tool_calls instead.
                var messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.User, Content = prompt }
                };

                var strategyText = string.Empty;
                var streamOptions = new StreamOptions { SuppressTools = true };
                await foreach (var chunk in _llmService.StreamAsync(messages, streamOptions, cancellationToken))
                {
                    strategyText += chunk.Content;
                }

                // Parse strategy from LLM response
                var strategy = ParseStrategyFromResponse(strategyText, failureContext, targetFile);

                if (strategy != null)
                {
                    if (_logger != null)
                        _logger?.WriteDebug($"DebugStrategyGeneratorService: strategy generated with {strategy.CodeSnippets.Count} snippets");
                }
                else
                {
                    if (_logger != null)
                        _logger?.WriteDebug("DebugStrategyGeneratorService: failed to parse strategy from LLM response");
                }

                return strategy;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    _logger?.WriteDebug($"DebugStrategyGeneratorService: exception during generation - {ex.Message}");
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
                // Extract the top-level JSON object block using a balanced-brace scan rather than a
                // greedy/non-greedy regex. This is robust to literal '{' / '}' / '[' / ']' / quotes
                // appearing inside string VALUES (e.g. a snippet code like
                // Console.WriteLine($"[Messages_CollectionChanged] ...") which contains ']', quotes
                // and interpolated braces). A regex like @"{[\s\S]*}" stops at the first ']' or is
                // otherwise fooled by nested brackets embedded in strings.
                var jsonText = ExtractTopLevelJsonObject(response);
                if (jsonText == null)
                    return null;

                JObject jsonObj;
                try
                {
                    jsonObj = JObject.Parse(jsonText);
                }
                catch (JsonException)
                {
                    return null;
                }

                if (jsonObj == null)
                    return null;

                var strategy = new InstrumentationStrategy
                {
                    Description = jsonObj["description"]?.Value<string>() ?? "Instrumentation"
                };

                var typeStr = jsonObj["instrumentationType"]?.Value<string>();
                if (Enum.TryParse<InstrumentationType>(typeStr ?? string.Empty, true, out var instrType))
                    strategy.InstrumentationType = instrType;

                strategy.TargetFile = jsonObj["targetFile"]?.Value<string>() ?? targetFile ?? "unknown.cs";
                strategy.Rationale = jsonObj["rationale"]?.Value<string>() ?? string.Empty;

                strategy.CodeSnippets = ExtractSnippets(jsonObj["snippets"] as JArray);

                return strategy.IsValid() ? strategy : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the outermost balanced JSON object from raw LLM output.
        /// Tolerates markdown fences, leading/trailing prose, and nested braces /
        /// brackets inside string values by scanning with a brace-depth counter that
        /// respects JSON string escapes.
        /// </summary>
        private static string? ExtractTopLevelJsonObject(string response)
        {
            int start = response.IndexOf('{');
            if (start < 0)
                return null;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < response.Length; i++)
            {
                char c = response[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        break;
                    case '{':
                        depth++;
                        break;
                    case '}':
                        depth--;
                        if (depth == 0)
                            return response.Substring(start, i - start + 1);
                        break;
                }
            }

            return null;
        }

        /// <summary>
        /// Parses the snippets array using Newtonsoft.Json, which correctly unescapes nested
        /// quotes, brackets and braces inside string values. Plain string values should never
        /// need manual unescaping again.
        /// </summary>
        private List<InstrumentationSnippet> ExtractSnippets(JArray? snippetsArray)
        {
            var snippets = new List<InstrumentationSnippet>();
            if (snippetsArray == null)
                return snippets;

            foreach (var token in snippetsArray)
            {
                if (!(token is JObject snippetObj))
                    continue;

                var lineToken = snippetObj["lineNumber"];
                var lineNum = 0;
                if (lineToken != null && lineToken.Type == JTokenType.Integer)
                {
                    lineNum = lineToken.Value<int>();
                }
                else if (lineToken != null)
                {
                    int.TryParse(lineToken.ToString(), out lineNum);
                }

                var code = snippetObj["code"]?.Value<string>() ?? string.Empty;
                var reason = snippetObj["reason"]?.Value<string>() ?? string.Empty;

                snippets.Add(new InstrumentationSnippet
                {
                    LineNumber = lineNum,
                    Code = code,
                    Reason = reason
                });
            }

            return snippets;
        }
    }
}
