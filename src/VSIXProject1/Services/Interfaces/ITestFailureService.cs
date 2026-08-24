using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for analyzing test failures through iterative debugging steps.
    /// Supports up to 5 iterations to prevent runaway analysis loops.
    /// </summary>
    public interface ITestFailureService
    {
        /// <summary>
        /// Analyzes a test failure by running the test and capturing diagnostic output.
        /// Enforces maximum 5 iterations to prevent infinite loops.
        /// </summary>
        /// <param name="testPath">Path or identifier of the test to analyze.</param>
        /// <param name="iteration">Current iteration number (0-based). Throws if >= 5.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Test run result with diagnostics and parsed stack frames.</returns>
        /// <exception cref="TestAnalysisException">Thrown if iteration count >= 5.</exception>
        Task<TestRunResult> AnalyzeFailureAsync(string testPath, int iteration, CancellationToken ct = default);
    }
}
