using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ContinueVS.Services;

namespace ContinueVS.Diagnostics
{
    /// <summary>
    /// Thread-safe implementation of IExecutionTracer.
    /// 
    /// Records step tokens (t1, t1.1, ..., t45) with timestamps, durations, and metadata.
    /// Outputs completed traces to LoggerService.Current and Debug.WriteLine in JSON format 
    /// (one per line) for consumption by VS Output Window and external trace processors.
    /// </summary>
    public sealed class ExecutionTracer : IExecutionTracer
    {
        private readonly List<ExecutionTracePoint> _tracePoints = new();
        private readonly object _lock = new object();
        private bool _disposed = false;

        /// <summary>Gets the number of recorded trace points.</summary>
        public int TraceCount
        {
            get
            {
                lock (_lock)
                {
                    return _tracePoints.Count;
                }
            }
        }

        /// <summary>
        /// Records a single trace point immediately and outputs to LoggerService and Debug.WriteLine.
        /// </summary>
        public void RecordTracePoint(
            string token,
            string component,
            IReadOnlyDictionary<string, object>? metadata = null)
        {
            ThrowIfDisposed();

            var tracePoint = new ExecutionTracePoint(token, component, durationMs: null, metadata: metadata);

            lock (_lock)
            {
                _tracePoints.Add(tracePoint);
            }

            var traceJson = $"[TRACE] {tracePoint}";
            // Output to LoggerService for file persistence
            _ = LoggerService.Current.WriteDebugAsync(traceJson);
            // Also output to Debug.WriteLine for real-time visibility in Output pane
            _ = LoggerService.Current.WriteDebugAsync(traceJson);
        }

        /// <summary>
        /// Begins a scoped trace operation. Returns a disposable that records duration on exit.
        /// </summary>
        public IDisposable BeginScope(
            string token,
            string component,
            IReadOnlyDictionary<string, object>? metadata = null)
        {
            ThrowIfDisposed();
            return new TraceScope(this, token, component, metadata);
        }

        /// <summary>
        /// Called by TraceScope.Dispose to record a completed scope with duration.
        /// </summary>
        internal void RecordScopeCompletion(
            string token,
            string component,
            double elapsedMs,
            IReadOnlyDictionary<string, object>? metadata = null)
        {
            var tracePoint = new ExecutionTracePoint(token, component, durationMs: elapsedMs, metadata: metadata);

            lock (_lock)
            {
                _tracePoints.Add(tracePoint);
            }

            var traceJson = $"[TRACE] {tracePoint}";
            // Output to LoggerService for file persistence
            _ = LoggerService.Current.WriteDebugAsync(traceJson);
            // Also output to Debug.WriteLine for Output pane consumption
            _ = LoggerService.Current.WriteDebugAsync(traceJson);
        }

        /// <summary>
        /// Retrieves all recorded trace points in chronological order.
        /// </summary>
        public Task<ExecutionTracePoint[]> GetTracePointsAsync()
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                var points = _tracePoints.ToArray();
                return Task.FromResult(points);
            }
        }

        /// <summary>
        /// Clears all recorded trace points from memory.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                _tracePoints.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ExecutionTracer));
        }

        /// <summary>
        /// Disposes the tracer and clears all collected traces.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            Clear();
            _disposed = true;
        }

        /// <summary>
        /// Inner disposable class for scoped trace operations.
        /// Measures elapsed time and records on Dispose.
        /// </summary>
        public sealed class TraceScope : IDisposable
        {
            private readonly ExecutionTracer _tracer;
            private readonly string _token;
            private readonly string _component;
            private readonly IReadOnlyDictionary<string, object>? _metadata;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            public TraceScope(
                ExecutionTracer tracer,
                string token,
                string component,
                IReadOnlyDictionary<string, object>? metadata)
            {
                _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
                _token = token ?? throw new ArgumentNullException(nameof(token));
                _component = component ?? throw new ArgumentNullException(nameof(component));
                _metadata = metadata;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (_disposed) return;

                _stopwatch.Stop();
                var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;

                _tracer.RecordScopeCompletion(_token, _component, elapsedMs, _metadata);
                _disposed = true;
            }
        }
    }
}
