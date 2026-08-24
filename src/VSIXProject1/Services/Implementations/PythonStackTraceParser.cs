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
    /// Parser for Python stack traces from Python runtime and pytest (gap29_1c).
    /// Handles standard traceback format, pytest output, chained exceptions, and multi-line messages.
    /// </summary>
    public class PythonStackTraceParser : IPythonParser
    {
        // Regex to match Python traceback frame: File "path", line N, in function_name
        private static readonly Regex FrameRegex = new Regex(
            @"File\s+[""']([^""'\n]+)[""']\s*,\s*line\s+(\d+)(?:\s*,\s*in\s+([^\n]+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Regex to match exception type and message: ExceptionType: message
        private static readonly Regex ExceptionRegex = new Regex(
            @"^(?<type>\w+(?:Error|Exception|Warning))\s*:\s*(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        // Regex to match pytest assertion errors (E prefix)
        private static readonly Regex PytestPrefixRegex = new Regex(
            @"^E\s+",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Regex to detect chained exceptions
        private static readonly Regex ChainedExceptionRegex = new Regex(
            @"(?:During\s+handling\s+of\s+the\s+above\s+exception|The\s+above\s+exception)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Name => "Python";

        public bool CanParse(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            int detectionScore = 0;

            // Check for File "..." pattern
            if (Regex.IsMatch(input!, @"File\s+[""'][^""'\n]+[""']", RegexOptions.IgnoreCase))
                detectionScore++;

            // Check for line keyword
            if (Regex.IsMatch(input!, @"line\s+\d+", RegexOptions.IgnoreCase))
                detectionScore++;

            // Check for Traceback header
            if (input!.Contains("Traceback") || input.Contains("traceback"))
                detectionScore++;

            // Check for Python exception patterns (Error or Exception suffix)
            if (Regex.IsMatch(input!, @"^\w+(?:Error|Exception)\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                detectionScore++;

            // Check for .py file extensions
            if (Regex.IsMatch(input!, @"\.py[cod]?[""']?\s*,"))
                detectionScore++;

            return detectionScore >= 2;
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
                result.DiagnosticsMessage = "Python parser: input was empty";
                return await Task.FromResult(result);
            }

            var frames = new List<StackTraceFrame>();
            var errors = new List<ParseError>();

            // Store original for inspection
            string workingInput = input!;

            // Strip pytest E prefixes if present
            bool isPytest = PytestPrefixRegex.IsMatch(workingInput);
            if (isPytest)
            {
                workingInput = Regex.Replace(workingInput, @"^E\s+", "", RegexOptions.Multiline);
            }

            // Extract exception type and message from last lines or final exception line
            string? exceptionType = null;
            string? exceptionMessage = null;

            var lines = workingInput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length > 0)
            {
                // Look for exception in last few lines (after stack frames)
                for (int i = lines.Length - 1; i >= Math.Max(0, lines.Length - 3); i--)
                {
                    var line = lines[i]?.Trim();
                    if (!string.IsNullOrEmpty(line))
                    if (!string.IsNullOrEmpty(line))
                    {
                        var exMatch = ExceptionRegex.Match(line);
                        if (exMatch.Success)
                        {
                            exceptionType = exMatch.Groups["type"].Value;
                            exceptionMessage = exMatch.Groups["message"].Value;
                            break;
                        }
                    }
                }
            }

            // Default exception type if not found
            if (string.IsNullOrWhiteSpace(exceptionType))
                exceptionType = "Exception";

            // Parse frames using regex
            var matches = FrameRegex.Matches(workingInput);
            if (matches.Count > 0)
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    string filePath = match.Groups[1].Value;
                    int lineNum = 0;
                    if (int.TryParse(match.Groups[2].Value, out var ln))
                        lineNum = ln;

                    string? functionName = match.Groups[3].Value;
                    if (string.IsNullOrEmpty(functionName))
                        functionName = "<module>";

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        frames.Add(new StackTraceFrame
                        {
                            FrameIndex = frames.Count,
                            FilePath = filePath,
                            MethodName = functionName?.Trim(),
                            LineNumber = lineNum,
                            ExceptionType = frames.Count == 0 ? exceptionType : null,
                            ExceptionMessage = frames.Count == 0 ? exceptionMessage : null,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }

            // Detect chained exceptions
            string? chainedExceptionType = null;
            if (ChainedExceptionRegex.IsMatch(workingInput))
            {
                // Extract chained exception type if present
                var chainedMatch = Regex.Match(workingInput, @"(?:During|The).+?(\w+(?:Error|Exception))\s*:", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (chainedMatch.Success)
                {
                    chainedExceptionType = chainedMatch.Groups[1].Value;
                }
            }

            if (frames.Count == 0)
            {
                errors.Add(new ParseError
                {
                    ParserName = Name,
                    ErrorMessage = "No Python stack frames could be parsed",
                    Severity = ParseErrorSeverity.Warning
                });
            }

            var diagnosticsMessage = $"Python parser: {frames.Count} frames extracted";
            if (isPytest)
                diagnosticsMessage += " (pytest format detected)";
            if (!string.IsNullOrEmpty(chainedExceptionType))
                diagnosticsMessage += $"; chained exception: {chainedExceptionType}";

            result.Frames = frames.ToArray();
            result.Errors = errors.ToArray();
            result.SuccessfulParserName = frames.Count > 0 ? Name : null;
            result.DiagnosticsMessage = diagnosticsMessage;

            return await Task.FromResult(result);
        }
    }
}
