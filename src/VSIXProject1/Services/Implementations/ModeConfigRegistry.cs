using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Default implementation of <see cref="IModeConfigRegistry"/>.
    /// Builds the five built-in mode configurations at construction time, sourcing
    /// system prompts from <see cref="ISystemPromptService"/>.
    /// Adding a new mode requires a single entry here — no C# orchestration code needed.
    /// </summary>
    public sealed class ModeConfigRegistry : IModeConfigRegistry
    {
        private static readonly IReadOnlyList<string> SharedCapabilities =
            new List<string> { "read_file", "codeblock_format", "session_history", "token_budget" };

        private readonly Dictionary<ChatMode, ModeConfig> _configs;

        /// <summary>
        /// Initializes the registry with prompts from <paramref name="systemPromptService"/>.
        /// </summary>
        /// <param name="systemPromptService">Source for per-mode system prompts.</param>
        public ModeConfigRegistry(ISystemPromptService systemPromptService)
        {
            if (systemPromptService == null)
                throw new ArgumentNullException(nameof(systemPromptService));

            _configs = new Dictionary<ChatMode, ModeConfig>
            {
                [ChatMode.Ask] = new ModeConfig
                {
                    Mode = ChatMode.Ask,
                    SystemPrompt = systemPromptService.GetPromptForMode("ask"),
                    EnabledCapabilities = new List<string>(SharedCapabilities),
                    AllowWriteTools = false,
                    AllowToolLoop  = false,
                    RequiresDebuggerContext = false,
                    ExportsPlanFile = false
                },
                [ChatMode.Agent] = new ModeConfig
                {
                    Mode = ChatMode.Agent,
                    SystemPrompt = systemPromptService.GetPromptForMode("agent"),
                    EnabledCapabilities = new List<string>(SharedCapabilities)
                    {
                        "write_file",
                        "tool_loop"
                    },
                    AllowWriteTools = true,
                    AllowToolLoop  = true,
                    RequiresDebuggerContext = false,
                    ExportsPlanFile = false
                },
                [ChatMode.Plan] = new ModeConfig
                {
                    Mode = ChatMode.Plan,
                    SystemPrompt = systemPromptService.GetPromptForMode("plan"),
                    EnabledCapabilities = new List<string>(SharedCapabilities)
                    {
                        "plan_export"
                    },
                    AllowWriteTools = false,
                    AllowToolLoop  = false,
                    RequiresDebuggerContext = false,
                    ExportsPlanFile = true
                },
                [ChatMode.Debug] = new ModeConfig
                {
                    Mode = ChatMode.Debug,
                    SystemPrompt = systemPromptService.GetPromptForMode("debug"),
                    EnabledCapabilities = new List<string>(SharedCapabilities)
                    {
                        "write_file",
                        "tool_loop",
                        "debugger_context"
                    },
                    AllowWriteTools = true,
                    AllowToolLoop  = true,
                    RequiresDebuggerContext = true,
                    ExportsPlanFile = false
                },
                [ChatMode.Reason] = new ModeConfig
                {
                    Mode = ChatMode.Reason,
                    SystemPrompt = systemPromptService.GetPromptForMode("reason"),
                    EnabledCapabilities = new List<string>(SharedCapabilities),
                    AllowWriteTools = false,
                    AllowToolLoop  = false,
                    RequiresDebuggerContext = false,
                    ExportsPlanFile = false
                }
            };
        }

        /// <inheritdoc />
        public ModeConfig GetConfig(ChatMode mode)
        {
            if (_configs.TryGetValue(mode, out var config))
                return config;

            throw new ArgumentOutOfRangeException(nameof(mode), mode,
                $"No ModeConfig registered for ChatMode '{mode}'. Add an entry to ModeConfigRegistry.");
        }

        /// <inheritdoc />
        public IReadOnlyList<ModeConfig> GetAllConfigs()
        {
            return new List<ModeConfig>(_configs.Values);
        }
    }
}
