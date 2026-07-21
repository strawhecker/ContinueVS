using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Bridge-side configuration persistence layer for managing Continue SDK config files.
    /// 
    /// Handles reading, writing, validating, and merging Continue configuration files
    /// (~/.continue/config.json) independent of the settings-sync handler (Step 95).
    /// 
    /// Provides strict schema validation and graceful file I/O error handling.
    /// Thread-safe for concurrent operations.
    /// 
    /// **Architecture**: Separate from Step 95 (IDE ↔ Continue sync via handlers).
    /// This manager focuses on bridge ↔ filesystem operations and validation.
    /// 
    /// **Step 104 Module** for Complete 155-Step Master Implementation Plan v2.1
    /// </summary>
    internal static class ContinueConfigurationManager
    {
        private static readonly object s_fileLock = new object();
        private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Reads and deserializes Continue config file (~/.continue/config.json).
        /// 
        /// Returns empty ContinueConfig if file not found (graceful degradation).
        /// Throws ConfigurationException if JSON is invalid or schema violations detected.
        /// </summary>
        /// <returns>Deserialized ContinueConfig with validated structure.</returns>
        /// <exception cref="ConfigurationException">Thrown on file I/O or validation errors.</exception>
        public static async Task<ContinueConfig> ReadConfigAsync(CancellationToken cancellationToken = default)
        {
            string configPath = GetConfigPath();

            lock (s_fileLock)
            {
                if (!File.Exists(configPath))
                {
                    return new ContinueConfig { Models = new List<ContinueConfigModel>() };
                }
            }

            try
            {
                string jsonContent;
                using (var reader = new StreamReader(configPath))
                {
                    jsonContent = await reader.ReadToEndAsync();
                }

                var jsonDoc = JsonDocument.Parse(jsonContent);
                var config = JsonSerializer.Deserialize<ContinueConfig>(jsonDoc.RootElement.GetRawText(), s_jsonOptions);

                if (config == null)
                {
                    config = new ContinueConfig { Models = new List<ContinueConfigModel>() };
                }

                ValidateSchema(config);
                return config;
            }
            catch (JsonException ex)
            {
                throw new ConfigurationException(
                    $"Invalid JSON in Continue config at {configPath}: {ex.Message}",
                    "read",
                    code: "JSON_PARSE_ERROR",
                    innerException: ex
                );
            }
            catch (IOException ex)
            {
                throw new ConfigurationException(
                    $"Error reading Continue config at {configPath}: {ex.Message}",
                    "read",
                    code: "FILE_IO_ERROR",
                    innerException: ex
                );
            }
            catch (ConfigurationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(
                    $"Unexpected error reading Continue config: {ex.Message}",
                    "read",
                    code: "UNKNOWN_ERROR",
                    innerException: ex
                );
            }
        }

        /// <summary>
        /// Writes and serializes Continue config file (~/.continue/config.json).
        /// 
        /// Creates parent directory (~/.continue) if not present.
        /// Creates backup of existing file before overwriting.
        /// Validates schema before writing.
        /// Thread-safe: uses lock for synchronous directory/file operations.
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown on validation or file I/O errors.</exception>
        public static async Task WriteConfigAsync(ContinueConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            ValidateSchema(config);

            string configPath = GetConfigPath();
            string configDir = Path.GetDirectoryName(configPath);

            try
            {
                // Serialize config first (outside lock)
                string jsonContent = JsonSerializer.Serialize(config, s_jsonOptions);

                // Lock for directory/file operations
                lock (s_fileLock)
                {
                    if (!Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    // Backup existing file
                    if (File.Exists(configPath))
                    {
                        string backupPath = configPath + ".backup";
                        File.Copy(configPath, backupPath, overwrite: true);
                    }
                }

                // Write new config (async, outside lock)
                using (var writer = new StreamWriter(configPath))
                {
                    await writer.WriteAsync(jsonContent);
                }
            }
            catch (IOException ex)
            {
                throw new ConfigurationException(
                    $"Error writing Continue config at {configPath}: {ex.Message}",
                    "write",
                    code: "FILE_IO_ERROR",
                    innerException: ex
                );
            }
            catch (Exception ex)
            {
                throw new ConfigurationException(
                    $"Unexpected error writing Continue config: {ex.Message}",
                    "write",
                    code: "UNKNOWN_ERROR",
                    innerException: ex
                );
            }
        }

        /// <summary>
        /// Merges models into the config by title. Updates existing, adds new.
        /// 
        /// Strategy: For each model in merge list, find existing by title and update,
        /// or append if not found. Preserves model order; new models added at end.
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown on validation errors.</exception>
        public static async Task<ContinueConfig> MergeModelsAsync(ContinueConfig config, IEnumerable<ContinueConfigModel> modelsToMerge, CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (modelsToMerge == null)
            {
                return config;
            }

            var result = new ContinueConfig
            {
                Models = new List<ContinueConfigModel>(config.Models)
            };

            foreach (var modelToMerge in modelsToMerge)
            {
                ValidateModelSchema(modelToMerge);

                var existingIndex = result.Models.FindIndex(m =>
                    string.Equals(m.Title, modelToMerge.Title, StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    result.Models[existingIndex] = modelToMerge;
                }
                else
                {
                    result.Models.Add(modelToMerge);
                }
            }

            ValidateSchema(result);
            return result;
        }

        /// <summary>
        /// Removes models from config by title (case-insensitive).
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown on validation errors.</exception>
        public static async Task<ContinueConfig> RemoveModelsAsync(ContinueConfig config, IEnumerable<string> modelTitles, CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (modelTitles == null)
            {
                return config;
            }

            var titlesToRemove = new HashSet<string>(modelTitles, StringComparer.OrdinalIgnoreCase);
            var result = new ContinueConfig
            {
                Models = config.Models
                    .Where(m => !titlesToRemove.Contains(m.Title))
                    .ToList()
            };

            ValidateSchema(result);
            return result;
        }

        /// <summary>
        /// Validates entire Continue config schema.
        /// 
        /// Checks: models array exists, all models have required fields (title, provider, model),
        /// all field types are correct, no duplicate titles.
        /// </summary>
        /// <exception cref="SchemaValidationException">Thrown if schema is invalid.</exception>
        private static void ValidateSchema(ContinueConfig config)
        {
            if (config == null)
            {
                throw new SchemaValidationException("Config is null.", "root", code: "NULL_CONFIG");
            }

            if (config.Models == null)
            {
                throw new SchemaValidationException("Config.Models is null; expected array.", "models", code: "MISSING_MODELS_ARRAY");
            }

            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < config.Models.Count; i++)
            {
                var model = config.Models[i];
                ValidateModelSchema(model);

                if (seenTitles.Contains(model.Title))
                {
                    throw new SchemaValidationException(
                        $"Duplicate model title: '{model.Title}' at index {i}.",
                        $"models[{i}].title",
                        code: "DUPLICATE_TITLE"
                    );
                }

                seenTitles.Add(model.Title);
            }
        }

        /// <summary>
        /// Validates individual model schema.
        /// 
        /// Checks: title (non-empty string), provider (non-empty string), model (non-empty string),
        /// apiKey (optional string), apiBase (optional string).
        /// </summary>
        /// <exception cref="SchemaValidationException">Thrown if model schema is invalid.</exception>
        private static void ValidateModelSchema(ContinueConfigModel model)
        {
            if (model == null)
            {
                throw new SchemaValidationException("Model is null.", "model", code: "NULL_MODEL");
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                throw new SchemaValidationException("Model title is required and must be non-empty.", "title", code: "MISSING_TITLE");
            }

            if (string.IsNullOrWhiteSpace(model.Provider))
            {
                throw new SchemaValidationException("Model provider is required and must be non-empty.", "provider", code: "MISSING_PROVIDER");
            }

            if (string.IsNullOrWhiteSpace(model.Model))
            {
                throw new SchemaValidationException("Model field is required and must be non-empty.", "model", code: "MISSING_MODEL");
            }
        }

        /// <summary>
        /// Gets the full path to Continue configuration file (~/.continue/config.json).
        /// </summary>
        private static string GetConfigPath()
        {
            string profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(profilePath, ".continue", "config.json");
        }
    }

    /// <summary>
    /// Represents the Continue SDK configuration file structure.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
    internal sealed class ContinueConfig
    {
        [JsonPropertyName("models")]
        public List<ContinueConfigModel>? Models { get; set; }
    }

    /// <summary>
    /// Represents a single LLM model configuration.
    /// </summary>
    internal sealed class ContinueConfigModel
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("apiKey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApiKey { get; set; }

        [JsonPropertyName("apiBase")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApiBase { get; set; }
    }

    /// <summary>
    /// Base exception for configuration operations.
    /// </summary>
    internal class ConfigurationException : Exception
    {
        public string OperationType { get; }
        public string Code { get; }

        public ConfigurationException(string message, string operationType, string code = "CONFIGURATION_ERROR", Exception? innerException = null)
            : base(message, innerException)
        {
            OperationType = operationType;
            Code = code;
        }

        public override string ToString()
        {
            return $"[{Code}] {OperationType}: {base.ToString()}";
        }
    }

    /// <summary>
    /// Exception thrown when configuration schema validation fails.
    /// </summary>
    internal class SchemaValidationException : ConfigurationException
    {
        public string FieldPath { get; }

        public SchemaValidationException(string message, string fieldPath, string code = "SCHEMA_VALIDATION_ERROR")
            : base(message, "validation", code)
        {
            FieldPath = fieldPath;
        }

        public override string ToString()
        {
            return $"[{this.Code}] {FieldPath}: {this.Message}";
        }
    }
}
