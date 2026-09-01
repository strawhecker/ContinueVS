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
    /// Manages durable token limit settings stored in ~/.continue/vsx-settings.json.
    /// 
    /// Allows users to configure:
    /// - maxContextTokens: Total available context tokens (default: 131072 = 2^17)
    /// - reserveTokensForResponse: Tokens to reserve for model output (default: 8192 = 2^13)
    /// - charsPerToken: Estimated character-to-token ratio (default: 4)
    /// 
    /// Settings are persisted to JSON and read on initialization.
    /// Thread-safe for concurrent read/write operations.
    /// </summary>
    internal static class TokenLimitSettings
    {
        private static readonly object s_settingsLock = new object();
        private static TokenLimitConfig? s_cachedSettings;
        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Represents the token limit settings structure stored in vsx-settings.json
        /// </summary>
        internal sealed class TokenLimitConfig
        {
            /// <summary>
            /// Total context window size in tokens (default: 131072 = 2^17)
            /// </summary>
            [JsonPropertyName("maxContextTokens")]
            public int MaxContextTokens { get; set; } = 131072;

            /// <summary>
            /// Tokens to reserve for model response (default: 8192 = 2^13)
            /// </summary>
            [JsonPropertyName("reserveTokensForResponse")]
            public int ReserveTokensForResponse { get; set; } = 8192;

            /// <summary>
            /// Estimated characters per token for token counting (default: 4)
            /// </summary>
            [JsonPropertyName("charsPerToken")]
            public int CharsPerToken { get; set; } = 4;

            /// <summary>
            /// Optional user-friendly description of settings
            /// </summary>
            [JsonPropertyName("description")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Description { get; set; }
        }

        /// <summary>
        /// Gets the directory path for Continue settings (~/.continue/)
        /// </summary>
        private static string GetSettingsDirectory()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".continue");
        }

        /// <summary>
        /// Gets the full path to vsx-settings.json
        /// </summary>
        private static string GetSettingsPath()
        {
            return Path.Combine(GetSettingsDirectory(), "vsx-settings.json");
        }

        /// <summary>
        /// Reads token limit settings from ~/.continue/vsx-settings.json.
        /// Returns defaults if file doesn't exist or is invalid.
        /// </summary>
        public static async Task<TokenLimitConfig> ReadSettingsAsync(CancellationToken cancellationToken = default)
        {
            lock (s_settingsLock)
            {
                if (s_cachedSettings != null)
                {
                    return s_cachedSettings;
                }
            }

            string settingsPath = GetSettingsPath();

            // If file doesn't exist, return defaults and cache
            if (!File.Exists(settingsPath))
            {
                _ = LoggerService.Current.WriteDebugAsync($"[b24-TOKEN-SETTINGS] Settings file not found at {settingsPath}, using defaults");
                var defaults = new TokenLimitConfig();
                lock (s_settingsLock)
                {
                    s_cachedSettings = defaults;
                }
                return defaults;
            }

            try
            {
                string jsonContent;
                using (var reader = new StreamReader(settingsPath))
                {
                    jsonContent = await reader.ReadToEndAsync();
                }

                var settings = JsonSerializer.Deserialize<TokenLimitConfig>(jsonContent, s_jsonOptions);
                if (settings == null)
                {
                    settings = new TokenLimitConfig();
                }

                _ = LoggerService.Current.WriteDebugAsync($"[b24-TOKEN-SETTINGS] Loaded settings: maxContext={settings.MaxContextTokens}, reserve={settings.ReserveTokensForResponse}, charsPerToken={settings.CharsPerToken}");

                lock (s_settingsLock)
                {
                    s_cachedSettings = settings;
                }

                return settings;
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[b24-TOKEN-SETTINGS] Error reading settings from {settingsPath}: {ex.Message}. Using defaults.", ex);
                var defaults = new TokenLimitConfig();
                lock (s_settingsLock)
                {
                    s_cachedSettings = defaults;
                }
                return defaults;
            }
        }

        /// <summary>
        /// Writes token limit settings to ~/.continue/vsx-settings.json.
        /// Creates the .continue directory if it doesn't exist.
        /// Note: This method performs synchronous I/O operations within a lock.
        /// </summary>
        public static Task WriteSettingsAsync(TokenLimitConfig settings, CancellationToken cancellationToken = default)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            string settingsDir = GetSettingsDirectory();
            string settingsPath = GetSettingsPath();

            try
            {
                // Ensure directory exists
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                    _ = LoggerService.Current.WriteDebugAsync($"[b24-TOKEN-SETTINGS] Created settings directory: {settingsDir}");
                }

                // Validate settings
                if (settings.MaxContextTokens <= 0)
                {
                    throw new ArgumentException("MaxContextTokens must be greater than 0", nameof(settings));
                }

                if (settings.ReserveTokensForResponse < 0)
                {
                    throw new ArgumentException("ReserveTokensForResponse cannot be negative", nameof(settings));
                }

                if (settings.CharsPerToken <= 0)
                {
                    throw new ArgumentException("CharsPerToken must be greater than 0", nameof(settings));
                }

                lock (s_settingsLock)
                {
                    // Write to file using async-compatible wrapper
                    string jsonContent = JsonSerializer.Serialize(settings, s_jsonOptions);
                    File.WriteAllText(settingsPath, jsonContent);

                    _ = LoggerService.Current.WriteDebugAsync($"[b24-TOKEN-SETTINGS] Saved settings to {settingsPath}");

                    // Update cache
                    s_cachedSettings = settings;
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[b24-TOKEN-SETTINGS] Error writing settings to {settingsPath}: {ex.Message}", ex);
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// Clears the cached settings, forcing a re-read from disk on next access.
        /// Useful for testing or manual refresh.
        /// </summary>
        public static void ClearCache()
        {
            lock (s_settingsLock)
            {
                s_cachedSettings = null;
            }
        }

        /// <summary>
        /// Gets the maximum usable context tokens (MaxContextTokens - ReserveTokensForResponse)
        /// </summary>
        public static int GetUsableContextTokens(TokenLimitConfig settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            int usable = settings.MaxContextTokens - settings.ReserveTokensForResponse;
            return Math.Max(0, usable);
        }
    }
}
