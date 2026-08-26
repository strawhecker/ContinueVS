namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a single code snippet to be inserted during instrumentation.
    /// Immutable after creation.
    /// </summary>
    public class InstrumentationSnippet
    {
        /// <summary>
        /// Line number where the snippet should be inserted (1-based index).
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The code to insert.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Explanation for why this snippet is inserted.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Whether this snippet has been applied to the source.
        /// </summary>
        public bool Applied { get; set; }
    }
}
