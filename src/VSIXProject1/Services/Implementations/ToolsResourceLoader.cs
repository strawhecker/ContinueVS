using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Loads built-in tool definitions from the embedded tools-defaults.json resource.
    /// Provides fallback to in-memory defaults if resource is unavailable.
    /// </summary>
    public static class ToolsResourceLoader
    {
        private static readonly string ResourceName = "ContinueVS.config.tools-defaults.json";

        /// <summary>
        /// Loads default tool definitions from the embedded resource.
        /// Falls back to empty list if resource is missing or corrupted.
        /// </summary>
        /// <returns>Enumerable of ToolDefinition instances from resource, or empty list on error.</returns>
        public static async Task<IEnumerable<ToolDefinition>> LoadDefaultToolsAsync()
        {
            Debug.WriteLine("[gap8_1-resource-load-start] LoadDefaultToolsAsync called");

            return await Task.Run(() =>
            {
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    using (var stream = assembly.GetManifestResourceStream(ResourceName))
                    {
                        if (stream == null)
                        {
                            Debug.WriteLine($"[gap8_1-resource-load-error] Resource not found: {ResourceName}");
                            return new List<ToolDefinition>();
                        }

                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            var json = reader.ReadToEnd();
                            Debug.WriteLine($"[gap8_1-resource-load-read] Read {json.Length} bytes from resource");

                            var root = JObject.Parse(json);
                            var toolsArray = root["tools"] as JArray;

                            if (toolsArray == null)
                            {
                                Debug.WriteLine("[gap8_1-resource-load-error] 'tools' array not found in resource JSON");
                                return new List<ToolDefinition>();
                            }

                            var tools = new List<ToolDefinition>();
                            foreach (var toolToken in toolsArray)
                            {
                                try
                                {
                                    var tool = toolToken.ToObject<ToolDefinition>();
                                    if (tool != null)
                                    {
                                        tools.Add(tool);
                                        Debug.WriteLine($"[gap8_1-resource-load-tool] Loaded tool: {tool.Name}, enabled={tool.IsEnabled}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[gap8_1-resource-load-error] Failed to deserialize tool: {ex.Message}");
                                }
                            }

                            Debug.WriteLine($"[gap8_1-resource-load-end] LoadDefaultToolsAsync completed: {tools.Count} tools loaded");
                            return tools;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[gap8_1-resource-load-error] Exception in LoadDefaultToolsAsync: {ex.GetType().Name}: {ex.Message}");
                    return new List<ToolDefinition>();
                }
            });
        }
    }
}
