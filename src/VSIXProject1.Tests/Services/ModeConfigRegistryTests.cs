#nullable enable

using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Services
{
    public class ModeConfigRegistryTests
    {
        private static IModeConfigRegistry CreateRegistry()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(s => s.GetPromptForMode(It.IsAny<string>()))
                .Returns<string>(mode => mode.Equals("bare", StringComparison.OrdinalIgnoreCase)
                    ? "Always include the language and file name in the info string when you write code blocks."
                    : $"prompt-{mode}");
            return new ModeConfigRegistry(mock.Object);
        }

        [Fact]
        public void GetConfig_AskMode_ReturnsNoWrite()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Ask);

            // Assert
            Assert.Equal(ChatMode.Ask, cfg.Mode);
            Assert.False(cfg.AllowWriteTools);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.False(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_AgentMode_AllowsToolsAndPhaseExecution()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Agent);

            // Assert
            Assert.Equal(ChatMode.Agent, cfg.Mode);
            Assert.True(cfg.AllowWriteTools);
            Assert.True(cfg.AllowPhaseExecution);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.True(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_PlanMode_ExportsPlanFile_NoWriteTools()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Plan);

            // Assert
            Assert.Equal(ChatMode.Plan, cfg.Mode);
            Assert.False(cfg.AllowWriteTools);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.True(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_DebugMode_AllowsToolsPhaseExecutionAndDebuggerContext()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Debug);

            // Assert
            Assert.Equal(ChatMode.Debug, cfg.Mode);
            Assert.True(cfg.AllowWriteTools);
            Assert.True(cfg.AllowPhaseExecution);
            Assert.True(cfg.RequiresDebuggerContext);
            Assert.True(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_ReasonMode_NoWrite()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Reason);

            // Assert
            Assert.Equal(ChatMode.Reason, cfg.Mode);
            Assert.False(cfg.AllowWriteTools);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.False(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_AllModes_SystemPromptHasNoModeIdentity_ForBare()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act & Assert
            foreach (ChatMode mode in Enum.GetValues(typeof(ChatMode)))
            {
                var cfg = registry.GetConfig(mode);
                Assert.False(string.IsNullOrWhiteSpace(cfg.SystemPrompt),
                    $"SystemPrompt should not be empty for mode {mode}");

                // gap97: Bare's prompt must carry NO mode-identity prose (no "You are in ... mode")
                // while every other mode does.
                if (mode == ChatMode.Bare)
                {
                    Assert.DoesNotContain("You are in", cfg.SystemPrompt, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        [Fact]
        public void GetAllConfigs_ReturnsSixEntries()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var all = registry.GetAllConfigs();

            // Assert
            Assert.Equal(6, all.Count);
        }

        [Fact]
        public void GetConfig_BareMode_NoWriteNoPhaseNoDebuggerNoPlanExport()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Bare);

            // Assert — bare is raw and read-only: no write tools, no phase execution,
            // no debugger context, no plan export.
            Assert.Equal(ChatMode.Bare, cfg.Mode);
            Assert.False(cfg.AllowWriteTools);
            Assert.False(cfg.AllowPhaseExecution);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.False(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_UnknownMode_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                registry.GetConfig((ChatMode)999));
        }

        [Theory]
        [InlineData(ChatMode.Ask,    false)]
        [InlineData(ChatMode.Agent,  true)]
        [InlineData(ChatMode.Plan,   false)]
        [InlineData(ChatMode.Debug,  true)]
        [InlineData(ChatMode.Reason, false)]
        [InlineData(ChatMode.Bare,   false)]
        public void GetConfig_AllowPhaseExecution_TrueForAgentAndDebugOnly(
            ChatMode mode, bool expected)
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(mode);

            // Assert
            Assert.Equal(expected, cfg.AllowPhaseExecution);
        }

        [Theory]
        [InlineData(ChatMode.Ask,    false)]
        [InlineData(ChatMode.Agent,  true)]
        [InlineData(ChatMode.Plan,   true)]
        [InlineData(ChatMode.Debug,  true)]
        [InlineData(ChatMode.Reason, false)]
        [InlineData(ChatMode.Bare,   false)]
        public void GetConfig_ExportsPlanFile_TrueForAgentPlanAndDebug(
            ChatMode mode, bool expected)
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(mode);

            // Assert
            Assert.Equal(expected, cfg.ExportsPlanFile);
        }
    }
}
