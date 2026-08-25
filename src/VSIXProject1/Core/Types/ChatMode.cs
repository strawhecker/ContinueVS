namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Defines the operational mode for the chat interface.
    /// </summary>
    public enum ChatMode
    {
        /// <summary>
        /// Chat mode: Basic Q&A with optional "Apply" button for code suggestions.
        /// </summary>
        Ask,

        /// <summary>
        /// Agent mode: Autonomous tool calling and code editing with user approval.
        /// </summary>
        Agent,

        /// <summary>
        /// Plan mode: Read-only plan generation and review.
        /// </summary>
        Plan,

        /// <summary>
        /// Debug mode: Instrumentation-driven error diagnosis with interactive refinement.
        /// </summary>
        Debug
    }
}
