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
            var planContent = "```A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n# My Plan\n## Step 1\nDo something.\n```\n";

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
            detector.ProcessChunk("```A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n");
            detector.ProcessChunk("# Plan Title\n");
            detector.ProcessChunk("Some content.\n");
            detector.ProcessChunk("```\n");
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
            detector.ProcessChunk("```");
            detector.ProcessChunk("A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n");
            detector.ProcessChunk("Line 1\n");
            detector.ProcessChunk("Line 2\n");
            detector.ProcessChunk("```\n");
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
            detector.ProcessChunk("```python\n");
            detector.ProcessChunk("def foo():\n");
            detector.ProcessChunk("    pass\n");
            detector.ProcessChunk("```\n");
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
            detector.ProcessChunk("```A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\r\n");
            detector.ProcessChunk("# Plan\r\n");
            detector.ProcessChunk("Content\r\n");
            detector.ProcessChunk("```\r\n");
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

            // Act (leading spaces before fence)
            detector.ProcessChunk("  ```A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n");
            detector.ProcessChunk("Indented plan\n");
            detector.ProcessChunk("```\n");
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
            detector.ProcessChunk("```A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n");

            // Act
            detector.Reset();
            detector.ProcessChunk("```python\n");
            detector.ProcessChunk("code\n");
            detector.ProcessChunk("```\n");
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
            detector.ProcessChunk("```\n");
            detector.ProcessChunk("Still no marker\n");
            detector.ProcessChunk("```\n");
            detector.CompleteDetection();

            // Assert
            Assert.False(detector.IsComplete);
        }

        [Fact]
        public void ProcessChunk_MultilineBuffer_CompleatsAfterClosingFence()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var largeContent = string.Concat(Enumerable.Range(0, 100).Select(i => $"Line {i}\n"));

            // Act
            detector.ProcessChunk("```A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n");
            detector.ProcessChunk(largeContent);
            detector.ProcessChunk("```\n");
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("Line 0", result);
            Assert.Contains("Line 99", result);
        }

        [Fact]
        public void CompleteDetection_WithoutClosingFence_StillCompletes()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act
            detector.ProcessChunk("```A485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n");
            detector.ProcessChunk("Incomplete plan\n");
            detector.CompleteDetection();

            // Assert (stream ended without closing fence, but detector completes anyway)
            Assert.True(detector.IsComplete);
            var result = detector.GetBufferedContent();
            Assert.Contains("Incomplete plan", result);
        }
    }
}
