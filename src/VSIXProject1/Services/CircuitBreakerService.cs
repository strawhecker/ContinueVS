using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VSIXProject1.Services
{
    /// <summary>
    /// Circuit Breaker Service (Step 108)
    /// 
    /// Host-side (.NET) service for tracking circuit-breaker state synchronized with the bridge.
    /// Provides thread-safe access to per-handler circuit state, metrics aggregation,
    /// and event relay to monitoring dashboards.
    /// </summary>
    public class CircuitBreakerService
    {
        // ====================================================================
        // STATE ENUMS
        // ====================================================================

        /// <summary>Circuit state enumeration</summary>
        public enum CircuitState
        {
            Closed,
            Open,
            HalfOpen
        }

        /// <summary>Alert severity levels</summary>
        public enum AlertSeverity
        {
            Info,
            Warning,
            Error,
            Critical
        }

        // ====================================================================
        // MODELS
        // ====================================================================

        /// <summary>Per-handler circuit metrics</summary>
        public class CircuitMetrics
        {
            public string? Handler { get; set; }
            public CircuitState State { get; set; }
            public DateTime LastStateChange { get; set; }
            public int ErrorCount { get; set; }
            public int SuccessCount { get; set; }
            public int TotalRequests { get; set; }
            public double ErrorRate { get; set; }
            public long P99LatencyMs { get; set; }
            public int ConsecutiveFailures { get; set; }
            public string? StateReason { get; set; }
        }

        /// <summary>Aggregate circuit metrics across all handlers</summary>
        public class AggregateMetrics
        {
            public int TotalCircuits { get; set; }
            public int ClosedCircuits { get; set; }
            public int OpenCircuits { get; set; }
            public int HalfOpenCircuits { get; set; }
            public int TotalStateChanges { get; set; }
            public int TotalAlerts { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        /// <summary>Circuit state change event</summary>
        public class StateChangeEvent
        {
            public string? Handler { get; set; }
            public CircuitState OldState { get; set; }
            public CircuitState NewState { get; set; }
            public string? Reason { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>Circuit alert event</summary>
        public class AlertEvent
        {
            public string? Handler { get; set; }
            public CircuitState State { get; set; }
            public string? AlertType { get; set; }
            public AlertSeverity Severity { get; set; }
            public Dictionary<string, object>? Details { get; set; }
            public DateTime Timestamp { get; set; }
        }

        // ====================================================================
        // FIELDS & PROPERTIES
        // ====================================================================

        private readonly ReaderWriterLockSlim _circuitLock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, CircuitMetrics> _circuits = new();
        private AggregateMetrics _aggregateMetrics = new();

        /// <summary>Raised when circuit state changes</summary>
        public event EventHandler<StateChangeEvent>? StateChanged;

        /// <summary>Raised when circuit alert triggered</summary>
        public event EventHandler<AlertEvent>? AlertTriggered;

        public bool IsEnabled { get; set; } = true;

        // ====================================================================
        // PUBLIC METHODS
        // ====================================================================

        /// <summary>Get circuit metrics for handler</summary>
        public CircuitMetrics? GetCircuitMetrics(string handlerType)
        {
            if (!IsEnabled)
                return null;

            _circuitLock.EnterReadLock();
            try
            {
                if (_circuits.TryGetValue(handlerType, out var metrics))
                {
                    return new CircuitMetrics
                    {
                        Handler = metrics.Handler,
                        State = metrics.State,
                        LastStateChange = metrics.LastStateChange,
                        ErrorCount = metrics.ErrorCount,
                        SuccessCount = metrics.SuccessCount,
                        TotalRequests = metrics.TotalRequests,
                        ErrorRate = metrics.ErrorRate,
                        P99LatencyMs = metrics.P99LatencyMs,
                        ConsecutiveFailures = metrics.ConsecutiveFailures,
                        StateReason = metrics.StateReason,
                    };
                }
                return null;
            }
            finally
            {
                _circuitLock.ExitReadLock();
            }
        }

        /// <summary>Get all circuit metrics</summary>
        public IEnumerable<CircuitMetrics> GetAllCircuitMetrics()
        {
            if (!IsEnabled)
                return new List<CircuitMetrics>();

            _circuitLock.EnterReadLock();
            try
            {
                var result = new List<CircuitMetrics>();
                foreach (var circuit in _circuits.Values)
                {
                    result.Add(new CircuitMetrics
                    {
                        Handler = circuit.Handler,
                        State = circuit.State,
                        LastStateChange = circuit.LastStateChange,
                        ErrorCount = circuit.ErrorCount,
                        SuccessCount = circuit.SuccessCount,
                        TotalRequests = circuit.TotalRequests,
                        ErrorRate = circuit.ErrorRate,
                        P99LatencyMs = circuit.P99LatencyMs,
                        ConsecutiveFailures = circuit.ConsecutiveFailures,
                        StateReason = circuit.StateReason,
                    });
                }
                return result;
            }
            finally
            {
                _circuitLock.ExitReadLock();
            }
        }

        /// <summary>Get aggregate metrics</summary>
        public AggregateMetrics? GetAggregateMetrics()
        {
            if (!IsEnabled)
                return null;

            _circuitLock.EnterReadLock();
            try
            {
                return new AggregateMetrics
                {
                    TotalCircuits = _aggregateMetrics.TotalCircuits,
                    ClosedCircuits = _aggregateMetrics.ClosedCircuits,
                    OpenCircuits = _aggregateMetrics.OpenCircuits,
                    HalfOpenCircuits = _aggregateMetrics.HalfOpenCircuits,
                    TotalStateChanges = _aggregateMetrics.TotalStateChanges,
                    TotalAlerts = _aggregateMetrics.TotalAlerts,
                    LastUpdated = _aggregateMetrics.LastUpdated,
                };
            }
            finally
            {
                _circuitLock.ExitReadLock();
            }
        }

        /// <summary>Update circuit state (called from bridge via event)</summary>
        public void UpdateCircuitState(string handlerType, CircuitState newState, string reason = "")
        {
            if (!IsEnabled)
                return;

            _circuitLock.EnterWriteLock();
            try
            {
                CircuitState oldState = CircuitState.Closed;

                if (_circuits.TryGetValue(handlerType, out var circuit))
                {
                    oldState = circuit.State;
                    if (newState != oldState)
                    {
                        circuit.State = newState;
                        circuit.LastStateChange = DateTime.UtcNow;
                        circuit.StateReason = reason;

                        _aggregateMetrics.TotalStateChanges++;

                        // Update state counts
                        _updateStateCounts();

                        // Emit event
                        StateChanged?.Invoke(this, new StateChangeEvent
                        {
                            Handler = handlerType,
                            OldState = oldState,
                            NewState = newState,
                            Reason = reason,
                            Timestamp = DateTime.UtcNow,
                        });
                    }
                }
                else
                {
                    // Create new circuit
                    _circuits[handlerType] = new CircuitMetrics
                    {
                        Handler = handlerType,
                        State = newState,
                        LastStateChange = DateTime.UtcNow,
                        StateReason = reason,
                    };
                    _aggregateMetrics.TotalCircuits++;
                    _updateStateCounts();
                }
            }
            finally
            {
                _circuitLock.ExitWriteLock();
            }
        }

        /// <summary>Record successful request</summary>
        public void RecordSuccess(string handlerType, long latencyMs = 0)
        {
            if (!IsEnabled)
                return;

            _circuitLock.EnterWriteLock();
            try
            {
                if (_circuits.TryGetValue(handlerType, out var circuit))
                {
                    circuit.SuccessCount++;
                    circuit.TotalRequests++;
                    circuit.ConsecutiveFailures = 0;
                    if (latencyMs > 0)
                    {
                        circuit.P99LatencyMs = Math.Max(circuit.P99LatencyMs, latencyMs);
                    }
                    _updateErrorRate(circuit);
                }
            }
            finally
            {
                _circuitLock.ExitWriteLock();
            }
        }

        /// <summary>Record failed request</summary>
        public void RecordFailure(string handlerType, long latencyMs = 0)
        {
            if (!IsEnabled)
                return;

            _circuitLock.EnterWriteLock();
            try
            {
                if (_circuits.TryGetValue(handlerType, out var circuit))
                {
                    circuit.ErrorCount++;
                    circuit.TotalRequests++;
                    circuit.ConsecutiveFailures++;
                    if (latencyMs > 0)
                    {
                        circuit.P99LatencyMs = Math.Max(circuit.P99LatencyMs, latencyMs);
                    }
                    _updateErrorRate(circuit);
                }
            }
            finally
            {
                _circuitLock.ExitWriteLock();
            }
        }

        /// <summary>Emit alert</summary>
        public void EmitAlert(string handlerType, string alertType, AlertSeverity severity, Dictionary<string, object>? details = null)
        {
            if (!IsEnabled)
                return;

            _circuitLock.EnterWriteLock();
            try
            {
                _aggregateMetrics.TotalAlerts++;
                var state = CircuitState.Closed;

                if (_circuits.TryGetValue(handlerType, out var circuit))
                {
                    state = circuit.State;
                }

                AlertTriggered?.Invoke(this, new AlertEvent
                {
                    Handler = handlerType,
                    State = state,
                    AlertType = alertType,
                    Severity = severity,
                    Details = details ?? new Dictionary<string, object>(),
                    Timestamp = DateTime.UtcNow,
                });
            }
            finally
            {
                _circuitLock.ExitWriteLock();
            }
        }

        /// <summary>Reset circuit metrics</summary>
        public void ResetCircuit(string handlerType)
        {
            if (!IsEnabled)
                return;

            _circuitLock.EnterWriteLock();
            try
            {
                if (_circuits.TryGetValue(handlerType, out var circuit))
                {
                    circuit.ErrorCount = 0;
                    circuit.SuccessCount = 0;
                    circuit.TotalRequests = 0;
                    circuit.ErrorRate = 0;
                    circuit.ConsecutiveFailures = 0;
                }
            }
            finally
            {
                _circuitLock.ExitWriteLock();
            }
        }

        /// <summary>Clear all circuits</summary>
        public void ClearAll()
        {
            _circuitLock.EnterWriteLock();
            try
            {
                _circuits.Clear();
                _aggregateMetrics = new AggregateMetrics();
            }
            finally
            {
                _circuitLock.ExitWriteLock();
            }
        }

        // ====================================================================
        // PRIVATE HELPERS
        // ====================================================================

        private void _updateErrorRate(CircuitMetrics circuit)
        {
            if (circuit.TotalRequests == 0)
            {
                circuit.ErrorRate = 0;
            }
            else
            {
                circuit.ErrorRate = (double)circuit.ErrorCount / circuit.TotalRequests;
            }
        }

        private void _updateStateCounts()
        {
            int closed = 0, open = 0, halfOpen = 0;

            foreach (var circuit in _circuits.Values)
            {
                switch (circuit.State)
                {
                    case CircuitState.Closed:
                        closed++;
                        break;
                    case CircuitState.Open:
                        open++;
                        break;
                    case CircuitState.HalfOpen:
                        halfOpen++;
                        break;
                }
            }

            _aggregateMetrics.ClosedCircuits = closed;
            _aggregateMetrics.OpenCircuits = open;
            _aggregateMetrics.HalfOpenCircuits = halfOpen;
            _aggregateMetrics.LastUpdated = DateTime.UtcNow;
        }

        /// <summary>Dispose service</summary>
        public void Dispose()
        {
            _circuitLock?.Dispose();
        }
    }
}
