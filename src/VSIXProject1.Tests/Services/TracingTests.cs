using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// xUnit test suite for DistributedTracingService (gap29_6).
    /// 
    /// Tests coverage:
    /// - Parsing W3C Trace Context format (RFC 9411)
    /// - Parsing OpenTelemetry format
    /// - Handling invalid/malformed headers
    /// - Recording distributed events (stub)
    /// - AsyncLocal context flow across await boundaries
    /// </summary>
    public class TracingTests
    {
        // ====================================================================
        // TEST 1 (CORE): ParseTraceId_W3CFormat_ReturnsValidTraceContext
        // ====================================================================

        [Fact]
        public async Task ParseTraceId_W3CFormat_ReturnsValidTraceContext()
        {
            // Arrange
            var service = new DistributedTracingService();
            var w3cHeader = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

            // Act
            var result = await service.ParseTraceIdAsync(w3cHeader);

            // Assert
            Assert.True(result.Success, "W3C format should parse successfully");
            Assert.NotNull(result.TraceContext);
            Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", result.TraceContext.TraceId);
            Assert.Equal("00f067aa0ba902b7", result.TraceContext.SpanId);
            Assert.Equal("W3C", result.TraceContext.Format);
            Assert.True(result.TraceContext.IsValid);
        }

        // ====================================================================
        // TEST 2 (CORE): ParseTraceId_OTelFormat_ReturnsValidTraceContext
        // ====================================================================

        [Fact]
        public async Task ParseTraceId_OTelFormat_ReturnsValidTraceContext()
        {
            // Arrange
            var service = new DistributedTracingService();
            var otelHeader = "abcdef0123456789abcdef0123456789-0123456789abcdef";

            // Act
            var result = await service.ParseTraceIdAsync(otelHeader);

            // Assert
            Assert.True(result.Success, "OpenTelemetry format should parse successfully");
            Assert.NotNull(result.TraceContext);
            Assert.Equal("abcdef0123456789abcdef0123456789", result.TraceContext.TraceId);
            Assert.Equal("0123456789abcdef", result.TraceContext.SpanId);
            Assert.Equal("OpenTelemetry", result.TraceContext.Format);
            Assert.True(result.TraceContext.IsValid);
        }

        // ====================================================================
        // TEST 3 (BONUS): ParseTraceId_InvalidHeader_ReturnsFailureWithErrors
        // ====================================================================

        [Fact]
        public async Task ParseTraceId_InvalidHeader_ReturnsFailureWithErrors()
        {
            // Arrange
            var service = new DistributedTracingService();
            // No dashes or valid structure—purely invalid
            var invalidHeader = "invalid";

            // Act
            var result = await service.ParseTraceIdAsync(invalidHeader);

            // Assert
            Assert.False(result.Success, "Invalid header should fail to parse");
            Assert.Null(result.TraceContext);
            Assert.NotEmpty(result.ErrorMessages);
            Assert.True(result.ErrorMessages.Count > 0, "Should contain error hints");
        }

        // ====================================================================
        // TEST 4 (BONUS): ParseTraceId_NullOrEmpty_ReturnsFailure
        // ====================================================================

        [Fact]
        public async Task ParseTraceId_NullOrEmpty_ReturnsFailure()
        {
            // Arrange
            var service = new DistributedTracingService();

            // Act
            var resultNull = await service.ParseTraceIdAsync(null);
            var resultEmpty = await service.ParseTraceIdAsync("");
            var resultWhitespace = await service.ParseTraceIdAsync("   ");

            // Assert
            Assert.False(resultNull.Success);
            Assert.False(resultEmpty.Success);
            Assert.False(resultWhitespace.Success);
            Assert.Null(resultNull.TraceContext);
            Assert.Null(resultEmpty.TraceContext);
            Assert.Null(resultWhitespace.TraceContext);
        }

        // ====================================================================
        // TEST 5 (BONUS): RecordDistributedEvent_CompleteSync
        // ====================================================================

        [Fact]
        public async Task RecordDistributedEvent_CompleteSync()
        {
            // Arrange
            var service = new DistributedTracingService();
            var traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
            var spanId = "00f067aa0ba902b7";
            var eventName = "TestEvent";

            // Act
            await service.RecordDistributedEventAsync(traceId, spanId, null, eventName);

            // Assert (no exception thrown = success for stub implementation)
            Assert.True(true, "Stub recording should complete without error");
        }

        // ====================================================================
        // TEST 6 (BONUS): SetCurrentTraceContext_And_GetCurrentTraceContext_RoundTrip
        // ====================================================================

        [Fact]
        public void SetCurrentTraceContext_And_GetCurrentTraceContext_RoundTrip()
        {
            // Arrange
            var service = new DistributedTracingService();
            var context = new TraceContext(
                traceId: "test-trace-123",
                spanId: "test-span-456",
                parentSpanId: null,
                isValid: true,
                format: "TestFormat"
            );

            // Act
            service.SetCurrentTraceContext(context);
            var retrieved = service.GetCurrentTraceContext();

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(context.TraceId, retrieved.TraceId);
            Assert.Equal(context.SpanId, retrieved.SpanId);
            Assert.Equal("TestFormat", retrieved.Format);
        }

        // ====================================================================
        // TEST 7 (BONUS): AsyncLocal_ContextFlowsAcrossAwaitBoundary
        // ====================================================================

        [Fact]
        public async Task AsyncLocal_ContextFlowsAcrossAwaitBoundary()
        {
            // Arrange
            var service = new DistributedTracingService();
            var initialContext = new TraceContext(
                traceId: "async-test-trace",
                spanId: "async-test-span",
                parentSpanId: "async-parent",
                isValid: true,
                format: "AsyncTest"
            );

            service.SetCurrentTraceContext(initialContext);

            // Act: Store initial context, then await a task and verify context persists
            var contextBeforeAwait = service.GetCurrentTraceContext();
            await Task.Delay(10); // Simulate async work
            var contextAfterAwait = service.GetCurrentTraceContext();

            // Assert: Context should flow across await boundary (AsyncLocal behavior)
            Assert.NotNull(contextBeforeAwait);
            Assert.NotNull(contextAfterAwait);
            Assert.Equal(contextBeforeAwait.TraceId, contextAfterAwait.TraceId);
            Assert.Equal(contextBeforeAwait.SpanId, contextAfterAwait.SpanId);
        }

        // ====================================================================
        // TEST 8 (BONUS): ParseTraceId_W3CFormat_CaseInsensitive
        // ====================================================================

        [Fact]
        public async Task ParseTraceId_W3CFormat_CaseInsensitive()
        {
            // Arrange
            var service = new DistributedTracingService();
            var w3cLower = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
            var w3cUpper = "00-4BF92F3577B34DA6A3CE929D0E0E4736-00F067AA0BA902B7-01";

            // Act
            var resultLower = await service.ParseTraceIdAsync(w3cLower);
            var resultUpper = await service.ParseTraceIdAsync(w3cUpper);

            // Assert
            Assert.True(resultLower.Success);
            Assert.True(resultUpper.Success);
            Assert.NotNull(resultLower.TraceContext);
            Assert.NotNull(resultUpper.TraceContext);
            // Regex preserves case from input; both parses succeed (case-insensitive matching)
            Assert.Equal(w3cLower.Split('-')[1], resultLower.TraceContext.TraceId);
            Assert.Equal(w3cUpper.Split('-')[1], resultUpper.TraceContext.TraceId);
        }
    }
}
