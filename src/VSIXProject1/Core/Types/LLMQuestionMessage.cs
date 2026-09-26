#nullable enable

using System;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an inline LLM question message in the chat stream.
    /// Derives from ChatMessage to integrate with existing message rendering pipeline.
    /// </summary>
    public class LLMQuestionMessage : ChatMessage
    {
        /// <summary>
        /// The actual question text to display to the user.
        /// </summary>
        public string QuestionText { get; }

        /// <summary>
        /// The type of question (Clarification, Selection, Confirmation, ThresholdWarning).
        /// </summary>
        public LLMQuestionType QuestionType { get; }

        /// <summary>
        /// The user's answer to the question (for UI binding).
        /// </summary>
        public string? QuestionAnswer { get; set; }

        /// <summary>
        /// Callback invoked when user provides answer.
        /// </summary>
        public Func<string, System.Threading.Tasks.Task>? OnAnswerAsync { get; set; }

        /// <summary>
        /// Callback invoked when user cancels the prompt.
        /// </summary>
        public Func<System.Threading.Tasks.Task>? OnCancelAsync { get; set; }

        /// <summary>
        /// Optional context from the LLM response (for reference).
        /// </summary>
        public string? Context { get; }

        /// <summary>
        /// If true, this question must be answered by a human. The agent must NOT auto-answer
        /// (e.g. on cancel) with a policy default when this is set.
        /// </summary>
        public bool RequireHumanDecision { get; }

        public LLMQuestionMessage(
            string questionText,
            LLMQuestionType questionType,
            string? context = null,
            bool requireHumanDecision = false)
        {
            QuestionText = questionText ?? throw new ArgumentNullException(nameof(questionText));
            QuestionType = questionType;
            Context = context;
            RequireHumanDecision = requireHumanDecision;

            // Initialize base ChatMessage properties
            Id = Guid.NewGuid().ToString();
            Role = ChatMessageRole.System;
            Content = questionText;
        }
    }
}
