using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a question posed by the LLM during analysis or instrumentation phases.
    /// Captures the question text, type, context, and hint for autonomous answering.
    /// </summary>
    public class LLMQuestionPrompt
    {
        /// <summary>
        /// Unique identifier for this question.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The question text as posed by the LLM.
        /// </summary>
        [JsonProperty("questionText")]
        public string QuestionText { get; set; } = string.Empty;

        /// <summary>
        /// Optional context or surrounding text from the LLM response.
        /// </summary>
        [JsonProperty("context")]
        public string? Context { get; set; }

        /// <summary>
        /// The type/category of the question (Clarification, Confirmation, Selection, ThresholdWarning).
        /// Used to select appropriate auto-answer policy.
        /// </summary>
        [JsonProperty("questionType")]
        public LLMQuestionType QuestionType { get; set; } = LLMQuestionType.Clarification;

        /// <summary>
        /// Timestamp when the question was detected.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional hint for the LLM to guide autonomous answering.
        /// May contain suggested answer or answer options.
        /// </summary>
        [JsonProperty("autoAnswerHint")]
        public string? AutoAnswerHint { get; set; }
    }
}
