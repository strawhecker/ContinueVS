using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for analyzing test failures through iterative debugging steps.
    /// Iteration limits are enforced by the outer tool orchestrator (gap23_3) via user-configurable MaxToolCalls.
    /// </summary>
    public interface ITestFailureService
    {
        /// <summary>
        /// Analyzes a test failure by running the test and capturing diagnostic output.
        /// Iteration limits are controlled by the user via tool orchestration settings.
        /// </summary>
        /// <param name="testPath">Path or identifier of the test to analyze.</param>
        /// <param name="iteration">Current iteration number (0-based) for logging/context only.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Test run result with diagnostics and parsed stack frames.</returns>
        Task<TestRunResult> AnalyzeFailureAsync(string testPath, int iteration, CancellationToken ct = default);
    }
}

