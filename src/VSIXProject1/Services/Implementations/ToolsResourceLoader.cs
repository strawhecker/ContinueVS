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
    /// Provides fallback to in-memory defaults (BuiltInToolsRegistry) if resource is unavailable.
    /// </summary>
    public static class ToolsResourceLoader
    {
        private static readonly string ResourceName = "ContinueVS.config.tools-defaults.json";

        /// <summary>
        /// Loads default tool definitions from the embedded resource.
        /// Falls back to in-memory BuiltInToolsRegistry if resource is missing or corrupted.
        /// </summary>
        /// <returns>Enumerable of ToolDefinition instances from resource or fallback.</returns>
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
                            Debug.WriteLine($"[gap8_1-resource-load-error] Resource '{ResourceName}' not found in assembly");
                            Debug.WriteLine("[gap8_1-resource-load-debug] Available embedded resources:");
                            
                            // Diagnostic: List all available resources to help with troubleshooting
                            var resourceNames = assembly.GetManifestResourceNames();
                            foreach (var name in resourceNames)
                            {
                                Debug.WriteLine($"  - {name}");
                            }

                            // Fallback to in-memory defaults
                            Debug.WriteLine("[gap8_1-resource-load-fallback] Falling back to BuiltInToolsRegistry");
                            var fallbackTools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
                            Debug.WriteLine($"[gap8_1-resource-load-fallback-end] Fallback provided {fallbackTools.Count} tools from BuiltInToolsRegistry");
                            return fallbackTools;
                        }

                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            var json = reader.ReadToEnd();
                            Debug.WriteLine($"[gap8_1-resource-load-read] Read {json.Length} bytes from resource '{ResourceName}'");

                            var root = JObject.Parse(json);
                            var toolsArray = root["tools"] as JArray;

                            if (toolsArray == null)
                            {
                                Debug.WriteLine("[gap8_1-resource-load-error] 'tools' array not found in resource JSON");
                                Debug.WriteLine("[gap8_1-resource-load-fallback] Falling back to BuiltInToolsRegistry");
                                var fallbackTools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
                                Debug.WriteLine($"[gap8_1-resource-load-fallback-end] Fallback provided {fallbackTools.Count} tools from BuiltInToolsRegistry");
                                return fallbackTools;
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

                            Debug.WriteLine($"[gap8_1-resource-load-end] LoadDefaultToolsAsync completed: {tools.Count} tools loaded from resource");
                            return tools;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[gap8_1-resource-load-error] Exception in LoadDefaultToolsAsync: {ex.GetType().Name}: {ex.Message}");
                    Debug.WriteLine($"[gap8_1-resource-load-error] StackTrace: {ex.StackTrace}");
                    Debug.WriteLine("[gap8_1-resource-load-fallback] Falling back to BuiltInToolsRegistry due to exception");
                    
                    try
                    {
                        var fallbackTools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
                        Debug.WriteLine($"[gap8_1-resource-load-fallback-end] Fallback provided {fallbackTools.Count} tools from BuiltInToolsRegistry");
                        return fallbackTools;
                    }
                    catch (Exception fallbackEx)
                    {
                        Debug.WriteLine($"[gap8_1-resource-load-error] Fallback also failed: {fallbackEx.Message}");
                        return new List<ToolDefinition>();
                    }
                }
            });
        }
    }
}
