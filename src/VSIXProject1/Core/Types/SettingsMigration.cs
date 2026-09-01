using System;
using System.Collections.Generic;
using System.Diagnostics;
using CoreTypes = ContinueVS.Core.Types;
using ContinueVS.Services;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Handles schema versioning and migration of ContinueConfig.CustomSettings.
    /// Supports upgrade paths: v0 → v1 → v2, etc.
    /// </summary>
    public static class SettingsMigration
    {
        /// <summary>
        /// Current schema version. Increment when CustomSettings structure changes.
        /// </summary>
        private const int CurrentVersion = 1;

        /// <summary>
        /// Schema version key in CustomSettings dictionary.
        /// </summary>
        private const string SchemaVersionKey = "_schemaVersion";

        /// <summary>
        /// Migrates CustomSettings to the current schema version.
        /// Applies all accumulated migrations (v0→v1, v1→v2, etc.) based on file version.
        /// </summary>
        /// <param name="config">The ContinueConfig to migrate (mutated in-place).</param>
        public static void MigrateCustomSettings(CoreTypes.ContinueConfig config)
        {
            if (config == null)
            {
                _ = LoggerService.Current.WriteDebugAsync("[SettingsMigration] Config is null, skipping migration.");
                return;
            }

            if (config.CustomSettings == null)
            {
                config.CustomSettings = new Dictionary<string, object>();
            }

            // Determine file version (default to 0 if not present)
            int fileVersion = 0;
            if (config.CustomSettings.TryGetValue(SchemaVersionKey, out var versionObj))
            {
                if (versionObj is int intVersion)
                {
                    fileVersion = intVersion;
                }
                else if (versionObj is long longVersion)
                {
                    fileVersion = (int)longVersion;
                }
                else if (int.TryParse(versionObj.ToString(), out var parsedVersion))
                {
                    fileVersion = parsedVersion;
                }
            }

            _ = LoggerService.Current.WriteDebugAsync($"[SettingsMigration] File version: {fileVersion}, current version: {CurrentVersion}");

            // Apply migrations in order
            if (fileVersion < 1)
            {
                MigrateV0ToV1(config);
            }

            // Update schema version to current
            config.CustomSettings[SchemaVersionKey] = CurrentVersion;
            _ = LoggerService.Current.WriteDebugAsync($"[SettingsMigration] Migration complete. Schema version now: {CurrentVersion}");
        }

        /// <summary>
        /// Migration v0 → v1: Rename obsolete setting keys and add defaults for new keys.
        /// </summary>
        private static void MigrateV0ToV1(CoreTypes.ContinueConfig config)
        {
            _ = LoggerService.Current.WriteDebugAsync("[SettingsMigration] Applying v0→v1 migration...");

            // Example v0→v1 migrations (based on Redux-persist pattern):
            // Rename old keys to new location structure

            // Old key format: "ui.someKey" or "sessionId"
            // New key format: "ui.someKey" or "session.id"

            var keysToRename = new Dictionary<string, string>
            {
                // { "oldSessionId", "session.id" },        // Example placeholder
                // { "oldTheme", "ui.theme" },              // Example placeholder
                // { "oldFontSize", "ui.fontSize" },        // Example placeholder
            };

            foreach (var kvp in keysToRename)
            {
                if (config.CustomSettings.TryGetValue(kvp.Key, out var value))
                {
                    config.CustomSettings[kvp.Value] = value;
                    config.CustomSettings.Remove(kvp.Key);
                    _ = LoggerService.Current.WriteDebugAsync($"[SettingsMigration] Renamed '{kvp.Key}' → '{kvp.Value}'");
                }
            }

            // Ensure new keys have sensible defaults if missing
            var newDefaults = new Dictionary<string, object>
            {
                // { "ui.theme", "system" },                 // Example: default to system theme
                // { "ui.fontSize", 14 },                    // Example: default to 14pt
            };

            foreach (var kvp in newDefaults)
            {
                if (!config.CustomSettings.ContainsKey(kvp.Key))
                {
                    config.CustomSettings[kvp.Key] = kvp.Value;
                    _ = LoggerService.Current.WriteDebugAsync($"[SettingsMigration] Added default for '{kvp.Key}' = {kvp.Value}");
                }
            }

            _ = LoggerService.Current.WriteDebugAsync("[SettingsMigration] v0→v1 migration complete.");
        }
    }
}
