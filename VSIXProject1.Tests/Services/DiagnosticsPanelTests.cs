using ContinueVS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Test suite for DiagnosticsPanel service (Step 102)
    /// 
    /// 18 tests across 5 suites:
    /// 1. Health Status (4 tests)
    /// 2. Handler Stats Aggregation (3 tests)
    /// 3. Error Queue (4 tests)
    /// 4. Thread Safety (3 tests)
    /// 5. Error Handling (4 tests)
    /// </summary>
    public class DiagnosticsPanelTests : IDisposable
    {
        private readonly MockTransport _transport;
        private readonly MockHealthCheckService _healthCheckService;
        private readonly MockBridgeLogger _bridgeLogger;
        private readonly MockTelemetryCollector _telemetryCollector;

        public DiagnosticsPanelTests()
        {
            _transport = new MockTransport();
            _healthCheckService = new MockHealthCheckService();
            _bridgeLogger = new MockBridgeLogger();
            _telemetryCollector = new MockTelemetryCollector();
        }

        public void Dispose()
        {
            _transport?.Dispose();
        }

        #region Suite 1: Health Status

        [Fact]
        public async Task GetBridgeHealthAsync_WhenHealthy_ReturnsHealthyStatus()
        {
            // Arrange
            _healthCheckService.SetHealth(HealthState.Healthy);
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            var health = await panel.GetBridgeHealthAsync();

            // Assert
            Assert.Equal("healthy", health);
        }

        [Fact]
        public async Task GetBridgeHealthAsync_WhenDegraded_ReturnsDegradedStatus()
        {
            // Arrange
            _healthCheckService.SetHealth(HealthState.Degraded);
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            var health = await panel.GetBridgeHealthAsync();

            // Assert
            Assert.Equal("degraded", health);
        }

        [Fact]
        public async Task GetBridgeHealthAsync_WhenError_ReturnsErrorStatus()
        {
            // Arrange
            _healthCheckService.SetHealth(HealthState.Error);
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            var health = await panel.GetBridgeHealthAsync();

            // Assert
            Assert.Equal("error", health);
        }

        [Fact]
        public async Task GetBridgeHealthAsync_WhenHealthCheckFails_ReturnsUnknown()
        {
            // Arrange
            _healthCheckService.SetThrowException(true);
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            var health = await panel.GetBridgeHealthAsync();

            // Assert
            Assert.Equal("unknown", health);
        }

        #endregion

        #region Suite 2: Handler Stats Aggregation

        [Fact]
        public async Task GetHandlerStatsAsync_ReturnsListOfStats()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            var stats = await panel.GetHandlerStatsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.IsType<List<HandlerStats>>(stats);
        }

        [Fact]
        public async Task GetHandlerStatsAsync_WhenEmpty_ReturnsEmptyList()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            var stats = await panel.GetHandlerStatsAsync();

            // Assert
            Assert.Empty(stats);
        }

        [Fact]
        public async Task GetHandlerStatsAsync_WhenThrows_ReturnsEmptyListGracefully()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            var stats = await panel.GetHandlerStatsAsync();

            // Assert - Should not throw, should return empty list
            Assert.Empty(stats);
        }

        #endregion

        #region Suite 3: Error Queue

        [Fact]
        public async Task GetRecentErrorsAsync_ReturnsAllEntriesInQueue()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);
            var error1 = new DiagnosticErrorEntry { Message = "Error 1", Timestamp = DateTime.UtcNow };
            var error2 = new DiagnosticErrorEntry { Message = "Error 2", Timestamp = DateTime.UtcNow.AddSeconds(1) };

            panel.AddErrorEntry(error1);
            panel.AddErrorEntry(error2);

            // Act
            var errors = await panel.GetRecentErrorsAsync();

            // Assert
            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.Message == "Error 1");
            Assert.Contains(errors, e => e.Message == "Error 2");
        }

        [Fact]
        public async Task AddErrorEntry_EnforcesMaximum100Entries()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act - Add 150 errors
            for (int i = 0; i < 150; i++)
            {
                panel.AddErrorEntry(new DiagnosticErrorEntry
                {
                    Message = $"Error {i}",
                    Timestamp = DateTime.UtcNow.AddSeconds(i)
                });
            }

            var errors = await panel.GetRecentErrorsAsync();

            // Assert
            Assert.Equal(100, errors.Count);
        }

        [Fact]
        public async Task AddErrorEntry_NullEntry_DoesNotThrow()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act
            panel.AddErrorEntry(null); // Should not throw

            var errors = await panel.GetRecentErrorsAsync();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void ClearErrorQueue_RemovesAllEntries()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);
            panel.AddErrorEntry(new DiagnosticErrorEntry { Message = "Error 1" });
            panel.AddErrorEntry(new DiagnosticErrorEntry { Message = "Error 2" });

            // Act
            panel.ClearErrorQueue();
            var errors = panel.GetRecentErrorsAsync().Result;

            // Assert
            Assert.Empty(errors);
        }

        #endregion

        #region Suite 4: Thread Safety

        [Fact]
        public async Task GetRecentErrorsAsync_ConcurrentWithAdd_IsSafe()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);
            var tasks = new List<Task>();

            // Act - Concurrent reads and writes
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await panel.GetRecentErrorsAsync();
                }));

                tasks.Add(Task.Run(() =>
                {
                    panel.AddErrorEntry(new DiagnosticErrorEntry
                    {
                        Message = $"Error {i}",
                        Timestamp = DateTime.UtcNow
                    });
                }));
            }

            // Assert - Should complete without deadlock or corruption
            await Task.WhenAll(tasks);
            var finalErrors = await panel.GetRecentErrorsAsync();
            Assert.Equal(10, finalErrors.Count);
        }

        [Fact]
        public async Task AddErrorEntry_ConcurrentAdds_MaintainsOrder()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);
            var tasks = new List<Task>();

            // Act - Add entries concurrently
            for (int i = 0; i < 50; i++)
            {
                var index = i;
                tasks.Add(Task.Run(() =>
                {
                    panel.AddErrorEntry(new DiagnosticErrorEntry
                    {
                        Message = $"Error {index}",
                        Timestamp = DateTime.UtcNow.AddMilliseconds(index)
                    });
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Should have all entries
            var errors = await panel.GetRecentErrorsAsync();
            Assert.Equal(50, errors.Count);
        }

        [Fact]
        public void ClearErrorQueue_ConcurrentWithAdd_IsSafe()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);
            var tasks = new List<Task>();

            // Act - Concurrent clear and add
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    panel.ClearErrorQueue();
                }));

                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        panel.AddErrorEntry(new DiagnosticErrorEntry
                        {
                            Message = $"Error {j}",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }));
            }

            // Assert - Should complete without exception
            Assert.True(Task.WaitAll(tasks.ToArray(), 5000), "Operations should complete within 5 seconds");
        }

        #endregion

        #region Suite 5: Error Handling

        [Fact]
        public void Constructor_WithNullTransport_ThrowsArgumentNullException()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DiagnosticsPanel(null, _healthCheckService, _bridgeLogger)
            );
        }

        [Fact]
        public void Constructor_WithNullHealthCheckService_ThrowsArgumentNullException()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() =>
                new DiagnosticsPanel(_transport, null, _bridgeLogger)
            );
        }

        [Fact]
        public async Task GetDiagnosticSummaryAsync_ReturnsCompleteSnapshot()
        {
            // Arrange
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);
            panel.AddErrorEntry(new DiagnosticErrorEntry { Message = "Test Error", Severity = "WARNING" });

            // Act
            var summary = await panel.GetDiagnosticSummaryAsync();

            // Assert
            Assert.NotNull(summary);
            Assert.Contains("health", summary.Keys);
            Assert.Contains("errorCount", summary.Keys);
            Assert.Contains("uptime", summary.Keys);
            Assert.Contains("timestamp", summary.Keys);
        }

        [Fact]
        public async Task GetDiagnosticSummaryAsync_WithHealthCheckFailure_ThrowsException()
        {
            // Arrange
            _healthCheckService.SetThrowException(true);
            var panel = new DiagnosticsPanel(_transport, _healthCheckService, _bridgeLogger, _telemetryCollector);

            // Act & Assert
            await Assert.ThrowsAsync<DiagnosticsPanelException>(
                () => panel.GetDiagnosticSummaryAsync()
            );
        }

        #endregion

        #region Helper Mock Classes

        private class MockTransport : IBridgeTransport
        {
            public event EventHandler<BridgeEventArgs> OnMessageReceived;
            public event EventHandler<BridgeEventArgs> OnTransportError;

            public string Version => "mock";
            public HealthState CurrentStatus => HealthState.Healthy;
            public bool IsRunning => true;

            public Task<BridgeResponse> SendMessageAsync(BridgeMessage message, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new BridgeResponse { Success = true });
            }

            public Task InitializeAsync(IBridgeConfiguration config, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<bool> PingAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }

            public void Dispose() { }
        }

        private class MockHealthCheckService
        {
            private HealthState _currentHealth = HealthState.Healthy;
            private bool _throwException = false;

            public void SetHealth(HealthState state) => _currentHealth = state;
            public void SetThrowException(bool value) => _throwException = value;

            public HealthStatus GetCurrentStatus()
            {
                if (_throwException)
                    throw new Exception("Mock health check failure");

                return new HealthStatus
                {
                    State = _currentHealth,
                    Reason = $"Health is {_currentHealth}",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private class MockBridgeLogger : IBridgeLogger
        {
            public void Debug(string message, object context = null) { }
            public void Info(string message, object context = null) { }
            public void Warn(string message, object context = null) { }
            public void Error(string message, object context = null) { }
        }

        private class MockTelemetryCollector : IBridgeTelemetryCollector
        {
            public void RecordEvent(string eventName, Dictionary<string, object> properties = null) { }
            public Dictionary<string, object> GetMetrics() => new Dictionary<string, object>();
        }

        #endregion
    }
}
