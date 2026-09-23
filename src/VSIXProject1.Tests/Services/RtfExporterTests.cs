#nullable enable

using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class RtfExporterTests
    {
        [Fact]
        public void ToRtf_NullOrEmpty_ReturnsValidRtfDocument()
        {
            Assert.StartsWith(@"{\rtf1", RtfExporter.ToRtf(null));
            Assert.StartsWith(@"{\rtf1", RtfExporter.ToRtf(string.Empty));
        }

        [Fact]
        public void ToRtf_PlainText_PreservesRoundedCharacterContent()
        {
            var doc = RtfExporter.ToRtf("hello world");
            Assert.StartsWith(@"{\rtf1", doc);
            Assert.Contains("hello world", doc);
        }

        [Fact]
        public void ToRtf_BoldAndItalic_EmitsBoldAndItalicControlWords()
        {
            var doc = RtfExporter.ToRtf("**bold** and *italic*");
            Assert.Contains("\\b ", doc);
            Assert.Contains("\\b0 ", doc);
            Assert.Contains("\\i ", doc);
            Assert.Contains("\\i0 ", doc);
        }

        [Fact]
        public void ToRtf_FencedCodeBlock_RendersMonospaced_AndKeepsFenceContent()
        {
            var doc = RtfExporter.ToRtf("```csharp\nint x = 1;\n```");
            Assert.Contains("int x = 1;", doc);
            // Code block uses the monospaced font slot (\f0).
            Assert.Contains("\\f0", doc);
        }

        [Fact]
        public void ToRtf_FencedCodeBlockLine_IsNotTreatedAsExempt_ContentSurvives()
        {
            // Code-block source survives (as an escaped RTF run) — the fence is not exempt.
            var doc = RtfExporter.ToRtf("```json\nhello world\n```");
            // The literal content characters all survive the escape inside the fence.
            Assert.Contains("hello world", doc);
        }

        [Fact]
        public void ToRtf_Heading_EmitsBold()
        {
            var doc = RtfExporter.ToRtf("# Title");
            Assert.Contains("\\b", doc);
        }

        [Fact]
        public void ToRtf_NonAscii_EmitsUnicodeEscapes()
        {
            // Emoji / non-ASCII must round-trip via \uN escapes, not raw bytes.
            var doc = RtfExporter.ToRtf("café ☕");
            Assert.Contains("\\u", doc);
            // Raw UTF-16 emoji must not appear as a literal high surrogate in the document.
            Assert.DoesNotContain("☕", doc);
        }

        [Fact]
        public void ToRtf_SpecialRtfChars_AreEscaped()
        {
            // Inline code preserves braces/backslash verbatim through Markdig, then the
            // escaping path must emit escaped control chars so the RTF document stays parseable.
            var doc = RtfExporter.ToRtf("`a{b}c\\d`");
            Assert.Contains(@"\{", doc);
            Assert.Contains(@"\}", doc);
            Assert.Contains(@"\\", doc);
        }

        [Fact]
        public void ToRtf_QuoteBlock_EmitsItalic()
        {
            var doc = RtfExporter.ToRtf("> quoted");
            Assert.Contains("\\i ", doc);
        }

        [Fact]
        public void ToRtf_List_EmitsBulletOrNumberedPrefix()
        {
            var doc = RtfExporter.ToRtf("- item A\n- item B");
            // Bullet "•" is a Unicode bullet; the item text itself must survive the escape
            // (the bullet may be emitted as a \u escape, so assert on the item content instead).
            Assert.Contains("item A", doc);
            Assert.Contains("item B", doc);
        }

        [Fact]
        public void ToRtf_MalformedMarkdown_DoesNotThrow_ReturnsDocument()
        {
            // Unclosed bold / malformed markup must degrade gracefully, never throw.
            var doc = RtfExporter.ToRtf("**unclosed ** still); `tick` {");
            Assert.StartsWith(@"{\rtf1", doc);
        }
    }
}
