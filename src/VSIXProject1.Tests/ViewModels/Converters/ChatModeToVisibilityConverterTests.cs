#nullable enable

using System;
using System.Globalization;
using System.Windows;
using Xunit;
using ContinueVS.ViewModels;
using ContinueVS.ViewModels.Converters;

namespace ContinueVS.Tests.ViewModels.Converters
{
    public class ChatModeToVisibilityConverterTests
    {
        [Fact]
        public void Convert_WithAskMode_ReturnsVisible()
        {
            // Arrange
            var converter = new ChatModeToVisibilityConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.Convert(ChatMode.Ask, typeof(Visibility), parameter: null!, culture);

            // Assert
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void Convert_WithAgentMode_ReturnsCollapsed()
        {
            // Arrange
            var converter = new ChatModeToVisibilityConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.Convert(ChatMode.Agent, typeof(Visibility), parameter: null!, culture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_WithPlanMode_ReturnsCollapsed()
        {
            // Arrange
            var converter = new ChatModeToVisibilityConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.Convert(ChatMode.Plan, typeof(Visibility), parameter: null!, culture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void Convert_WithNullValue_ReturnsCollapsed()
        {
            // Arrange
            var converter = new ChatModeToVisibilityConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.Convert(value: null!, typeof(Visibility), parameter: null!, culture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void ConvertBack_ThrowsNotSupportedException()
        {
            // Arrange
            var converter = new ChatModeToVisibilityConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                converter.ConvertBack(Visibility.Visible, typeof(ChatMode), parameter: null!, culture));
        }
    }
}
