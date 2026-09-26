namespace ContinueVS.Core.Types
{
    /// <summary>
    /// A lightweight, immutable-read handle describing a single being-debugged process ("session")
    /// for the debugging lifecycle (gap94). Implemented as a plain net472 sealed class. Instances
    /// are built from the live <c>EnvDTE.Debugger</c> via <c>DebuggerInterop</c> and are the unit of
    /// session selection: <see cref="Services.Interfaces.IDebuggerService.SelectSessionAsync"/>
    /// validates against <see cref="Services.Interfaces.IDebuggerService.GetSessionsAsync"/> before
    /// binding the current session that later <c>debug_*</c> tools act on.
    /// </summary>
    public sealed class DebugSessionInfo
    {
        /// <summary>
        /// Stable, deterministic session handle (e.g. <c>"proc:{ProcessId}"</c>). Owned by us so
        /// session selection and pruning never depend on LLM-provided random ids.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// The OS process id of the being-debugged process.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Best-effort display name of the process.
        /// </summary>
        public string? ProcessName { get; set; }

        /// <summary>
        /// Current debugger mode echo: <c>"run"</c>, <c>"break"</c>, or <c>"none"</c>.
        /// </summary>
        public string Mode { get; set; } = "none";

        /// <summary>
        /// True when the debugger is paused at a breakpoint/statement.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// True when the debugger is running (not paused).
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Best-effort start time of the session. EnvDTE does not expose process start time, so this
        /// is populated only where known and may be null — consumers must treat null as "unknown".
        /// </summary>
        public System.DateTime? StartTime { get; set; }
    }
}
