using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IAgentCommandDispatcher.
    /// Routes incoming agent commands to tool handlers with mode-based policy validation.
    /// Logs all dispatches to FileLogger for audit trail.
    /// </summary>
    public partial class AgentCommandDispatcher : IAgentCommandDispatcher
    {
        private readonly IToolService _toolService;
        private readonly ILlmService _llmService;
        private readonly IModeConfigRegistry _modeConfigRegistry;
        private readonly IBridgeLogger _logger;

        /// <summary>
        /// Initializes a new instance of AgentCommandDispatcher.
        /// </summary>
        /// <param name="toolService">Service for tool execution and management.</param>
        /// <param name="llmService">Service for LLM interactions.</param>
        /// <param name="modeConfigRegistry">Registry providing mode policy configuration.</param>
        /// <param name="logger">Logger for audit trail and diagnostics.</param>
        public AgentCommandDispatcher(
            IToolService toolService,
            ILlmService llmService,
            IModeConfigRegistry modeConfigRegistry,
            IBridgeLogger logger)
        {
            _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _modeConfigRegistry = modeConfigRegistry ?? throw new ArgumentNullException(nameof(modeConfigRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ToolResult> DispatchAgentCommandAsync(
            string commandName,
            IDictionary<string, object> commandArguments,
            ChatMode currentMode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("Command name cannot be null or empty.", nameof(commandName));

            // Check if the current mode allows tool looping
            var modeConfig = _modeConfigRegistry.GetConfig(currentMode);
            if (!modeConfig.AllowToolLoop)
            {
                var errorMsg = $"Tool looping is not allowed in {currentMode} mode.";
                _ = _logger.WriteDebugAsync($"[gap58-dispatch] ✗ Dispatch blocked: {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }

            // Validate command is authorized for this mode
            ValidateCommandForMode(commandName, currentMode, modeConfig);

            var sw = Stopwatch.StartNew();
            try
            {
                // Log the dispatch
                _ = _logger.WriteDebugAsync($"[gap58-dispatch] Routing {commandName} with args={commandArguments?.Count ?? 0}");

                // Invoke the tool via IToolService
                var result = await _toolService.InvokeAsync(
                    commandName,
                    commandArguments ?? new Dictionary<string, object>(),
                    ct);

                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;

                _ = _logger.WriteDebugAsync(
                    $"[gap58-dispatch] ✓ {commandName} completed in {sw.ElapsedMilliseconds}ms, success={result.IsSuccess}");

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _ = _logger.WriteDebugAsync(
                    $"[gap58-dispatch] ✗ {commandName} failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");

                return new ToolResult
                {
                    ToolName = commandName,
                    IsSuccess = false,
                    Output = $"Tool execution failed: {ex.Message}",
                    Timestamp = DateTime.UtcNow,
                    DurationMs = sw.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Validates that the command is authorized for the current mode.
        /// Ask mode: only read-only tools are allowed (read_file, list_files, search_code).
        /// Agent/Debug modes: all tools are allowed.
        /// Other modes: tool invocation is not supported.
        /// </summary>
        /// <param name="commandName">The command name to validate.</param>
        /// <param name="currentMode">The current chat mode.</param>
        /// <param name="modeConfig">The configuration for the current mode.</param>
        /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
        private void ValidateCommandForMode(string commandName, ChatMode currentMode, ModeConfig modeConfig)
        {
            switch (currentMode)
            {
                case ChatMode.Ask:
                    // Ask mode: read-only tools only
                    var readOnlyTools = new[] { "read_file", "list_files", "search_code" };
                    if (!Array.Exists(readOnlyTools, t => t.Equals(commandName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var errorMsg = $"Command '{commandName}' is not allowed in Ask mode. Allowed: {string.Join(", ", readOnlyTools)}";
                        _ = _logger.WriteDebugAsync($"[gap58-dispatch] ✗ Validation failed: {errorMsg}");
                        throw new InvalidOperationException(errorMsg);
                    }
                    break;

                case ChatMode.Agent:
                case ChatMode.Debug:
                    // Agent and Debug modes: all tools allowed (subject to tool system policy)
                    _ = _logger.WriteDebugAsync($"[gap58-dispatch] ✓ Command '{commandName}' validated for {currentMode} mode");
                    break;

                case ChatMode.Plan:
                case ChatMode.Reason:
                    // Plan and Reason modes: no tool invocation
                    var modeErrorMsg = $"Tool invocation is not supported in {currentMode} mode.";
                    _ = _logger.WriteDebugAsync($"[gap58-dispatch] ✗ Validation failed: {modeErrorMsg}");
                    throw new InvalidOperationException(modeErrorMsg);

                default:
                    var unknownModeMsg = $"Unknown chat mode: {currentMode}";
                    _ = _logger.WriteDebugAsync($"[gap58-dispatch] ✗ Validation failed: {unknownModeMsg}");
                    throw new InvalidOperationException(unknownModeMsg);
            }
        }
    }
}
