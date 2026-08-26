using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Analyzes build/test failures and generates refined code changes to address root causes.
    /// Used during Debug mode execution for intelligent error recovery.
    /// </summary>
    public interface IFailureAnalyzerService
    {
        /// <summary>
        /// Analyzes an error and generates a refinement attempt with LLM-based hypotheses and refined change.
        /// </summary>
        /// <param name="errorOutput">Raw error output from compiler, test runner, or exception handler.</param>
        /// <param name="previousChange">The CodeChange that led to this error (context for refinement).</param>
        /// <param name="sessionContext">Additional context (e.g., file content, build output) for LLM analysis.</param>
        /// <param name="isAutonomousMode">True if running in autonomous mode; false for interactive mode (affects prompt/response behavior).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>RefinementAttempt with analysis, hypotheses, refined change, and confidence score.</returns>
        Task<RefinementAttempt> AnalyzeFailureAsync(
            string errorOutput,
            CodeChange previousChange,
            string sessionContext,
            bool isAutonomousMode,
            CancellationToken cancellationToken = default);
    }
}
