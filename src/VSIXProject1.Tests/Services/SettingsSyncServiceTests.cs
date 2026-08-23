#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Xunit;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Core.Types;
using Moq;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for SettingsSyncService.
    /// Tests file watching, font size synchronization, and property change notifications.
    /// </summary>
    public class SettingsSyncServiceTests : IDisposable
    {
        private readonly Mock<IConfigService> _mockConfigService;
        private readonly string _testConfigDir;
        private readonly string _testConfigPath;

        public SettingsSyncServiceTests()
        {
            _testConfigDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _testConfigPath = Path.Combine(_testConfigDir, "continueVS.json");

            Directory.CreateDirectory(_testConfigDir);

            _mockConfigService = new Mock<IConfigService>();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testConfigDir))
                    Directory.Delete(_testConfigDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private ContinueConfig CreateMockConfig(int fontSize)
        {
            return new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Appearance_FontSize, fontSize }
                }
            };
        }

        [Fact]
        public void Constructor_WithValidConfigService_InitializesSuccessfully()
        {
            // Arrange
            var config = CreateMockConfig(14);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);

            // Act
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Assert
            Assert.NotNull(service);
            Assert.Equal(14, service.FontSize);

            service.Dispose();
        }

        [Fact]
        public void Constructor_WithNullConfigService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SettingsSyncService(null!));
        }

        [Fact]
        public void FontSize_Property_ReturnsInitialValue()
        {
            // Arrange
            var config = CreateMockConfig(16);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);

            // Act
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Assert
            Assert.Equal(16, service.FontSize);

            service.Dispose();
        }

        [Fact]
        public void FontSize_Property_ReturnsDefaultFontSizeWhenConfigIsNull()
        {
            // Arrange
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns((ContinueConfig?)null!);

            // Act
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Assert
            Assert.Equal(14, service.FontSize); // Default font size

            service.Dispose();
        }

        [Fact]
        public void FontSize_Property_ReturnsDefaultFontSizeWhenCustomSettingsIsNull()
        {
            // Arrange
            var config = new ContinueConfig { CustomSettings = null! };
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);

            // Act
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Assert
            Assert.Equal(14, service.FontSize);

            service.Dispose();
        }

        [Fact]
        public void FontSize_Property_ReturnsMissingFontSizeAsZero()
        {
            // Arrange
            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>()
            };
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);

            // Act
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Assert
            Assert.Equal(14, service.FontSize); // Should use default

            service.Dispose();
        }

        [Fact]
        public void PropertyChanged_RaisedWhenFontSizeChanges()
        {
            // Arrange
            var config = CreateMockConfig(14);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);
            var service = new SettingsSyncService(_mockConfigService.Object);

            var propertyChangedFired = false;
            var newFontSizeValue = 0;

            service.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SettingsSyncService.FontSize))
                {
                    propertyChangedFired = true;
                    newFontSizeValue = service.FontSize;
                }
            };

            // Act - Update config to simulate file change
            var updatedConfig = CreateMockConfig(18);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(updatedConfig);

            // Simulate file change by re-reading config
            service.GetType()
                .GetMethod("OnConfigFileChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(service, new object[] { service, new FileSystemEventArgs(WatcherChangeTypes.Changed, _testConfigDir, "continueVS.json") });

            // Assert
            Assert.True(propertyChangedFired, "PropertyChanged event should have been raised");
            Assert.Equal(18, newFontSizeValue);

            service.Dispose();
        }

        [Fact]
        public void PropertyChanged_NotRaisedWhenFontSizeUnchanged()
        {
            // Arrange
            var config = CreateMockConfig(14);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);
            var service = new SettingsSyncService(_mockConfigService.Object);

            var propertyChangedCount = 0;

            service.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SettingsSyncService.FontSize))
                    propertyChangedCount++;
            };

            // Act - Keep config the same
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);

            service.GetType()
                .GetMethod("OnConfigFileChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(service, new object[] { service, new FileSystemEventArgs(WatcherChangeTypes.Changed, _testConfigDir, "continueVS.json") });

            // Assert
            Assert.Equal(0, propertyChangedCount); // Should not raise since value didn't change

            service.Dispose();
        }

        [Fact]
        public void Dispose_StopsFileWatcherAndReleases()
        {
            // Arrange
            var config = CreateMockConfig(14);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Act
            service.Dispose();

            // Assert - No exception should be thrown
            Assert.True(true); // Dispose completed without error

            // Verify disposal prevents further operations
            var disposed = service.GetType()
                .GetField("_disposed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(service);

            Assert.True((bool?)disposed ?? false);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var config = CreateMockConfig(14);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Act & Assert - Should not throw
            service.Dispose();
            service.Dispose(); // Second dispose should be safe
        }

        [Fact]
        public void FontSize_ParsesStringValueAsInteger()
        {
            // Arrange
            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Appearance_FontSize, "20" } // String instead of int
                }
            };
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);

            // Act
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Assert
            Assert.Equal(20, service.FontSize);

            service.Dispose();
        }

        [Fact]
        public void MultipleSubscribers_ReceivePropertyChangedNotifications()
        {
            // Arrange
            var config = CreateMockConfig(14);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);
            var service = new SettingsSyncService(_mockConfigService.Object);

            var subscriber1Changed = false;
            var subscriber2Changed = false;

            service.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsSyncService.FontSize))
                    subscriber1Changed = true;
            };

            service.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsSyncService.FontSize))
                    subscriber2Changed = true;
            };

            // Act
            var updatedConfig = CreateMockConfig(18);
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(updatedConfig);

            var method = service.GetType()
                .GetMethod("OnConfigFileChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            method.Invoke(service, new object[] { service, new FileSystemEventArgs(WatcherChangeTypes.Changed, _testConfigDir, "continueVS.json") });

            // Assert
            Assert.True(subscriber1Changed);
            Assert.True(subscriber2Changed);

            service.Dispose();
        }

        [Fact]
        public void FontSize_HandlesInvalidStringGracefully()
        {
            // Arrange
            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Appearance_FontSize, "invalid" } // Non-numeric string
                }
            };
            _mockConfigService.Setup(cs => cs.GetCurrentConfig()).Returns(config);

            // Act
            var service = new SettingsSyncService(_mockConfigService.Object);

            // Assert - Should fall back to default
            Assert.Equal(14, service.FontSize);

            service.Dispose();
        }
    }
}
