using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Media;
using ContinueVS.Core.Parsers;
using Xunit;

namespace ContinueVS.Tests.Parsers
{
    public class SelectiveMarkdownParserTests
    {
        [Fact]
        public void ParseLine_WithBoldText_ReturnsBoldRun()
        {
            // Arrange
            string input = "This is **bold** text";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            Assert.True(runs.Any(r => r.FontWeight == FontWeights.Bold && r.Text == "bold"));
        }

        [Fact]
        public void ParseLine_WithItalicText_ReturnsItalicRun()
        {
            // Arrange
            string input = "This is *italic* text";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            Assert.True(runs.Any(r => r.FontStyle == FontStyles.Italic && r.Text == "italic"));
        }

        [Fact]
        public void ParseLine_WithCodeText_ReturnsCodeRun()
        {
            // Arrange
            string input = "This is `code` text";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            Assert.True(runs.Any(r => r.FontFamily?.Source == "Courier New" && r.Text == "code"));
        }

        [Fact]
        public void ParseLine_WithMixedFormatting_ReturnsMixedRuns()
        {
            // Arrange
            string input = "**bold** and *italic* and `code`";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            Assert.True(runs.Any(r => r.FontWeight == FontWeights.Bold), "Bold run not found");
            Assert.True(runs.Any(r => r.FontStyle == FontStyles.Italic), "Italic run not found");
            Assert.True(runs.Any(r => r.FontFamily?.Source == "Courier New"), "Code run not found");
        }

        [Fact]
        public void ParseLine_WithPlainText_ReturnsPlainRun()
        {
            // Arrange
            string input = "This is plain text";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            Assert.Single(runs);
            Assert.Equal("This is plain text", runs[0].Text);
        }

        [Fact]
        public void ParseLine_WithEmptyString_ReturnsEmptyRun()
        {
            // Arrange
            string input = string.Empty;

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            Assert.Single(runs);
            Assert.Equal(string.Empty, runs[0].Text);
        }

        [Fact]
        public void ParseLine_WithUnclosedBold_TreatsAsPlainText()
        {
            // Arrange
            string input = "This is **unclosed bold text";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            // Unclosed delimiters should not be treated as formatting
            Assert.DoesNotContain(runs, r => r.FontWeight == FontWeights.Bold);
        }

        [Fact]
        public void ParseLine_WithAdjacentBold_ParsesBothCorrectly()
        {
            // Arrange
            string input = "**bold1** **bold2**";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            var boldRuns = runs.Where(r => r.FontWeight == FontWeights.Bold).ToList();
            Assert.Equal(2, boldRuns.Count);
            Assert.Contains(boldRuns, r => r.Text == "bold1");
            Assert.Contains(boldRuns, r => r.Text == "bold2");
        }

        [Fact]
        public void ParseLine_WithNestedFormatting_ParsesOuterOnly()
        {
            // Arrange
            string input = "**bold with *italic* inside**";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            // Bold segment should contain the full text including markdown delimiters
            Assert.True(runs.Any(r => r.FontWeight == FontWeights.Bold));
        }

        [Fact]
        public void ParseLine_PreservesTextContent()
        {
            // Arrange
            string input = "Hello **world** and `code`";
            var expectedContent = "Hello world and code";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);
            var actualContent = string.Concat(runs.Select(r => r.Text));

            // Assert
            Assert.Equal(expectedContent, actualContent);
        }

        [Fact]
        public void ParseLine_WithMultipleCodeSegments_ParsesBothCorrectly()
        {
            // Arrange
            string input = "Use `var x = 1` and `new Object()`";

            // Act
            var runs = SelectiveMarkdownParser.ParseLine(input);

            // Assert
            Assert.NotNull(runs);
            var codeRuns = runs.Where(r => r.FontFamily?.Source == "Courier New").ToList();
            Assert.Equal(2, codeRuns.Count);
            Assert.Contains(codeRuns, r => r.Text == "var x = 1");
            Assert.Contains(codeRuns, r => r.Text == "new Object()");
        }
    }
}
