namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Defines the continuation policy for workflow execution (gap27_11).
    /// Controls whether the agent auto-continues to the next tool or waits for user approval.
    /// </summary>
    public enum ContinuationPolicy
    {
        /// <summary>
        /// Auto mode: Continue to next tool without pause.
        /// Agent executes tool actions sequentially without interruption.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Interactive mode: Show UI prompt before each tool execution.
        /// User must approve each action before the agent proceeds.
        /// </summary>
        Interactive = 1,

        /// <summary>
        /// Bypass mode: Skip confirmation dialogs and execute without warnings.
        /// Risky — use only when full automation is required.
        /// </summary>
        Bypass = 2
    }
}
