#nullable enable

using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces.Parsers;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Scaffolded JavaScript/TypeScript stack trace parser (gap29_1b).
    /// Future implementation will parse Node.js, browser, and webpack stack traces with source map support.
    /// </summary>
    public class JavaScriptStackTraceParser : IJavaScriptParser
    {
        public string Name => "JavaScript";

        public bool CanParse(string? input)
        {
            return false; // Deferred to gap29_1b
        }

        public async Task<ParseResult> ParseAsync(string? input)
        {
            return await Task.FromResult(new ParseResult
            {
                Errors = new[] { new ParseError
                {
                    ParserName = Name,
                    ErrorMessage = "JavaScript stack trace parsing deferred to gap29_1b",
                    Severity = ParseErrorSeverity.Error
                }},
                DiagnosticsMessage = "JavaScript parser: not yet implemented (gap29_1b)"
            });
        }
    }
}
