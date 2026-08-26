namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Defines the execution mode for Debug mode sessions.
    /// Controls whether the debugger prompts the user for decisions or auto-answers on their behalf.
    /// </summary>
    public enum DebugExecutionMode
    {
        /// <summary>
        /// Autonomous mode: Auto-answers all prompts without user interaction.
        /// LLM generates hypotheses and applies refinements automatically.
        /// Phase failures trigger auto-recovery attempts up to retry threshold.
        /// </summary>
        Autonomous = 0,

        /// <summary>
        /// Interactive mode: Prompts user before critical decisions.
        /// User approval required for phase continuation, retry threshold responses, and risky changes.
        /// Halts execution while awaiting user input.
        /// </summary>
        Interactive = 1
    }
}
