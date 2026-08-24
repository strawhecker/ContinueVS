#nullable enable

using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces.Parsers;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Scaffolded C++ native stack trace parser (gap29_1a).
    /// Future implementation will parse Windows native exceptions, debugger output, and crash dumps.
    /// </summary>
    public class CppNativeStackTraceParser : ICppNativeParser
    {
        public string Name => "CppNative";

        public bool CanParse(string? input)
        {
            return false; // Deferred to gap29_1a
        }

        public async Task<ParseResult> ParseAsync(string? input)
        {
            return await Task.FromResult(new ParseResult
            {
                Errors = new[] { new ParseError
                {
                    ParserName = Name,
                    ErrorMessage = "C++ native stack trace parsing deferred to gap29_1a",
                    Severity = ParseErrorSeverity.Error
                }},
                DiagnosticsMessage = "CppNative parser: not yet implemented (gap29_1a)"
            });
        }
    }
}
