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
                .Returns<string>(mode => $"prompt-{mode}");
            return new ModeConfigRegistry(mock.Object);
        }

        [Fact]
        public void GetConfig_AskMode_ReturnsNoWriteNoLoop()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Ask);

            // Assert
            Assert.Equal(ChatMode.Ask, cfg.Mode);
            Assert.False(cfg.AllowWriteTools);
            Assert.False(cfg.AllowToolLoop);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.False(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_AgentMode_AllowsToolLoopAndWriteTools()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Agent);

            // Assert
            Assert.Equal(ChatMode.Agent, cfg.Mode);
            Assert.True(cfg.AllowWriteTools);
            Assert.True(cfg.AllowToolLoop);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.False(cfg.ExportsPlanFile);
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
            Assert.False(cfg.AllowToolLoop);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.True(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_DebugMode_AllowsToolLoopWriteToolsAndDebuggerContext()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Debug);

            // Assert
            Assert.Equal(ChatMode.Debug, cfg.Mode);
            Assert.True(cfg.AllowWriteTools);
            Assert.True(cfg.AllowToolLoop);
            Assert.True(cfg.RequiresDebuggerContext);
            Assert.False(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_ReasonMode_NoWriteNoLoop()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var cfg = registry.GetConfig(ChatMode.Reason);

            // Assert
            Assert.Equal(ChatMode.Reason, cfg.Mode);
            Assert.False(cfg.AllowWriteTools);
            Assert.False(cfg.AllowToolLoop);
            Assert.False(cfg.RequiresDebuggerContext);
            Assert.False(cfg.ExportsPlanFile);
        }

        [Fact]
        public void GetConfig_AllModes_SystemPromptNotEmpty()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act & Assert
            foreach (ChatMode mode in Enum.GetValues(typeof(ChatMode)))
            {
                var cfg = registry.GetConfig(mode);
                Assert.False(string.IsNullOrWhiteSpace(cfg.SystemPrompt),
                    $"SystemPrompt should not be empty for mode {mode}");
            }
        }

        [Fact]
        public void GetAllConfigs_ReturnsFiveEntries()
        {
            // Arrange
            var registry = CreateRegistry();

            // Act
            var all = registry.GetAllConfigs();

            // Assert
            Assert.Equal(5, all.Count);
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
    }
}
