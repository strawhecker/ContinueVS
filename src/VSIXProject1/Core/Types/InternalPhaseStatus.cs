namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration of internal phase execution statuses.
    /// </summary>
    public enum InternalPhaseStatus
    {
        /// <summary>
        /// Phase has been created but not yet started.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Phase is currently executing.
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Phase completed successfully.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Phase execution failed.
        /// </summary>
        Failed = 3
    }
}
