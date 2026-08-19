namespace ContinueVS.Core
{
    /// <summary>
    /// System message prompts for each operational mode.
    /// These are fallback prompts used when the external config file is unavailable.
    /// </summary>
    internal static class ChatModeSystemPrompts
    {
        /// <summary>
        /// System prompt for Ask mode: guidance for basic Q&A interaction. Note: differs from contune
        /// </summary>
        public const string DEFAULT_ASK_SYSTEM_MESSAGE = 
            "You are in chat mode.";

        /// <summary>
        /// System prompt for Agent mode: guidance for autonomous tool calling.
        /// </summary>
        public const string DEFAULT_AGENT_SYSTEM_MESSAGE = 
            "You are in agent mode. Use multiple tools simultaneously if needed. Always include the language and file path in the info string when you write code blocks. " +
            "For implementation, use edit tools (not suggestion blocks). Use abbreviated syntax for larger files (// ... existing code ...).";

        /// <summary>
        /// System prompt for Plan mode: guidance for read-only plan generation.
        /// </summary>
        public const string DEFAULT_PLAN_SYSTEM_MESSAGE = 
            "You are in plan mode, in which you help the user understand and construct a plan. Only use read-only tools. Do not use any tools that would write to non-temporary files. " +
            "If the user wants to make changes, offer that they can switch to Agent Mode to give you access to write tools to make the suggested updates. " +
            "Always include the language and file name in the info string when you write code blocks. For planning purposes only, output code blocks for suggestion and planning. When ready to implement, request to switch to Agent Mode.";
    }
}
