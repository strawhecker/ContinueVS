using ContinueVS.Services.Utilities;
using Xunit;

namespace ContinueVS.Tests.Services.Utilities
{
    public class PlanFileDetectorTests
    {
        [Fact]
        public void ProcessChunk_WithMarkersOnSameLine_DetectsPlanFile()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var planContent = "start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n# My Plan\n## Step 1\nDo something.\nstop_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n";

            // Act
            detector.ProcessChunk(planContent);
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("# My Plan", result);
            Assert.Contains("## Step 1", result);
        }

        [Fact]
        public void ProcessChunk_WithClosingFence_CompletesProperly()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            detector.ProcessChunk("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.ProcessChunk("# Plan Title\n");
            detector.ProcessChunk("Some content.\n");
            detector.ProcessChunk("stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("# Plan Title", result);
            Assert.Contains("Some content.", result);
        }

        [Fact]
        public void ProcessChunk_ChunkedInput_AccumulatesCorrectly()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            detector.ProcessChunk("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.ProcessChunk("Line 1\n");
            detector.ProcessChunk("Line 2\n");
            detector.ProcessChunk("stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("Line 1", result);
            Assert.Contains("Line 2", result);
        }

        [Fact]
        public void ProcessChunk_WithoutMarkerInFence_DoesNotDetect()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            detector.ProcessChunk("def foo():\n");
            detector.ProcessChunk("    pass\n");
            detector.CompleteDetection();

            // Assert
            Assert.False(detector.IsComplete);
        }

        [Fact]
        public void ProcessChunk_WithCarriageReturns_HandlesCorrectly()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            detector.ProcessChunk("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\r\n");
            detector.ProcessChunk("# Plan\r\n");
            detector.ProcessChunk("Content\r\n");
            detector.ProcessChunk("stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8\r\n");
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("# Plan", result);
            Assert.Contains("Content", result);
        }

        [Fact]
        public void ProcessChunk_WithWhitespaceBeforeFence_IgnoresProperly()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act (leading spaces before marker)
            detector.ProcessChunk("  start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.ProcessChunk("Indented plan\n");
            detector.ProcessChunk("stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("Indented plan", result);
        }

        [Fact]
        public void Reset_ClearsState()
        {
            // Arrange
            var detector = new PlanFileDetector();
            detector.ProcessChunk("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");

            // Act
            detector.Reset();
            detector.ProcessChunk("random text\n");
            detector.ProcessChunk("code\n");
            detector.CompleteDetection();

            // Assert
            Assert.False(detector.IsComplete);
        }

        [Fact]
        public void GetBufferedContent_WhenNotComplete_Throws()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => detector.GetBufferedContent());
        }

        [Fact]
        public void ProcessChunk_IgnoresContentWithoutMarker()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            detector.ProcessChunk("This is random text\n");
            detector.ProcessChunk("Still no marker\n");
            detector.CompleteDetection();

            // Assert
            Assert.False(detector.IsComplete);
        }

        [Fact]
        public void ProcessChunk_MultilineBuffer_CompleetsAfterClosingFence()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var largeContent = string.Concat(System.Linq.Enumerable.Range(0, 100).Select(i => $"Line {i}\n"));

            // Act
            detector.ProcessChunk("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.ProcessChunk(largeContent);
            detector.ProcessChunk("stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("Line 0", result);
        }

        [Fact]
        public void CompleteDetection_WithoutClosingFence_StillCompletes()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            detector.ProcessChunk("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8\n");
            detector.ProcessChunk("Content line 1\n");
            detector.ProcessChunk("Content line 2\n");
            detector.CompleteDetection(); // No closing fence, but we call CompleteDetection

            // Assert - if we're buffering and stream ends, we should mark as complete
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("Content line 1", result);
        }

        [Fact]
        public void GetMarkerStart_ReturnsCorrectMarker()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            var marker = detector.GetMarkerStart();

            // Assert
            Assert.Equal("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8", marker);
        }

        [Fact]
        public void GetMarkerStop_ReturnsCorrectMarker()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            var marker = detector.GetMarkerStop();

            // Assert
            Assert.Equal("stop_A485254C_7481_47BB_A8CF_45B8DEED2DD8", marker);
        }
    }
}
