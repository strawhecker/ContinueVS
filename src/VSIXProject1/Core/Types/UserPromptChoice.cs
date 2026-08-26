namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the user's response to an interactive prompt in Debug mode.
    /// Determines whether to retry, skip, or cancel the current operation.
    /// </summary>
    public enum UserPromptChoice
    {
        /// <summary>
        /// Retry the failed operation (e.g., retry a failed phase or apply a refined change).
        /// </summary>
        Retry = 0,

        /// <summary>
        /// Skip the current operation (e.g., skip a phase, abort instrumentation attempt).
        /// Phase marked as skipped; execution continues to next phase.
        /// </summary>
        Skip = 1,

        /// <summary>
        /// Cancel the entire debug session.
        /// Execution halts immediately; no further phases or changes are applied.
        /// </summary>
        Cancel = 2
    }
}
