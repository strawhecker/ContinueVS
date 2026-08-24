#nullable enable

using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for parsing stack traces from various programming languages and runtimes.
    /// Orchestrates multiple format-specific parsers using strategy pattern.
    /// Returns structured result with frames and detailed error information.
    /// </summary>
    public interface IStackTraceService
    {
        /// <summary>
        /// Parses a stack trace input and returns structured result.
        /// Attempts multiple parsers in priority order based on format detection.
        /// </summary>
        /// <param name="input">The stack trace text to parse.</param>
        /// <returns>ParseResult with frames and/or errors.</returns>
        Task<ParseResult> ParseAsync(string? input);
    }
}
