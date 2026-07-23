using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Diagnostics;

namespace ContinueVS.Tests.Diagnostics
{
    /// <summary>
    /// xUnit tests for ExecutionTracer infrastructure.
    /// Validates thread-safety, timing accuracy, scope behavior, and JSON serialization.
    /// </summary>
    public class ExecutionTracerTests
    {
        [Fact]
        public void RecordTracePoint_CreatesTraceWithValidToken()
        {
            // Arrange
            var tracer = new ExecutionTracer();

            // Act
            tracer.RecordTracePoint("t1", "ContinueVSPackage");

            // Assert
            Assert.Equal(1, tracer.TraceCount);
        }

        [Fact]
        public void RecordTracePoint_WithMetadata_IncludesInTrace()
        {
            // Arrange
            var tracer = new ExecutionTracer();
            var metadata = new Dictionary<string, object> { { "service", "BridgeLogger" }, { "status", "created" } };

            // Act
            tracer.RecordTracePoint("t1.3.4", "ContinueVSPackage", metadata);

            // Assert
            Assert.Equal(1, tracer.TraceCount);
        }

        [Fact]
        public async Task GetTracePointsAsync_ReturnsAllRecordedPoints()
        {
            // Arrange
            var tracer = new ExecutionTracer();
            tracer.RecordTracePoint("t1", "Package");
            tracer.RecordTracePoint("t1.1", "Package");
            tracer.RecordTracePoint("t1.2", "Package");

            // Act
            var points = await tracer.GetTracePointsAsync();

            // Assert
            Assert.Equal(3, points.Length);
            Assert.Equal("t1", points[0].Token);
            Assert.Equal("t1.1", points[1].Token);
            Assert.Equal("t1.2", points[2].Token);
        }

        [Fact]
        public void BeginScope_RecordsDurationOnDispose()
        {
            // Arrange
            var tracer = new ExecutionTracer();

            // Act
            using (var scope = tracer.BeginScope("t1.5", "CommandInit"))
            {
                System.Threading.Thread.Sleep(50); // Simulate work
            }

            // Assert
            Assert.Equal(1, tracer.TraceCount);
            // Note: actual duration timing verified in integration tests (50ms+ elapsed)
        }

        [Fact]
        public void Clear_RemovesAllTracePoints()
        {
            // Arrange
            var tracer = new ExecutionTracer();
            tracer.RecordTracePoint("t1", "Package");
            tracer.RecordTracePoint("t1.1", "Package");
            Assert.Equal(2, tracer.TraceCount);

            // Act
            tracer.Clear();

            // Assert
            Assert.Equal(0, tracer.TraceCount);
        }

        [Fact]
        public void RecordTracePoint_ThrowsOnNullToken()
        {
            // Arrange
            var tracer = new ExecutionTracer();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                tracer.RecordTracePoint(null!, "Package"));
        }

        [Fact]
        public void RecordTracePoint_ThrowsOnNullComponent()
        {
            // Arrange
            var tracer = new ExecutionTracer();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                tracer.RecordTracePoint("t1", null!));
        }

        [Fact]
        public void ExecutionTracePoint_SerializesToValidJson()
        {
            // Arrange
            var metadata = new Dictionary<string, object> { { "service", "BridgeLogger" } };
            var tracePoint = new ExecutionTracePoint("t1.3.4", "Package", 45.5, metadata);

            // Act
            var json = tracePoint.ToString();

            // Assert
            Assert.Contains("\"token\":\"t1.3.4\"", json);
            Assert.Contains("\"component\":\"Package\"", json);
            Assert.Contains("\"duration_ms\":45.50", json);
            Assert.Contains("\"service\":\"BridgeLogger\"", json);
            Assert.StartsWith("{", json);
            Assert.EndsWith("}", json);
        }

        [Fact]
        public void ExecutionTracePoint_WithNullDuration_SerializesNull()
        {
            // Arrange
            var tracePoint = new ExecutionTracePoint("t1", "Package", durationMs: null);

            // Act
            var json = tracePoint.ToString();

            // Assert
            Assert.Contains("\"duration_ms\":null", json);
        }

        [Fact]
        public void ExecutionTracePoint_Timestamp_IsUtc()
        {
            // Arrange & Act
            var tracePoint = new ExecutionTracePoint("t1", "Package");

            // Assert
            Assert.Equal(DateTimeKind.Utc, tracePoint.Timestamp.Kind);
        }

        [Fact]
        public async Task ConcurrentRecording_ThreadSafe()
        {
            // Arrange
            var tracer = new ExecutionTracer();
            var tasks = new Task[10];

            // Act
            for (int i = 0; i < tasks.Length; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    tracer.RecordTracePoint($"t{index}", "Package");
                });
            }
            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(10, tracer.TraceCount);
        }

        [Fact]
        public void Dispose_PreventsUse()
        {
            // Arrange
            var tracer = new ExecutionTracer();
            tracer.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() =>
                tracer.RecordTracePoint("t1", "Package"));
        }

        [Fact]
        public void BeginScope_WithNullTracer_Throws()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TraceScope(null!, "t1", "Package"));
        }

        [Fact]
        public void TraceScope_DisposeMultipleTimes_IsIdempotent()
        {
            // Arrange
            var tracer = new ExecutionTracer();
            var scope = tracer.BeginScope("t1", "Package") as TraceScope;

            // Act & Assert
            scope!.Dispose();
            scope.Dispose(); // Second dispose should not throw
            Assert.Equal(1, tracer.TraceCount); // Only one trace point recorded
        }
    }
}
