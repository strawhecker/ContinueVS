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

            // Get the mode configuration for policy-based checks
            var modeConfig = _modeConfigRegistry.GetConfig(currentMode);

            // Validate command is authorized for this mode (checks read-only policies, etc.)
            ValidateCommandForMode(commandName, currentMode, modeConfig);

            var sw = Stopwatch.StartNew();
            try
            {
                // Log the dispatch
                _logger?.WriteDebug($"[gap58-dispatch] Routing {commandName} with args={commandArguments?.Count ?? 0}");

                // Invoke the tool via IToolService
                var result = await _toolService.InvokeAsync(
                    commandName,
                    commandArguments ?? new Dictionary<string, object>(),
                    ct);

                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;

                _logger?.WriteDebug(
                    $"[gap58-dispatch] ✓ {commandName} completed in {sw.ElapsedMilliseconds}ms, success={result.IsSuccess}");

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger?.WriteDebug(
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
        /// Plan/Ask/Reason modes: only tools available in those modes are allowed (read-only tools).
        /// Agent/Debug modes: all tools are allowed.
        /// </summary>
        /// <param name="commandName">The command name to validate.</param>
        /// <param name="currentMode">The current chat mode.</param>
        /// <param name="modeConfig">The configuration for the current mode.</param>
        /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
        private void ValidateCommandForMode(string commandName, ChatMode currentMode, ModeConfig modeConfig)
        {
            switch (currentMode)
            {
                case ChatMode.Plan:
                case ChatMode.Ask:
                case ChatMode.Reason:
                    // Plan, Ask, and Reason modes: only read-only tools (from mode-filtered set)
                    var availableTools = _toolService.GetAvailableTools(currentMode);
                    var availableToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var tool in availableTools)
                    {
                        availableToolNames.Add(tool.Name);
                    }

                    if (!availableToolNames.Contains(commandName))
                    {
                        var errorMsg = $"Command '{commandName}' is not allowed in {currentMode} mode.";
                        _logger?.WriteDebug($"[gap58-dispatch] ✗ Validation failed: {errorMsg}");
                        throw new InvalidOperationException(errorMsg);
                    }
                    break;

                case ChatMode.Agent:
                case ChatMode.Debug:
                    // Agent and Debug modes: all tools allowed (subject to tool system policy)
                    _logger?.WriteDebug($"[gap58-dispatch] ✓ Command '{commandName}' validated for {currentMode} mode");
                    break;

                default:
                    var unknownModeMsg = $"Unknown chat mode: {currentMode}";
                    _logger?.WriteDebug($"[gap58-dispatch] ✗ Validation failed: {unknownModeMsg}");
                    throw new InvalidOperationException(unknownModeMsg);
            }
        }
    }
}
