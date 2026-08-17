using System;
using System.Globalization;
using ContinueVS.ViewModels;
using ContinueVS.ViewModels.Converters;
using Xunit;

namespace ContinueVS.Tests.ViewModels.Converters
{
    public class ChatModeConverterTests
    {
        private readonly ChatModeToBoolConverter _converter = new();

        [Fact]
        public void Convert_AskModeWithAskParameter_ReturnsTrue()
        {
            var result = _converter.Convert(ChatMode.Ask, typeof(bool), "Ask", CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void Convert_AskModeWithAgentParameter_ReturnsFalse()
        {
            var result = _converter.Convert(ChatMode.Ask, typeof(bool), "Agent", CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_AskModeWithPlanParameter_ReturnsFalse()
        {
            var result = _converter.Convert(ChatMode.Ask, typeof(bool), "Plan", CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_AgentModeWithAgentParameter_ReturnsTrue()
        {
            var result = _converter.Convert(ChatMode.Agent, typeof(bool), "Agent", CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void Convert_AgentModeWithAskParameter_ReturnsFalse()
        {
            var result = _converter.Convert(ChatMode.Agent, typeof(bool), "Ask", CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_PlanModeWithPlanParameter_ReturnsTrue()
        {
            var result = _converter.Convert(ChatMode.Plan, typeof(bool), "Plan", CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void Convert_PlanModeWithAskParameter_ReturnsFalse()
        {
            var result = _converter.Convert(ChatMode.Plan, typeof(bool), "Ask", CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_PlanModeWithAgentParameter_ReturnsFalse()
        {
            var result = _converter.Convert(ChatMode.Plan, typeof(bool), "Agent", CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void ConvertBack_TrueWithAskParameter_ReturnsAskMode()
        {
            var result = _converter.ConvertBack(true, typeof(ChatMode), "Ask", CultureInfo.InvariantCulture);
            Assert.Equal(ChatMode.Ask, (ChatMode)result);
        }

        [Fact]
        public void ConvertBack_TrueWithAgentParameter_ReturnsAgentMode()
        {
            var result = _converter.ConvertBack(true, typeof(ChatMode), "Agent", CultureInfo.InvariantCulture);
            Assert.Equal(ChatMode.Agent, (ChatMode)result);
        }

        [Fact]
        public void ConvertBack_TrueWithPlanParameter_ReturnsPlanMode()
        {
            var result = _converter.ConvertBack(true, typeof(ChatMode), "Plan", CultureInfo.InvariantCulture);
            Assert.Equal(ChatMode.Plan, (ChatMode)result);
        }

        [Fact]
        public void ConvertBack_FalseValue_ReturnsAskMode()
        {
            var result = _converter.ConvertBack(false, typeof(ChatMode), "Plan", CultureInfo.InvariantCulture);
            Assert.Equal(ChatMode.Ask, (ChatMode)result);
        }

        [Fact]
        public void ConvertBack_InvalidParameter_ReturnsAskMode()
        {
            var result = _converter.ConvertBack(true, typeof(ChatMode), "InvalidMode", CultureInfo.InvariantCulture);
            Assert.Equal(ChatMode.Ask, (ChatMode)result);
        }

        [Fact]
        public void Convert_NullValue_ReturnsFalse()
        {
            var result = _converter.Convert(null, typeof(bool), "Ask", CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_CaseInsensitiveParameter_WorksCorrectly()
        {
            var result = _converter.Convert(ChatMode.Ask, typeof(bool), "ask", CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void ConvertBack_CaseInsensitiveParameter_WorksCorrectly()
        {
            var result = _converter.ConvertBack(true, typeof(ChatMode), "plan", CultureInfo.InvariantCulture);
            Assert.Equal(ChatMode.Plan, (ChatMode)result);
        }
    }
}
