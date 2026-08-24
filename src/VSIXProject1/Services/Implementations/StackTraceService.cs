#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Interfaces.Parsers;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Orchestrator service for stack trace parsing.
    /// Uses format detection and strategy pattern to parse multiple languages/formats.
    /// </summary>
    public class StackTraceService : IStackTraceService
    {
        private readonly IFormatDetector _formatDetector;

        public StackTraceService(IFormatDetector formatDetector)
        {
            _formatDetector = formatDetector ?? throw new ArgumentNullException(nameof(formatDetector));
        }

        public async Task<ParseResult> ParseAsync(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return new ParseResult
                {
                    Errors = new[] { new ParseError
                    {
                        ParserName = "StackTraceService",
                        ErrorMessage = "Input is null or empty",
                        Severity = ParseErrorSeverity.Error
                    }},
                    DiagnosticsMessage = "StackTraceService: input was empty"
                };
            }

            try
            {
                // Detect format and get parser strategies
                var strategies = await _formatDetector.DetectFormatsAsync(input);

                if (strategies.Count == 0)
                {
                    return new ParseResult
                    {
                        Errors = new[] { new ParseError
                        {
                            ParserName = "StackTraceService",
                            ErrorMessage = "No compatible parsers detected for input format",
                            Severity = ParseErrorSeverity.Error
                        }},
                        DiagnosticsMessage = "StackTraceService: no format detected; no parsers attempted"
                    };
                }

                var aggregatedErrors = new List<ParseError>();
                var allResults = new List<ParseResult>();

                // Try each parser in order of confidence
                foreach (var strategy in strategies)
                {
                    if (strategy.Parser == null)
                        continue;

                    try
                    {
                        var result = await strategy.Parser.ParseAsync(input);
                        allResults.Add(result);

                        // If successful, return immediately
                        if (result.IsSuccessful)
                        {
                            result.DiagnosticsMessage = $"Successfully parsed with {strategy.Parser.Name} " +
                                $"(confidence: {strategy.Confidence:P0}). {result.DiagnosticsMessage}";
                            return result;
                        }

                        // Collect errors from partial success
                        if (result.Errors.Length > 0)
                        {
                            aggregatedErrors.AddRange(result.Errors);
                        }
                    }
                    catch (Exception ex)
                    {
                        aggregatedErrors.Add(new ParseError
                        {
                            ParserName = strategy.Parser.Name,
                            ErrorMessage = $"Exception during parsing: {ex.Message}",
                            Severity = ParseErrorSeverity.Error
                        });
                    }
                }

                // All parsers failed or found partial results
                var failedMessage = $"StackTraceService: all {strategies.Count} parsers failed. " +
                    $"Attempted: {string.Join(", ", strategies.Select(s => $"{s.Parser?.Name}({s.Confidence:P0})"))}. " +
                    $"Total errors: {aggregatedErrors.Count}";

                return new ParseResult
                {
                    Frames = Array.Empty<StackTraceFrame>(),
                    Errors = aggregatedErrors.ToArray(),
                    SuccessfulParserName = null,
                    DiagnosticsMessage = failedMessage
                };
            }
            catch (Exception ex)
            {
                return new ParseResult
                {
                    Errors = new[] { new ParseError
                    {
                        ParserName = "StackTraceService",
                        ErrorMessage = $"Unhandled exception: {ex.Message}",
                        Severity = ParseErrorSeverity.Error
                    }},
                    DiagnosticsMessage = $"StackTraceService catastrophic failure: {ex.Message}"
                };
            }
        }
    }
}
