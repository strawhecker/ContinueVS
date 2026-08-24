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
    /// Parser for .NET Framework exception stack traces.
    /// Handles classic .NET Framework format: "  at MethodName(...) in FilePath:line LineNumber"
    /// </summary>
    public class DotNetFrameworkStackTraceParser : IDotNetFrameworkParser
    {
        private static readonly Regex FrameRegex = new Regex(
            @"\s+at\s+(?<method>[^\s]+\([^\)]*\))\s+in\s+(?<file>.+?):line\s+(?<line>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex ExceptionRegex = new Regex(
            @"(?<type>\S+Exception|\S+Error):\s+(?<message>.+?)(?=\n\s+at|\Z)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);

        public string Name => "DotNetFramework";

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
                result.DiagnosticsMessage = "DotNetFramework parser: input was empty";
                return await Task.FromResult(result);
            }

            try
            {
                var frames = new List<StackTraceFrame>();
                var errors = new List<ParseError>();

                // Normalize line endings (handle both \r\n and literal \\r\\n from test data)
                var normalizedInput = input!.Replace("\\r\\n", "\r\n");

                // Extract exception type and message from first line
                string? exceptionType = null;
                string? exceptionMessage = null;
                var exceptionMatch = ExceptionRegex.Match(normalizedInput);
                if (exceptionMatch.Success)
                {
                    exceptionType = exceptionMatch.Groups["type"]?.Value;
                    exceptionMessage = exceptionMatch.Groups["message"]?.Value?.Trim();
                }

                // Extract frames
                var frameMatches = FrameRegex.Matches(normalizedInput);
                if (frameMatches.Count == 0)
                {
                    errors.Add(new ParseError
                    {
                        ParserName = Name,
                        ErrorMessage = "No stack frames found matching .NET Framework regex pattern",
                        Severity = ParseErrorSeverity.Error
                    });
                }
                else
                {
                    for (int i = 0; i < frameMatches.Count; i++)
                    {
                        var match = frameMatches[i];
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
                result.DiagnosticsMessage = $"DotNetFramework parser: parsed {frames.Count} frames, {errors.Count} errors";

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
                    DiagnosticsMessage = $"DotNetFramework parser failed: {ex.Message}"
                });
            }
        }
    }
}
