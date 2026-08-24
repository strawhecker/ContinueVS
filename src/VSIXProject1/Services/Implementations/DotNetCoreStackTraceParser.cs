#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces.Parsers;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Parser for .NET Core exception stack traces.
    /// Handles modern .NET Core format with async context support.
    /// </summary>
    public class DotNetCoreStackTraceParser : IDotNetCoreParser
    {
        private static readonly Regex FrameRegex = new Regex(
            @"\s+at\s+(?<method>[^\s]+\([^\)]*\))\s+in\s+(?<file>.+?):line\s+(?<line>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex AsyncFrameRegex = new Regex(
            @"\s+at\s+(?<method>.*?)<(?<async>[^>]+)>(?:\((?<params>[^\)]*)\))?\s+in\s+(?<file>[^:]+):line\s+(?<line>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex ExceptionRegex = new Regex(
            @"(?<type>\S+Exception|\S+Error)(?:\[.*?\])?:\s+(?<message>.+?)(?=\n\s+at|\Z)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);

        public string Name => "DotNetCore";

        public bool CanParse(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            return input!.Contains("  at ") && input!.Contains(" in ");
        }

        public async Task<ParseResult> ParseAsync(string? input)
        {
            var result = new ParseResult();

            if (string.IsNullOrWhiteSpace(input))
            {
                result.Errors = new[] { new ParseError
                {
                    ParserName = Name,
                    ErrorMessage = "Input is null or empty",
                    Severity = ParseErrorSeverity.Error
                }};
                result.DiagnosticsMessage = "DotNetCore parser: input was empty";
                return await Task.FromResult(result);
            }

            try
            {
                var frames = new List<StackTraceFrame>();
                var errors = new List<ParseError>();

                // Normalize line endings (handle both \r\n and literal \\r\\n from test data)
                var normalizedInput = input!.Replace("\\r\\n", "\r\n");

                // Extract exception type and message
                string? exceptionType = null;
                string? exceptionMessage = null;
                var exceptionMatch = ExceptionRegex.Match(normalizedInput);
                if (exceptionMatch.Success)
                {
                    exceptionType = exceptionMatch.Groups["type"]?.Value;
                    exceptionMessage = exceptionMatch.Groups["message"]?.Value?.Trim();
                }

                // Try async frame pattern first (more specific)
                var asyncMatches = AsyncFrameRegex.Matches(normalizedInput);
                var standardMatches = FrameRegex.Matches(normalizedInput);

                var allMatches = new List<(Match match, bool isAsync)>();

                // Add async matches
                foreach (Match match in asyncMatches)
                {
                    allMatches.Add((match, true));
                }

                // Add standard matches that weren't already captured
                foreach (Match match in standardMatches)
                {
                    if (!allMatches.Any(m => m.match.Index == match.Index))
                    {
                        allMatches.Add((match, false));
                    }
                }

                // Sort by index to maintain order
                allMatches = allMatches.OrderBy(m => m.match.Index).ToList();

                if (allMatches.Count == 0)
                {
                    errors.Add(new ParseError
                    {
                        ParserName = Name,
                        ErrorMessage = "No stack frames found matching .NET Core regex patterns",
                        Severity = ParseErrorSeverity.Error
                    });
                }
                else
                {
                    for (int i = 0; i < allMatches.Count; i++)
                    {
                        var (match, isAsync) = allMatches[i];
                        var methodName = match.Groups["method"]?.Value?.Trim();
                        var filePath = match.Groups["file"]?.Value?.Trim();
                        var lineStr = match.Groups["line"].Value.Trim();

                        if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(lineStr))
                        {
                            errors.Add(new ParseError
                            {
                                ParserName = Name,
                                ErrorMessage = $"Frame {i}: incomplete match (missing method, file, or line)",
                                LineContent = match.Value,
                                InputLineNumber = i,
                                Severity = ParseErrorSeverity.Warning
                            });
                            continue;
                        }

                        if (!int.TryParse(lineStr, out var lineNumber))
                        {
                            errors.Add(new ParseError
                            {
                                ParserName = Name,
                                ErrorMessage = $"Frame {i}: line number not an integer: {lineStr}",
                                LineContent = match.Value,
                                InputLineNumber = i,
                                Severity = ParseErrorSeverity.Warning
                            });
                            continue;
                        }

                        frames.Add(new StackTraceFrame
                        {
                            FrameIndex = i,
                            MethodName = methodName,
                            FilePath = filePath,
                            LineNumber = lineNumber,
                            ExceptionType = exceptionType,
                            ExceptionMessage = exceptionMessage,
                            SourceLineContent = match.Value
                        });
                    }
                }

                result.Frames = frames.ToArray();
                result.Errors = errors.ToArray();
                result.SuccessfulParserName = frames.Count > 0 ? Name : null;
                result.DiagnosticsMessage = $"DotNetCore parser: parsed {frames.Count} frames, {errors.Count} errors";

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return await Task.FromResult(new ParseResult
                {
                    Errors = new[] { new ParseError
                    {
                        ParserName = Name,
                        ErrorMessage = $"Unhandled exception during parsing: {ex.Message}",
                        Severity = ParseErrorSeverity.Error
                    }},
                    DiagnosticsMessage = $"DotNetCore parser failed: {ex.Message}"
                });
            }
        }
    }
}
