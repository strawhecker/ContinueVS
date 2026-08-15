#nullable enable

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Xunit;
using ContinueVS.ViewModels.Converters;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.UI
{
    public class ConverterTests
    {
        [Theory]
        [InlineData(true, Visibility.Visible)]
        [InlineData(false, Visibility.Collapsed)]
        public void BooleanToVisibilityConverter_Convert_ReturnsCorrectVisibility(bool input, Visibility expected)
        {
            // Arrange
            var converter = new BooleanToVisibilityConverter();

            // Act
            var result = converter.Convert(input, typeof(Visibility), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BooleanToVisibilityConverter_Convert_WithNullValue_ReturnsCollapsed()
        {
            // Arrange
            var converter = new BooleanToVisibilityConverter();

            // Act
            var result = converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void BooleanToVisibilityConverter_Convert_WithNonBoolValue_ReturnsCollapsed()
        {
            // Arrange
            var converter = new BooleanToVisibilityConverter();

            // Act
            var result = converter.Convert("not a bool", typeof(Visibility), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Theory]
        [InlineData(Visibility.Visible, true)]
        [InlineData(Visibility.Collapsed, false)]
        [InlineData(Visibility.Hidden, false)]
        public void BooleanToVisibilityConverter_ConvertBack_ReturnsCorrectBoolean(Visibility input, bool expected)
        {
            // Arrange
            var converter = new BooleanToVisibilityConverter();

            // Act
            var result = converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BooleanToVisibilityConverter_ConvertBack_WithNullValue_ReturnsFalse()
        {
            // Arrange
            var converter = new BooleanToVisibilityConverter();

            // Act
            var result = converter.ConvertBack(null, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(false, result);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void InverseBooleanConverter_Convert_ReturnsNegatedBoolean(bool input, bool expected)
        {
            // Arrange
            var converter = new InverseBooleanConverter();

            // Act
            var result = converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void InverseBooleanConverter_Convert_WithNullValue_ReturnsTrue()
        {
            // Arrange
            var converter = new InverseBooleanConverter();

            // Act
            var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public void InverseBooleanConverter_Convert_WithNonBoolValue_ReturnsTrue()
        {
            // Arrange
            var converter = new InverseBooleanConverter();

            // Act
            var result = converter.Convert("not a bool", typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(true, result);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void InverseBooleanConverter_ConvertBack_ReturnsNegatedBoolean(bool input, bool expected)
        {
            // Arrange
            var converter = new InverseBooleanConverter();

            // Act
            var result = converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0, "0%")]
        [InlineData(50, "50%")]
        [InlineData(100, "100%")]
        [InlineData(150, "100%")] // Clamped to 100
        [InlineData(-50, "0%")]   // Clamped to 0
        public void ProgressPercentageConverter_Convert_WithIntInput_ReturnsFormattedPercentage(int input, string expected)
        {
            // Arrange
            var converter = new ProgressPercentageConverter();

            // Act
            var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0.0, "0%")]
        [InlineData(0.5, "50%")]
        [InlineData(1.0, "100%")]
        public void ProgressPercentageConverter_Convert_WithDecimalInput_ReturnsFormattedPercentage(double input, string expected)
        {
            // Arrange
            var converter = new ProgressPercentageConverter();

            // Act
            var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ProgressPercentageConverter_Convert_WithNullValue_ReturnsZeroPercent()
        {
            // Arrange
            var converter = new ProgressPercentageConverter();

            // Act
            var result = converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal("0%", result);
        }

        [Fact]
        public void ProgressPercentageConverter_Convert_WithInvalidValue_ReturnsZeroPercent()
        {
            // Arrange
            var converter = new ProgressPercentageConverter();

            // Act
            var result = converter.Convert("not a number", typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal("0%", result);
        }

        [Theory]
        [InlineData("0%", 0.0)]
        [InlineData("50%", 0.5)]
        [InlineData("100%", 1.0)]
        public void ProgressPercentageConverter_ConvertBack_WithPercentageString_ReturnsDecimalValue(string input, double expected)
        {
            // Arrange
            var converter = new ProgressPercentageConverter();

            // Act
            var result = converter.ConvertBack(input, typeof(double), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            var doubleResult = (double?)result;
            Assert.NotNull(doubleResult);
            Assert.True(System.Math.Abs(doubleResult.Value - expected) < 0.01);
        }

        [Fact]
        public void ProgressPercentageConverter_ConvertBack_WithNullValue_ReturnsZero()
        {
            // Arrange
            var converter = new ProgressPercentageConverter();

            // Act
            var result = converter.ConvertBack(null, typeof(double), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0.0, result);
        }

        [Theory]
        [InlineData(ChatMessageRole.User, HorizontalAlignment.Right)]
        [InlineData(ChatMessageRole.Assistant, HorizontalAlignment.Left)]
        [InlineData(ChatMessageRole.System, HorizontalAlignment.Stretch)]
        [InlineData(ChatMessageRole.Tool, HorizontalAlignment.Stretch)]
        [InlineData(ChatMessageRole.Thinking, HorizontalAlignment.Stretch)]
        public void RoleToAlignmentConverter_Convert_ReturnsCorrectAlignment(ChatMessageRole role, HorizontalAlignment expected)
        {
            // Arrange
            var converter = new RoleToAlignmentConverter();

            // Act
            var result = converter.Convert(role, typeof(HorizontalAlignment), string.Empty, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void RoleToAlignmentConverter_Convert_WithNullValue_ReturnsStretch()
        {
            // Arrange
            var converter = new RoleToAlignmentConverter();

            // Act
            var result = converter.Convert((object?)null, typeof(HorizontalAlignment), string.Empty, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(HorizontalAlignment.Stretch, result);
        }

        [Fact]
        public void RoleToAlignmentConverter_ConvertBack_ReturnsUnsetValue()
        {
            // Arrange
            var converter = new RoleToAlignmentConverter();

            // Act
            var result = converter.ConvertBack(HorizontalAlignment.Right, typeof(ChatMessageRole), string.Empty, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(DependencyProperty.UnsetValue, result);
        }

        [Theory]
        [InlineData(ChatMessageRole.User)]
        [InlineData(ChatMessageRole.Assistant)]
        [InlineData(ChatMessageRole.System)]
        public void RoleToColorConverter_Convert_ReturnsSolidColorBrush(ChatMessageRole role)
        {
            // Arrange
            var converter = new RoleToColorConverter();

            // Act
            var result = converter.Convert(role, typeof(Brush), string.Empty, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsType<SolidColorBrush>(result);
        }

        [Fact]
        public void RoleToColorConverter_ConvertBack_ReturnsUnsetValue()
        {
            // Arrange
            var converter = new RoleToColorConverter();

            // Act
            var result = converter.ConvertBack(Brushes.Red, typeof(ChatMessageRole), string.Empty, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(DependencyProperty.UnsetValue, result);
        }
    }
}
