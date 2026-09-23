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

#nullable enable

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests for the read_plan / update_plan built-in tools plus the active-plan binding
    /// (set/keep/clear semantics) consumed by them.
    /// </summary>
    public class ToolServiceUpdatePlanTests
    {
        private Mock<IIdeService> CreateMockIdeService(string gitRoot, string? activeDoc = null)
        {
            var mock = new Mock<IIdeService>();
            mock.Setup(s => s.GetGitRootPathAsync()).ReturnsAsync(gitRoot ?? string.Empty);
            if (activeDoc != null)
                mock.Setup(s => s.GetActiveDocumentPathAsync()).ReturnsAsync(activeDoc);
            else
                mock.Setup(s => s.GetActiveDocumentPathAsync()).ReturnsAsync("none");
            mock.Setup(s => s.IsOpenInViewerAsync(It.IsAny<string>())).ReturnsAsync(false);
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService()
        {
            var mock = new Mock<IConfigService>();
            mock.Setup(s => s.GetEnabledTools())
                .Returns(BuiltInToolsRegistry.GetAllBuiltInTools() as IEnumerable<ToolDefinition>);
            return mock;
        }

        [Fact]
        public void SetAndGetActivePlanBinding_RoundTrips()
        {
            var ideServiceMock = CreateMockIdeService(string.Empty);
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            Assert.False(service.GetActivePlanBinding().isBound);

            service.SetActivePlanPath(@"C:\Users\test\.continueVS\plans\refactor_20240101_000000.md");
            var (isBound, path) = service.GetActivePlanBinding();
            Assert.True(isBound);
            Assert.Equal(@"C:\Users\test\.continueVS\plans\refactor_20240101_000000.md", path);

            service.SetActivePlanPath(null);
            Assert.False(service.GetActivePlanBinding().isBound);
        }

        [Fact]
        public async Task ReadPlan_NoBinding_NoPath_ReturnsNoActivePlanSignal()
        {
            var ideServiceMock = CreateMockIdeService(string.Empty);
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var result = await service.InvokeAsync("read_plan", new Dictionary<string, object>());

            Assert.False(result.IsSuccess);
            Assert.Contains("No active plan", result.Output);
        }

        [Fact]
        public async Task ReadPlan_BoundPlan_ReturnsPlanText()
        {
            // Create a real temp plan file.
            var planPath = Path.Combine(Path.GetTempPath(), "plan_" + Guid.NewGuid() + ".md");
            const string content = "# My Plan\nStep 1\nStep 2";
            File.WriteAllText(planPath, content);

            try
            {
                var ideServiceMock = CreateMockIdeService(string.Empty);
                ideServiceMock.Setup(s => s.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(content);
                var configServiceMock = CreateMockConfigService();
                var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

                service.SetActivePlanPath(planPath);

                var result = await service.InvokeAsync("read_plan", new Dictionary<string, object>());

                Assert.True(result.IsSuccess);
                Assert.Contains(content, result.Output);
                Assert.Contains(planPath, result.Output);
            }
            finally
            {
                if (File.Exists(planPath)) File.Delete(planPath);
            }
        }

        [Fact]
        public async Task ReadPlan_ExplicitPath_ResolvesRelativeToGitRoot()
        {
            var gitRoot = Path.Combine(Path.GetTempPath(), "repo_" + Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(gitRoot, "plans"));
            var planRelative = Path.Combine("plans", "plan.md");
            var planPath = Path.Combine(gitRoot, planRelative);
            const string content = "plan body";
            File.WriteAllText(planPath, content);

            try
            {
                var ideServiceMock = CreateMockIdeService(gitRoot);
                ideServiceMock.Setup(s => s.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(content);
                var configServiceMock = CreateMockConfigService();
                var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

                var result = await service.InvokeAsync(
                    "read_plan",
                    new Dictionary<string, object> { { "path", planRelative } });

                Assert.True(result.IsSuccess);
                Assert.Contains(content, result.Output);
            }
            finally
            {
                if (Directory.Exists(gitRoot)) Directory.Delete(gitRoot, true);
            }
        }

        [Fact]
        public async Task UpdatePlan_ReplacesAllMatchesAndReportsCount()
        {
            var planPath = Path.Combine(Path.GetTempPath(), "plan_" + Guid.NewGuid() + ".md");
            const string before = "⏳ Step 1\nWork\n⏳ Step 2";
            const string after = "✅ Step 1\nWork\n✅ Step 2";
            File.WriteAllText(planPath, before);

            try
            {
                var ideServiceMock = CreateMockIdeService(string.Empty);
                ideServiceMock.Setup(s => s.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(before);
                ideServiceMock.Setup(s => s.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Callback<string, string>((p, c) => File.WriteAllText(p, c));
                var configServiceMock = CreateMockConfigService();
                var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);
                service.SetActivePlanPath(planPath);

                var result = await service.InvokeAsync(
                    "update_plan",
                    new Dictionary<string, object> { { "find", "⏳" }, { "replace", "✅" } });

                Assert.True(result.IsSuccess);
                Assert.Contains("2 occurrence(s)", result.Output);
                Assert.Equal("2", result.Metadata!["matches"]);
                Assert.Equal(after, File.ReadAllText(planPath));
            }
            finally
            {
                if (File.Exists(planPath)) File.Delete(planPath);
            }
        }

        [Fact]
        public async Task UpdatePlan_NoMatch_ReportsZeroAndLeavesUnchanged()
        {
            var planPath = Path.Combine(Path.GetTempPath(), "plan_" + Guid.NewGuid() + ".md");
            const string before = "# Plan\nStep 1";
            File.WriteAllText(planPath, before);

            try
            {
                var ideServiceMock = CreateMockIdeService(string.Empty);
                ideServiceMock.Setup(s => s.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(before);
                ideServiceMock.Setup(s => s.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Callback<string, string>((p, c) => File.WriteAllText(p, c));
                var configServiceMock = CreateMockConfigService();
                var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);
                service.SetActivePlanPath(planPath);

                var result = await service.InvokeAsync(
                    "update_plan",
                    new Dictionary<string, object> { { "find", "NOPE" }, { "replace", "✅" } });

                Assert.True(result.IsSuccess);
                Assert.Contains("No matches", result.Output);
                Assert.Equal("0", result.Metadata!["matches"]);
                Assert.Equal(before, File.ReadAllText(planPath));
            }
            finally
            {
                if (File.Exists(planPath)) File.Delete(planPath);
            }
        }

        [Fact]
        public async Task UpdatePlan_EmptyFind_ReturnsError()
        {
            var ideServiceMock = CreateMockIdeService(string.Empty);
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var result = await service.InvokeAsync(
                "update_plan",
                new Dictionary<string, object> { { "find", "" }, { "replace", "✅" } });

            Assert.False(result.IsSuccess);
            Assert.Contains("find cannot be null or empty", result.Output);
        }

        [Fact]
        public void PlanToolsSetting_DefaultEnabled()
        {
            var defaults = UserSettings.GetDefaults();
            Assert.True((bool)defaults[UserSettings.Tool_PlanToolsEnabled]);
        }

        [Fact]
        public void GetAvailableTools_ReadAndUpdatePlan_AvailableInAgentMode()
        {
            var ideServiceMock = CreateMockIdeService(string.Empty);
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var agentNames = service.GetAvailableTools(ChatMode.Agent).Select(t => t.Name).ToList();
            Assert.Contains("read_plan", agentNames);
            Assert.Contains("update_plan", agentNames);
        }

        [Fact]
        public void GetAvailableTools_ReadAndUpdatePlan_NotAvailableInPlanMode()
        {
            var ideServiceMock = CreateMockIdeService(string.Empty);
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var planNames = service.GetAvailableTools(ChatMode.Plan).Select(t => t.Name).ToList();
            Assert.DoesNotContain("read_plan", planNames);
            Assert.DoesNotContain("update_plan", planNames);
        }
    }
}
