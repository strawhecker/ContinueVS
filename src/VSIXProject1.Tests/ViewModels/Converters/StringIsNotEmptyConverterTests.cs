#nullable enable

using System.Globalization;
using ContinueVS.ViewModels.Converters;
using Xunit;

namespace ContinueVS.Tests.ViewModels.Converters
{
    /// <summary>
    /// gap86_1: Tests for the StringIsNotEmptyConverter that drives the "Use text below as
    /// answer" claim button's IsEnabled state. The claim action copies (never cuts/clears) the
    /// main composer text, so the button must only be enabled when the composer has content.
    /// </summary>
    public class StringIsNotEmptyConverterTests
    {
        private readonly StringIsNotEmptyConverter _converter = new StringIsNotEmptyConverter();

        [Theory]
        [InlineData("hello", true)]
        [InlineData("with spaces", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        public void Convert_ReturnsWhetherStringIsNonEmpty(object? input, bool expected)
        {
            var result = _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_ThrowsNotSupported()
        {
            Assert.Throws<System.NotSupportedException>(() =>
                _converter.ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture));
        }
    }
}
