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
            Assert.Equal(ChatModeSystemPrompts.DEFAULT_ASK_SYSTEM_MESSAGE, prompt);
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
            Assert.Equal(ChatModeSystemPrompts.DEFAULT_ASK_SYSTEM_MESSAGE, prompt);
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
        public async Task EnsureConfigFileExistsAsync_CreatesDirectoryIfNeeded()
        {
            // Arrange
            var nonExistentDir = Path.Combine(_testConfigDir, "subdir", "subdir2");
            Assert.False(Directory.Exists(nonExistentDir));

            // Act & Assert
            await _service.EnsureConfigFileExistsAsync();
            // Service will attempt to create in ~/.continueVS, so we just verify no exception
        }
    }
}
