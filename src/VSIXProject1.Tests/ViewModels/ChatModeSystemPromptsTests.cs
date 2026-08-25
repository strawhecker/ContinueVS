#nullable enable

using System;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatModeSystemPromptsTests
    {
        [Theory]
        [InlineData(ChatMode.Ask)]
        [InlineData(ChatMode.Agent)]
        [InlineData(ChatMode.Plan)]
        public void SystemPrompts_AreNotEmpty_ForAllModes(ChatMode mode)
        {
            // Arrange
            var selectedPrompt = mode switch
            {
                ChatMode.Ask => ChatModeSystemPrompts.DEFAULT_ASK_SYSTEM_MESSAGE,
                ChatMode.Agent => ChatModeSystemPrompts.DEFAULT_AGENT_SYSTEM_MESSAGE,
                ChatMode.Plan => ChatModeSystemPrompts.DEFAULT_PLAN_SYSTEM_MESSAGE,
                _ => ""
            };

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(selectedPrompt));
            Assert.NotEmpty(selectedPrompt);
        }

        [Fact]
        public void SystemPrompts_AskMode_ContainsApplyButtonReference()
        {
            // Assert
            Assert.Contains("Apply", ChatModeSystemPrompts.DEFAULT_ASK_SYSTEM_MESSAGE, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SystemPrompts_AgentMode_ContainsToolReference()
        {
            // Assert
            Assert.Contains("tool", ChatModeSystemPrompts.DEFAULT_AGENT_SYSTEM_MESSAGE, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SystemPrompts_PlanMode_ContainsReadOnlyReference()
        {
            // Assert
            Assert.Contains("read-only", ChatModeSystemPrompts.DEFAULT_PLAN_SYSTEM_MESSAGE, StringComparison.OrdinalIgnoreCase);
        }
    }
}
