#nullable enable

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enum representing the response strategy for autonomous mode auto-answering.
    /// Different question types may use different response strategies.
    /// </summary>
    public enum AutoAnswerResponse
    {
        /// <summary>
        /// Default/balanced response strategy.
        /// Used for most questions; safe, reasonable default answers.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Conservative response strategy.
        /// Prioritizes safety; prefers "no" or minimal action responses.
        /// </summary>
        Conservative = 1,

        /// <summary>
        /// Aggressive response strategy.
        /// Prioritizes progress; prefers "yes" or action-oriented responses.
        /// </summary>
        Aggressive = 2,

        /// <summary>
        /// No automatic response; requires user interaction even in Autonomous mode.
        /// </summary>
        None = 3
    }
}
