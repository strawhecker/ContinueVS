using System.Globalization;
using ContinueVS.ViewModels.Converters;
using Xunit;

namespace ContinueVS.Tests.ViewModels.Converters
{
    /// <summary>
    /// Tests for the gap98 PercentToWidthTwoConverter: formats a context-usage value as a
    /// right-aligned, rounded-up whole percent in the shape "%2d%%".
    /// </summary>
    public class PercentToWidthTwoConverterTests
    {
        private readonly PercentToWidthTwoConverter _converter = new PercentToWidthTwoConverter();

        [Theory]
        [InlineData(0.0, " 0%")]
        [InlineData(0.4, " 1%")]   // 0.4 -> ceil -> 1
        [InlineData(44.2, "45%")]  // 44.2 -> ceil -> 45
        [InlineData(79.9, "80%")]  // 79.9 -> ceil -> 80
        [InlineData(99.5, "100%")] // 99.5 -> ceil -> 100
        [InlineData(100.0, "100%")]
        public void Convert_RoundsUpWholePercent(double input, string expected)
        {
            var result = _converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_Null_ReturnsZeroPercent()
        {
            var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(" 0%", result);
        }

        [Fact]
        public void Convert_Negative_ReturnsZeroPercent()
        {
            var result = _converter.Convert(-5.0, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(" 0%", result);
        }

        [Fact]
        public void Convert_ClampsAboveHundred()
        {
            var result = _converter.Convert(250.0, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("100%", result);
        }
    }
}
