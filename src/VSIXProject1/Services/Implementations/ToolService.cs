#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IToolService that routes tool invocations to built-in tools (via IIdeService),
    /// MCP tools (via IMcpService), and HTTP endpoints (stubbed for now).
    /// </summary>
    public class ToolService : IToolService
    {
        private readonly IIdeService _ideService;
        private readonly IConfigService _configService;
        private readonly IMcpService? _mcpService;
        private readonly Dictionary<string, ToolDefinition> _builtInToolRegistry = new();
        private readonly Dictionary<string, ToolDefinition> _mcpToolRegistry = new();
        private readonly object _registryLock = new object();

        public event EventHandler<ToolErrorEventArgs>? Error;

        /// <summary>
        /// Initializes a new instance of ToolService.
        /// </summary>
        /// <param name="ideService">The IDE service for file and subprocess operations.</param>
        /// <param name="configService">The configuration service for tool definitions.</param>
        /// <param name="mcpService">Optional MCP service for Model Context Protocol tools.</param>
        public ToolService(
            IIdeService ideService,
            IConfigService configService,
            IMcpService? mcpService = null)
        {
            _ideService = ideService ?? throw new ArgumentNullException(nameof(ideService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _mcpService = mcpService;

            InitializeToolRegistry();
        }

        /// <summary>
        /// Gets all available tools (both built-in and MCP).
        /// </summary>
        public IEnumerable<ToolDefinition> GetAvailableTools()
        {
            lock (_registryLock)
            {
                var allTools = _builtInToolRegistry.Values.Concat(_mcpToolRegistry.Values).ToList();
                return allTools;
            }
        }

        /// <summary>
        /// Gets a specific tool by name.
        /// </summary>
        public ToolDefinition? GetTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName))
                return null;

            lock (_registryLock)
            {
                if (_builtInToolRegistry.TryGetValue(toolName, out var tool))
                    return tool;

                if (_mcpToolRegistry.TryGetValue(toolName, out var mcpTool))
                    return mcpTool;

                return null;
            }
        }

        /// <summary>
        /// Invokes a tool with the given arguments.
        /// Routes based on tool type: built-in, MCP, or HTTP.
        /// </summary>
        public async Task<ToolResult> InvokeAsync(
            string toolName,
            IDictionary<string, object> args,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(toolName))
                return CreateErrorResult(toolName, "Tool name cannot be null or empty");

            var tool = GetTool(toolName);
            if (tool == null)
                return CreateErrorResult(toolName, $"Tool '{toolName}' not found");

            try
            {
                return tool.ToolType switch
                {
                    "builtin" => await InvokeBuiltInAsync(toolName, args, ct),
                    "mcp" => await InvokeMcpToolAsync(tool.McpServerId ?? string.Empty, toolName, args),
                    "http" => CreateErrorResult(toolName, "HTTP tools not yet implemented"),
                    _ => CreateErrorResult(toolName, $"Unknown tool type: {tool.ToolType}")
                };
            }
            catch (Exception ex)
            {
                var errorArgs = new ToolErrorEventArgs
                {
                    ToolName = toolName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };

                Error?.Invoke(this, errorArgs);
                return CreateErrorResult(toolName, ex.Message);
            }
        }

        /// <summary>
        /// Routes built-in tool invocations to appropriate IIdeService methods.
        /// </summary>
        private async Task<ToolResult> InvokeBuiltInAsync(
            string toolName,
            IDictionary<string, object> args,
            CancellationToken ct)
        {
            return toolName switch
            {
                "read_file" => await ReadFileInternalAsync(GetArgString(args, "filepath")),
                "write_file" => await WriteFileInternalAsync(
                    GetArgString(args, "filepath"),
                    GetArgString(args, "contents")),
                "search_codebase" => await SearchCodebaseInternalAsync(
                    GetArgString(args, "query"),
                    GetArgInt(args, "maxResults", 10)),
                "run_subprocess" => await RunSubprocessInternalAsync(
                    GetArgString(args, "command"),
                    GetArgString(args, "cwd", ".")),
                _ => CreateErrorResult(toolName, $"Unknown built-in tool: {toolName}")
            };
        }

        /// <summary>
        /// Reads the contents of a file.
        /// </summary>
        public async Task<string> ReadFileAsync(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath));

            var contents = await _ideService.ReadFileAsync(filepath);
            return contents;
        }

        /// <summary>
        /// Writes contents to a file.
        /// </summary>
        public async Task WriteFileAsync(string filepath, string contents)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath));

            await _ideService.WriteFileAsync(filepath, contents);
        }

        /// <summary>
        /// Searches the codebase for matches to a query.
        /// </summary>
        public async Task<IEnumerable<CodeSearchResult>> SearchCodebaseAsync(string query, int maxResults)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentNullException(nameof(query));

            if (maxResults <= 0)
                maxResults = 10;

            // Stub implementation: search workspace files
            var results = new List<CodeSearchResult>();
            var workspaceFiles = _ideService.GetWorkspaceFiles("*.cs");

            foreach (var filePath in workspaceFiles.Take(100))
            {
                try
                {
                    var content = await _ideService.ReadFileAsync(filePath);
                    var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    for (int i = 0; i < lines.Length && results.Count < maxResults; i++)
                    {
                        if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new CodeSearchResult
                            {
                                FilePath = filePath,
                                LineNumber = i + 1,
                                LineContent = lines[i],
                                Relevance = 0.9
                            });
                        }
                    }
                }
                catch
                {
                    // Skip files that can't be read
                }
            }

            return results;
        }

        /// <summary>
        /// Runs a subprocess command.
        /// </summary>
        public async Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command));

            var result = await _ideService.RunSubprocessAsync(command, cwd ?? ".");
            return result;
        }

        /// <summary>
        /// Loads available MCP tools from a server.
        /// </summary>
        public async Task LoadMcpToolsAsync(string serverId)
        {
            if (string.IsNullOrEmpty(serverId))
                throw new ArgumentNullException(nameof(serverId));

            if (_mcpService == null)
                throw new InvalidOperationException("MCP service is not available");

            // Load tools from MCP server and add to registry
            var tools = _mcpService.GetServerTools(serverId);
            lock (_registryLock)
            {
                foreach (var tool in tools)
                {
                    if (!string.IsNullOrEmpty(tool.Name))
                    {
                        _mcpToolRegistry[tool.Name] = tool;
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Invokes a tool provided by an MCP server.
        /// </summary>
        public async Task<ToolResult> InvokeMcpToolAsync(
            string serverId,
            string toolName,
            IDictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(serverId))
                return CreateErrorResult(toolName, "MCP server ID cannot be null or empty");

            if (string.IsNullOrEmpty(toolName))
                return CreateErrorResult(toolName, "Tool name cannot be null or empty");

            if (_mcpService == null)
                return CreateErrorResult(toolName, "MCP service is not available");

            try
            {
                // MCP service integration point - stub for now
                // In production, this would call the actual MCP service tool invocation
                var result = new ToolResult
                {
                    ToolName = toolName,
                    Output = "MCP tool invocation not yet fully implemented",
                    IsSuccess = false
                };

                return result;
            }
            catch (Exception ex)
            {
                var errorArgs = new ToolErrorEventArgs
                {
                    ToolName = toolName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };

                Error?.Invoke(this, errorArgs);
                return CreateErrorResult(toolName, ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for read file as ToolResult.
        /// </summary>
        private async Task<ToolResult> ReadFileInternalAsync(string filepath)
        {
            try
            {
                var contents = await ReadFileAsync(filepath);
                return new ToolResult
                {
                    ToolName = "read_file",
                    Output = contents,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("read_file", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for write file as ToolResult.
        /// </summary>
        private async Task<ToolResult> WriteFileInternalAsync(string filepath, string contents)
        {
            try
            {
                await WriteFileAsync(filepath, contents);
                return new ToolResult
                {
                    ToolName = "write_file",
                    Output = $"File written: {filepath}",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("write_file", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for search codebase as ToolResult.
        /// </summary>
        private async Task<ToolResult> SearchCodebaseInternalAsync(string query, int maxResults)
        {
            try
            {
                var results = await SearchCodebaseAsync(query, maxResults);
                var resultList = results.ToList();
                return new ToolResult
                {
                    ToolName = "search_codebase",
                    Output = $"Found {resultList.Count} results",
                    RawOutput = resultList,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("search_codebase", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for run subprocess as ToolResult.
        /// </summary>
        private async Task<ToolResult> RunSubprocessInternalAsync(string command, string cwd)
        {
            try
            {
                var (stdout, stderr) = await RunSubprocessAsync(command, cwd);
                return new ToolResult
                {
                    ToolName = "run_subprocess",
                    Output = stdout,
                    Metadata = new Dictionary<string, string> { { "stderr", stderr } },
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("run_subprocess", ex.Message);
            }
        }

        /// <summary>
        /// Initializes the tool registry by loading enabled tools from configuration.
        /// </summary>
        private void InitializeToolRegistry()
        {
            lock (_registryLock)
            {
                _builtInToolRegistry.Clear();

                // Load enabled tools from configuration
                var enabledTools = _configService.GetEnabledTools();
                foreach (var tool in enabledTools)
                {
                    if (!string.IsNullOrEmpty(tool.Name))
                    {
                        if (tool.ToolType == "builtin")
                        {
                            _builtInToolRegistry[tool.Name] = tool;
                        }
                        else if (tool.ToolType == "mcp")
                        {
                            _mcpToolRegistry[tool.Name] = tool;
                        }
                    }
                }

                // Ensure built-in tools are always available (with defaults if not in config)
                EnsureBuiltInToolDefaults();
            }
        }

        /// <summary>
        /// Ensures that core built-in tools have definitions, creating defaults if necessary.
        /// </summary>
        private void EnsureBuiltInToolDefaults()
        {
            var defaultTools = new[]
            {
                CreateBuiltInToolDefinition("read_file", "Read the contents of a file", new[] { "filepath" }),
                CreateBuiltInToolDefinition("write_file", "Write contents to a file", new[] { "filepath", "contents" }),
                CreateBuiltInToolDefinition("search_codebase", "Search the codebase for matches to a query", new[] { "query", "maxResults" }),
                CreateBuiltInToolDefinition("run_subprocess", "Run a subprocess command", new[] { "command", "cwd" })
            };

            foreach (var tool in defaultTools)
            {
                if (!_builtInToolRegistry.ContainsKey(tool.Name))
                {
                    _builtInToolRegistry[tool.Name] = tool;
                }
            }
        }

        /// <summary>
        /// Creates a built-in tool definition.
        /// </summary>
        private ToolDefinition CreateBuiltInToolDefinition(string name, string description, string[] parameterNames)
        {
            var parameters = parameterNames.Select((pname, idx) => new ParameterDefinition
            {
                Name = pname,
                Type = "string",
                Description = $"Parameter: {pname}",
                IsRequired = idx == 0 // First parameter is required
            }).ToList();

            return new ToolDefinition
            {
                Name = name,
                Description = description,
                Category = "Built-In",
                Parameters = parameters,
                IsEnabled = true,
                IsAsync = true,
                ToolType = "builtin"
            };
        }

        /// <summary>
        /// Creates a tool error result.
        /// </summary>
        private ToolResult CreateErrorResult(string toolName, string message)
        {
            return new ToolResult
            {
                ToolName = toolName,
                Output = message,
                IsSuccess = false
            };
        }

        /// <summary>
        /// Gets a string argument from the arguments dictionary.
        /// </summary>
        private string GetArgString(IDictionary<string, object> args, string key, string defaultValue = "")
        {
            if (args == null || !args.TryGetValue(key, out var value))
                return defaultValue;

            return value?.ToString() ?? defaultValue;
        }

        /// <summary>
        /// Gets an integer argument from the arguments dictionary.
        /// </summary>
        private int GetArgInt(IDictionary<string, object> args, string key, int defaultValue = 0)
        {
            if (args == null || !args.TryGetValue(key, out var value))
                return defaultValue;

            if (value is int intVal)
                return intVal;

            if (int.TryParse(value?.ToString(), out var parsed))
                return parsed;

            return defaultValue;
        }
    }
}
