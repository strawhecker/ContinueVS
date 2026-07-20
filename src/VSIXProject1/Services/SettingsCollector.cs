using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Collects and manages settings from Continue configuration files (Step 95).
    ///
    /// Provides thread-safe read access to settings stored in ~/.continue/config.json.
    /// Supports caching with TTL to reduce file I/O overhead.
    /// Masks sensitive fields (API keys) in logs and diagnostic output.
    /// </summary>
    internal static class SettingsCollector
    {
        /// <summary>Settings cache entry with TTL.</summary>
        private sealed class CacheEntry
        {
            public Dictionary<string, object>? Settings { get; set; }
            public DateTime ExpiresAt { get; set; }

            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }

        private static readonly object s_cacheLock = new object();
        private static CacheEntry? s_cachedSettings;
        private static readonly TimeSpan s_cacheTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Reads settings from ~/.continue/config.json with caching.
        ///
        /// Returns cached settings if cache is valid and not expired.
        /// Falls back to direct file read if cache is expired or unavailable.
        /// Returns empty dictionary if file doesn't exist (graceful degradation).
        /// </summary>
        /// <returns>Dictionary of settings (model, provider, temperature, etc.) or empty dict if not found.</returns>
        /// <exception cref="SettingsCollectorException">Thrown if JSON is invalid.</exception>
        public static async Task<Dictionary<string, object>> ReadSettingsAsync()
        {
            lock (s_cacheLock)
            {
                if (s_cachedSettings != null && !s_cachedSettings.IsExpired)
                {
                    return new Dictionary<string, object>(s_cachedSettings.Settings);
                }
            }

            // Cache miss or expired; read from file
            var settings = await ReadSettingsFromFileAsync();

            // Update cache
            lock (s_cacheLock)
            {
                s_cachedSettings = new CacheEntry
                {
                    Settings = settings,
                    ExpiresAt = DateTime.UtcNow.Add(s_cacheTtl),
                };
            }

            return new Dictionary<string, object>(settings ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// Clears the settings cache (e.g., after configuration changes).
        /// </summary>
        public static void ClearCache()
        {
            lock (s_cacheLock)
            {
                s_cachedSettings = null;
            }
        }

        /// <summary>
        /// Reads settings directly from ~/.continue/config.json.
        /// </summary>
        /// <returns>Dictionary of settings or empty dict if file not found.</returns>
        /// <exception cref="SettingsCollectorException">Thrown if JSON is invalid.</exception>
        private static async Task<Dictionary<string, object>> ReadSettingsFromFileAsync()
        {
            string configPath = GetContinueConfigPath();
            if (!File.Exists(configPath))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                string jsonContent;
                using (var reader = new StreamReader(configPath))
                {
                    jsonContent = await reader.ReadToEndAsync();
                }

                var jsonDoc = JsonDocument.Parse(jsonContent);

                var settings = ExtractSettings(jsonDoc.RootElement);
                return settings;
            }
            catch (JsonException ex)
            {
                throw new SettingsCollectorException(
                    $"Invalid JSON in Continue config: {ex.Message}",
                    configPath,
                    ex
                );
            }
            catch (IOException ex)
            {
                throw new SettingsCollectorException(
                    $"Error reading Continue config: {ex.Message}",
                    configPath,
                    ex
                );
            }
        }

        /// <summary>
        /// Extracts settings fields from the Continue config JSON root element.
        /// 
        /// Looks for 'settings' or 'config' object containing:
        /// - model (string)
        /// - provider (string)
        /// - temperature (number)
        /// - contextWindow (number)
        /// - maxTokens (number)
        /// - systemPrompt (string)
        /// - endpoint (string)
        /// </summary>
        private static Dictionary<string, object> ExtractSettings(JsonElement root)
        {
            var result = new Dictionary<string, object>();

            // Try to find settings object
            JsonElement settingsElement = default;
            if (root.TryGetProperty("settings", out var settingsProp))
            {
                settingsElement = settingsProp;
            }
            else if (root.TryGetProperty("config", out var configProp))
            {
                settingsElement = configProp;
            }
            else
            {
                // Assume root is the settings object
                settingsElement = root;
            }

            if (settingsElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            // Extract known fields
            ExtractStringField(settingsElement, "model", result);
            ExtractStringField(settingsElement, "provider", result);
            ExtractNumberField(settingsElement, "temperature", result);
            ExtractNumberField(settingsElement, "contextWindow", result);
            ExtractNumberField(settingsElement, "maxTokens", result);
            ExtractStringField(settingsElement, "systemPrompt", result);
            ExtractStringField(settingsElement, "endpoint", result);

            return result;
        }

        /// <summary>
        /// Extracts a string field from JSON element if it exists.
        /// </summary>
        private static void ExtractStringField(JsonElement element, string fieldName, Dictionary<string, object> result)
        {
            if (element.TryGetProperty(fieldName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                result[fieldName] = prop.GetString() ?? string.Empty;
            }
        }

        /// <summary>
        /// Extracts a numeric field from JSON element if it exists.
        /// </summary>
        private static void ExtractNumberField(JsonElement element, string fieldName, Dictionary<string, object> result)
        {
            if (element.TryGetProperty(fieldName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var doubleVal))
                {
                    result[fieldName] = doubleVal;
                }
            }
        }

        /// <summary>
        /// Gets the full path to Continue configuration file (~/.continue/config.json).
        /// </summary>
        private static string GetContinueConfigPath()
        {
            string profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(profilePath, ".continue", "config.json");
        }

        /// <summary>
        /// Masks sensitive fields in settings for logging (e.g., API keys in endpoints).
        /// </summary>
        public static Dictionary<string, object> MaskSensitiveFields(Dictionary<string, object> settings)
        {
            var masked = new Dictionary<string, object>(settings);

            // Mask endpoint URLs containing query parameters (likely API keys)
            if (masked.TryGetValue("endpoint", out var endpoint) && endpoint is string endpointStr)
            {
                if (endpointStr.Contains("?"))
                {
                    masked["endpoint"] = "[MASKED_URL]";
                }
            }

            return masked;
        }
    }

    /// <summary>
    /// Exception thrown by SettingsCollector on read/parse failures.
    /// </summary>
    internal sealed class SettingsCollectorException : Exception
    {
        /// <summary>Path to the Continue config file that failed.</summary>
        public string ConfigPath { get; }

        public SettingsCollectorException(string message, string configPath, Exception? innerException = null)
            : base(message, innerException)
        {
            ConfigPath = configPath;
        }

        public override string ToString()
        {
            return $"{base.ToString()}\nConfig path: {ConfigPath}";
        }
    }
}
