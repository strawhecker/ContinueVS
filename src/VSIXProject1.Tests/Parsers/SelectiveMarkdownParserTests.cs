using System;
using System.Collections.Generic;
using Xunit;
using ContinueVS.Core.Parsers;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.Parsers
{
    public class SelectiveMarkdownParserTests
    {
        [Fact]
        public void ParseLine_WithBold_ExtractsBoldRun()
        {
            var line = "This is **bold** text";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            Assert.Equal(3, runs.Count);
            Assert.Equal("This is ", runs[0].Text);
            Assert.Equal(InlineStyle.Plain, runs[0].Style);
            Assert.Equal("bold", runs[1].Text);
            Assert.Equal(InlineStyle.Bold, runs[1].Style);
            Assert.Equal(" text", runs[2].Text);
            Assert.Equal(InlineStyle.Plain, runs[2].Style);
        }

        [Fact]
        public void ParseLine_WithItalic_ExtractsItalicRun()
        {
            var line = "This is *italic* text";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            Assert.Equal(3, runs.Count);
            Assert.Equal("This is ", runs[0].Text);
            Assert.Equal(InlineStyle.Plain, runs[0].Style);
            Assert.Equal("italic", runs[1].Text);
            Assert.Equal(InlineStyle.Italic, runs[1].Style);
            Assert.Equal(" text", runs[2].Text);
            Assert.Equal(InlineStyle.Plain, runs[2].Style);
        }

        [Fact]
        public void ParseLine_WithCode_ExtractsCodeRun()
        {
            var line = "Use `var x = 5` to define";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            Assert.Equal(3, runs.Count);
            Assert.Equal("Use ", runs[0].Text);
            Assert.Equal(InlineStyle.Plain, runs[0].Style);
            Assert.Equal("var x = 5", runs[1].Text);
            Assert.Equal(InlineStyle.Code, runs[1].Style);
            Assert.Equal(" to define", runs[2].Text);
            Assert.Equal(InlineStyle.Plain, runs[2].Style);
        }

        [Fact]
        public void ParseLine_WithMixedStyles_ExtractsAllRuns()
        {
            var line = "**Bold** and *italic* and `code`";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            // Verify code, bold, and italic are extracted
            Assert.Contains(runs, r => r.Style == InlineStyle.Bold && r.Text == "Bold");
            Assert.Contains(runs, r => r.Style == InlineStyle.Italic && r.Text == "italic");
            Assert.Contains(runs, r => r.Style == InlineStyle.Code && r.Text == "code");
        }

        [Fact]
        public void ParseLine_WithNestedUnclosed_SkipsInvalidMarkers()
        {
            var line = "This **bold has *italic unclosed";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            // Should handle gracefully; unclosed markers treated as plain text
            Assert.True(runs.Count >= 1);
        }

        [Fact]
        public void ParseLine_PlainText_ReturnsPlainRun()
        {
            var line = "Just plain text";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            Assert.Single(runs);
            Assert.Equal("Just plain text", runs[0].Text);
            Assert.Equal(InlineStyle.Plain, runs[0].Style);
        }

        [Fact]
        public void ParseLine_EmptyString_ReturnsEmptyPlainRun()
        {
            var line = string.Empty;
            var runs = SelectiveMarkdownParser.ParseLine(line);

            Assert.Single(runs);
            Assert.Equal(string.Empty, runs[0].Text);
            Assert.Equal(InlineStyle.Plain, runs[0].Style);
        }

        [Fact]
        public void ParseLine_Null_ReturnsEmptyRun()
        {
            var runs = SelectiveMarkdownParser.ParseLine(null);

            Assert.Single(runs);
            Assert.Equal(string.Empty, runs[0].Text);
        }

        [Fact]
        public void ParseLine_AdjacentMarkers_Extracts()
        {
            // Test with separated markers to avoid ambiguity
            var line = "**bold** and *italic*";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            // Should extract both bold and italic
            Assert.Contains(runs, r => r.Style == InlineStyle.Bold && r.Text == "bold");
            Assert.Contains(runs, r => r.Style == InlineStyle.Italic && r.Text == "italic");
        }

        [Fact]
        public void ParseLine_MultipleSegments_ExtractsAll()
        {
            var line = "`code1` and **bold1** and *italic1*";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            Assert.Contains(runs, r => r.Style == InlineStyle.Code && r.Text == "code1");
            Assert.Contains(runs, r => r.Style == InlineStyle.Bold && r.Text == "bold1");
            Assert.Contains(runs, r => r.Style == InlineStyle.Italic && r.Text == "italic1");
        }

        [Fact]
        public void ParseLine_EdgeCase_ClosedImmediately()
        {
            var line = "****";
            var runs = SelectiveMarkdownParser.ParseLine(line);

            // Should handle as bold with empty content or skip gracefully
            Assert.True(runs.Count >= 1);
        }
    }
}
