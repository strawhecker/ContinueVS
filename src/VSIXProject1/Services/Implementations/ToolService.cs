#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ISessionService? _sessionService;
        private readonly IMcpService? _mcpService;
        private readonly IBridgeLogger? _logger;
        private readonly Dictionary<string, ToolDefinition> _builtInToolRegistry = new();
        private readonly Dictionary<string, ToolDefinition> _mcpToolRegistry = new();
        private readonly object _registryLock = new object();
        private readonly ToolOverrideProcessor _overrideProcessor = new();

        public event EventHandler<ToolErrorEventArgs>? Error;

        /// <summary>
        /// Initializes a new instance of ToolService.
        /// </summary>
        /// <param name="ideService">The IDE service for file and subprocess operations.</param>
        /// <param name="configService">The configuration service for tool definitions.</param>
        /// <param name="sessionService">Optional session service for tracking tool call counts.</param>
        /// <param name="mcpService">Optional MCP service for Model Context Protocol tools.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public ToolService(
            IIdeService ideService,
            IConfigService configService,
            ISessionService? sessionService = null,
            IMcpService? mcpService = null,
            IBridgeLogger? logger = null)
        {
            _ideService = ideService ?? throw new ArgumentNullException(nameof(ideService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _sessionService = sessionService;
            _mcpService = mcpService;
            _logger = logger;

            InitializeToolRegistry();
        }

        /// <summary>
        /// Gets all available tools (both built-in and MCP), with overrides applied.
        /// </summary>
        public IEnumerable<ToolDefinition> GetAvailableTools()
        {
            lock (_registryLock)
            {
                var allTools = _builtInToolRegistry.Values.Concat(_mcpToolRegistry.Values).ToList();
                _ = _logger?.WriteDebugAsync($"[gap8_1-toolsvc-available] GetAvailableTools: {_builtInToolRegistry.Count} built-in, {_mcpToolRegistry.Count} mcp, total={allTools.Count}");

                // Apply overrides from configuration
                var overrideConfig = _configService.GetToolOverrideConfig();
                allTools = _overrideProcessor.ApplyOverrides(allTools, overrideConfig).ToList();

                // Defensive: Log warning if tools are unexpectedly empty
                if (allTools.Count == 0)
                {
                    string warningMessage = 
                        $"[WARNING-gap8_1] GetAvailableTools returned ZERO tools. " +
                        $"Built-in: {_builtInToolRegistry.Count}, MCP: {_mcpToolRegistry.Count}. " +
                        $"The AI system will have no tools available for this request.";
                    _ = _logger?.WriteWarningAsync(warningMessage);
                }

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
        /// Checks limit before execution and increments tool call counter in current session.
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
                // Check limit before executing tool (gap23_4_3)
                if (_sessionService != null)
                {
                    try
                    {
                        var session = _sessionService.GetCurrentSession();
                        if (session != null)
                        {
                            var config = _configService?.GetCurrentConfig();
                            if (config != null && session.ToolCallsExecuted >= config.MaxToolCallsPerSession)
                            {
                                var limitMessage = $"Max tool calls ({config.MaxToolCallsPerSession}) reached. Start a new session to continue.";
                                _ = _logger?.WriteWarningAsync($"[gap23_4_3-limit] {limitMessage}");
                                throw new InvalidOperationException(limitMessage);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        throw; // Re-throw limit exceeded exceptions
                    }
                    catch
                    {
                        // Silently ignore other session access errors in unit test contexts
                    }
                }

                // Increment tool call counter before execution
                if (_sessionService != null)
                {
                    try
                    {
                        var session = _sessionService.GetCurrentSession();
                        if (session != null)
                        {
                            session.ToolCallsExecuted++;
                        }
                    }
                    catch
                    {
                        // Silently ignore session access errors in unit test contexts
                    }
                }

                return tool.ToolType switch
                {
                    "builtin" => await InvokeBuiltInAsync(toolName, args, ct),
                    "mcp" => await InvokeMcpToolAsync(tool.McpServerId ?? string.Empty, toolName, args),
                    "http" => CreateErrorResult(toolName, "HTTP tools not yet implemented"),
                    _ => CreateErrorResult(toolName, $"Unknown tool type: {tool.ToolType}")
                };
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw limit exceeded exceptions
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
                "read_file_range" => await ReadFileRangeInternalAsync(
                    GetArgString(args, "filepath"),
                    GetArgInt(args, "startLine", 1),
                    GetArgInt(args, "endLine", 999999)),
                "grep_search" => await GrepSearchInternalAsync(
                    GetArgString(args, "directory"),
                    GetArgString(args, "pattern"),
                    GetArgString(args, "filePattern", ".*")),
                "single_find_and_replace" => await SingleFindAndReplaceInternalAsync(
                    GetArgString(args, "filepath"),
                    GetArgString(args, "pattern"),
                    GetArgString(args, "replacement"),
                    GetArgString(args, "flags", "")),
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
        /// Internal wrapper for read_file_range (gap23_2b).
        /// Reads a specific line range from a file without loading the entire file.
        /// </summary>
        private async Task<ToolResult> ReadFileRangeInternalAsync(string filepath, int startLine, int endLine)
        {
            try
            {
                if (string.IsNullOrEmpty(filepath))
                    return CreateErrorResult("read_file_range", "filepath cannot be null or empty");

                if (startLine < 1)
                    return CreateErrorResult("read_file_range", "startLine must be >= 1");

                if (endLine < startLine)
                    return CreateErrorResult("read_file_range", "endLine must be >= startLine");

                var contents = await _ideService.ReadFileAsync(filepath);
                var lines = contents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                // Adjust for 1-based indexing
                int actualStart = Math.Max(0, startLine - 1);
                int actualEnd = Math.Min(lines.Length - 1, endLine - 1);

                if (actualStart >= lines.Length)
                    return CreateErrorResult("read_file_range", $"startLine ({startLine}) exceeds file line count ({lines.Length})");

                var rangeLines = lines.Skip(actualStart).Take(actualEnd - actualStart + 1);
                var result = string.Join("\n", rangeLines);

                return new ToolResult
                {
                    ToolName = "read_file_range",
                    Output = result,
                    Metadata = new Dictionary<string, string>
                    {
                        { "filepath", filepath },
                        { "startLine", startLine.ToString() },
                        { "endLine", endLine.ToString() },
                        { "linesReturned", rangeLines.Count().ToString() }
                    },
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("read_file_range", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for grep_search (gap23_2b).
        /// Searches for files matching a regex pattern.
        /// </summary>
        private async Task<ToolResult> GrepSearchInternalAsync(string directory, string pattern, string filePattern)
        {
            try
            {
                if (string.IsNullOrEmpty(pattern))
                    return CreateErrorResult("grep_search", "pattern cannot be null or empty");

                var workspaceFiles = _ideService.GetWorkspaceFiles(filePattern ?? "*.*");
                var matches = new List<string>();
                var regex = new System.Text.RegularExpressions.Regex(pattern);

                foreach (var filePath in workspaceFiles.Take(100))
                {
                    try
                    {
                        var content = await _ideService.ReadFileAsync(filePath);
                        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                        for (int i = 0; i < lines.Length && matches.Count < 50; i++)
                        {
                            if (regex.IsMatch(lines[i]))
                            {
                                matches.Add($"{filePath}:{i + 1}: {lines[i]}");
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }

                var output = matches.Count == 0 ? "No matches found" : string.Join("\n", matches);
                return new ToolResult
                {
                    ToolName = "grep_search",
                    Output = output,
                    Metadata = new Dictionary<string, string>
                    {
                        { "pattern", pattern },
                        { "matchCount", matches.Count.ToString() }
                    },
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("grep_search", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for single_find_and_replace (gap23_2b).
        /// Performs regex find-and-replace in a single file.
        /// </summary>
        private async Task<ToolResult> SingleFindAndReplaceInternalAsync(string filepath, string pattern, string replacement, string flags)
        {
            try
            {
                if (string.IsNullOrEmpty(filepath))
                    return CreateErrorResult("single_find_and_replace", "filepath cannot be null or empty");

                if (string.IsNullOrEmpty(pattern))
                    return CreateErrorResult("single_find_and_replace", "pattern cannot be null or empty");

                var contents = await _ideService.ReadFileAsync(filepath);
                var options = System.Text.RegularExpressions.RegexOptions.None;

                if (!string.IsNullOrEmpty(flags))
                {
                    if (flags.Contains('i'))
                        options |= System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                    if (flags.Contains('m'))
                        options |= System.Text.RegularExpressions.RegexOptions.Multiline;
                }

                var regex = new System.Text.RegularExpressions.Regex(pattern, options);
                var newContents = regex.Replace(contents, replacement ?? "");
                int replacementCount = regex.Matches(contents).Count;

                // Write back to file
                await _ideService.WriteFileAsync(filepath, newContents);

                return new ToolResult
                {
                    ToolName = "single_find_and_replace",
                    Output = $"Successfully replaced {replacementCount} occurrence(s) in {filepath}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "filepath", filepath },
                        { "pattern", pattern },
                        { "replacementCount", replacementCount.ToString() }
                    },
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("single_find_and_replace", ex.Message);
            }
        }

        /// <summary>
        /// Initializes the tool registry by loading enabled tools from configuration.
        /// </summary>
        private void InitializeToolRegistry()
        {
            lock (_registryLock)
            {
                _ = _logger?.WriteDebugAsync("[gap8_1-toolsvc-init-start] InitializeToolRegistry called");
                _builtInToolRegistry.Clear();

                // Load enabled tools from configuration
                var enabledTools = _configService.GetEnabledTools().ToList();
                _ = _logger?.WriteDebugAsync($"[gap8_1-toolsvc-load-config] Loaded {enabledTools.Count} enabled tools from config");
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

                int totalTools = _builtInToolRegistry.Count + _mcpToolRegistry.Count;
                _ = _logger?.WriteDebugAsync($"[gap8_1-toolsvc-init-end] InitializeToolRegistry complete: {_builtInToolRegistry.Count} built-in tools registered");

                // Fail-fast diagnostic check for zero tools
                if (totalTools == 0)
                {
                    string diagnosticMessage = 
                        "[CRITICAL-gap8_1] Tool registry is EMPTY after initialization! " +
                        "This indicates a configuration or initialization failure. " +
                        "Built-in tools: 0, MCP tools: 0. " +
                        "Check: (1) BuiltInToolsRegistry.GetAllBuiltInTools() returns tools, " +
                        "(2) ConfigService.GetEnabledTools() is not corrupted, " +
                        "(3) Configuration file is valid.";

                    // Fire-and-forget async logging (don't await in synchronous constructor context)
                    _ = _logger?.WriteErrorAsync(diagnosticMessage);

                    // Throw to fail fast and alert developer/user immediately
                    throw new InvalidOperationException(
                        "ToolService initialization failed: zero tools registered. " +
                        "The Continue AI will not have access to any tools. " +
                        "Check the debug output and configuration file.");
                }
            }
        }

        /// <summary>
        /// Ensures that core built-in tools have definitions, populated from BuiltInToolsRegistry.
        /// </summary>
        private void EnsureBuiltInToolDefaults()
        {
            _ = _logger?.WriteDebugAsync("[gap8_1-toolsvc-defaults-start] EnsureBuiltInToolDefaults called");
            var defaultTools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            int addedCount = 0;

            foreach (var tool in defaultTools)
            {
                if (!_builtInToolRegistry.ContainsKey(tool.Name))
                {
                    _builtInToolRegistry[tool.Name] = tool;
                    addedCount++;
                }
            }
            _ = _logger?.WriteDebugAsync($"[gap8_1-toolsvc-defaults-end] EnsureBuiltInToolDefaults: {defaultTools.Count} defaults checked, {addedCount} added");
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
