using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using Moq;
using Newtonsoft.Json;

namespace ContinueVS.Tests.Services
{
    public class ConfigServiceUIStateTests
    {
        private ConfigService CreateConfigService()
        {
            return new ConfigService(null);
        }

        [Fact]
        public async Task GetUIStateAsync_DeserializesFromCustomSettings()
        {
            var configService = CreateConfigService();

            // Create a mock config with UIState in CustomSettings
            var uiState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "read_file", ToolPolicy.AutoApprove }
                }
            };
            var json = JsonConvert.SerializeObject(uiState);

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { "ui.state", json }
                }
            };

            // Manually set the internal state (since we can't easily mock it)
            var field = typeof(ConfigService).GetField("_currentConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initializedField = typeof(ConfigService).GetField("_initialized", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            field?.SetValue(configService, config);
            initializedField?.SetValue(configService, true);

            var retrievedState = await configService.GetUIStateAsync();

            Assert.NotNull(retrievedState);
            Assert.Single(retrievedState.ToolSettings);
            Assert.Equal(ToolPolicy.AutoApprove, retrievedState.ToolSettings["read_file"]);
        }

        [Fact]
        public async Task SaveUIStateAsync_SerializesToCustomSettings()
        {
            var configService = CreateConfigService();

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>()
            };

            var field = typeof(ConfigService).GetField("_currentConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initializedField = typeof(ConfigService).GetField("_initialized", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            field?.SetValue(configService, config);
            initializedField?.SetValue(configService, true);

            // Mock SaveConfigAsync to prevent actual file I/O
            var saveConfigMethod = typeof(ConfigService).GetMethod("SaveConfigAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var uiState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "edit_file", ToolPolicy.Disabled }
                }
            };

            // We can't easily mock the SaveConfigAsync, so we'll just verify the CustomSettings update
            await configService.SaveUIStateAsync(uiState);

            Assert.True(config.CustomSettings.ContainsKey("ui.state"));
            var savedJson = config.CustomSettings["ui.state"] as string;
            Assert.NotNull(savedJson);

            var deserializedState = JsonConvert.DeserializeObject<UIState>(savedJson!);
            Assert.NotNull(deserializedState);
            Assert.Single(deserializedState.ToolSettings);
            Assert.Equal(ToolPolicy.Disabled, deserializedState.ToolSettings["edit_file"]);
        }

        [Fact]
        public async Task GetUIStateAsync_ReturnsEmptyUIState_WhenKeyMissing()
        {
            var configService = CreateConfigService();

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>()
            };

            var field = typeof(ConfigService).GetField("_currentConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initializedField = typeof(ConfigService).GetField("_initialized", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            field?.SetValue(configService, config);
            initializedField?.SetValue(configService, true);

            var uiState = await configService.GetUIStateAsync();

            Assert.NotNull(uiState);
            Assert.Empty(uiState.ToolSettings);
            Assert.Empty(uiState.ToolGroupSettings);
            Assert.Empty(uiState.RuleSettings);
        }

        [Fact]
        public async Task SaveUIStateAsync_UpdatesLastModified()
        {
            var configService = CreateConfigService();

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>()
            };

            var field = typeof(ConfigService).GetField("_currentConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initializedField = typeof(ConfigService).GetField("_initialized", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            field?.SetValue(configService, config);
            initializedField?.SetValue(configService, true);

            var beforeSave = DateTime.UtcNow.AddSeconds(-1);
            var uiState = new UIState();
            await configService.SaveUIStateAsync(uiState);
            var afterSave = DateTime.UtcNow.AddSeconds(1);

            var savedJson = config.CustomSettings["ui.state"] as string;
            var deserializedState = JsonConvert.DeserializeObject<UIState>(savedJson!);

            Assert.InRange(deserializedState!.LastModified, beforeSave, afterSave);
        }

        [Fact]
        public async Task RoundTrip_UIStatePreserved()
        {
            var configService = CreateConfigService();

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>()
            };

            var field = typeof(ConfigService).GetField("_currentConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initializedField = typeof(ConfigService).GetField("_initialized", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            field?.SetValue(configService, config);
            initializedField?.SetValue(configService, true);

            var originalState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "read_file", ToolPolicy.AutoApprove },
                    { "edit_file", ToolPolicy.AskFirst }
                },
                ToolGroupSettings = new Dictionary<string, bool>
                {
                    { "file_operations", false }
                },
                OnboardingCardVisible = false,
                TTSActive = true
            };

            await configService.SaveUIStateAsync(originalState);
            var retrievedState = await configService.GetUIStateAsync();

            Assert.Equal(originalState.ToolSettings.Count, retrievedState.ToolSettings.Count);
            Assert.Equal(ToolPolicy.AutoApprove, retrievedState.ToolSettings["read_file"]);
            Assert.Equal(ToolPolicy.AskFirst, retrievedState.ToolSettings["edit_file"]);
            Assert.False(retrievedState.ToolGroupSettings["file_operations"]);
            Assert.False(retrievedState.OnboardingCardVisible);
            Assert.True(retrievedState.TTSActive);
        }
    }
}
