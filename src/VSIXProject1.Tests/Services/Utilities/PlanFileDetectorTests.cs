using System;
using Xunit;
using ContinueVS.Services.Utilities;

namespace ContinueVS.Tests.Services.Utilities
{
    /// <summary>
    /// Unit tests for PlanFileDetector.
    /// gap70: Tests pattern matching, buffer accumulation, fence tracking, and edge cases.
    /// </summary>
    public class PlanFileDetectorTests
    {
        private const string MarkerFileName = "A485254C_7481_47BB_A8CF_45B8DEED2DD8.md";

        [Fact]
        public void Detector_WhenMarkerDetected_SetsCompleteTrue()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var chunk1 = "```\nA485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n";
            var chunk2 = "# Plan\n";
            var chunk3 = "```\n";

            // Act
            detector.ProcessChunk(chunk1);
            detector.ProcessChunk(chunk2);
            detector.ProcessChunk(chunk3);
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
        }

        [Fact]
        public void Detector_BuffersContentBetweenFences()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var stream = "```\nA485254C_7481_47BB_A8CF_45B8DEED2DD8.md\n# My Plan\n## Step 1\nDo something\n```\n";

            // Act
            detector.ProcessChunk(stream);
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var content = detector.GetBufferedContent();
            Assert.Contains("# My Plan", content);
            Assert.Contains("## Step 1", content);
            Assert.Contains("Do something", content);
        }

        [Fact]
        public void Detector_IgnoresOtherMarkdownFiles()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var stream = "```\nMyOtherFile.md\n# Some content\n```\n";

            // Act
            detector.ProcessChunk(stream);

            // Assert
            Assert.False(detector.IsComplete);
        }

        [Fact]
        public void Detector_HandlesChunkedInput()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act - feed input in small chunks
            detector.ProcessChunk("```");
            detector.ProcessChunk("\nA48");
            detector.ProcessChunk("5254C_7481_47BB_A8CF_45B8DEED2DD8.md\n");
            detector.ProcessChunk("Line 1\n");
            detector.ProcessChunk("Line 2\n");
            detector.ProcessChunk("```");
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var content = detector.GetBufferedContent();
            Assert.Contains("Line 1", content);
            Assert.Contains("Line 2", content);
        }

        [Fact]
        public void Detector_ThrowsWhenAccessingContentBeforeComplete()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => detector.GetBufferedContent());
        }

        [Fact]
        public void Detector_HandlesEmptyContentBetweenFences()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var stream = $"```\n{MarkerFileName}\n```\n";

            // Act
            detector.ProcessChunk(stream);
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
            var content = detector.GetBufferedContent();
            Assert.Empty(content);
        }

        [Fact]
        public void Detector_IgnoresIncompleteMarker()
        {
            // Arrange
            var detector = new PlanFileDetector();

            // Act - similar but not exact marker
            detector.ProcessChunk("```\nA485254C_7481_47BB_A8CF_45B8DEED2DD.md\n# Content\n```\n");

            // Assert
            Assert.False(detector.IsComplete);
        }

        [Fact]
        public void Detector_HandlesMultilineWithCarriageReturns()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var stream = "```\r\nA485254C_7481_47BB_A8CF_45B8DEED2DD8.md\r\n# Plan\r\n```\r\n";

            // Act
            detector.ProcessChunk(stream);
            detector.CompleteDetection();

            // Assert
            Assert.True(detector.IsComplete);
        }

        [Fact]
        public void Detector_Reset_ClearsState()
        {
            // Arrange
            var detector = new PlanFileDetector();
            detector.ProcessChunk($"```\n{MarkerFileName}\n# Content\n```\n");
            detector.CompleteDetection();
            Assert.True(detector.IsComplete);

            // Act
            detector.Reset();

            // Assert
            Assert.False(detector.IsComplete);
            Assert.Throws<InvalidOperationException>(() => detector.GetBufferedContent());
        }

        [Fact]
        public void Detector_HandlesMarkerWithAdditionalSpaces()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var stream = $"  ```\n  {MarkerFileName}\n  Content\n  ```\n";

            // Act
            detector.ProcessChunk(stream);
            detector.CompleteDetection();

            // Assert
            // Should detect marker even with leading whitespace
            Assert.True(detector.IsComplete);
        }

        [Fact]
        public void Detector_StopsProcessingAfterComplete()
        {
            // Arrange
            var detector = new PlanFileDetector();
            detector.ProcessChunk($"```\n{MarkerFileName}\n# Plan\n```\n");
            detector.CompleteDetection();
            Assert.True(detector.IsComplete);
            var firstContent = detector.GetBufferedContent();

            // Act - try to process more chunks after complete
            detector.ProcessChunk("More content that should be ignored\n");

            // Assert
            var secondContent = detector.GetBufferedContent();
            Assert.Equal(firstContent, secondContent);
        }

        [Fact]
        public void Detector_PreservesNewLines()
        {
            // Arrange
            var detector = new PlanFileDetector();
            var stream = "```\n" + MarkerFileName + "\nLine1\n\nLine3\n```\n";

            // Act
            detector.ProcessChunk(stream);
            detector.CompleteDetection();
            var content = detector.GetBufferedContent();

            // Assert
            Assert.Contains("Line1", content);
            Assert.Contains("Line3", content);
            // Verify blank line is preserved by checking line count or structure
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            Assert.True(lines.Length >= 3);
        }
    }
}
