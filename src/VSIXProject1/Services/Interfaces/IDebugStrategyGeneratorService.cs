using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for generating instrumentation strategies via the LLM.
    /// The LLM decides what instrumentation is needed based on failure context.
    /// </summary>
    public interface IDebugStrategyGeneratorService
    {
        /// <summary>
        /// Generates an instrumentation strategy based on user instruction and failure context.
        /// </summary>
        /// <param name="instruction">User's description of the problem to debug.</param>
        /// <param name="failureContext">Optional additional context (error message, stack trace).</param>
        /// <param name="targetFile">Optional hint for which file to instrument.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Generated strategy, or null if LLM response could not be parsed.</returns>
        Task<InstrumentationStrategy?> GenerateStrategyAsync(
            string instruction,
            string? failureContext = null,
            string? targetFile = null,
            CancellationToken cancellationToken = default);
    }
}
