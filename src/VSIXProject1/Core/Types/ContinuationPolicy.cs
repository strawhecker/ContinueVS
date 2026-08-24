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
        /// Deferred mode: Queue tool execution for later review.
        /// Agent defers execution; user can approve/reject from audit log.
        /// Safest option for exploration and complex workflows.
        /// </summary>
        Deferred = 2
    }
}
