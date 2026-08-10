using System;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Core.Config
{
    /// <summary>
    /// Thread-safe singleton cache for storing and retrieving configuration state.
    /// Provides atomic snapshots to isolate concurrent reads from updates.
    /// </summary>
    internal sealed class ConfigCache
    {
        /// <summary>
        /// Immutable snapshot of cached configuration state.
        /// </summary>
        internal class ConfigSnapshot
        {
            public object[]? Models { get; set; }
            public object? TabAutocompleteModel { get; set; }
            public object[]? Tools { get; set; }
            public object[]? SlashCommands { get; set; }
            public object[]? ContextProviders { get; set; }
            public object[]? McpServerStatuses { get; set; }
            public object[]? Rules { get; set; }
            public object? ModelsByRole { get; set; }
            public object? SelectedModelByRole { get; set; }
        }

        private readonly object _lock = new object();
        private ConfigSnapshot _currentSnapshot = null!;

        private ConfigCache()
        {
            System.Diagnostics.Debug.WriteLine("[c13-CACHE-INIT-START] ConfigCache singleton initialization starting");

            try
            {
                // Seed cache with mock tools for testing
                var mockTools = CreateMockTools();
                SetConfig(tools: mockTools);
                System.Diagnostics.Debug.WriteLine("[c15-CACHE-SEEDED] Cache initialized with 3 mock tools");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[c13-CACHE-INIT-ERROR] Exception during initialization: {ex.Message}");

                // Ensure _currentSnapshot is set to safe default
                _currentSnapshot = new ConfigSnapshot
                {
                    Models = new object[0],
                    TabAutocompleteModel = null,
                    Tools = new object[0],
                    SlashCommands = new object[0],
                    ContextProviders = new object[0],
                    McpServerStatuses = new object[0],
                    Rules = new object[0],
                    ModelsByRole = null,
                    SelectedModelByRole = null
                };
                System.Diagnostics.Debug.WriteLine("[c13-CACHE-INIT-FALLBACK] Using safe default snapshot");
            }
        }

        /// <summary>
        /// Create 3 mock tools for testing cache plumbing.
        /// Format matches OpenAI function calling spec used by Continue.
        /// </summary>
        private object[] CreateMockTools()
        {
            return new object[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "read_file",
                        description = "Read contents of a file",
                        parameters = new { type = "object", properties = new { } }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "search_codebase",
                        description = "Search codebase for patterns",
                        parameters = new { type = "object", properties = new { } }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "edit_existing_file",
                        description = "Edit an existing file",
                        parameters = new { type = "object", properties = new { } }
                    }
                }
            };
        }

        /// <summary>
        /// Atomically store complete configuration.
        /// </summary>
        public void SetConfig(
            object[]? models = null,
            object? tabAutocompleteModel = null,
            object[]? tools = null,
            object[]? slashCommands = null,
            object[]? contextProviders = null,
            object[]? mcpServerStatuses = null,
            object[]? rules = null,
            object? modelsByRole = null,
            object? selectedModelByRole = null)
        {
            lock (_lock)
            {
                _currentSnapshot = new ConfigSnapshot
                {
                    Models = models ?? new object[0],
                    TabAutocompleteModel = tabAutocompleteModel,
                    Tools = tools ?? new object[0],
                    SlashCommands = slashCommands ?? new object[0],
                    ContextProviders = contextProviders ?? new object[0],
                    McpServerStatuses = mcpServerStatuses ?? new object[0],
                    Rules = rules ?? new object[0],
                    ModelsByRole = modelsByRole,
                    SelectedModelByRole = selectedModelByRole
                };

                System.Diagnostics.Debug.WriteLine(
                    $"[c13-CONFIG-UPDATED] models={_currentSnapshot.Models?.Length}, " +
                    $"tools={_currentSnapshot.Tools?.Length}, " +
                    $"slashCommands={_currentSnapshot.SlashCommands?.Length}, " +
                    $"contextProviders={_currentSnapshot.ContextProviders?.Length}, " +
                    $"mcpServerStatuses={_currentSnapshot.McpServerStatuses?.Length}, " +
                    $"rules={_currentSnapshot.Rules?.Length}");
            }
        }

        /// <summary>
        /// Retrieve immutable snapshot of current configuration.
        /// Thread-safe; returns consistent state for concurrent readers.
        /// </summary>
        public ConfigSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                if (_currentSnapshot == null)
                {
                    System.Diagnostics.Debug.WriteLine("[c13-SNAPSHOT-UNINITIALIZED] _currentSnapshot is null, returning safe default");
                    return new ConfigSnapshot
                    {
                        Models = new object[0],
                        TabAutocompleteModel = null,
                        Tools = new object[0],
                        SlashCommands = new object[0],
                        ContextProviders = new object[0],
                        McpServerStatuses = new object[0],
                        Rules = new object[0],
                        ModelsByRole = null,
                        SelectedModelByRole = null
                    };
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[c13-SNAPSHOT-READ] returning snapshot with " +
                    $"{_currentSnapshot.Models?.Length} models, " +
                    $"{_currentSnapshot.Tools?.Length} tools, " +
                    $"{_currentSnapshot.SlashCommands?.Length} slashCommands");

                return _currentSnapshot;
            }
        }

        /// <summary>
        /// Store raw config object (e.g., from JSON message).
        /// Extracts top-level and nested config fields.
        /// </summary>
        public void Store(object? configData)
        {
            if (configData == null)
            {
                System.Diagnostics.Debug.WriteLine("[c13-STORE-NULL] Null config received, skipping");
                return;
            }

            try
            {
                // If it's a JObject, extract fields; otherwise treat entire object as config
                if (configData is JObject jObj)
                {
                    var models = jObj["models"]?.ToObject<object[]>();
                    var tabAutocompleteModel = jObj["tabAutocompleteModel"];
                    var tools = jObj["tools"]?.ToObject<object[]>();
                    var slashCommands = jObj["slashCommands"]?.ToObject<object[]>();
                    var contextProviders = jObj["contextProviders"]?.ToObject<object[]>();
                    var mcpServerStatuses = jObj["mcpServerStatuses"]?.ToObject<object[]>();
                    var rules = jObj["rules"]?.ToObject<object[]>();
                    var modelsByRole = jObj["modelsByRole"];
                    var selectedModelByRole = jObj["selectedModelByRole"];

                    SetConfig(
                        models: models,
                        tabAutocompleteModel: tabAutocompleteModel,
                        tools: tools,
                        slashCommands: slashCommands,
                        contextProviders: contextProviders,
                        mcpServerStatuses: mcpServerStatuses,
                        rules: rules,
                        modelsByRole: modelsByRole,
                        selectedModelByRole: selectedModelByRole);

                    System.Diagnostics.Debug.WriteLine(
                        $"[c13-STORE-EXTRACTED] Extracted config from JObject");
                }
                else
                {
                    // Try generic dynamic access
                    System.Diagnostics.Debug.WriteLine(
                        $"[c13-STORE-DYNAMIC] Storing generic config object type={configData.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[c13-STORE-ERROR] Error storing config: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieve cached config as dynamic object.
        /// Returns entire snapshot wrapped as anonymous object for compatibility.
        /// </summary>
        public object Retrieve()
        {
            var snapshot = GetSnapshot();
            return new
            {
                config = new
                {
                    models = snapshot.Models,
                    tabAutocompleteModel = snapshot.TabAutocompleteModel,
                    tools = snapshot.Tools,
                    slashCommands = snapshot.SlashCommands,
                    contextProviders = snapshot.ContextProviders,
                    mcpServerStatuses = snapshot.McpServerStatuses,
                    rules = snapshot.Rules,
                    modelsByRole = snapshot.ModelsByRole,
                    selectedModelByRole = snapshot.SelectedModelByRole
                }
            };
        }

        // Singleton instance
        private static readonly Lazy<ConfigCache> _instance = 
            new Lazy<ConfigCache>(() => new ConfigCache(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static ConfigCache Instance => _instance.Value;
    }
}
