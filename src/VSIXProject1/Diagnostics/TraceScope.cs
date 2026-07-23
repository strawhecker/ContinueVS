using System;
using System.Collections.Generic;

namespace ContinueVS.Diagnostics
{
    /// <summary>
    /// Disposable helper class for scoped trace operations.
    /// 
    /// Usage:
    ///     using (var scope = tracer.BeginScope("t1.3", "BridgeLogger"))
    ///     {
    ///         // Do work
    ///     } // Scope automatically records duration on exit
    /// 
    /// This avoids manual Stopwatch management and ensures traces are always recorded.
    /// </summary>
    public sealed class TraceScope : IDisposable
    {
        private readonly IExecutionTracer _tracer;
        private readonly string _token;
        private readonly string _component;
        private readonly IReadOnlyDictionary<string, object>? _metadata;
        private readonly System.Diagnostics.Stopwatch _stopwatch;
        private bool _disposed;

        /// <summary>
        /// Creates a new trace scope that will measure elapsed time until Dispose.
        /// </summary>
        /// <param name="tracer">Tracer instance to record completion.</param>
        /// <param name="token">Step token identifier (e.g., "t1.3").</param>
        /// <param name="component">Component name (e.g., "BridgeLogger").</param>
        /// <param name="metadata">Optional initial metadata (collected on entry, included in trace).</param>
        public TraceScope(
            IExecutionTracer tracer,
            string token,
            string component,
            IReadOnlyDictionary<string, object>? metadata = null)
        {
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _metadata = metadata;
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        /// <summary>
        /// Stops the timer and records the completed trace point with duration.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _stopwatch.Stop();
            var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;

            // For now, delegate to ExecutionTracer's internal method if available.
            // If tracer is mock/stub, this gracefully handles the call.
            if (_tracer is ExecutionTracer et)
            {
                et.RecordScopeCompletion(_token, _component, elapsedMs, _metadata);
            }

            _disposed = true;
        }
    }
}
