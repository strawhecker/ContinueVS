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
    /// Parser for C++ native stack traces from Windows exceptions, debugger output, and crash dumps (gap29_1a).
    /// Handles MSVC debugger format, mangled names, hex addresses, and Windows mini-dump notation.
    /// </summary>
    public class CppNativeStackTraceParser : ICppNativeParser
    {
        // Regex to match MSVC debugger call stack frames: address module!function [file:line] or similar variants
        private static readonly Regex FrameRegex = new Regex(
            @"(?<address>0x[0-9a-fA-F]+)?\s*(?<module>\w+(?:\.\w+)?)?!(?<function>[^\s\[\]]+)\s*(?:\[(?<file>[^\]]+):(?<line>\d+)\])?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Regex to match frames in debugger call stack without explicit "!" separator
        private static readonly Regex AltFrameRegex = new Regex(
            @"^\s*(?<function>\w+::\w+(?:\([^\)]*\))?|\?\w+@@[^\s]+)\s+(?<file>[A-Za-z]:[^\s:]+):(?<line>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Regex to match hex addresses (0x12345678)
        private static readonly Regex AddressRegex = new Regex(
            @"0x[0-9a-fA-F]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex to detect mangled names (C++ ABI: start with ? or contain @@)
        private static readonly Regex MangledNameRegex = new Regex(
            @"(\?[\w@$]+|[\w:]+@@[\w@$]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex to extract exception type and message from crash dump or debugger output
        private static readonly Regex ExceptionRegex = new Regex(
            @"(?<type>\w+(?:Exception|Error|Fault))\s*:?\s*(?<message>.*?)(?=\n|$|0x)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public string Name => "CppNative";

        public bool CanParse(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Heuristic detection: require 2+ indicators that this is C++ native format
            int detectionScore = 0;

            // Check for hex addresses (0x pattern)
            if (Regex.IsMatch(input, @"0x[0-9a-fA-F]+"))
                detectionScore++;

            // Check for C++ file extensions or Windows module patterns
            if (Regex.IsMatch(input, @"\.(?:cpp|cxx|cc|h|hpp|exe|dll|sys|lib|pdb)(?:\s|:|$|!)", RegexOptions.IgnoreCase))
                detectionScore++;

            // Check for C++ mangled names (? prefix or @@ separator)
            if (Regex.IsMatch(input, @"\?[\w@$]+|[\w:]+@@[\w@$]+"))
                detectionScore++;

            // Check for MSVC debugger module!function pattern
            if (Regex.IsMatch(input, @"\w+!\w+"))
                detectionScore++;

            // Check for Windows API patterns (kernel32, ntdll, msvcrt, etc.)
            if (Regex.IsMatch(input, @"\b(?:kernel32|ntdll|msvcrt|user32|advapi32)\.dll", RegexOptions.IgnoreCase))
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
                result.DiagnosticsMessage = "CppNative parser: input was empty";
                return await Task.FromResult(result);
            }

            var frames = new List<StackTraceFrame>();
            var errors = new List<ParseError>();

            // Extract exception type and message if present
            string? exceptionType = null;
            string? exceptionMessage = null;
            var exceptionMatch = ExceptionRegex.Match(input);
            if (exceptionMatch.Success)
            {
                exceptionType = exceptionMatch.Groups["type"].Value?.Trim();
                exceptionMessage = exceptionMatch.Groups["message"].Value?.Trim();
            }

            // Parse frames from debugger output
            int frameIndex = 0;
            var lines = input!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Try primary regex: address module!function [file:line]
                var match = FrameRegex.Match(line);
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["function"].Value))
                {
                    var frame = ParseFrameFromMatch(match, frameIndex, exceptionType, exceptionMessage);
                    if (frame != null)
                    {
                        frames.Add(frame);
                        frameIndex++;
                        continue;
                    }
                }

                // Try alternative regex: function file:line (for simple format)
                match = AltFrameRegex.Match(line);
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["function"].Value))
                {
                    var frame = ParseFrameFromAltMatch(match, frameIndex, exceptionType, exceptionMessage);
                    if (frame != null)
                    {
                        frames.Add(frame);
                        frameIndex++;
                        continue;
                    }
                }

                // Try simple pattern: 0xADDR function [file:line]
                if (Regex.IsMatch(line, @"0x[0-9a-fA-F]+.*?(\w+::\w+|\?[\w@$]+)"))
                {
                    var simpleMatch = Regex.Match(line, @"(?<address>0x[0-9a-fA-F]+).*?(?<function>\w+::\w+|\?[\w@$]+)");
                    if (simpleMatch.Success)
                    {
                        var frame = new StackTraceFrame
                        {
                            FrameIndex = frameIndex,
                            MethodName = simpleMatch.Groups["function"].Value,
                            ExceptionType = exceptionType,
                            ExceptionMessage = exceptionMessage,
                            Timestamp = DateTime.UtcNow
                        };
                        frames.Add(frame);
                        frameIndex++;
                    }
                }
            }

            // Return result with parsed frames or error if none found
            if (frames.Count > 0)
            {
                result.Frames = frames.ToArray();
                result.SuccessfulParserName = Name;
                result.DiagnosticsMessage = $"CppNative parser: successfully parsed {frames.Count} frame(s)";
            }
            else
            {
                errors.Add(new ParseError
                {
                    ParserName = Name,
                    ErrorMessage = "No C++ native frames detected in input",
                    Severity = ParseErrorSeverity.Warning
                });
                result.Errors = errors.ToArray();
                result.DiagnosticsMessage = "CppNative parser: no frames found";
            }

            return await Task.FromResult(result);
        }

        private StackTraceFrame? ParseFrameFromMatch(Match match, int frameIndex, string? exceptionType, string? exceptionMessage)
        {
            var functionName = match.Groups["function"].Value?.Trim();
            var filePath = match.Groups["file"].Value?.Trim();
            var lineStr = match.Groups["line"].Value?.Trim();

            if (string.IsNullOrWhiteSpace(functionName))
                return null;

            var frame = new StackTraceFrame
            {
                FrameIndex = frameIndex,
                MethodName = functionName,
                FilePath = filePath,
                ExceptionType = exceptionType,
                ExceptionMessage = exceptionMessage,
                Timestamp = DateTime.UtcNow
            };

            if (int.TryParse(lineStr, out var lineNum))
                frame.LineNumber = lineNum;

            // Normalize Windows paths
            if (!string.IsNullOrWhiteSpace(filePath))
                frame.FilePath = UnquotePath(filePath!);

            return frame;
        }

        private StackTraceFrame? ParseFrameFromAltMatch(Match match, int frameIndex, string? exceptionType, string? exceptionMessage)
        {
            var functionName = match.Groups["function"].Value?.Trim();
            var filePath = match.Groups["file"].Value?.Trim();
            var lineStr = match.Groups["line"].Value?.Trim();

            if (string.IsNullOrWhiteSpace(functionName))
                return null;

            var frame = new StackTraceFrame
            {
                FrameIndex = frameIndex,
                MethodName = functionName,
                FilePath = filePath,
                ExceptionType = exceptionType,
                ExceptionMessage = exceptionMessage,
                Timestamp = DateTime.UtcNow
            };

            if (int.TryParse(lineStr, out var lineNum))
                frame.LineNumber = lineNum;

            return frame;
        }

        private string UnquotePath(string path)
        {
            if (path.StartsWith("[") && path.EndsWith("]"))
                return path.Substring(1, path.Length - 2);
            return path;
        }
    }
}
