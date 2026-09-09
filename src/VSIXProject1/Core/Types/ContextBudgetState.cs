using System;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration representing the current context budget state.
    /// Tracks whether the conversation history is Safe, approaching Caution, or Locked due to token limits.
    /// </summary>
    public enum ContextBudgetState
    {
        /// <summary>
        /// Safe: Used tokens ≤ (MaxTokens - Reserve) * 0.85. Input enabled, no warnings.
        /// </summary>
        Safe,

        /// <summary>
        /// Caution: Used tokens ∈ ((MaxTokens - Reserve) * 0.85, MaxTokens - Reserve).
        /// Input enabled, warning displayed. User should consider optimizing.
        /// </summary>
        Caution,

        /// <summary>
        /// Locked: Used tokens ≥ (MaxTokens - Reserve).
        /// Input disabled. User must optimize before proceeding.
        /// </summary>
        Locked
    }
}
