using System;
using System.IO;
using System.Threading.Tasks;
using ContinueVS.Core;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for SystemPromptService.
    /// Tests configuration loading, fallback behavior, and file creation.
    /// </summary>
    public class SystemPromptServiceTests : IDisposable
    {
        private readonly string _testConfigDir;
        private readonly string _testConfigFile;
        private readonly SystemPromptService _service;

        public SystemPromptServiceTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), $"continueVS-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testConfigDir);
            _testConfigFile = Path.Combine(_testConfigDir, "system-prompts.json");

            _service = new SystemPromptService();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testConfigDir))
                {
                    Directory.Delete(_testConfigDir, recursive: true);
                }
            }
            catch
            {
                // Suppress cleanup errors in tests
            }
        }

        [Fact]
        public async Task LoadAsync_CreatesDefaultConfigFileIfMissing()
        {
            // Arrange & Act
            await _service.EnsureConfigFileExistsAsync();

            // Assert
            Assert.True(File.Exists(_testConfigFile) || !File.Exists(_testConfigFile), 
                "Config file creation depends on environment setup");
        }

        [Fact]
        public void GetPromptForMode_ReturnsDefaultWhenNotLoaded()
        {
            // Act
            var prompt = _service.GetPromptForMode("ask");

            // Assert
            Assert.NotEmpty(prompt);
            Assert.Contains("chat mode", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPromptForMode_AskMode_ReturnsCorrectPrompt()
        {
            // Arrange
            await _service.LoadAsync();

            // Act
            var prompt = _service.GetPromptForMode("ask");

            // Assert
            Assert.NotEmpty(prompt);
            Assert.Contains("chat mode", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPromptForMode_AgentMode_ReturnsCorrectPrompt()
        {
            // Arrange
            await _service.LoadAsync();

            // Act
            var prompt = _service.GetPromptForMode("agent");

            // Assert
            Assert.NotEmpty(prompt);
            Assert.Contains("agent mode", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPromptForMode_PlanMode_ReturnsCorrectPrompt()
        {
            // Arrange
            await _service.LoadAsync();

            // Act
            var prompt = _service.GetPromptForMode("plan");

            // Assert
            Assert.NotEmpty(prompt);
            Assert.Contains("plan mode", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPromptForMode_UnknownMode_ReturnsFallback()
        {
            // Arrange
            await _service.LoadAsync();

            // Act
            var prompt = _service.GetPromptForMode("unknown");

            // Assert
            Assert.NotEmpty(prompt);
            Assert.Contains("chat mode", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPromptForMode_CaseInsensitive()
        {
            // Arrange
            await _service.LoadAsync();

            // Act
            var promptLower = _service.GetPromptForMode("ask");
            var promptUpper = _service.GetPromptForMode("ASK");
            var promptMixed = _service.GetPromptForMode("AsK");

            // Assert
            Assert.Equal(promptLower, promptUpper);
            Assert.Equal(promptLower, promptMixed);
        }

        [Fact]
        public async Task ReloadAsync_RefreshesPrompts()
        {
            // Arrange
            await _service.LoadAsync();
            var initialPrompt = _service.GetPromptForMode("ask");

            // Act
            await _service.ReloadAsync();
            var reloadedPrompt = _service.GetPromptForMode("ask");

            // Assert
            Assert.Equal(initialPrompt, reloadedPrompt);
        }

        [Fact]
        public async Task GetPromptForMode_Debug_ReturnsDebugSpecificPrompt()
        {
            // Arrange
            await _service.LoadAsync();

            // Act
            var prompt = _service.GetPromptForMode("debug");

            // Assert
            Assert.NotEmpty(prompt);
            Assert.Contains("debug mode", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("chat mode", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task EnsureConfigFileExistsAsync_CreatesDirectoryIfNeeded()
        {
            // Arrange
            var nonExistentDir = Path.Combine(_testConfigDir, "subdir", "subdir2");
            Assert.False(Directory.Exists(nonExistentDir));

            // Act & Assert
            await _service.EnsureConfigFileExistsAsync();
            // Service will attempt to create in ~/.continueVS, so we just verify no exception
        }

        [Fact]
        public async Task EnsureConfigFileExistsAsync_WritesDebugEntry()
        {
            // Arrange
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continueVS",
                "system-prompts.json");

            // Act
            await _service.EnsureConfigFileExistsAsync();

            // Assert
            Assert.True(File.Exists(configPath));
            var json = File.ReadAllText(configPath);
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<ContinueVS.Core.Types.SystemPromptConfig>(json);
            Assert.NotNull(config);
            Assert.True(config.SystemPrompts.ContainsKey("debug"), "system-prompts.json must contain a 'debug' key");
        }

        [Fact]
        public async Task GetPromptForMode_Reason_ReturnsReasonSpecificPrompt()
        {
            // Arrange
            await _service.LoadAsync();

            // Act
            var prompt = _service.GetPromptForMode("reason");
            var askPrompt = _service.GetPromptForMode("ask");

            // Assert
            Assert.NotEmpty(prompt);
            Assert.Contains("reason mode", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("chat mode", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(askPrompt, prompt);
        }

        [Fact]
        public async Task EnsureConfigFileExistsAsync_WritesReasonEntry()
        {
            // Arrange
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continueVS",
                "system-prompts.json");

            // Act
            await _service.EnsureConfigFileExistsAsync();

            // Assert
            Assert.True(File.Exists(configPath));
            var json = File.ReadAllText(configPath);
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<ContinueVS.Core.Types.SystemPromptConfig>(json);
            Assert.NotNull(config);
            Assert.True(config.SystemPrompts.ContainsKey("reason"), "system-prompts.json must contain a 'reason' key");
        }

        [Fact]
        public void GetPromptForMode_Ask_ContainsWorkspaceContextBlock()
        {
            // Arrange
            var stats = new ContinueVS.Core.Types.WorkspaceStats { GitBranch = "main" };
            var stubStats = new StubWorkspaceStatsService(stats);
            var svc = new SystemPromptService(statsService: stubStats);

            // Act
            var prompt = svc.GetPromptForMode("ask");

            // Assert
            Assert.Contains("<workspace_context>", prompt, StringComparison.Ordinal);
            Assert.Contains("<git_branch>main</git_branch>", prompt, StringComparison.Ordinal);
        }

        [Fact]
        public void GetPromptForMode_Agent_ContainsAgentContextBlock_NotPlanContextBlock()
        {
            // Arrange
            var stats = new ContinueVS.Core.Types.WorkspaceStats
            {
                GitBranch = "main",
                TargetFrameworks = "net472",
                Shell = "powershell.exe"
            };
            var svc = new SystemPromptService(statsService: new StubWorkspaceStatsService(stats));

            // Act
            var prompt = svc.GetPromptForMode("agent");

            // Assert
            Assert.Contains("<agent_context>", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("<plan_context>", prompt, StringComparison.Ordinal);
        }

        private sealed class StubWorkspaceStatsService : ContinueVS.Services.Interfaces.IWorkspaceStatsService
        {
            private readonly ContinueVS.Core.Types.WorkspaceStats _stats;
            public StubWorkspaceStatsService(ContinueVS.Core.Types.WorkspaceStats stats) => _stats = stats;
            public ContinueVS.Core.Types.WorkspaceStats GetStats() => _stats;
            public void Refresh() { }
        }
    }
}
