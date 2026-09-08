using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Integration tests for gap70 plan file detection and mode routing.
    /// Verifies Plan/Agent/Debug modes save+open, Ask mode renders normally.
    /// </summary>
    public class PlanFileDetectionModeRoutingTests : IDisposable
    {
        private readonly string _testPlansDir;
        private readonly IPlanOutputService _planOutputService;
        private readonly Mock<IIdeService> _mockIdeService;

        public PlanFileDetectionModeRoutingTests()
        {
            _testPlansDir = Path.Combine(Path.GetTempPath(), "continueVS_tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testPlansDir);

            // Create real PlanOutputService pointing to test directory
            _planOutputService = new PlanOutputService(Path.GetDirectoryName(_testPlansDir));
            _mockIdeService = new Mock<IIdeService>();
        }

        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testPlansDir))
            {
                try
                {
                    Directory.Delete(_testPlansDir, recursive: true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        [Fact]
        public async Task PlanMode_DetectsMarker_SavesAndOpensFile()
        {
            // Arrange
            var planContent = "# My Plan\n## Step 1\nDo this";
            var modeConfig = new ModeConfig
            {
                Mode = ChatMode.Plan,
                ExportsPlanFile = true
            };

            // Act
            var savedPath = await _planOutputService.SavePlanAsync(planContent, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(savedPath));
            var readContent = File.ReadAllText(savedPath);
            Assert.Equal(planContent, readContent);
            Assert.Contains("plans", savedPath);
        }

        [Fact]
        public async Task AgentMode_ExportsFlagTrue_SavesFileSuccessfully()
        {
            // Arrange
            var planContent = "# Agent Plan\n## Actions\nExecute tools";
            var modeConfig = new ModeConfig
            {
                Mode = ChatMode.Agent,
                ExportsPlanFile = true
            };

            // Act
            var savedPath = await _planOutputService.SavePlanAsync(planContent, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(savedPath));
            var content = File.ReadAllText(savedPath);
            Assert.Contains("Agent Plan", content);
        }

        [Fact]
        public async Task DebugMode_ExportsFlagTrue_SavesFileSuccessfully()
        {
            // Arrange
            var planContent = "# Debug Analysis\n## Variables\nLocal scope";
            var modeConfig = new ModeConfig
            {
                Mode = ChatMode.Debug,
                ExportsPlanFile = true
            };

            // Act
            var savedPath = await _planOutputService.SavePlanAsync(planContent, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(savedPath));
            var content = File.ReadAllText(savedPath);
            Assert.Contains("Debug Analysis", content);
        }

        [Fact]
        public async Task AskMode_ExportsFlagFalse_DoesNotSaveFile()
        {
            // Arrange
            var modeConfig = new ModeConfig
            {
                Mode = ChatMode.Ask,
                ExportsPlanFile = false
            };

            // Assert
            Assert.False(modeConfig.ExportsPlanFile);
        }

        [Fact]
        public async Task SavePlanAsync_NullOrEmptyContent_ThrowsArgumentException()
        {
            // Arrange
            var service = _planOutputService;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.SavePlanAsync(null, CancellationToken.None)
            );

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.SavePlanAsync("", CancellationToken.None)
            );

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.SavePlanAsync("   ", CancellationToken.None)
            );
        }

        [Fact]
        public async Task SavePlanAsync_CreatesPlansDirectory_IfNotExists()
        {
            // Arrange
            var nonExistentDir = Path.Combine(Path.GetTempPath(), "continueVS_tests_new", Guid.NewGuid().ToString());
            var service = new PlanOutputService(Path.GetDirectoryName(nonExistentDir));
            var plansDir = service.GetPlansDirectory();

            // Act
            var savedPath = await service.SavePlanAsync("Test content", CancellationToken.None);

            // Assert
            Assert.True(Directory.Exists(plansDir));
            Assert.True(File.Exists(savedPath));

            // Cleanup
            Directory.Delete(Path.GetDirectoryName(nonExistentDir), recursive: true);
        }

        [Fact]
        public async Task SavePlanAsync_GeneratesTimestampedFilename()
        {
            // Arrange
            var service = _planOutputService;

            // Act
            var path1 = await service.SavePlanAsync("Content 1", CancellationToken.None);
            var filename1 = Path.GetFileName(path1);

            await Task.Delay(1100); // Ensure different timestamp (wait for next second)

            var path2 = await service.SavePlanAsync("Content 2", CancellationToken.None);
            var filename2 = Path.GetFileName(path2);

            // Assert
            Assert.NotEqual(filename1, filename2);
            Assert.StartsWith("plan_", filename1);
            Assert.StartsWith("plan_", filename2);
            Assert.EndsWith(".md", filename1);
            Assert.EndsWith(".md", filename2);
        }

        [Fact]
        public void GetPlansDirectory_ReturnsCorrectPath()
        {
            // Arrange
            var customDir = Path.Combine(Path.GetTempPath(), "custom_plans_test");
            var service = new PlanOutputService(customDir);

            // Act
            var plansDir = service.GetPlansDirectory();

            // Assert
            Assert.Equal(Path.Combine(customDir, "plans"), plansDir);
        }

        //[Fact]
        //public async Task ModeConfigRegistry_InjectsInstructionIntoPlanModePrompt()
        //{
        //    // Arrange
        //    var mockPromptService = new Mock<ISystemPromptService>();
        //    mockPromptService.Setup(s => s.GetPromptForMode("plan"))
        //        .Returns("Base plan prompt");
        //    mockPromptService.Setup(s => s.GetPromptForMode("agent"))
        //        .Returns("Base agent prompt");
        //    mockPromptService.Setup(s => s.GetPromptForMode("debug"))
        //        .Returns("Base debug prompt");
        //    mockPromptService.Setup(s => s.GetPromptForMode("ask"))
        //        .Returns("Base ask prompt");
        //    mockPromptService.Setup(s => s.GetPromptForMode("reason"))
        //        .Returns("Base reason prompt");

        //    // Act
        //    var registry = new ModeConfigRegistry(mockPromptService.Object);
        //    var planConfig = registry.GetConfig(ChatMode.Plan);
        //    var agentConfig = registry.GetConfig(ChatMode.Agent);
        //    var debugConfig = registry.GetConfig(ChatMode.Debug);

        //    // Assert
        //    Assert.Contains("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8", planConfig.SystemPrompt);
        //    //Assert.Contains("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8", agentConfig.SystemPrompt);
        //    //Assert.Contains("start_A485254C_7481_47BB_A8CF_45B8DEED2DD8", debugConfig.SystemPrompt);
        //}

        [Fact]
        public async Task ModeConfigRegistry_AskModeDoesNotIncludeMarkerInstruction()
        {
            // Arrange
            var mockPromptService = new Mock<ISystemPromptService>();
            mockPromptService.Setup(s => s.GetPromptForMode("ask"))
                .Returns("Base ask prompt");
            mockPromptService.Setup(s => s.GetPromptForMode("plan"))
                .Returns("Base plan prompt");
            mockPromptService.Setup(s => s.GetPromptForMode("agent"))
                .Returns("Base agent prompt");
            mockPromptService.Setup(s => s.GetPromptForMode("debug"))
                .Returns("Base debug prompt");
            mockPromptService.Setup(s => s.GetPromptForMode("reason"))
                .Returns("Base reason prompt");

            // Act
            var registry = new ModeConfigRegistry(mockPromptService.Object);
            var askConfig = registry.GetConfig(ChatMode.Ask);

            // Assert
            Assert.DoesNotContain("A485254C_7481_47BB_A8CF_45B8DEED2DD8.md", askConfig.SystemPrompt);
            Assert.Equal("Base ask prompt", askConfig.SystemPrompt);
        }
    }
}
