#nullable enable

using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces.Parsers;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Scaffolded Python stack trace parser (gap29_1c).
    /// Future implementation will parse Python traceback and pytest output formats.
    /// </summary>
    public class PythonStackTraceParser : IPythonParser
    {
        public string Name => "Python";

        public bool CanParse(string? input)
        {
            return false; // Deferred to gap29_1c
        }

        public async Task<ParseResult> ParseAsync(string? input)
        {
            return await Task.FromResult(new ParseResult
            {
                Errors = new[] { new ParseError
                {
                    ParserName = Name,
                    ErrorMessage = "Python stack trace parsing deferred to gap29_1c",
                    Severity = ParseErrorSeverity.Error
                }},
                DiagnosticsMessage = "Python parser: not yet implemented (gap29_1c)"
            });
        }
    }
}
