using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace VSIXProject1.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for MaxToolCallsPerSession setting in SettingsViewModel.
    /// Tests cover loading default value, saving custom value, and range validation.
    /// </summary>
    public class MaxToolCallsSettingTests
    {
        /// <summary>
        /// Test: Load default MaxToolCallsPerSession value (100) from empty config.
        /// Arrange: Mock IConfigService with empty CustomSettings dictionary
        /// Act: Create SettingsViewModel and call LoadSettings()
        /// Assert: MaxToolCallsPerSession == 100
        /// </summary>
        [Fact]
        public void LoadDefaultValue_ReturnsDefault100WhenSettingNotInConfig()
        {
            // Arrange
            var mockConfigService = new Mock<IConfigService>();
            var emptySettings = new Dictionary<string, object>();
            var mockConfig = new ContinueConfig { CustomSettings = emptySettings };
            mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(mockConfig);

            // Act
            var viewModel = new SettingsViewModel(mockConfigService.Object);
            viewModel.LoadSettings();

            // Assert
            Assert.Equal(100, viewModel.MaxToolCallsPerSession);
        }

        /// <summary>
        /// Test: Save custom MaxToolCallsPerSession value (50) to config.
        /// Arrange: Create SettingsViewModel, set MaxToolCallsPerSession = 50
        /// Act: Call SaveSettingsAsync()
        /// Assert: CustomSettings[Agent_MaxToolCallsPerSession] == 50
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.TaskAsync SaveCustomValue_PersistsToConfig()
        {
            // Arrange
            var mockConfigService = new Mock<IConfigService>();
            var customSettings = new Dictionary<string, object>();
            var mockConfig = new ContinueConfig { CustomSettings = customSettings };
            mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(mockConfig);
            mockConfigService.Setup(cs => cs.SaveConfigAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);

            var viewModel = new SettingsViewModel(mockConfigService.Object);
            viewModel.LoadSettings();

            // Act
            viewModel.MaxToolCallsPerSession = 50;
            await viewModel.SaveSettingsAsync();

            // Assert
            Assert.True(customSettings.ContainsKey(UserSettings.Agent_MaxToolCallsPerSession));
            Assert.Equal(50, customSettings[UserSettings.Agent_MaxToolCallsPerSession]);
        }

        /// <summary>
        /// Test: Validate range coercion for out-of-range values.
        /// Arrange: Create SettingsViewModel
        /// Act: Set MaxToolCallsPerSession to values below 1 and above 1000
        /// Assert: Values are coerced to valid range [1, 1000]
        /// </summary>
        [Theory]
        [InlineData(-5, 1)]
        [InlineData(0, 1)]
        [InlineData(2000, 1000)]
        [InlineData(1500, 1000)]
        [InlineData(500, 500)]
        [InlineData(1, 1)]
        [InlineData(1000, 1000)]
        public void ValidateRange_CoercesOutOfRangeValues(int inputValue, int expectedValue)
        {
            // Arrange
            var mockConfigService = new Mock<IConfigService>();
            var mockConfig = new ContinueConfig { CustomSettings = new Dictionary<string, object>() };
            mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(mockConfig);

            var viewModel = new SettingsViewModel(mockConfigService.Object);

            // Act
            viewModel.MaxToolCallsPerSession = inputValue;

            // Assert
            Assert.Equal(expectedValue, viewModel.MaxToolCallsPerSession);
        }
    }
}
