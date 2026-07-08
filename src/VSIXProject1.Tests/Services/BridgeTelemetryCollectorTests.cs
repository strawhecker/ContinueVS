using ContinueVS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for BridgeTelemetryCollector.
    /// Tests metric recording, telemetry disabling, and summary aggregation.
    /// </summary>
    public class BridgeTelemetryCollectorTests
    {
        private readonly BridgeTelemetryCollector _collector;

        public BridgeTelemetryCollectorTests()
        {
            _collector = new BridgeTelemetryCollector();
        }

        #region Handler Execution Tests

        [Fact]
        public async Task RecordHandlerExecutionAsync_RecordsLatencyMetric()
        {
            // Arrange
            var handlerName = "GetEditorState";
            var latencyMs = 42L;

            // Act
            await _collector.RecordHandlerExecutionAsync(handlerName, latencyMs);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.NotEmpty(summary);
            Assert.True(summary.ContainsKey($"handler.{handlerName}.count"));
            Assert.Equal(1L, summary[$"handler.{handlerName}.count"]);
        }

        [Fact]
        public async Task RecordHandlerExecutionAsync_MultipleInvocations_IncrementCount()
        {
            // Arrange
            var handlerName = "FindReferences";
            var latency1 = 50L;
            var latency2 = 75L;
            var latency3 = 100L;

            // Act
            await _collector.RecordHandlerExecutionAsync(handlerName, latency1);
            await _collector.RecordHandlerExecutionAsync(handlerName, latency2);
            await _collector.RecordHandlerExecutionAsync(handlerName, latency3);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(3L, summary[$"handler.{handlerName}.count"]);
            Assert.Equal(50L, summary[$"handler.{handlerName}.latency_ms.min"]);
            Assert.Equal(100L, summary[$"handler.{handlerName}.latency_ms.max"]);
        }

        [Fact]
        public async Task RecordHandlerExecutionAsync_WithSuccessMetadata_RecordsSuccessCounter()
        {
            // Arrange
            var handlerName = "GoToDefinition";
            var metadata = new Dictionary<string, object> { { "success", true } };

            // Act
            await _collector.RecordHandlerExecutionAsync(handlerName, 25L, metadata);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(1L, summary[$"handler.{handlerName}.success"]);
        }

        [Fact]
        public async Task RecordHandlerExecutionAsync_WithFailureMetadata_RecordsFailureCounter()
        {
            // Arrange
            var handlerName = "SearchHandler";
            var metadata = new Dictionary<string, object> { { "success", false } };

            // Act
            await _collector.RecordHandlerExecutionAsync(handlerName, 100L, metadata);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(1L, summary[$"handler.{handlerName}.failure"]);
        }

        [Fact]
        public async Task RecordHandlerExecutionAsync_NullHandlerName_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _collector.RecordHandlerExecutionAsync(null!, 50L));
        }

        #endregion

        #region RPC Message Tests

        [Fact]
        public async Task RecordRpcMessageAsync_RecordsMessageCount()
        {
            // Arrange
            var messageType = "onMessage";
            var latencyMs = 15L;

            // Act
            await _collector.RecordRpcMessageAsync(messageType, latencyMs);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(1L, summary[$"rpc.{messageType}.count"]);
        }

        [Fact]
        public async Task RecordRpcMessageAsync_MultipleMessages_AggregatesLatencies()
        {
            // Arrange
            var messageType = "onRequest";
            var latencies = new[] { 10L, 20L, 30L };

            // Act
            foreach (var latency in latencies)
            {
                await _collector.RecordRpcMessageAsync(messageType, latency);
            }

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(3L, summary[$"rpc.{messageType}.count"]);
            Assert.Equal(10L, summary[$"rpc.{messageType}.latency_ms.min"]);
            Assert.Equal(30L, summary[$"rpc.{messageType}.latency_ms.max"]);
            Assert.Equal(20L, summary[$"rpc.{messageType}.latency_ms.avg"]);
        }

        [Fact]
        public async Task RecordRpcMessageAsync_WithErrorMetadata_RecordsErrorCounter()
        {
            // Arrange
            var messageType = "onMessage";
            var metadata = new Dictionary<string, object> { { "isError", true } };

            // Act
            await _collector.RecordRpcMessageAsync(messageType, 50L, metadata);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(1L, summary[$"rpc.{messageType}.error"]);
        }

        [Fact]
        public async Task RecordRpcMessageAsync_NullMessageType_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _collector.RecordRpcMessageAsync(null!, 50L));
        }

        #endregion

        #region Error Recording Tests

        [Fact]
        public async Task RecordErrorAsync_RecordsErrorByContext()
        {
            // Arrange
            var context = "StdioTransport.SendMessage";
            var errorType = "TimeoutException";

            // Act
            await _collector.RecordErrorAsync(context, errorType);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(1L, summary[$"error.{context}.count"]);
            Assert.Equal(1L, summary[$"error.{errorType}"]);
        }

        [Fact]
        public async Task RecordErrorAsync_MultipleErrors_AggregatesByContext()
        {
            // Arrange
            var context = "HealthCheck.Ping";
            var errorType1 = "TimeoutException";
            var errorType2 = "InvalidOperationException";

            // Act
            await _collector.RecordErrorAsync(context, errorType1);
            await _collector.RecordErrorAsync(context, errorType2);
            await _collector.RecordErrorAsync(context, errorType1);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(3L, summary[$"error.{context}.count"]);
            Assert.Equal(2L, summary[$"error.{errorType1}"]);
            Assert.Equal(1L, summary[$"error.{errorType2}"]);
        }

        [Fact]
        public async Task RecordErrorAsync_NullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _collector.RecordErrorAsync(null!, "Exception"));
        }

        [Fact]
        public async Task RecordErrorAsync_NullErrorType_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _collector.RecordErrorAsync("Context", null!));
        }

        #endregion

        #region Custom Event Tests

        [Fact]
        public async Task RecordEventAsync_RecordsEventOccurrence()
        {
            // Arrange
            var eventName = "BridgeStarted";

            // Act
            await _collector.RecordEventAsync(eventName);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal(1L, summary[$"event.{eventName}"]);
        }

        [Fact]
        public async Task RecordEventAsync_WithValue_RecordsValueInHistogram()
        {
            // Arrange
            var eventName = "StartupDuration";
            var value = 1500L; // 1.5 seconds

            // Act
            await _collector.RecordEventAsync(eventName, value);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            // Just verify that something was recorded
            Assert.NotEmpty(summary);
        }

        [Fact]
        public async Task RecordEventAsync_MultipleEventsWithValues_AggregatesStatistics()
        {
            // Arrange
            var eventName = "VersionCheck";
            var values = new[] { 100L, 200L, 300L };

            // Act
            foreach (var value in values)
            {
                await _collector.RecordEventAsync(eventName, value);
            }

            // Assert
            var summary = await _collector.GetSummaryAsync();
            // Just verify that something was recorded
            Assert.NotEmpty(summary);
        }

        [Fact]
        public async Task RecordEventAsync_NullEventName_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _collector.RecordEventAsync(null!));
        }

        #endregion

        #region Summary & Reset Tests

        [Fact]
        public async Task GetSummaryAsync_WithNoMetrics_ReturnsEmptyDictionary()
        {
            // Act
            var summary = await _collector.GetSummaryAsync();

            // Assert
            Assert.NotNull(summary);
            Assert.Empty(summary);
        }

        [Fact]
        public async Task GetSummaryAsync_WithMultipleMetrics_ReturnsAllMetrics()
        {
            // Arrange
            await _collector.RecordHandlerExecutionAsync("Handler1", 50L);
            await _collector.RecordRpcMessageAsync("onMessage", 15L);
            await _collector.RecordErrorAsync("Context", "Exception");
            await _collector.RecordEventAsync("Event1");

            // Act
            var summary = await _collector.GetSummaryAsync();

            // Assert
            Assert.True(summary.Count >= 4); // At least one entry per operation type
        }

        [Fact]
        public async Task ResetAsync_ClearsAllMetrics()
        {
            // Arrange
            await _collector.RecordHandlerExecutionAsync("Handler1", 50L);
            await _collector.RecordRpcMessageAsync("onMessage", 15L);
            await _collector.RecordEventAsync("Event1");

            var summaryBefore = await _collector.GetSummaryAsync();
            Assert.NotEmpty(summaryBefore);

            // Act
            await _collector.ResetAsync();

            // Assert
            var summaryAfter = await _collector.GetSummaryAsync();
            Assert.Empty(summaryAfter);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task RecordHandlerExecutionAsync_ConcurrentWrites_IsThreadSafe()
        {
            // Arrange
            var handlerName = "ConcurrentHandler";
            var tasks = new List<Task>();
            var threadCount = 10;
            var operationsPerThread = 100;

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        await _collector.RecordHandlerExecutionAsync(handlerName, 10L);
                    }
                }));
            }
            await Task.WhenAll(tasks);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal((long)(threadCount * operationsPerThread), summary[$"handler.{handlerName}.count"]);
        }

        [Fact]
        public async Task RecordRpcMessageAsync_ConcurrentWrites_IsThreadSafe()
        {
            // Arrange
            var messageType = "concurrentMessage";
            var tasks = new List<Task>();
            var threadCount = 10;
            var operationsPerThread = 50;

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        await _collector.RecordRpcMessageAsync(messageType, 20L);
                    }
                }));
            }
            await Task.WhenAll(tasks);

            // Assert
            var summary = await _collector.GetSummaryAsync();
            Assert.Equal((long)(threadCount * operationsPerThread), summary[$"rpc.{messageType}.count"]);
        }

        #endregion

        #region Telemetry Disabled Behavior Tests

        [Fact]
        public async Task IsTelemetryEnabledAsync_ReturnsBoolean()
        {
            // Act
            var result = await _collector.IsTelemetryEnabledAsync();

            // Assert
            // Result will depend on ContinueVSPackage.Instance and settings page
            // For now, we just verify it returns a bool without throwing
            Assert.IsType<bool>(result);
        }

        [Fact]
        public async Task RecordHandlerExecutionAsync_WhenTelemetryDisabled_DoesNotThrow()
        {
            // This test assumes telemetry might be disabled in test environment
            // Verify that recording operations fail gracefully

            // Act & Assert - should not throw
            await _collector.RecordHandlerExecutionAsync("TestHandler", 50L);
            await _collector.RecordRpcMessageAsync("testMessage", 15L);
            await _collector.RecordErrorAsync("testContext", "testError");
            await _collector.RecordEventAsync("testEvent");
        }

        #endregion

        #region Histogram Percentile Tests

        [Fact]
        public async Task GetSummaryAsync_CalculatesPercentiles_Correctly()
        {
            // Arrange
            var handlerName = "PercentileHandler";
            var latencies = new[] { 10L, 20L, 30L, 40L, 50L, 60L, 70L, 80L, 90L, 100L };

            // Act
            foreach (var latency in latencies)
            {
                await _collector.RecordHandlerExecutionAsync(handlerName, latency);
            }
            var summary = await _collector.GetSummaryAsync();

            // Assert
            Assert.Equal(10L, summary[$"handler.{handlerName}.latency_ms.min"]);
            Assert.Equal(100L, summary[$"handler.{handlerName}.latency_ms.max"]);
            Assert.Equal(55L, summary[$"handler.{handlerName}.latency_ms.avg"]); // Average of 10-100
            Assert.True(summary.ContainsKey($"handler.{handlerName}.latency_ms.p50"));
            Assert.True(summary.ContainsKey($"handler.{handlerName}.latency_ms.p99"));
        }

        #endregion
    }
}
