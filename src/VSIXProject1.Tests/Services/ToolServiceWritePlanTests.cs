using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// gap84 tests for the write_plan built-in tool execution in ToolService.
    /// </summary>
    public class ToolServiceWritePlanTests
    {
        private Mock<IIdeService> CreateMockIdeService()
        {
            var mock = new Mock<IIdeService>();
            mock.Setup(s => s.ReadFileAsync(It.IsAny<string>())).ReturnsAsync("file content");
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService()
        {
            var mock = new Mock<IConfigService>();
            mock.Setup(s => s.GetEnabledTools())
                .Returns(BuiltInToolsRegistry.GetAllBuiltInTools() as IEnumerable<ToolDefinition>);
            mock.Setup(s => s.GetToolOverrideConfig()).Returns(new ToolOverrideConfig());
            mock.Setup(s => s.GetCurrentConfig()).Returns(new ContinueConfig());
            return mock;
        }

        private Mock<IPlanOutputService> CreateMockPlanService(string savedPath)
        {
            var mock = new Mock<IPlanOutputService>();
            mock.Setup(s => s.SavePlanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(savedPath);
            return mock;
        }

        [Fact]
        public async Task WritePlan_Success_SavesAndOpensFile()
        {
            const string savedPath = @"C:\Users\test\.continueVS\plans\refactor_20240101_000000.md";
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var planServiceMock = CreateMockPlanService(savedPath);

            var service = new ToolService(
                ideServiceMock.Object,
                configServiceMock.Object,
                planOutputService: planServiceMock.Object);

            var result = await service.InvokeAsync(
                "write_plan",
                new Dictionary<string, object>
                {
                    { "title", "Refactor Plan" },
                    { "plan", "# Refactor Plan\nStep 1..." }
                });

            Assert.True(result.IsSuccess);
            Assert.Equal("write_plan", result.ToolName);
            Assert.Equal($"Plan saved to {savedPath}", result.Output);
            planServiceMock.Verify(
                s => s.SavePlanAsync("Refactor Plan", "# Refactor Plan\nStep 1...", It.IsAny<CancellationToken>()),
                Times.Once);
            ideServiceMock.Verify(s => s.OpenFileInEditorAsync(savedPath), Times.Once);
        }

        [Fact]
        public async Task WritePlan_EmptyPlan_FailsAndDoesNotSave()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var planServiceMock = CreateMockPlanService(@"C:\path_ignored.md");

            var service = new ToolService(
                ideServiceMock.Object,
                configServiceMock.Object,
                planOutputService: planServiceMock.Object);

            var result = await service.InvokeAsync(
                "write_plan",
                new Dictionary<string, object>
                {
                    { "title", "Plan Title" },
                    { "plan", "" }
                });

            Assert.False(result.IsSuccess);
            Assert.Contains("plan cannot be null or empty", result.Output);
            planServiceMock.Verify(
                s => s.SavePlanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            ideServiceMock.Verify(s => s.OpenFileInEditorAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task WritePlan_EmptyTitle_UsesPlanFallback()
        {
            const string savedPath = @"C:\Users\test\.continueVS\plans\plan_20240101_000000.md";
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var planServiceMock = CreateMockPlanService(savedPath);

            var service = new ToolService(
                ideServiceMock.Object,
                configServiceMock.Object,
                planOutputService: planServiceMock.Object);

            var result = await service.InvokeAsync(
                "write_plan",
                new Dictionary<string, object>
                {
                    { "title", "   " },
                    { "plan", "# Plan body content" }
                });

            Assert.True(result.IsSuccess);
            planServiceMock.Verify(
                s => s.SavePlanAsync("plan", "# Plan body content", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task WritePlan_NullPlanService_Fails()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();

            var service = new ToolService(
                ideServiceMock.Object,
                configServiceMock.Object);

            var result = await service.InvokeAsync(
                "write_plan",
                new Dictionary<string, object>
                {
                    { "title", "Title" },
                    { "plan", "Some plan content" }
                });

            Assert.False(result.IsSuccess);
            Assert.Contains("Plan output service not available", result.Output);
        }

        [Fact]
        public async Task WritePlan_WhenSaveThrows_ReturnsError()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var planServiceMock = new Mock<IPlanOutputService>();
            planServiceMock.Setup(s => s.SavePlanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disk full"));

            var service = new ToolService(
                ideServiceMock.Object,
                configServiceMock.Object,
                planOutputService: planServiceMock.Object);

            var result = await service.InvokeAsync(
                "write_plan",
                new Dictionary<string, object>
                {
                    { "title", "Title" },
                    { "plan", "Some plan content" }
                });

            Assert.False(result.IsSuccess);
            Assert.Contains("disk full", result.Output);
        }

        [Fact]
        public void GetAvailableTools_IncludesWritePlan()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var toolNames = service.GetAvailableTools().Select(t => t.Name).ToList();
            Assert.Contains("write_plan", toolNames);
        }

        [Fact]
        public void GetAvailableTools_WritePlan_AvailableInPlanAndAskModes()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var planNames = service.GetAvailableTools(ChatMode.Plan).Select(t => t.Name).ToList();
            var askNames = service.GetAvailableTools(ChatMode.Ask).Select(t => t.Name).ToList();

            Assert.Contains("write_plan", planNames);
            Assert.Contains("write_plan", askNames);
        }
    }
}
