using System;
using System.Collections.Generic;
using Xunit;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Core.Tests
{
    public class SettingsMigrationTests
    {
        [Fact]
        public void MigrateCustomSettings_WithNullConfig_DoesNotThrow()
        {
            // Arrange: null config
            CoreTypes.ContinueConfig nullConfig = null;

            // Act & Assert: should not throw
            CoreTypes.SettingsMigration.MigrateCustomSettings(nullConfig);
        }

        [Fact]
        public void MigrateCustomSettings_WithNullCustomSettings_InitializesAndMigrates()
        {
            // Arrange
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = null
            };

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert: CustomSettings should be initialized and versioned
            Assert.NotNull(config.CustomSettings);
            Assert.True(config.CustomSettings.ContainsKey("_schemaVersion"));
            Assert.Equal(1, config.CustomSettings["_schemaVersion"]);
        }

        [Fact]
        public void MigrateCustomSettings_V0ToV1_IncrementsSchemaVersion()
        {
            // Arrange: config with no schema version (v0)
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { "someOldKey", "oldValue" }
                }
            };

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert: schema version should be set to 1
            Assert.True(config.CustomSettings.TryGetValue("_schemaVersion", out var versionObj));
            Assert.Equal(1, (int)versionObj);
        }

        [Fact]
        public void MigrateCustomSettings_AlreadyV1_DoesNotChange()
        {
            // Arrange: config already at v1
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { "_schemaVersion", 1 },
                    { "ui.fontSize", 14 }
                }
            };
            var originalCount = config.CustomSettings.Count;
            var originalFontSize = config.CustomSettings["ui.fontSize"];

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert: no new keys added, existing values unchanged
            Assert.Equal(originalCount, config.CustomSettings.Count);
            Assert.Equal(originalFontSize, config.CustomSettings["ui.fontSize"]);
            Assert.Equal(1, config.CustomSettings["_schemaVersion"]);
        }

        [Fact]
        public void MigrateCustomSettings_PreservesExistingCustomSettings()
        {
            // Arrange
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { "ui.theme", "dark" },
                    { "user.name", "John Doe" }
                }
            };

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert: existing settings preserved
            Assert.Equal("dark", config.CustomSettings["ui.theme"]);
            Assert.Equal("John Doe", config.CustomSettings["user.name"]);
            Assert.Equal(1, config.CustomSettings["_schemaVersion"]);
        }

        [Fact]
        public void MigrateCustomSettings_SchemaVersionAsLong_ConvertedToInt()
        {
            // Arrange: schema version stored as long (JSON deserialization quirk)
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { "_schemaVersion", 0L }  // long zero
                }
            };

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert: should handle long gracefully and upgrade
            Assert.True(config.CustomSettings.TryGetValue("_schemaVersion", out var versionObj));
            Assert.Equal(1, (int)versionObj);
        }

        [Fact]
        public void MigrateCustomSettings_SchemaVersionAsString_Parsed()
        {
            // Arrange: schema version stored as string (edge case)
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { "_schemaVersion", "0" }  // string zero
                }
            };

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert: should parse string and upgrade
            Assert.True(config.CustomSettings.TryGetValue("_schemaVersion", out var versionObj));
            Assert.Equal(1, (int)versionObj);
        }

        [Fact]
        public void MigrateCustomSettings_FutureVersion_UpdatesToCurrentVersion()
        {
            // Arrange: config has a future version (shouldn't happen, but test graceful handling)
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { "_schemaVersion", 999 }
                }
            };

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert: version should be updated to current version (1)
            // Future versions are not processed, but current version is applied
            Assert.Equal(1, config.CustomSettings["_schemaVersion"]);
        }

        [Fact]
        public void MigrateCustomSettings_EmptyCustomSettings_Versioned()
        {
            // Arrange
            var config = new CoreTypes.ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>()
            };

            // Act
            CoreTypes.SettingsMigration.MigrateCustomSettings(config);

            // Assert
            Assert.True(config.CustomSettings.ContainsKey("_schemaVersion"));
            Assert.Equal(1, config.CustomSettings["_schemaVersion"]);
        }
    }
}
