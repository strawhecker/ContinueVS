namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents information about a debugger breakpoint.
    /// </summary>
    public class BreakpointInfo
    {
        /// <summary>
        /// File path where breakpoint is set.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Line number where breakpoint is set (1-based).
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Whether the breakpoint is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Number of times breakpoint has been hit in this session.
        /// </summary>
        public int HitCount { get; set; }

        /// <summary>
        /// Unique identifier for the breakpoint in the debugger.
        /// </summary>
        public string? BreakpointId { get; set; }

        /// <summary>
        /// Optional condition expression (e.g., "x > 10").
        /// </summary>
        public string? Condition { get; set; }
    }
}
