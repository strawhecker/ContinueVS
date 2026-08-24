#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Services.Interfaces.Parsers;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Format detector using heuristic analysis to identify stack trace formats.
    /// Returns parsers in priority order based on confidence scoring.
    /// </summary>
    public class StackTraceFormatDetector : IFormatDetector
    {
        private readonly IDotNetFrameworkParser _dotNetFrameworkParser;
        private readonly IDotNetCoreParser _dotNetCoreParser;
        private readonly ICppNativeParser _cppNativeParser;
        private readonly IJavaScriptParser _javaScriptParser;
        private readonly IPythonParser _pythonParser;

        public StackTraceFormatDetector(
            IDotNetFrameworkParser dotNetFrameworkParser,
            IDotNetCoreParser dotNetCoreParser,
            ICppNativeParser cppNativeParser,
            IJavaScriptParser javaScriptParser,
            IPythonParser pythonParser)
        {
            _dotNetFrameworkParser = dotNetFrameworkParser ?? throw new ArgumentNullException(nameof(dotNetFrameworkParser));
            _dotNetCoreParser = dotNetCoreParser ?? throw new ArgumentNullException(nameof(dotNetCoreParser));
            _cppNativeParser = cppNativeParser ?? throw new ArgumentNullException(nameof(cppNativeParser));
            _javaScriptParser = javaScriptParser ?? throw new ArgumentNullException(nameof(javaScriptParser));
            _pythonParser = pythonParser ?? throw new ArgumentNullException(nameof(pythonParser));
        }

        public async Task<List<ParserStrategy>> DetectFormatsAsync(string? input)
        {
            var strategies = new List<ParserStrategy>();

            if (string.IsNullOrWhiteSpace(input))
            {
                return await Task.FromResult(strategies);
            }

            // Check .NET Framework format
            if (input!.Contains("  at ") && input.Contains(" in ") && input.Contains("line "))
            {
                // Distinguish between Framework and Core by checking for async markers or newer patterns
                bool hasAsyncMarker = input.Contains("<") && input.Contains(">");
                bool hasModernPattern = input.Contains("at <") || input.Contains("async");

                if (!hasAsyncMarker && !hasModernPattern)
                {
                    strategies.Add(new ParserStrategy
                    {
                        Parser = _dotNetFrameworkParser,
                        Confidence = 0.95,
                        Reason = "Strong match: classic .NET Framework format with 'at ... in ... line' pattern"
                    });
                }

                if (hasAsyncMarker || hasModernPattern)
                {
                    strategies.Add(new ParserStrategy
                    {
                        Parser = _dotNetCoreParser,
                        Confidence = 0.90,
                        Reason = "Strong match: .NET Core format with async context or modern frame layout"
                    });
                }

                if (!hasAsyncMarker && !hasModernPattern)
                {
                    // Secondary attempt as fallback
                    strategies.Add(new ParserStrategy
                    {
                        Parser = _dotNetCoreParser,
                        Confidence = 0.50,
                        Reason = "Fallback: .NET Core parser as secondary attempt"
                    });
                }
                else
                {
                    // Secondary attempt as fallback
                    strategies.Add(new ParserStrategy
                    {
                        Parser = _dotNetFrameworkParser,
                        Confidence = 0.50,
                        Reason = "Fallback: .NET Framework parser as secondary attempt"
                    });
                }
            }

            // Check C++ native format (0x addresses, .cpp/.h files)
            if (input.Contains("0x") || input.Contains(".cpp") || input.Contains(".h"))
            {
                strategies.Add(new ParserStrategy
                {
                    Parser = _cppNativeParser,
                    Confidence = 0.70,
                    Reason = "Medium match: C++ format indicators (hex addresses or .cpp/.h files)"
                });
            }

            // Check JavaScript format (.js/.ts, Error: header, at prefix)
            if ((input.Contains(".js") || input.Contains(".ts")) || 
                input.Contains("Error:") && input.Contains("  at "))
            {
                strategies.Add(new ParserStrategy
                {
                    Parser = _javaScriptParser,
                    Confidence = 0.75,
                    Reason = "Medium-high match: JavaScript format indicators (.js/.ts files or 'Error:' header)"
                });
            }

            // Check Python format (File \"...\", line keyword, Traceback header)
            if (input.Contains("File \"") || (input.Contains("line ") && input.Contains("File ")) || 
                input.Contains("Traceback"))
            {
                strategies.Add(new ParserStrategy
                {
                    Parser = _pythonParser,
                    Confidence = 0.80,
                    Reason = "Medium-high match: Python format indicators (File/line keywords or Traceback header)"
                });
            }

            // Sort by confidence (descending)
            return await Task.FromResult(strategies!.OrderByDescending(s => s.Confidence).ToList());
        }
    }
}
