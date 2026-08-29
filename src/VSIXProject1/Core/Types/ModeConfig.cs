using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Describes the complete policy and identity of a single chat mode.
    /// All mode-specific behavior is expressed here; no mode logic lives in C# orchestration code.
    /// </summary>
    public sealed class ModeConfig
    {
        /// <summary>
        /// The chat mode this configuration describes.
        /// </summary>
        public ChatMode Mode { get; set; }

        /// <summary>
        /// The system prompt sent to the LLM for this mode.
        /// Sourced from <see cref="ContinueVS.Services.Interfaces.ISystemPromptService"/>.
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// The capability identifiers that are enabled for this mode.
        /// Maps to the capability registry table in session-context.md (gap44).
        /// </summary>
        public IReadOnlyList<string> EnabledCapabilities { get; set; } = new List<string>();

        /// <summary>
        /// When true, write-side tools (edit_file, write_file, etc.) may be offered to the LLM.
        /// False for Ask, Plan, and Reason modes.
        /// </summary>
        public bool AllowWriteTools { get; set; }

        /// <summary>
        /// When true, the tool-call loop continues after each LLM response until no pending tool calls remain.
        /// False for Ask, Plan, and Reason modes; true for Agent and Debug modes.
        /// </summary>
        public bool AllowToolLoop { get; set; }

        /// <summary>
        /// When true, debugger state (stack frames, locals, exceptions) is injected into the context block.
        /// True only for Debug mode.
        /// </summary>
        public bool RequiresDebuggerContext { get; set; }

        /// <summary>
        /// When true, the plan produced during this session is exported to a file via IPlanService on completion.
        /// True only for Plan mode.
        /// </summary>
        public bool ExportsPlanFile { get; set; }
    }
}

