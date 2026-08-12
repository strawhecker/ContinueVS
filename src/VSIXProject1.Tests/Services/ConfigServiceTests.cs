using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using CoreTypes = ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services.Tests
{
    public class ConfigServiceTests : IDisposable
    {
        private readonly string _testConfigPath;

        public ConfigServiceTests()
        {
            _testConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continue", "config.json");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testConfigPath))
                {
                    File.Delete(_testConfigPath);
                }
            }
            catch { }
        }
        [Fact]
        public async Task InitializeAsync_CreatesDefaultConfig_WhenFileDoesNotExist()
        {
            Dispose();
            var service = new ConfigService();

            await service.InitializeAsync();

            var config = service.GetCurrentConfig();
            Assert.NotNull(config);
            Assert.Empty(config.Models);
            Assert.Empty(config.Profiles);
        }

        [Fact]
        public void GetCurrentConfig_ThrowsInvalidOperationException_WhenNotInitialized()
        {
            var service = new ConfigService();

            var ex = Assert.Throws<InvalidOperationException>(() => service.GetCurrentConfig());
            Assert.Contains("InitializeAsync", ex.Message);
        }

        [Fact]
        public async Task AddModelAsync_AddsModel_AndFiresConfigChangedEvent()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var eventRaised = false;
            service.ConfigChanged += (s, e) =>
            {
                eventRaised = true;
                Assert.Equal("models", e.ConfigKey);
            };

            var model = new CoreTypes.ModelInfo { Id = "test-1", Name = "Test Model", Provider = "OpenAI" };

            await service.AddModelAsync(model);

            var config = service.GetCurrentConfig();
            Assert.Single(config.Models);
            Assert.True(eventRaised);
        }

        [Fact]
        public async Task AddModelAsync_ThrowsArgumentNullException_WhenModelIsNull()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.AddModelAsync(null!));
        }

        [Fact]
        public async Task RemoveModelAsync_RemovesModel_AndFiresEvent()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var model = new CoreTypes.ModelInfo { Id = "test-1", Name = "Test Model", Provider = "OpenAI" };
            await service.AddModelAsync(model);

            var eventRaised = false;
            service.ConfigChanged += (s, e) => eventRaised = true;

            await service.RemoveModelAsync("test-1");

            var config = service.GetCurrentConfig();
            Assert.Empty(config.Models);
            Assert.True(eventRaised);
        }

        [Fact]
        public async Task SelectModelAsync_SetsSelectedModel()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var model = new CoreTypes.ModelInfo { Id = "test-1", Name = "Test Model", Provider = "OpenAI" };
            await service.AddModelAsync(model);

            await service.SelectModelAsync("test-1");

            var config = service.GetCurrentConfig();
            Assert.Equal("test-1", config.SelectedModelId);
        }

        [Fact]
        public async Task GetSelectedModel_ReturnsSelectedModel()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var model = new CoreTypes.ModelInfo { Id = "test-1", Name = "Test Model", Provider = "OpenAI" };
            await service.AddModelAsync(model);
            await service.SelectModelAsync("test-1");

            var selected = service.GetSelectedModel();
            Assert.NotNull(selected);
            Assert.Equal("test-1", selected.Id);
        }

        [Fact]
        public async Task GetSelectedModel_ReturnsNull_WhenNoModelSelected()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var selected = service.GetSelectedModel();
            Assert.Null(selected);
        }

        [Fact]
        public async Task GetEnabledTools_ReturnsOnlyEnabledTools()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var config = service.GetCurrentConfig();
            config.Tools.Clear();
            config.Tools.Add(new CoreTypes.ToolDefinition { Name = "tool1", IsEnabled = true });
            config.Tools.Add(new CoreTypes.ToolDefinition { Name = "tool2", IsEnabled = false });
            await service.SaveConfigAsync();

            var enabled = service.GetEnabledTools().ToList();
            Assert.Single(enabled);
            Assert.Equal("tool1", enabled[0].Name);
        }

        [Fact]
        public async Task SetToolEnabledAsync_TogglesToolEnabled()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var config = service.GetCurrentConfig();
            config.Tools.Clear();
            config.Tools.Add(new CoreTypes.ToolDefinition { Name = "tool1", IsEnabled = true });
            await service.SaveConfigAsync();

            await service.SetToolEnabledAsync("tool1", false);

            config = service.GetCurrentConfig();
            var tool = config.Tools.FirstOrDefault(t => t.Name == "tool1");
            Assert.NotNull(tool);
            Assert.False(tool.IsEnabled);
        }

        [Fact]
        public async Task GetProfiles_ReturnsProfiles()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var config = service.GetCurrentConfig();
            var profile = new CoreTypes.ProfileInfo { Id = "prof1", Name = "Default Profile" };
            config.Profiles.Add(profile);
            await service.SaveConfigAsync();

            var profiles = service.GetProfiles().ToList();
            Assert.Single(profiles);
            Assert.Equal("prof1", profiles[0].Id);
        }

        [Fact]
        public async Task SelectProfileAsync_SetsSelectedProfile()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var config = service.GetCurrentConfig();
            var profile = new CoreTypes.ProfileInfo { Id = "prof1", Name = "Default Profile" };
            config.Profiles.Add(profile);
            await service.SaveConfigAsync();

            await service.SelectProfileAsync("prof1");

            config = service.GetCurrentConfig();
            Assert.Contains("selectedProfileId", config.CustomSettings);
            Assert.Equal("prof1", config.CustomSettings["selectedProfileId"]);
        }

        [Fact]
        public async Task SaveConfigAsync_PersistsConfiguration()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var model = new CoreTypes.ModelInfo { Id = "test-1", Name = "Test Model", Provider = "OpenAI" };
            await service.AddModelAsync(model);
            await service.SaveConfigAsync();

            var service2 = new ConfigService();
            await service2.InitializeAsync();

            var config = service2.GetCurrentConfig();
            Assert.Single(config.Models);
            Assert.Equal("test-1", config.Models[0].Id);
        }

        [Fact]
        public async Task ReloadConfigAsync_DiscardsUnsavedChanges()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            var model = new CoreTypes.ModelInfo { Id = "test-1", Name = "Test Model", Provider = "OpenAI" };
            await service.AddModelAsync(model);

            var config = service.GetCurrentConfig();
            config.Models.Add(new CoreTypes.ModelInfo { Id = "test-2", Name = "Unsaved Model", Provider = "Anthropic" });

            await service.ReloadConfigAsync();

            config = service.GetCurrentConfig();
            Assert.Single(config.Models);
            Assert.Equal("test-1", config.Models[0].Id);
        }

        [Fact]
        public async Task RemoveModelAsync_ThrowsArgumentException_WhenModelIdIsNull()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            await Assert.ThrowsAsync<ArgumentException>(() => service.RemoveModelAsync(null!));
        }

        [Fact]
        public async Task SelectModelAsync_ThrowsArgumentException_WhenModelIdIsNull()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            await Assert.ThrowsAsync<ArgumentException>(() => service.SelectModelAsync(null!));
        }

        [Fact]
        public async Task SetToolEnabledAsync_ThrowsArgumentException_WhenToolNameIsNull()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            await Assert.ThrowsAsync<ArgumentException>(() => service.SetToolEnabledAsync(null!, true));
        }

        [Fact]
        public async Task SelectProfileAsync_ThrowsArgumentException_WhenProfileIdIsNull()
        {
            var service = new ConfigService();
            await service.InitializeAsync();

            await Assert.ThrowsAsync<ArgumentException>(() => service.SelectProfileAsync(null!));
        }
    }
}
