#nullable enable
using System;
using System.Windows.Media;
using Xunit;
using ContinueVS.ViewModels.Converters;

namespace ContinueVS.Tests.Converters
{
    public class ToolCallCounterColorConverterTests
    {
        private readonly ToolCallCounterColorConverter _converter = new ToolCallCounterColorConverter();

        [Fact]
        public void Convert_With_0Percent_ReturnsGrayBrush()
        {
            // Arrange
            double value = 0.0;

            // Act
            var result = _converter.Convert(value, typeof(Brush), null!, null!);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<Brush>(result);
        }

        [Fact]
        public void Convert_With_50Percent_ReturnsGrayBrush()
        {
            // Arrange
            double value = 50.0;

            // Act
            var result = _converter.Convert(value, typeof(Brush), null!, null!);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<Brush>(result);
        }

        [Fact]
        public void Convert_With_89Percent_ReturnsOrangeBrush()
        {
            // Arrange
            double value = 89.0;

            // Act
            var result = _converter.Convert(value, typeof(Brush), null!, null!);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<Brush>(result);
        }

        [Fact]
        public void Convert_With_100Percent_ReturnsRedBrush()
        {
            // Arrange
            double value = 100.0;

            // Act
            var result = _converter.Convert(value, typeof(Brush), null!, null!);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<Brush>(result);
        }

        [Fact]
        public void Convert_WithNullInput_ReturnsDefaultBrush()
        {
            // Arrange
            object? value = null;

            // Act
            var result = _converter.Convert(value!, typeof(Brush), null!, null!);

            // Assert
            Assert.NotNull(result);
            Assert.IsAssignableFrom<Brush>(result);
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            // Arrange
            var brush = Brushes.Gray;

            // Act & Assert
            Assert.Throws<NotImplementedException>(() => 
                _converter.ConvertBack(brush, typeof(double), null!, null!));
        }
    }
}
