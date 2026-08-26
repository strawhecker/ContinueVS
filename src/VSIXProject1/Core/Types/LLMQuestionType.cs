#nullable enable

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enum representing the type/category of questions posed by the LLM.
    /// Used to select appropriate auto-answer policies and prompting strategies.
    /// </summary>
    public enum LLMQuestionType
    {
        /// <summary>
        /// Question requesting clarification or explanation.
        /// Example: "Should I use synchronous or asynchronous processing here?"
        /// </summary>
        Clarification = 0,

        /// <summary>
        /// Question requesting user confirmation or approval.
        /// Example: "Should I apply this risky optimization?"
        /// </summary>
        Confirmation = 1,

        /// <summary>
        /// Question presenting multiple options for selection.
        /// Example: "Which error handling strategy should I use: (1) Retry, (2) Fallback, (3) Cancel?"
        /// </summary>
        Selection = 2,

        /// <summary>
        /// Question warning about threshold or limit being reached.
        /// Example: "Retry attempts have exceeded threshold. Continue?"
        /// </summary>
        ThresholdWarning = 3
    }
}
