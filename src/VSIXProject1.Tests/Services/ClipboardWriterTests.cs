#nullable enable

using System.Windows;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class ClipboardWriterTests
    {
        private const string SampleRaw =
            "# Title\n\n**bold** text\n\n```csharp\nint x = 1;\n```";

        [Fact]
        public void BuildDataObject_Pretty_ContainsBothRtfAndUnicodeText()
        {
            var data = ClipboardWriter.BuildDataObject(SampleRaw, MessageViewMode.Pretty);

            Assert.True(data.GetDataPresent(DataFormats.Rtf));
            Assert.True(data.GetDataPresent(DataFormats.UnicodeText));

            var rtf = (string)data.GetData(DataFormats.Rtf);
            var plain = (string)data.GetData(DataFormats.UnicodeText);
            Assert.StartsWith(@"{\rtf1", rtf);
            Assert.Equal(SampleRaw, plain); // single-sourced: plain is exactly the raw content
        }

        [Fact]
        public void BuildDataObject_Raw_ContainsOnlyUnicodeText()
        {
            var data = ClipboardWriter.BuildDataObject(SampleRaw, MessageViewMode.Raw);

            Assert.True(data.GetDataPresent(DataFormats.UnicodeText));
            Assert.False(data.GetDataPresent(DataFormats.Rtf));

            var plain = (string)data.GetData(DataFormats.UnicodeText);
            Assert.Equal(SampleRaw, plain); // byte-faithful: raw is exactly the source
        }

        [Fact]
        public void BuildDataObject_Raw_IsByteFaithful()
        {
            const string raw = "line1\nline2\n```\n{  curly  }\n```";
            var data = ClipboardWriter.BuildDataObject(raw, MessageViewMode.Raw);
            Assert.Equal(raw, (string)data.GetData(DataFormats.UnicodeText));
        }

        [Fact]
        public void BuildDataObject_Pretty_CoShipsExactPlainText()
        {
            // Even in pretty mode the plain format must be the exact raw (never a rich extract),
            // so code editors / VS still get usable text.
            const string raw = "some `inline` & \\ path";
            var data = ClipboardWriter.BuildDataObject(raw, MessageViewMode.Pretty);
            Assert.Equal(raw, (string)data.GetData(DataFormats.UnicodeText));
        }

        [Fact]
        public void BuildDataObject_EmptyContent_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ClipboardWriter.BuildDataObject(string.Empty, MessageViewMode.Pretty));
            Assert.Throws<System.ArgumentException>(() => ClipboardWriter.BuildDataObject(null!, MessageViewMode.Pretty));
        }

        [Fact]
        public void BuildDataObject_Raw_NoRtfFormatAtAll_OnlyUnicode()
        {
            var data = ClipboardWriter.BuildDataObject("x", MessageViewMode.Raw);
            // Only UnicodeText present; raw carries no rich interpretation (a guarantee, not a fallback).
            var formats = data.GetFormats();
            Assert.Contains(DataFormats.UnicodeText, formats);
            Assert.DoesNotContain(DataFormats.Rtf, formats);
        }
    }
}
