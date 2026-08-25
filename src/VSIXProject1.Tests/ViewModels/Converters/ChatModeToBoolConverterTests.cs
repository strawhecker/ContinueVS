#nullable enable

using System;
using System.Globalization;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.ViewModels;
using ContinueVS.ViewModels.Converters;

namespace ContinueVS.Tests.ViewModels.Converters
{
    public class ChatModeToBoolConverterTests
    {
        [Fact]
        public void Convert_WithAskModeAndAskParameter_ReturnsTrue()
        {
            // Arrange
            var converter = new ChatModeToBoolConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.Convert(ChatMode.Ask, typeof(bool), "Ask", culture);

            // Assert
            Assert.True((bool)result);
        }

        [Fact]
        public void Convert_WithAgentModeAndAskParameter_ReturnsFalse()
        {
            // Arrange
            var converter = new ChatModeToBoolConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.Convert(ChatMode.Agent, typeof(bool), "Ask", culture);

            // Assert
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_WithPlanModeAndPlanParameter_ReturnsTrue()
        {
            // Arrange
            var converter = new ChatModeToBoolConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.Convert(ChatMode.Plan, typeof(bool), "Plan", culture);

            // Assert
            Assert.True((bool)result);
        }

        [Fact]
        public void ConvertBack_WithTrueAndAgentParameter_ReturnsAgentMode()
        {
            // Arrange
            var converter = new ChatModeToBoolConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.ConvertBack(true, typeof(ChatMode), "Agent", culture);

            // Assert
            Assert.Equal(ChatMode.Agent, result);
        }

        [Fact]
        public void ConvertBack_WithFalse_ReturnsAskMode()
        {
            // Arrange
            var converter = new ChatModeToBoolConverter();
            var culture = CultureInfo.InvariantCulture;

            // Act
            var result = converter.ConvertBack(false, typeof(ChatMode), "Plan", culture);

            // Assert
            Assert.Equal(ChatMode.Ask, result);
        }
    }
}
