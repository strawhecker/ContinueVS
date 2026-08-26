using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for detecting and handling LLM questions posed during phases.
    /// Supports both interactive (user prompting) and autonomous (policy-based) answering.
    /// </summary>
    public interface ILlmQuestionService
    {
        /// <summary>
        /// Detects if an LLM response contains an embedded question.
        /// Parses the response text and returns a structured LLMQuestionPrompt if a question is found.
        /// </summary>
        /// <param name="llmResponse">The LLM response text to parse.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An LLMQuestionPrompt if a question is detected; null if no question found.</returns>
        Task<LLMQuestionPrompt?> DetectLLMQuestionAsync(string llmResponse, CancellationToken ct = default);

        /// <summary>
        /// Handles an LLM question by either prompting the user (interactive mode) or applying policy (autonomous mode).
        /// </summary>
        /// <param name="question">The LLM question to handle.</param>
        /// <param name="isAutonomous">If true, applies auto-answer policy; if false, prompts user.</param>
        /// <param name="policy">The response strategy to use in autonomous mode.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The answer to the question as a string.</returns>
        Task<string> HandleLLMQuestionAsync(LLMQuestionPrompt question, bool isAutonomous, AutoAnswerResponse policy = AutoAnswerResponse.Default, CancellationToken ct = default);
    }
}
