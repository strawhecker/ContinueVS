#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private readonly IPlanOutputService? _planOutputService;
        private readonly IInteractivePromptService? _interactivePromptService;
        private readonly IContextRetirementService? _contextRetirementService;
        private readonly IDebuggerService? _debuggerService;
        private readonly Dictionary<string, ToolDefinition> _builtInToolRegistry = new();
        private readonly Dictionary<string, ToolDefinition> _mcpToolRegistry = new();
        private readonly object _registryLock = new object();
        private readonly ToolOverrideProcessor _overrideProcessor = new();

        // Active-plan binding (session-scoped, non-saved). Captured at message-send time when the
        // user's active document is a plan under ~/.continueVS/plans/. Consumed by read_plan /
        // update_plan tools. Guarded to remain thread-safe across the tool-loop.
        private readonly object _planBindingLock = new object();
        private string? _activePlanPath;

        /// <summary>
        /// Static mapping of tool names to UserSettings constants for filtering.
        /// Used to check if a tool should be available based on user settings.
        /// </summary>
        private static readonly Dictionary<string, string> ToolNameToUserSettingKey = new()
        {
            // Read-Only Tools
            { "read_file", UserSettings.Tool_ReadFileEnabled },
            { "read_file_range", UserSettings.Tool_ReadFileRangeEnabled },
            { "ls", UserSettings.Tool_ListDirectoryEnabled },
            { "file_glob_search", UserSettings.Tool_FileGlobSearchEnabled },
            { "search_codebase", UserSettings.Tool_SearchCodeEnabled },
            { "grep_search", UserSettings.Tool_GrepSearchEnabled },
            { "view_diff", UserSettings.Tool_ViewDiffEnabled },
            { "git_status", UserSettings.Tool_GitStatusEnabled },
            { "git_diff", UserSettings.Tool_GitDiffEnabled },
            { "git_log", UserSettings.Tool_GitLogEnabled },
            { "get_problems", UserSettings.Tool_GetProblemsEnabled },
            { "view_file", UserSettings.Tool_ViewFileEnabled },
            { "read_currently_open_file", UserSettings.Tool_ReadCurrentlyOpenFileEnabled },

            // Write Tools
            { "edit_file", UserSettings.Tool_EditFileEnabled },
            { "create_new_file", UserSettings.Tool_CreateNewFileEnabled },
            { "create_folder", UserSettings.Tool_CreateFolderEnabled },
            { "run_terminal_command", UserSettings.Tool_RunTerminalCommandEnabled },
            { "git_commit", UserSettings.Tool_GitCommitEnabled },
            { "create_rule_block", UserSettings.Tool_CreateRuleBlockEnabled },
            { "create_snippet", UserSettings.Tool_CreateSnippetEnabled },
            { "open_file", UserSettings.Tool_OpenFileEnabled },
            { "single_find_and_replace", UserSettings.Tool_SingleFindAndReplaceEnabled },
            { "run_pytest", UserSettings.Tool_RunPytestEnabled },
            { "write_plan", UserSettings.Tool_WritePlanEnabled },
            { "read_plan", UserSettings.Tool_PlanToolsEnabled },
            { "update_plan", UserSettings.Tool_PlanToolsEnabled },
            { "ask_user", UserSettings.Tool_AskUserEnabled },
            { "retire_from_context", UserSettings.Tool_RetireFromContextEnabled },

            // gap92_3 Tier-2 debug evaluate/mutate tools (default-disabled)
            { "debug_evaluate", UserSettings.Tool_DebugEvaluateEnabled },
            { "debug_set_value", UserSettings.Tool_DebugSetValueEnabled },
            { "debug_memory_read", UserSettings.Tool_DebugMemoryReadEnabled },
            { "debug_memory_write", UserSettings.Tool_DebugMemoryWriteEnabled },
            { "debug_run_to_cursor", UserSettings.Tool_DebugRunToCursorEnabled },
            { "debug_thread_set_state", UserSettings.Tool_DebugThreadStateEnabled }
        };

        public event EventHandler<ToolErrorEventArgs>? Error;

        /// <summary>
        /// Initializes a new instance of ToolService.
        /// </summary>
        /// <param name="ideService">The IDE service for file and subprocess operations.</param>
        /// <param name="configService">The configuration service for tool definitions.</param>
        /// <param name="sessionService">Optional session service for tracking tool call counts.</param>
        /// <param name="mcpService">Optional MCP service for Model Context Protocol tools.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="planOutputService">Optional plan output service for persisting plans (write_plan).</param>
        /// <param name="interactivePromptService">Optional interactive prompt service for surfacing user questions (ask_user).</param>
        /// <param name="contextRetirementService">Optional context retirement service for the retire_from_context tool.</param>
        public ToolService(
            IIdeService ideService,
            IConfigService configService,
            ISessionService? sessionService = null,
            IMcpService? mcpService = null,
            IBridgeLogger? logger = null,
            IPlanOutputService? planOutputService = null,
            IInteractivePromptService? interactivePromptService = null,
            IContextRetirementService? contextRetirementService = null,
            IDebuggerService? debuggerService = null)
        {
            _ideService = ideService ?? throw new ArgumentNullException(nameof(ideService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _sessionService = sessionService;
            _mcpService = mcpService;
            _logger = logger;
            _planOutputService = planOutputService;
            _interactivePromptService = interactivePromptService;
            _contextRetirementService = contextRetirementService;
            _debuggerService = debuggerService;

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
                _logger?.WriteDebug($"[gap8_1-toolsvc-available] GetAvailableTools: {_builtInToolRegistry.Count} built-in, {_mcpToolRegistry.Count} mcp, total={allTools.Count}");

                // Apply overrides from configuration
                var overrideConfig = _configService.GetToolOverrideConfig();
                allTools = _overrideProcessor.ApplyOverrides(allTools, overrideConfig).ToList();

                // Apply user settings filtering (second gate: per-tool enable/disable)
                allTools = ApplyUserSettingsFilter(allTools).ToList();

                // CRITICAL: Filter out disabled tools - they should never be returned to the LLM
                allTools = allTools.Where(t => t.IsEnabled).ToList();
                _logger?.WriteDebug($"[gap8_1-toolsvc-filter-disabled] After filtering disabled: {allTools.Count} tools remaining");

                // Defensive: Log warning if tools are unexpectedly empty
                if (allTools.Count == 0)
                {
                    string warningMessage =
                        $"[WARNING-gap8_1] GetAvailableTools returned ZERO tools. " +
                        $"Built-in: {_builtInToolRegistry.Count}, MCP: {_mcpToolRegistry.Count}. " +
                        $"The AI system will have no tools available for this request.";
                    _logger?.WriteWarning(warningMessage);
                }

                // Deduplicate by tool name (if a tool appears twice due to combining registries,
                // keep the first occurrence - built-in tools take precedence)
                var dedupedTools = new Dictionary<string, ToolDefinition>();
                foreach (var tool in allTools)
                {
                    if (!dedupedTools.ContainsKey(tool.Name))
                    {
                        dedupedTools[tool.Name] = tool;
                        _logger?.WriteDebug($"[gap8_1-dedup] Tool '{tool.Name}' added to deduplicated list");
                    }
                    else
                    {
                        _logger?.WriteDebug($"[gap8_1-dedup] Duplicate tool '{tool.Name}' skipped (keeping first occurrence)");
                    }
                }

                return dedupedTools.Values.ToList();
            }
        }

        /// <summary>
        /// Gets all available tools for a specific ChatMode, filtered by SupportedModes.
        /// Tools with empty SupportedModes are available in all modes (backward compatibility).
        /// </summary>
        public IEnumerable<ToolDefinition> GetAvailableTools(ChatMode mode)
        {
            var allTools = GetAvailableTools();
            _logger?.WriteDebug($"[gap71-toolsvc-mode-filter] GetAvailableTools(mode={mode}): Starting filter from {allTools.Count()} tools");

            var filteredTools = allTools.Where(tool =>
            {
                // If SupportedModes is empty, tool is available in all modes (backward compatibility)
                if (tool.SupportedModes == null || tool.SupportedModes.Count == 0)
                    return true;

                // Otherwise, only include if the requested mode is in SupportedModes
                return tool.SupportedModes.Contains(mode);
            }).ToList();

            _logger?.WriteDebug($"[gap71-toolsvc-mode-filter] Filtered to {filteredTools.Count} tools for mode {mode}");

            return filteredTools;
        }

        /// <summary>
        /// Applies user settings filtering to a list of tools.
        /// Excludes tools that are disabled in the user's CustomSettings.
        /// </summary>
        private IEnumerable<ToolDefinition> ApplyUserSettingsFilter(IEnumerable<ToolDefinition> tools)
        {
            try
            {
                var customSettings = _configService.GetCurrentConfig()?.CustomSettings ?? new Dictionary<string, object>();
                var defaults = UserSettings.GetDefaults();

                var filtered = tools.Where(tool =>
                {
                    // Check if this tool has a user settings key
                    if (!ToolNameToUserSettingKey.TryGetValue(tool.Name, out var settingKey))
                    {
                        // Tool has no user setting, allow it (backward compatibility)
                        return true;
                    }

                    // Get the user's setting value, or use default if not set
                    object? settingValue;
                    if (customSettings.TryGetValue(settingKey, out var customValue))
                    {
                        settingValue = customValue;
                    }
                    else
                    {
                        settingValue = defaults.TryGetValue(settingKey, out var defaultValue) ? defaultValue : true;
                    }

                    // Convert value to bool
                    bool isEnabled = Convert.ToBoolean(settingValue);

                    if (!isEnabled)
                    {
                        _logger?.WriteDebug($"[user-settings-filter] Tool '{tool.Name}' filtered (disabled in user settings via {settingKey})");
                    }

                    return isEnabled;
                }).ToList();

                _logger?.WriteDebug($"[user-settings-filter] Filtered {tools.Count()} tools -> {filtered.Count} after user settings");
                return filtered;
            }
            catch (Exception ex)
            {
                // If anything fails, return the original tools to avoid blocking
                _logger?.WriteWarning($"[user-settings-filter] Error applying user settings filter: {ex.Message}. Returning all tools.");
                return tools;
            }
        }
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
        /// Checks the per-action budget before execution (resets on Send; auto-continuations
        /// accumulate but never reset - gap79) and increments the per-action tool call counter.
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
                // Check per-action budget before executing tool (gap79)
                if (_sessionService != null)
                {
                    try
                    {
                        var session = _sessionService.GetCurrentSession();
                        if (session != null)
                        {
                            var config = _configService?.GetCurrentConfig();
                            if (config != null)
                            {
                                int maxToolCalls = UserSettings.DefaultsAsInt(
                                    config.CustomSettings,
                                    UserSettings.Agent_MaxToolCallsPerAction,
                                    100);

                                if (session.ToolCallsExecuted >= maxToolCalls)
                                {
                                    var limitMessage = $"Per-action tool budget ({maxToolCalls}) reached. Press Send again for a fresh budget.";
                                    _logger?.WriteWarning($"[gap79-limit] {limitMessage}");
                                    throw new InvalidOperationException(limitMessage);
                                }
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
                "read_currently_open_file" => await ReadCurrentlyOpenFileInternalAsync(),
                "write_file" => await WriteFileInternalAsync(
                    GetArgString(args, "filepath"),
                    GetArgString(args, "contents")),
                "create_new_file" => await CreateNewFileInternalAsync(
                    GetArgString(args, "filepath"),
                    GetArgString(args, "contents")),
                "edit_file" => await EditFileInternalAsync(
                    GetArgString(args, "filepath"),
                    GetArgString(args, "oldText"),
                    GetArgString(args, "newText")),
                "create_folder" => await CreateFolderInternalAsync(
                    GetArgString(args, "folderpath")),
                "ls" => await ListDirectoryInternalAsync(
                    GetArgString(args, "dirPath"),
                    args.TryGetValue("recursive", out var rec) && (rec is bool b ? b : bool.TryParse(rec?.ToString() ?? "false", out var parsed) && parsed)),
                "search_codebase" => await SearchCodebaseInternalAsync(
                    GetArgString(args, "query"),
                    GetArgInt(args, "maxResults", 10)),
                "file_glob_search" => await FileGlobSearchInternalAsync(
                    GetArgString(args, "pattern"),
                    GetArgInt(args, "maxResults", 100)),
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
                "run_terminal_command" => await RunTerminalCommandInternalAsync(
                    GetArgString(args, "command"),
                    args.TryGetValue("waitForCompletion", out var wait) && (wait is bool b ? b : bool.TryParse(wait?.ToString() ?? "true", out var parsed) && parsed)),
                "view_diff" => await ViewDiffInternalAsync(),
                "create_rule_block" => await CreateRuleBlockInternalAsync(
                    GetArgString(args, "name"),
                    GetArgString(args, "rule")),
                "run_pytest" => await RunPytestInternalAsync(
                    GetArgString(args, "testPath", "")),
                "get_problems" => await GetProblemsInternalAsync(),
                "view_file" => await ViewFileInternalAsync(
                    GetArgString(args, "filepath")),
                "open_file" => await OpenFileInternalAsync(
                    GetArgString(args, "filepath")),
                "git_status" => await GitStatusInternalAsync(),
                "git_diff" => await GitDiffInternalAsync(
                    GetArgString(args, "filePath", "")),
                "git_log" => await GitLogInternalAsync(
                    GetArgInt(args, "maxCommits", 10)),
                "git_commit" => await GitCommitInternalAsync(
                    GetArgString(args, "message")),
                "create_snippet" => await CreateSnippetInternalAsync(
                    GetArgString(args, "name"),
                    GetArgString(args, "code")),
                "write_plan" => await WritePlanInternalAsync(args, ct),
                "read_plan" => await ReadPlanInternalAsync(args, ct),
                "update_plan" => await UpdatePlanInternalAsync(args, ct),
                "ask_user" => await InvokeAskUserAsync(args, ct),
                "retire_from_context" => await InvokeRetireFromContextAsync(args, ct),
                // gap92_3 Tier-2 debug evaluate/mutate tools
                "debug_evaluate" => await EvaluateInternalAsync(args),
                "debug_set_value" => await SetValueInternalAsync(args),
                "debug_memory_read" => await MemoryReadInternalAsync(args),
                "debug_memory_write" => await MemoryWriteInternalAsync(args),
                "debug_run_to_cursor" => await RunToCursorInternalAsync(args),
                "debug_thread_set_state" => await SetThreadStateInternalAsync(args),
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
        /// Sets the active (bound) plan path for the current session scope. Non-saved, in-memory.
        /// </summary>
        public void SetActivePlanPath(string? path)
        {
            lock (_planBindingLock)
            {
                _activePlanPath = string.IsNullOrWhiteSpace(path) ? null : path;
            }
        }

        /// <summary>
        /// Gets the currently bound active plan path (false + null when none bound).
        /// </summary>
        public (bool isBound, string? path) GetActivePlanBinding()
        {
            lock (_planBindingLock)
            {
                return (string.IsNullOrWhiteSpace(_activePlanPath) == false, _activePlanPath);
            }
        }

        /// <summary>
        /// Resolves the target plan path for read_plan / update_plan. Prefers an explicitly supplied
        /// repo-root-relative 'path' argument; otherwise falls back to the bound active plan.
        /// Returns null when neither a path nor a binding is available. The caller decides how null
        /// is surfaced (the tools must be non-silent about "no active plan").
        /// </summary>
        private async Task<string?> ResolvePlanTargetAsync(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                try
                {
                    var gitRoot = await _ideService.GetGitRootPathAsync();
                    if (!string.IsNullOrWhiteSpace(gitRoot))
                    {
                        // Repo-root restricted: resolve relative to the git root so the tool cannot
                        // reach arbitrary PC paths.
                        var combined = Path.IsPathRooted(explicitPath)
                            ? explicitPath
                            : Path.Combine(gitRoot, explicitPath);
                        return Path.GetFullPath(combined);
                    }
                }
                catch
                {
                    // Fall through to the binding below.
                }
            }

            var (isBound, boundPath) = GetActivePlanBinding();
            if (isBound && !string.IsNullOrWhiteSpace(boundPath))
                return Path.GetFullPath(boundPath);

            return null;
        }

        /// <summary>
        /// read_plan: read the bound (active) plan file and return its full text plus which plan it
        /// came from. Non-silent: when no plan is bound and no explicit path is given, returns an
        /// explicit "no active plan" signal.
        /// </summary>
        private async Task<ToolResult> ReadPlanInternalAsync(IDictionary<string, object> args, CancellationToken ct)
        {
            try
            {
                var explicitPath = GetArgString(args, "path");
                var target = await ResolvePlanTargetAsync(explicitPath);

                if (target == null)
                {
                    return CreateErrorResult(
                        "read_plan",
                        "No active plan bound. Open a plan file (~/.continueVS/plans/...) as the active " +
                        "document and send again to bind it, or pass an explicit repo-root-relative 'path'.");
                }

                if (!File.Exists(target))
                {
                    return CreateErrorResult("read_plan", $"Plan file not found: {target}");
                }

                var contents = await ReadFileAsync(target);
                return new ToolResult
                {
                    ToolName = "read_plan",
                    Output = $"=== Plan: {target} ===\n\n{contents}",
                    IsSuccess = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "path", target }
                    }
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("read_plan", ex.Message);
            }
        }

        /// <summary>
        /// update_plan: apply an exact find/replace to the bound (active) plan file. Repo-root
        /// restricted. Returns the match count so a bad 'find' (count 0) is visible instead of
        /// silently doing nothing. Never silent on "no active plan".
        /// </summary>
        private async Task<ToolResult> UpdatePlanInternalAsync(IDictionary<string, object> args, CancellationToken ct)
        {
            try
            {
                var find = GetArgString(args, "find");
                var replace = GetArgString(args, "replace");
                if (string.IsNullOrEmpty(find))
                {
                    return CreateErrorResult("update_plan", "find cannot be null or empty");
                }

                var explicitPath = GetArgString(args, "path");
                var target = await ResolvePlanTargetAsync(explicitPath);

                if (target == null)
                {
                    return CreateErrorResult(
                        "update_plan",
                        "No active plan bound. Open a plan file (~/.continueVS/plans/...) as the active " +
                        "document and send again to bind it, or pass an explicit repo-root-relative 'path'.");
                }

                if (!File.Exists(target))
                {
                    return CreateErrorResult("update_plan", $"Plan file not found: {target}");
                }

                var contents = await ReadFileAsync(target);
                // Count exact occurrences before mutating.
                var count = CountOccurrences(contents, find);
                var updated = contents.Replace(find, replace);

                await WriteFileAsync(target, updated);

                var result = count == 0
                    ? $"No matches of the given text found; plan is unchanged ({target})." +
                      "Re-read the plan and adjust your 'find' to match exactly."
                    : $"Replaced {count} occurrence(s) in plan ({target}).";

                return new ToolResult
                {
                    ToolName = "update_plan",
                    Output = result,
                    IsSuccess = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "path", target },
                        { "matches", count.ToString() }
                    }
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("update_plan", ex.Message);
            }
        }

        /// <summary>
        /// Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>
        /// using an ordinal (case-sensitive, exact) comparison. Used to surface no-op find/replace.
        /// </summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
                return 0;

            int count = 0, index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
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
        /// Internal wrapper for read currently open file as ToolResult.
        /// </summary>
        private async Task<ToolResult> ReadCurrentlyOpenFileInternalAsync()
        {
            try
            {
                var contents = await _ideService.ReadCurrentlyOpenFileAsync();
                if (string.IsNullOrEmpty(contents))
                {
                    return new ToolResult
                    {
                        ToolName = "read_currently_open_file",
                        Output = "No file is currently open in the IDE",
                        IsSuccess = false
                    };
                }
                return new ToolResult
                {
                    ToolName = "read_currently_open_file",
                    Output = contents,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("read_currently_open_file", ex.Message);
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
        /// Internal wrapper for create new file as ToolResult.
        /// </summary>
        private async Task<ToolResult> CreateNewFileInternalAsync(string filepath, string contents)
        {
            try
            {
                await _ideService.CreateFileAsync(filepath, contents);
                return new ToolResult
                {
                    ToolName = "create_new_file",
                    Output = $"File created: {filepath}",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("create_new_file", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for create folder as ToolResult.
        /// </summary>
        private async Task<ToolResult> CreateFolderInternalAsync(string folderpath)
        {
            try
            {
                await _ideService.CreateFolderAsync(folderpath);
                return new ToolResult
                {
                    ToolName = "create_folder",
                    Output = $"Folder created: {folderpath}",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("create_folder", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for edit_file.
        /// Replaces the first occurrence of oldText with newText in the given file.
        /// </summary>
        private async Task<ToolResult> EditFileInternalAsync(string filepath, string oldText, string newText)
        {
            try
            {
                if (string.IsNullOrEmpty(filepath))
                    return CreateErrorResult("edit_file", "filepath cannot be null or empty");

                if (string.IsNullOrEmpty(oldText))
                    return CreateErrorResult("edit_file", "oldText cannot be null or empty");

                if (!File.Exists(filepath))
                    return CreateErrorResult("edit_file", $"File not found: {filepath}");

                var contents = await _ideService.ReadFileAsync(filepath);
                var newContents = ApplyLineEndingSafeEdit(contents, oldText, newText);
                if (newContents == null)
                    return CreateErrorResult("edit_file", "oldText not found in file");

                await _ideService.WriteFileAsync(filepath, newContents);

                return new ToolResult
                {
                    ToolName = "edit_file",
                    Output = $"File edited: {filepath}",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("edit_file", ex.Message);
            }
        }

        /// <summary>
        /// Replaces the first occurrence of oldText with newText in contents while
        /// tolerating line-ending differences. All three strings are normalized to LF
        /// for the search/replace, then the result is converted back to the file's
        /// dominant line ending. Returns null if oldText was not found.
        /// </summary>
        private static string? ApplyLineEndingSafeEdit(string contents, string oldText, string newText)
        {
            // Detect the dominant line ending used by the file so we can restore it.
            string newline = DetectDominantNewline(contents);

            string normalizedContents = NormalizeLineEndings(contents);
            string normalizedOld = NormalizeLineEndings(oldText);
            string normalizedNew = NormalizeLineEndings(newText ?? string.Empty);

            int index = normalizedContents.IndexOf(normalizedOld, StringComparison.Ordinal);
            if (index < 0)
                return null;

            string result = normalizedContents.Substring(0, index)
                + normalizedNew
                + normalizedContents.Substring(index + normalizedOld.Length);

            // Restore the file's original line ending style.
            return RestoreLineEndings(result, newline);
        }

        /// <summary>
        /// Detects the dominant newline sequence in a block of text.
        /// Prefers CRLF, then lone CR, and defaults to LF when there are no newlines.
        /// </summary>
        private static string DetectDominantNewline(string text)
        {
            int crlf = 0, cr = 0, lf = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        crlf++;
                        i++;
                    }
                    else
                    {
                        cr++;
                    }
                }
                else if (c == '\n')
                {
                    lf++;
                }
            }

            if (crlf > 0 && crlf >= lf && crlf >= cr)
                return "\r\n";
            if (cr > 0 && cr > lf && cr > crlf)
                return "\r";
            return "\n";
        }

        /// <summary>
        /// Normalizes all line endings (CRLF, lone CR) to LF (\n).
        /// </summary>
        private static string NormalizeLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        /// <summary>
        /// Converts a normalized (LF-only) string back to the given newline sequence.
        /// If newline is LF, the text is returned unchanged.
        /// </summary>
        private static string RestoreLineEndings(string text, string newline)
        {
            if (newline == "\n")
                return text;
            if (newline == "\r")
                return text.Replace("\n", "\r");
            return text.Replace("\n", "\r\n");
        }

        /// <summary>
        /// Internal wrapper for list directory (ls) as ToolResult.
        /// </summary>
        private async Task<ToolResult> ListDirectoryInternalAsync(string dirPath, bool recursive)
        {
            try
            {
                var items = await _ideService.ListDirectoryAsync(dirPath, recursive);
                var itemList = items?.ToList() ?? new List<string>();
                return new ToolResult
                {
                    ToolName = "ls",
                    Output = string.Join("\n", itemList),
                    RawOutput = itemList,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("ls", ex.Message);
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
        /// Internal wrapper for file_glob_search.
        /// Searches for files matching a glob pattern in the workspace.
        /// </summary>
        private Task<ToolResult> FileGlobSearchInternalAsync(string glob, int maxResults)
        {
            try
            {
                if (string.IsNullOrEmpty(glob))
                    return Task.FromResult(CreateErrorResult("file_glob_search", "glob cannot be null or empty"));

                var workspaceFiles = _ideService.GetWorkspaceFiles(glob);
                var matchedFiles = workspaceFiles.Take(maxResults).ToList();

                return Task.FromResult(new ToolResult
                {
                    ToolName = "file_glob_search",
                    Output = $"Found {matchedFiles.Count} files matching pattern '{glob}'",
                    RawOutput = matchedFiles,
                    IsSuccess = true
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(CreateErrorResult("file_glob_search", ex.Message));
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
        /// Binary and oversized files are filtered out BEFORE reading so they never
        /// pollute the tool result with junk content.
        /// </summary>
        private async Task<ToolResult> GrepSearchInternalAsync(string directory, string pattern, string filePattern)
        {
            try
            {
                if (string.IsNullOrEmpty(pattern))
                    return CreateErrorResult("grep_search", "pattern cannot be null or empty");

                var workspaceFiles = _ideService
                    .GetWorkspaceFiles(filePattern ?? "*.*")
                    .Where(f => IsGrepCandidateFile(f))
                    .ToList();

                var matches = new List<string>();
                var regex = new System.Text.RegularExpressions.Regex(
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                const int MaxMatches = 50;
                foreach (var filePath in workspaceFiles)
                {
                    if (matches.Count >= MaxMatches)
                        break;

                    try
                    {
                        // Re-verify the first bytes are text before reading the whole file.
                        if (!IsTextContent(filePath))
                            continue;

                        var content = await _ideService.ReadFileAsync(filePath);
                        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                        for (int i = 0; i < lines.Length && matches.Count < MaxMatches; i++)
                        {
                            if (regex.IsMatch(lines[i]))
                            {
                                matches.Add($"{filePath}:{i + 1}: {lines[i].Trim()}");
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
                        { "matchCount", matches.Count.ToString() },
                        { "filesScanned", workspaceFiles.Count.ToString() }
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
        /// Well-known binary / non-source extensions that must never be grep'd.
        /// </summary>
        private static readonly HashSet<string> BinaryFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".tiff", ".webp", ".heic", ".avif", ".svgz",
            // Fonts
            ".ttf", ".otf", ".woff", ".woff2", ".eot",
            // Archives
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".zst",
            // Compiled / binaries
            ".dll", ".exe", ".so", ".dylib", ".a", ".lib", ".obj", ".o", ".pdb", ".class",
            ".jar", ".war", ".pyc", ".pyo",
            // Media
            ".mp3", ".mp4", ".avi", ".mov", ".mkv", ".wav", ".flac", ".ogg", ".webm",
            // Documents / office
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".dwg", ".eps",
            // Generated / lockfiles / misc
            ".resx", ".resources", ".snap", ".cache", ".lock", ".wasm"
        };

        /// <summary>
        /// Whether a file should be considered for grep: known-text extension, not oversized.
        /// Returns false for binaries and huge files so they never pollute the tool context.
        /// </summary>
        private static bool IsGrepCandidateFile(string filePath)
        {
            try
            {
                if (BinaryFileExtensions.Contains(Path.GetExtension(filePath)))
                    return false;

                var fi = new FileInfo(filePath);
                if (fi.Length > 2_000_000)   // skip very large files (e.g. generated/bundled)
                    return false;

                return true;
            }
            catch
            {
                return false; // if we can't stat it, skip it
            }
        }

        /// <summary>
        /// Sniffs the leading bytes for binary content: NUL bytes or a high ratio of
        /// control characters indicate a non-text file that grep should ignore.
        /// </summary>
        private static bool IsTextContent(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                var buffer = new byte[4096];
                int read = fs.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    return false;

                int suspicious = 0;
                for (int i = 0; i < read; i++)
                {
                    byte b = buffer[i];
                    if (b == 0)
                        return false;                                   // NUL -> binary
                    if (b < 9 || (b > 13 && b < 32))                    // non-whitespace control char
                        suspicious++;
                }
                return suspicious <= read * 0.25;                       // >25% control -> binary
            }
            catch
            {
                return false; // can't read the header -> skip
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
                _logger?.WriteDebug("[gap8_1-toolsvc-init-start] InitializeToolRegistry called");
                _builtInToolRegistry.Clear();

                // Load enabled tools from configuration
                var enabledTools = _configService.GetEnabledTools().ToList();
                _logger?.WriteDebug($"[gap8_1-toolsvc-load-config] Loaded {enabledTools.Count} enabled tools from config");
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
                _logger?.WriteDebug($"[gap8_1-toolsvc-init-end] InitializeToolRegistry complete: {_builtInToolRegistry.Count} built-in tools registered");

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
                    _logger?.WriteError(diagnosticMessage);

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
        /// ONLY adds tools that are NOT already in the registry, and respects config DisabledTools list.
        /// If a tool is explicitly disabled in config, it is NOT added from defaults.
        /// </summary>
        private void EnsureBuiltInToolDefaults()
        {
            _logger?.WriteDebug("[gap8_1-toolsvc-defaults-start] EnsureBuiltInToolDefaults called");
            var defaultTools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            var overrideConfig = _configService.GetToolOverrideConfig();
            int addedCount = 0;

            foreach (var tool in defaultTools)
            {
                if (!_builtInToolRegistry.ContainsKey(tool.Name))
                {
                    // Check if tool is explicitly disabled in override config
                    if (overrideConfig?.DisabledTools.Contains(tool.Name) ?? false)
                    {
                        _logger?.WriteDebug($"[gap8_1-toolsvc-defaults-skip] Tool '{tool.Name}' disabled in config, skipping default");
                        // Don't add it - respect the disabled setting
                        continue;
                    }

                    _builtInToolRegistry[tool.Name] = tool;
                    addedCount++;
                    _logger?.WriteDebug($"[gap8_1-toolsvc-defaults-added] Tool '{tool.Name}' added from defaults");
                }
            }
            _logger?.WriteDebug($"[gap8_1-toolsvc-defaults-end] EnsureBuiltInToolDefaults: {defaultTools.Count} defaults checked, {addedCount} added");
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
        /// Internal wrapper for run_terminal_command.
        /// </summary>
        private async Task<ToolResult> RunTerminalCommandInternalAsync(string command, bool waitForCompletion)
        {
            try
            {
                if (string.IsNullOrEmpty(command))
                    return CreateErrorResult("run_terminal_command", "command cannot be null or empty");

                var output = await _ideService.RunCommandAsync(command);
                return new ToolResult
                {
                    ToolName = "run_terminal_command",
                    Output = output,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("run_terminal_command", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for view_diff.
        /// </summary>
        private async Task<ToolResult> ViewDiffInternalAsync()
        {
            try
            {
                var diff = await _ideService.GetDiffAsync();
                return new ToolResult
                {
                    ToolName = "view_diff",
                    Output = diff ?? "No changes detected",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("view_diff", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for create_rule_block.
        /// </summary>
        private async Task<ToolResult> CreateRuleBlockInternalAsync(string name, string rule)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return CreateErrorResult("create_rule_block", "name cannot be null or empty");

                if (string.IsNullOrEmpty(rule))
                    return CreateErrorResult("create_rule_block", "rule cannot be null or empty");

                // Stub implementation - would save to a rules registry
                return new ToolResult
                {
                    ToolName = "create_rule_block",
                    Output = $"Rule '{name}' created successfully",
                    IsSuccess = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "ruleName", name },
                        { "ruleLength", rule.Length.ToString() }
                    }
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("create_rule_block", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for run_pytest.
        /// </summary>
        private async Task<ToolResult> RunPytestInternalAsync(string testPath)
        {
            try
            {
                // Stub - pytest not integrated with .NET projects
                return new ToolResult
                {
                    ToolName = "run_pytest",
                    Output = "pytest is not available in this .NET project",
                    IsSuccess = false
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("run_pytest", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for get_problems.
        /// </summary>
        private async Task<ToolResult> GetProblemsInternalAsync()
        {
            try
            {
                var problems = await _ideService.GetProblemsAsync();
                return new ToolResult
                {
                    ToolName = "get_problems",
                    Output = problems ?? "No problems detected",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("get_problems", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for view_file.
        /// </summary>
        private async Task<ToolResult> ViewFileInternalAsync(string filepath)
        {
            try
            {
                if (string.IsNullOrEmpty(filepath))
                    return CreateErrorResult("view_file", "filepath cannot be null or empty");

                var contents = await _ideService.ReadFileAsync(filepath);
                var lines = contents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var numbered = string.Join("\n", lines.Select((line, idx) => $"{idx + 1}: {line}"));

                return new ToolResult
                {
                    ToolName = "view_file",
                    Output = numbered,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("view_file", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for open_file.
        /// </summary>
        private async Task<ToolResult> OpenFileInternalAsync(string filepath)
        {
            try
            {
                if (string.IsNullOrEmpty(filepath))
                    return CreateErrorResult("open_file", "filepath cannot be null or empty");

                await _ideService.OpenFileInEditorAsync(filepath);
                return new ToolResult
                {
                    ToolName = "open_file",
                    Output = $"File opened: {filepath}",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("open_file", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for git_status.
        /// </summary>
        private async Task<ToolResult> GitStatusInternalAsync()
        {
            try
            {
                var status = await _ideService.GetGitStatusAsync();
                return new ToolResult
                {
                    ToolName = "git_status",
                    Output = status ?? "No git repository",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("git_status", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for git_diff.
        /// </summary>
        private async Task<ToolResult> GitDiffInternalAsync(string filePath)
        {
            try
            {
                var diff = await _ideService.GetGitDiffAsync(filePath);
                return new ToolResult
                {
                    ToolName = "git_diff",
                    Output = diff ?? "No changes or file not tracked",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("git_diff", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for git_log.
        /// </summary>
        private async Task<ToolResult> GitLogInternalAsync(int maxCommits)
        {
            try
            {
                var log = await _ideService.GetGitLogAsync(maxCommits);
                return new ToolResult
                {
                    ToolName = "git_log",
                    Output = log ?? "No commits found",
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("git_log", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for git_commit.
        /// </summary>
        private async Task<ToolResult> GitCommitInternalAsync(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return CreateErrorResult("git_commit", "message cannot be null or empty");

                var commitHash = await _ideService.CreateGitCommitAsync(message);
                return new ToolResult
                {
                    ToolName = "git_commit",
                    Output = $"Commit created: {commitHash}",
                    IsSuccess = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "commitHash", commitHash }
                    }
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("git_commit", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for create_snippet.
        /// </summary>
        private async Task<ToolResult> CreateSnippetInternalAsync(string name, string code)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return CreateErrorResult("create_snippet", "name cannot be null or empty");

                if (string.IsNullOrEmpty(code))
                    return CreateErrorResult("create_snippet", "code cannot be null or empty");

                // Stub implementation - would save to a snippets registry
                return new ToolResult
                {
                    ToolName = "create_snippet",
                    Output = $"Snippet '{name}' created successfully",
                    IsSuccess = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "snippetName", name },
                        { "codeLength", code.Length.ToString() }
                    }
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("create_snippet", ex.Message);
            }
        }

        /// <summary>
        /// Internal wrapper for write_plan.
        /// Saves the plan to the plans directory via IPlanOutputService and reveals it in the IDE.
        /// </summary>
        private async Task<ToolResult> WritePlanInternalAsync(IDictionary<string, object> args, CancellationToken ct)
        {
            try
            {
                var plan = GetArgString(args, "plan");
                if (string.IsNullOrEmpty(plan))
                {
                    return CreateErrorResult("write_plan", "plan cannot be null or empty");
                }

                var title = GetArgString(args, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "plan";
                }

                if (_planOutputService == null)
                {
                    return CreateErrorResult("write_plan", "Plan output service not available");
                }

                var path = await _planOutputService.SavePlanAsync(title, plan, ct);

                await _ideService.OpenFileInEditorAsync(path);

                return new ToolResult
                {
                    ToolName = "write_plan",
                    Output = $"Plan saved to {path}",
                    IsSuccess = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "path", path }
                    }
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("write_plan", ex.Message);
            }
        }
        /// <summary>
        /// Internal implementation of the ask_user tool.
        /// Surfaces a question to the user (via the interactive prompt service) and returns their answer
        /// as the tool result so it can be fed back to the LLM. Supports multiple-choice (answers list)
        /// and open-ended (no answers) questions.
        /// </summary>
        private async Task<ToolResult> InvokeAskUserAsync(IDictionary<string, object> args, CancellationToken ct)
        {
            try
            {
                var question = GetArgString(args, "question");
                if (string.IsNullOrWhiteSpace(question))
                {
                    return CreateErrorResult("ask_user", "question cannot be null or empty");
                }

                var answers = GetArgArray<string>(args, "answers");

                if (_interactivePromptService == null)
                {
                    return CreateErrorResult("ask_user", "Interactive prompt service not available");
                }

                // Build the question prompt. Multiple-choice answers map to a Selection question whose
                // auto-answer hint carries the options; the interactive handler surfaces them and lets
                // the user pick or type prose. Open-ended questions fall back to a plain clarity question.
                var prompt = new LLMQuestionPrompt
                {
                    QuestionText = question,
                    Context = answers != null && answers.Count > 0
                        ? "Please choose one of the following options or type your own answer: " + string.Join(", ", answers)
                        : null,
                    QuestionType = answers != null && answers.Count > 0
                        ? LLMQuestionType.Selection
                        : LLMQuestionType.Clarification,
                    AutoAnswerHint = answers != null && answers.Count > 0
                        ? string.Join(", ", answers)
                        : null
                };

                var answer = await _interactivePromptService.PromptOnLLMQuestionAsync(prompt, isInteractiveMode: true);
                var sanitized = SanitizeUserAnswer(answer);

                return new ToolResult
                {
                    ToolName = "ask_user",
                    Output = sanitized,
                    IsSuccess = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "question", question },
                        { "providedAnswers", answers != null ? string.Join(", ", answers) : "" }
                    }
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("ask_user", ex.Message);
            }
        }

        /// <summary>
        /// Sanitizes user-supplied free-text before it is returned to the LLM as a tool result.
        /// Trims whitespace and strips control characters.
        /// </summary>
        private static string SanitizeUserAnswer(string? answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "[no answer]";

            var sb = new System.Text.StringBuilder(answer!.Length);
            foreach (var c in answer)
            {
                if (!char.IsControl(c))
                    sb.Append(c);
            }
            var trimmed = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "[no answer]" : trimmed;
        }

        /// <summary>
        /// Internal implementation of the retire_from_context tool (context pruning &amp; error
        /// supersession). Toggles the status of an earlier message between "active" and "retired":
        /// retiring reuses the shared IsDeleted tombstone (via the context retirement service) so
        /// the item is excluded from future context, and an auditable record is appended to the
        /// idempotent reference file. Calling it again on the same message toggles back to active.
        ///
        /// Returns an EMPTY, successful result (terminus): the tool adds no content for the LLM to
        /// reason about. When the harness sees an empty result with no other pending tool call, the
        /// turn ends.
        /// </summary>
        private async Task<ToolResult> InvokeRetireFromContextAsync(IDictionary<string, object> args, CancellationToken ct)
        {
            try
            {
                var messageId = GetArgString(args, "message_id");
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    return CreateErrorResult("retire_from_context", "message_id cannot be null or empty");
                }

                var reason = GetArgString(args, "reason");
                if (string.IsNullOrWhiteSpace(reason))
                {
                    return CreateErrorResult("retire_from_context", "reason cannot be null or empty");
                }

                var replacedById = GetArgString(args, "replaced_by_id");
                if (string.IsNullOrWhiteSpace(replacedById))
                {
                    replacedById = null;
                }

                if (_contextRetirementService == null)
                {
                    return CreateErrorResult("retire_from_context", "Context retirement service not available");
                }

                if (_sessionService == null)
                {
                    return CreateErrorResult("retire_from_context", "Session service not available");
                }

                // Resolve the message so we can validate existence and capture verbatim bytes.
                ChatMessage? target = null;
                try
                {
                    var session = _sessionService.GetCurrentSession();
                    target = session?.Messages.FirstOrDefault(m => string.Equals(m.Id, messageId, StringComparison.Ordinal));
                }
                catch
                {
                    // Session access may fail in unit-test contexts; the service will validate below.
                }

                if (target == null)
                {
                    return CreateErrorResult("retire_from_context", $"Message with ID '{messageId}' not found in current session");
                }

                var sessionId = _sessionService.GetCurrentSession()?.Id ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return CreateErrorResult("retire_from_context", "No active session");
                }

                bool isRetired = await _contextRetirementService.IsRetiredAsync(sessionId, messageId);

                if (isRetired)
                {
                    // Toggle back to active — reason should describe the state being moved to.
                    await _contextRetirementService.UnretireAsync(sessionId, messageId, reason);
                    _logger?.WriteDebug($"[sv-retire] retire_from_context: unretired message {messageId} ({reason})");
                }
                else
                {
                    // Capture verbatim JSON so the original bytes survive in the reference file
                    // regardless of later mutable changes to the live message.
                    var verbatimJson = JsonConvert.SerializeObject(target, Formatting.None);
                    await _contextRetirementService.RetireAsync(sessionId, messageId, reason, replacedById, verbatimJson);
                    _logger?.WriteDebug($"[sv-retire] retire_from_context: retired message {messageId} ({reason}, replacedBy={replacedById ?? "none"})");
                }

                // Empty successful result => terminus. The harness ends the turn when there is no
                // other pending tool call and no output to feed back into context.
                return new ToolResult
                {
                    ToolName = "retire_from_context",
                    Output = string.Empty,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResult("retire_from_context", ex.Message);
            }
        }

        // -----------------------------------------------------------------------
        // gap92_3 — Tier-2 debug evaluate/mutate tool handlers.
        // These return ToolResults that surface the DebugInspectionResult state-echo plus
        // the benign reason, so the LLM stays grounded in the debugger's live truth.
        // When the debugger service is absent (unit-test construction without it) they
        // return an explicit unavailable error.
        // -----------------------------------------------------------------------

        private async Task<ToolResult> EvaluateInternalAsync(IDictionary<string, object> args)
        {
            try
            {
                if (_debuggerService == null) return CreateDebuggerUnavailableResult("debug_evaluate");
                var state = EmptyDebugState();
                var result = await _debuggerService.EvaluateAsync(
                    state,
                    GetArgInt(args, "threadId"),
                    GetArgInt(args, "frameIndex"),
                    GetArgString(args, "expression"));
                if (!result.Ok) return CreateDebugRejectedResult("debug_evaluate", result.State, result.Reason);
                return new ToolResult
                {
                    ToolName = "debug_evaluate",
                    Output = $"{result.Data?.Type} {result.Data?.Name} = {result.Data?.Value}",
                    Metadata = new Dictionary<string, string> { { "ok", "true" }, { "mode", result.State.Mode } },
                    IsSuccess = true
                };
            }
            catch (Exception ex) { return CreateErrorResult("debug_evaluate", ex.Message); }
        }

        private async Task<ToolResult> SetValueInternalAsync(IDictionary<string, object> args)
        {
            try
            {
                if (_debuggerService == null) return CreateDebuggerUnavailableResult("debug_set_value");
                var state = EmptyDebugState();
                var result = await _debuggerService.SetValueAsync(
                    state,
                    GetArgInt(args, "threadId"),
                    GetArgInt(args, "frameIndex"),
                    GetArgString(args, "name"),
                    GetArgString(args, "value"));
                if (!result.Ok) return CreateDebugRejectedResult("debug_set_value", result.State, result.Reason);
                return new ToolResult
                {
                    ToolName = "debug_set_value",
                    Output = $"Variable written ({GetArgString(args, "name")})",
                    Metadata = new Dictionary<string, string> { { "ok", "true" }, { "mode", result.State.Mode } },
                    IsSuccess = true
                };
            }
            catch (Exception ex) { return CreateErrorResult("debug_set_value", ex.Message); }
        }

        private async Task<ToolResult> MemoryReadInternalAsync(IDictionary<string, object> args)
        {
            try
            {
                if (_debuggerService == null) return CreateDebuggerUnavailableResult("debug_memory_read");
                var state = EmptyDebugState();
                var result = await _debuggerService.MemoryReadAsync(state, GetArgString(args, "address"), GetArgInt(args, "length"));
                if (!result.Ok) return CreateDebugRejectedResult("debug_memory_read", result.State, result.Reason);
                return new ToolResult
                {
                    ToolName = "debug_memory_read",
                    Output = result.Data ?? string.Empty,
                    IsSuccess = true
                };
            }
            catch (Exception ex) { return CreateErrorResult("debug_memory_read", ex.Message); }
        }

        private async Task<ToolResult> MemoryWriteInternalAsync(IDictionary<string, object> args)
        {
            try
            {
                if (_debuggerService == null) return CreateDebuggerUnavailableResult("debug_memory_write");
                var state = EmptyDebugState();
                var result = await _debuggerService.MemoryWriteAsync(state, GetArgString(args, "address"), GetArgString(args, "bytes"));
                if (!result.Ok) return CreateDebugRejectedResult("debug_memory_write", result.State, result.Reason);
                return new ToolResult
                {
                    ToolName = "debug_memory_write",
                    Output = "Memory written",
                    IsSuccess = true
                };
            }
            catch (Exception ex) { return CreateErrorResult("debug_memory_write", ex.Message); }
        }

        private async Task<ToolResult> RunToCursorInternalAsync(IDictionary<string, object> args)
        {
            try
            {
                if (_debuggerService == null) return CreateDebuggerUnavailableResult("debug_run_to_cursor");
                var state = EmptyDebugState();
                var result = await _debuggerService.RunToCursorAsync(state);
                if (!result.Ok) return CreateDebugRejectedResult("debug_run_to_cursor", result.State, result.Reason);
                return new ToolResult
                {
                    ToolName = "debug_run_to_cursor",
                    Output = $"Run-to-cursor issued (mode now '{result.State.Mode}')",
                    Metadata = new Dictionary<string, string> { { "mode", result.State.Mode } },
                    IsSuccess = true
                };
            }
            catch (Exception ex) { return CreateErrorResult("debug_run_to_cursor", ex.Message); }
        }

        private async Task<ToolResult> SetThreadStateInternalAsync(IDictionary<string, object> args)
        {
            try
            {
                if (_debuggerService == null) return CreateDebuggerUnavailableResult("debug_thread_set_state");
                var state = EmptyDebugState();
                var action = GetArgString(args, "action");
                var threadId = GetArgInt(args, "threadId");

                DebugInspectionResult<bool> result;
                if (string.Equals(action, "thaw", StringComparison.OrdinalIgnoreCase))
                    result = await _debuggerService.ThawThreadAsync(state, threadId);
                else
                    result = await _debuggerService.FreezeThreadAsync(state, threadId);

                if (!result.Ok) return CreateDebugRejectedResult("debug_thread_set_state", result.State, result.Reason);
                return new ToolResult
                {
                    ToolName = "debug_thread_set_state",
                    Output = $"Thread {threadId} {action}",
                    Metadata = new Dictionary<string, string> { { "ok", "true" }, { "mode", result.State.Mode } },
                    IsSuccess = true
                };
            }
            catch (Exception ex) { return CreateErrorResult("debug_thread_set_state", ex.Message); }
        }

        private static DebugSessionState EmptyDebugState()
            => new DebugSessionState { Mode = "none" };

        private ToolResult CreateDebuggerUnavailableResult(string toolName)
            => CreateErrorResult(toolName, "Debugger service not available");

        private static ToolResult CreateDebugRejectedResult(string toolName, DebugSessionState state, string? reason)
        {
            return new ToolResult
            {
                ToolName = toolName,
                Output = $"Rejected ({reason ?? "rejected"}); mode='{state.Mode}'",
                Metadata = new Dictionary<string, string>
                {
                    { "ok", "false" },
                    { "mode", state.Mode },
                    { "reason", reason ?? string.Empty }
                },
                IsSuccess = false
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

        /// <summary>
        /// Gets an array argument from the arguments dictionary, or null if absent.
        /// Handles List&lt;T&gt;, T[], IEnumerable&lt;T&gt;, and object arrays containing T.
        /// </summary>
        private List<T>? GetArgArray<T>(IDictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var value))
                return null;

            if (value == null)
                return null;

            if (value is List<T> listVal)
                return listVal;

            if (value is T[] arrayVal)
                return arrayVal.ToList();

            if (value is IEnumerable<T> enumerable)
                return enumerable.ToList();

            // Some JSON deserializers materialize arrays as object[].
            if (value is IEnumerable<object> objectEnum)
            {
                var cast = objectEnum.Select(o => (T)Convert.ChangeType(o, typeof(T))).ToList();
                return cast;
            }

            return null;
        }
    }
}
