using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the status of a phase or plan execution.
    /// </summary>
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum ExecutionStatus
    {
        /// <summary>
        /// Execution has not yet started.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Execution is currently in progress.
        /// </summary>
        Running = 1,

        /// <summary>
        /// Execution completed successfully.
        /// </summary>
        Succeeded = 2,

        /// <summary>
        /// Execution failed due to an error.
        /// </summary>
        Failed = 3,

        /// <summary>
        /// Execution was skipped by user or policy.
        /// </summary>
        Skipped = 4,

        /// <summary>
        /// Execution was cancelled by user.
        /// </summary>
        Cancelled = 5
    }
}
