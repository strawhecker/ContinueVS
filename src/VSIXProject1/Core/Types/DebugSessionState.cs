namespace ContinueVS.Core.Types
{
    /// <summary>
    /// A lightweight, echo-style snapshot of the current debugger session state.
    /// Returned by every <see cref="Services.Interfaces.IDebuggerService"/> operation so the
    /// LLM's next decision is grounded in the live debugger truth. This is an additive DTO —
    /// it does not mutate the stable <see cref="RuntimeState"/> core shape.
    /// </summary>
    public sealed class DebugSessionState
    {
        /// <summary>
        /// Human-readable debug mode: <c>"run"</c>, <c>"break"</c>, or <c>"none"</c>.
        /// </summary>
        public string Mode { get; set; } = "none";

        /// <summary>
        /// The id of the currently selected thread, when known and applicable.
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// The name (or stack-address marker) of the currently selected frame, when applicable.
        /// </summary>
        public string? Frame { get; set; }

        /// <summary>
        /// Why execution is currently paused (e.g. <c>"breakpoint"</c>, <c>"step"</c>), when known.
        /// </summary>
        public string? BreakReason { get; set; }

        /// <summary>
        /// True when the debugger is actively running or paused in a live session.
        /// </summary>
        public bool IsDebuggerActive { get; set; }

        /// <summary>
        /// True when a program is currently loaded/being debugged (<c>CurrentProgram != null</c>).
        /// </summary>
        public bool IsActiveProgram { get; set; }

        /// <summary>
        /// True when the current mode is "break" (paused at a location) as opposed to "run" or "none".
        /// </summary>
        public bool IsBreakMode => string.Equals(Mode, "break", System.StringComparison.OrdinalIgnoreCase);
    }
}
