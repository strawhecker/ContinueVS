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
    /// Parser for JavaScript/TypeScript stack traces from Node.js, browsers, and webpack (gap29_1b).
    /// Handles Node.js format, browser console format, async stack traces, and Error objects.
    /// </summary>
    public class JavaScriptStackTraceParser : IJavaScriptParser
    {
        // Node.js/browser frame format: at Object.method (file:line:col) or similar, with optional async prefix
        private static readonly Regex FrameRegex = new Regex(
            @"at\s+(?:async\s+)?([^\s(]+)\s+\(([^:]+):(\d+):(\d+)\)|at\s+(?:async\s+)?([^\s(]+)\s+\(([^:]+):(\d+)\)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Exception type and message: Error: message or ErrorType: message
        private static readonly Regex ExceptionRegex = new Regex(
            @"^(?<type>\w+Error|Error)\s*:\s*(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        // Async stack trace marker
        private static readonly Regex AsyncMarkerRegex = new Regex(
            @"(?:async|await|Promise|then)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Name => "JavaScript";

        public bool CanParse(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            int detectionScore = 0;

            // Check for "at " prefix pattern (Node.js and browsers)
            if (Regex.IsMatch(input, @"\bat\s+", RegexOptions.Multiline))
                detectionScore++;

            // Check for JavaScript/TypeScript file extensions
            if (Regex.IsMatch(input, @"\.(?:js|ts|jsx|tsx|mjs|mts)(?:\s|:|$|[:\)])", RegexOptions.IgnoreCase))
                detectionScore++;

            // Check for "Error:" keyword
            if (Regex.IsMatch(input, @"\b\w+Error\s*:|^Error\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                detectionScore++;

            // Check for async/await patterns
            if (Regex.IsMatch(input, @"\b(?:async|await|Promise|then)\b", RegexOptions.IgnoreCase))
                detectionScore++;

            // Check for function name patterns common in JS (Object.<anonymous>, etc)
            if (Regex.IsMatch(input, @"Object\.<anonymous>|at\s+\S+\s*\("))
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
                result.DiagnosticsMessage = "JavaScript parser: input was empty";
                return await Task.FromResult(result);
            }

            var frames = new List<StackTraceFrame>();
            var errors = new List<ParseError>();

            // Extract exception type and message from first line
            string? exceptionType = null;
            string? exceptionMessage = null;
            var lines = input!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            if (lines.Length > 0)
            {
                var firstLine = lines[0];
                var exceptionMatch = ExceptionRegex.Match(firstLine);
                if (exceptionMatch.Success)
                {
                    exceptionType = exceptionMatch.Groups["type"].Value;
                    exceptionMessage = exceptionMatch.Groups["message"].Value;
                }
            }

            // Default exception type if not found
            if (string.IsNullOrWhiteSpace(exceptionType))
                exceptionType = "Error";

            // Parse frames
            var matches = FrameRegex.Matches(input);
            if (matches.Count > 0)
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];

                    // Extract function name (try group 1 or group 5 depending on regex match)
                    string? functionName = !string.IsNullOrEmpty(match.Groups[1].Value) 
                        ? match.Groups[1].Value 
                        : match.Groups[5].Value;

                    // Extract file path (try group 2 or group 6)
                    string? filePath = !string.IsNullOrEmpty(match.Groups[2].Value)
                        ? match.Groups[2].Value
                        : match.Groups[6].Value;

                    // Extract line number (try group 3 or group 7)
                    int lineNum = 0;
                    if (int.TryParse(!string.IsNullOrEmpty(match.Groups[3].Value) ? match.Groups[3].Value : match.Groups[7].Value, out var ln))
                        lineNum = ln;

                    // Extract column number (only in first format: group 4)
                    int colNum = 0;
                    if (int.TryParse(match.Groups[4].Value, out var cn))
                        colNum = cn;

                    if (!string.IsNullOrEmpty(functionName) && !string.IsNullOrEmpty(filePath))
                    {
                        frames.Add(new StackTraceFrame
                        {
                            FrameIndex = frames.Count,
                            FilePath = filePath,
                            MethodName = functionName,
                            LineNumber = lineNum,
                            ColumnNumber = colNum,
                            ExceptionType = frames.Count == 0 ? exceptionType : null
                        });
                    }
                }
            }

            // If still no frames but looks like it could be JS, record warning
            if (frames.Count == 0)
            {
                errors.Add(new ParseError
                {
                    ParserName = Name,
                    ErrorMessage = "No stack frames matched JavaScript stack trace format",
                    Severity = ParseErrorSeverity.Warning
                });
            }

            result.Frames = frames.ToArray();
            result.Errors = errors.ToArray();
            result.DiagnosticsMessage = frames.Count > 0 
                ? $"JavaScript parser: extracted {frames.Count} frames, exception type: {exceptionType}"
                : "JavaScript parser: no frames extracted";

            return await Task.FromResult(result);
        }
    }
}
