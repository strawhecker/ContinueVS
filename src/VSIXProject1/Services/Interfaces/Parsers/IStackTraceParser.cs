#nullable enable

using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces.Parsers
{
    /// <summary>
    /// Base interface for stack trace parsers implementing the Strategy pattern.
    /// Each parser handles a specific format (e.g., .NET Framework, .NET Core, C++, JavaScript, Python).
    /// </summary>
    public interface IStackTraceParser
    {
        /// <summary>
        /// Unique name of this parser (e.g., "DotNetFramework", "DotNetCore", "CppNative").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Determines if this parser can handle the given input.
        /// Uses heuristic format detection (not fool-proof).
        /// </summary>
        /// <param name="input">The stack trace input to evaluate.</param>
        /// <returns>True if the parser believes it can parse this format.</returns>
        bool CanParse(string? input);

        /// <summary>
        /// Attempts to parse the input stack trace.
        /// Returns a ParseResult with frames and any errors encountered.
        /// </summary>
        /// <param name="input">The stack trace input to parse.</param>
        /// <returns>A ParseResult containing parsed frames and/or errors.</returns>
        Task<ParseResult> ParseAsync(string? input);
    }
}
