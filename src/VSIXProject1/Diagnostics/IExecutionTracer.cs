using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContinueVS.Diagnostics
{
    /// <summary>
    /// Service abstraction for recording and retrieving execution trace points.
    /// 
    /// Implementations capture step tokens (t1, t1.1, ..., t45) with timing and context,
    /// enabling post-execution analysis of the bridge initialization pipeline.
    /// </summary>
    public interface IExecutionTracer
    {
        /// <summary>
        /// Records a single trace point immediately.
        /// </summary>
        /// <param name="token">Step token identifier (e.g., "t1.3.4").</param>
        /// <param name="component">Component or service name.</param>
        /// <param name="metadata">Optional contextual metadata (e.g., service name, status).</param>
        void RecordTracePoint(
            string token,
            string component,
            IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Begins a scoped trace operation that measures elapsed time until Dispose.
        /// Intended for use with C# `using` statements.
        /// </summary>
        /// <param name="token">Step token identifier.</param>
        /// <param name="component">Component name.</param>
        /// <param name="metadata">Optional contextual metadata collected on scope entry.</param>
        /// <returns>An IDisposable that records duration on Dispose.</returns>
        IDisposable BeginScope(
            string token,
            string component,
            IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Retrieves all recorded trace points asynchronously.
        /// </summary>
        /// <returns>Array of ExecutionTracePoint in chronological order.</returns>
        Task<ExecutionTracePoint[]> GetTracePointsAsync();

        /// <summary>
        /// Clears all recorded trace points from memory.
        /// </summary>
        void Clear();
    }
}
