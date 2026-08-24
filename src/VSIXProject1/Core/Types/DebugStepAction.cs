namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enum representing debug stepping actions.
    /// </summary>
    public enum DebugStepAction
    {
        /// <summary>
        /// Step over current line (skip method bodies).
        /// </summary>
        StepOver = 1,

        /// <summary>
        /// Step into current line (enter method bodies).
        /// </summary>
        StepInto = 2,

        /// <summary>
        /// Step out of current method.
        /// </summary>
        StepOut = 3,

        /// <summary>
        /// Resume execution (run until next breakpoint).
        /// </summary>
        Continue = 4,

        /// <summary>
        /// Pause execution at current location.
        /// </summary>
        Pause = 5
    }
}
