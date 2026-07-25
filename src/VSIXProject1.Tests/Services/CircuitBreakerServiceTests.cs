using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using VSIXProject1.Services;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Circuit Breaker Service Tests (Step 108)
    /// 
    /// Comprehensive xUnit test suite covering:
    /// - Service initialization
    /// - State tracking
    /// - Event relay
    /// - Thread safety
    /// - Error handling
    /// </summary>
    public class CircuitBreakerServiceTests : IDisposable
    {
        private CircuitBreakerService _service;

        public CircuitBreakerServiceTests()
        {
            _service = new CircuitBreakerService();
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        // ====================================================================
        // TEST SUITE 1: SERVICE INITIALIZATION (3 tests)
        // ====================================================================

        [Fact]
        public void Service_Initializes_Enabled()
        {
            Assert.True(_service.IsEnabled);
        }

        [Fact]
        public void Service_Initializes_Empty()
        {
            var metrics = _service.GetAllCircuitMetrics();
            Assert.Empty(metrics);
        }

        [Fact]
        public void Service_Initializes_Aggregate_Metrics()
        {
            var agg = _service.GetAggregateMetrics();
            Assert.NotNull(agg);
            Assert.Equal(0, agg.TotalCircuits);
            Assert.Equal(0, agg.ClosedCircuits);
            Assert.Equal(0, agg.OpenCircuits);
            Assert.Equal(0, agg.HalfOpenCircuits);
        }

        // ====================================================================
        // TEST SUITE 2: STATE TRACKING (6 tests)
        // ====================================================================

        [Fact]
        public void UpdateCircuitState_Creates_New_Circuit()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed, "Test");

            var metrics = _service.GetCircuitMetrics("handler1");
            Assert.NotNull(metrics);
            Assert.Equal("handler1", metrics.Handler);
            Assert.Equal(CircuitBreakerService.CircuitState.Closed, metrics.State);
        }

        [Fact]
        public void UpdateCircuitState_Updates_Existing_Circuit()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Open, "Error threshold");

            var metrics = _service.GetCircuitMetrics("handler1");
            Assert.Equal(CircuitBreakerService.CircuitState.Open, metrics.State);
            Assert.Equal("Error threshold", metrics.StateReason);
        }

        [Fact]
        public void GetCircuitMetrics_Returns_Snapshot()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);
            _service.RecordSuccess("handler1", 100);
            _service.RecordFailure("handler1", 200);

            var metrics = _service.GetCircuitMetrics("handler1");
            Assert.Equal(1, metrics.SuccessCount);
            Assert.Equal(1, metrics.ErrorCount);
            Assert.Equal(2, metrics.TotalRequests);
            Assert.Equal(0.5, metrics.ErrorRate);
        }

        [Fact]
        public void GetAllCircuitMetrics_Returns_All_Circuits()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);
            _service.UpdateCircuitState("handler2", CircuitBreakerService.CircuitState.Open);
            _service.UpdateCircuitState("handler3", CircuitBreakerService.CircuitState.HalfOpen);

            var metrics = _service.GetAllCircuitMetrics().ToList();
            Assert.Equal(3, metrics.Count);
        }

        [Fact]
        public void GetAggregateMetrics_Updates_State_Counts()
        {
            _service.UpdateCircuitState("h1", CircuitBreakerService.CircuitState.Closed);
            _service.UpdateCircuitState("h2", CircuitBreakerService.CircuitState.Open);
            _service.UpdateCircuitState("h3", CircuitBreakerService.CircuitState.HalfOpen);

            var agg = _service.GetAggregateMetrics();
            Assert.Equal(3, agg.TotalCircuits);
            Assert.Equal(1, agg.ClosedCircuits);
            Assert.Equal(1, agg.OpenCircuits);
            Assert.Equal(1, agg.HalfOpenCircuits);
        }

        [Fact]
        public void ResetCircuit_Clears_Metrics()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);
            _service.RecordSuccess("handler1", 100);
            _service.RecordSuccess("handler1", 150);

            var metricsBefore = _service.GetCircuitMetrics("handler1");
            Assert.Equal(2, metricsBefore.SuccessCount);

            _service.ResetCircuit("handler1");
            var metricsAfter = _service.GetCircuitMetrics("handler1");
            Assert.Equal(0, metricsAfter.SuccessCount);
            Assert.Equal(0, metricsAfter.TotalRequests);
        }

        // ====================================================================
        // TEST SUITE 3: EVENT RELAY (4 tests)
        // ====================================================================

        [Fact]
        public void StateChanged_Event_Fired_On_Transition()
        {
            CircuitBreakerService.StateChangeEvent firedEvent = null;
            _service.StateChanged += (s, e) => { firedEvent = e; };

            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Open, "Error threshold");

            Assert.NotNull(firedEvent);
            Assert.Equal("handler1", firedEvent.Handler);
            Assert.Equal(CircuitBreakerService.CircuitState.Closed, firedEvent.OldState);
            Assert.Equal(CircuitBreakerService.CircuitState.Open, firedEvent.NewState);
            Assert.Equal("Error threshold", firedEvent.Reason);
        }

        [Fact]
        public void StateChanged_Event_Not_Fired_On_Same_State()
        {
            int eventCount = 0;
            _service.StateChanged += (s, e) => { eventCount++; };

            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            // Should only fire once (on initial creation)
            Assert.Equal(1, eventCount);
        }

        [Fact]
        public void AlertTriggered_Event_Fired()
        {
            CircuitBreakerService.AlertEvent firedEvent = null;
            _service.AlertTriggered += (s, e) => { firedEvent = e; };

            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Open);
            _service.EmitAlert("handler1", "HIGH_ERROR_RATE", CircuitBreakerService.AlertSeverity.Warning,
                new Dictionary<string, object> { { "rate", 0.75 } });

            Assert.NotNull(firedEvent);
            Assert.Equal("handler1", firedEvent.Handler);
            Assert.Equal("HIGH_ERROR_RATE", firedEvent.AlertType);
            Assert.Equal(CircuitBreakerService.AlertSeverity.Warning, firedEvent.Severity);
            Assert.Equal(0.75, firedEvent.Details["rate"]);
        }

        [Fact]
        public void Multiple_Event_Listeners_Receive_Events()
        {
            int listener1Count = 0;
            int listener2Count = 0;

            _service.StateChanged += (s, e) => { listener1Count++; };
            _service.StateChanged += (s, e) => { listener2Count++; };

            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Open);

            Assert.Equal(1, listener1Count);
            Assert.Equal(1, listener2Count);
        }

        // ====================================================================
        // TEST SUITE 4: THREAD SAFETY (4 tests)
        // ====================================================================

        [Fact]
        public async Task Concurrent_Updates_Are_Safe()
        {
            var tasks = new List<Task>();

            // 50 concurrent update tasks
            for (int i = 0; i < 50; i++)
            {
                int handlerNum = i % 10;
                var state = (CircuitBreakerService.CircuitState)(i % 3);
                tasks.Add(Task.Run(() =>
                {
                    _service.UpdateCircuitState($"handler{handlerNum}", state);
                }));
            }

            await Task.WhenAll(tasks.ToArray());

            var agg = _service.GetAggregateMetrics();
            Assert.Equal(10, agg.TotalCircuits);
        }

        [Fact]
        public async Task Concurrent_Reads_Are_Safe()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            var tasks = new List<Task>();

            // 50 concurrent read tasks
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    _ = _service.GetCircuitMetrics("handler1");
                    _ = _service.GetAllCircuitMetrics();
                    _ = _service.GetAggregateMetrics();
                }));
            }

            await Task.WhenAll(tasks.ToArray());
            // If we reach here without deadlock/exception, test passes
            Assert.True(true);
        }

        [Fact]
        public async Task Concurrent_Reads_And_Writes_Are_Safe()
        {
            var tasks = new List<Task>();

            // Mix read and write tasks
            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        _service.UpdateCircuitState($"handler{i % 10}", CircuitBreakerService.CircuitState.Open);
                    }));
                }
                else
                {
                    tasks.Add(Task.Run(() =>
                    {
                        _ = _service.GetAllCircuitMetrics();
                    }));
                }
            }

            await Task.WhenAll(tasks.ToArray());
            Assert.True(true);
        }

        [Fact]
        public async Task Metrics_Recording_Is_Thread_Safe()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            var tasks = new List<Task>();

            // 100 concurrent record tasks
            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                {
                    tasks.Add(Task.Run(() => _service.RecordSuccess("handler1", 50)));
                }
                else
                {
                    tasks.Add(Task.Run(() => _service.RecordFailure("handler1", 100)));
                }
            }

            await Task.WhenAll(tasks.ToArray());

            var metrics = _service.GetCircuitMetrics("handler1");
            Assert.Equal(100, metrics.TotalRequests);
            Assert.Equal(50, metrics.SuccessCount);
            Assert.Equal(50, metrics.ErrorCount);
        }

        // ====================================================================
        // TEST SUITE 5: ERROR HANDLING & DISABLED STATE (5 tests)
        // ====================================================================

        [Fact]
        public void Service_Disabled_Returns_Null()
        {
            _service.IsEnabled = false;
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            var metrics = _service.GetCircuitMetrics("handler1");
            Assert.Null(metrics);
        }

        [Fact]
        public void Service_Disabled_Returns_Empty_List()
        {
            _service.IsEnabled = false;
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            var metrics = _service.GetAllCircuitMetrics();
            Assert.Empty(metrics);
        }

        [Fact]
        public void Service_Disabled_Returns_Null_Aggregate()
        {
            _service.IsEnabled = false;
            var agg = _service.GetAggregateMetrics();
            Assert.Null(agg);
        }

        [Fact]
        public void GetCircuitMetrics_Unknown_Handler_Returns_Null()
        {
            var metrics = _service.GetCircuitMetrics("unknown_handler");
            Assert.Null(metrics);
        }

        [Fact]
        public void RecordSuccess_Unknown_Handler_Does_Not_Throw()
        {
            // Should not throw even for unknown handler
            _service.RecordSuccess("unknown_handler", 100);
            // If we reach here without exception, test passes
            Assert.True(true);
        }

        // ====================================================================
        // TEST SUITE 6: METRICS CALCULATION (3 tests)
        // ====================================================================

        [Fact]
        public void Error_Rate_Calculated_Correctly()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            // 3 successes, 1 failure = 25% error rate
            _service.RecordSuccess("handler1");
            _service.RecordSuccess("handler1");
            _service.RecordSuccess("handler1");
            _service.RecordFailure("handler1");

            var metrics = _service.GetCircuitMetrics("handler1");
            Assert.Equal(0.25, metrics.ErrorRate);
        }

        [Fact]
        public void P99_Latency_Tracks_Maximum()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            _service.RecordSuccess("handler1", 50);
            _service.RecordSuccess("handler1", 200);
            _service.RecordSuccess("handler1", 100);

            var metrics = _service.GetCircuitMetrics("handler1");
            Assert.Equal(200, metrics.P99LatencyMs);
        }

        [Fact]
        public void Consecutive_Failures_Tracked()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            _service.RecordFailure("handler1");
            _service.RecordFailure("handler1");
            var metricsAfterFailures = _service.GetCircuitMetrics("handler1");
            Assert.Equal(2, metricsAfterFailures.ConsecutiveFailures);

            _service.RecordSuccess("handler1");
            var metricsAfterSuccess = _service.GetCircuitMetrics("handler1");
            Assert.Equal(0, metricsAfterSuccess.ConsecutiveFailures);
        }

        // ====================================================================
        // TEST SUITE 7: CLEAR AND CLEANUP (2 tests)
        // ====================================================================

        [Fact]
        public void ClearAll_Removes_All_Circuits()
        {
            _service.UpdateCircuitState("h1", CircuitBreakerService.CircuitState.Closed);
            _service.UpdateCircuitState("h2", CircuitBreakerService.CircuitState.Open);

            _service.ClearAll();

            var metrics = _service.GetAllCircuitMetrics();
            Assert.Empty(metrics);

            var agg = _service.GetAggregateMetrics();
            Assert.Equal(0, agg.TotalCircuits);
        }

        [Fact]
        public void Service_Dispose_Does_Not_Throw()
        {
            _service.UpdateCircuitState("handler1", CircuitBreakerService.CircuitState.Closed);

            // Should not throw
            _service.Dispose();
            // If we reach here without exception, test passes
            Assert.True(true);
        }
    }
}
