using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of ILlmQuestionService.
    /// Detects LLM questions and handles them via interactive prompting or auto-answer policy.
    /// </summary>
    public class LlmQuestionService : ILlmQuestionService
    {
        private readonly IInteractivePromptService _promptService;
        private readonly IBridgeLogger? _logger;

        public LlmQuestionService(IInteractivePromptService promptService, IBridgeLogger? logger = null)
        {
            if (promptService == null)
                throw new ArgumentNullException(nameof(promptService));

            _promptService = promptService;
            _logger = logger;
        }

        public async Task<LLMQuestionPrompt?> DetectLLMQuestionAsync(string llmResponse, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(llmResponse))
                return null;

            // Simple heuristic: detect question marks and common question patterns
            if (!llmResponse.Contains("?"))
                return null;

            // Extract everything from the start up to the first "?" (including context)
            var questionIndex = llmResponse.IndexOf('?');
            var questionText = llmResponse.Substring(0, questionIndex + 1).Trim();

            // Classify question type based on keywords
            var questionType = ClassifyQuestionType(questionText);

            if (_logger != null)
                await _logger.WriteDebugAsync($"[gap29_8_9] Detected LLM question: {questionText} (Type: {questionType})");

            return new LLMQuestionPrompt
            {
                QuestionText = questionText,
                Context = llmResponse,
                QuestionType = questionType,
                Timestamp = DateTime.UtcNow,
                AutoAnswerHint = ExtractAutoAnswerHint(questionText)
            };
        }

        public async Task<string> HandleLLMQuestionAsync(LLMQuestionPrompt question, bool isAutonomous, AutoAnswerResponse policy = AutoAnswerResponse.Default, CancellationToken ct = default)
        {
            if (question == null)
                throw new ArgumentNullException(nameof(question));
            if (string.IsNullOrWhiteSpace(question.QuestionText))
                throw new ArgumentException("Question text cannot be empty.", nameof(question));

            if (isAutonomous)
            {
                // Apply auto-answer policy
                var answer = AutoAnswerPolicyRegistry.GetDefaultAnswer(question.QuestionType, policy);

                if (_logger != null)
                    await _logger.WriteDebugAsync($"[gap29_8_9] Autonomous mode: auto-answered question '{question.QuestionText}' with '{answer}'");

                return answer;
            }
            else
            {
                // Delegate to interactive prompt service
                var answer = await _promptService.PromptOnLLMQuestionAsync(question, isInteractiveMode: true);

                if (_logger != null)
                    await _logger.WriteDebugAsync($"[gap29_8_9] Interactive mode: user answered question '{question.QuestionText}' with '{answer}'");

                return answer;
            }
        }

        /// <summary>
        /// Classifies a question into one of the predefined types based on keywords and structure.
        /// </summary>
        private static LLMQuestionType ClassifyQuestionType(string questionText)
        {
            var lower = questionText.ToLowerInvariant();

            // Check more specific keywords first (higher priority)
            if (lower.Contains("threshold") || lower.Contains("limit") || lower.Contains("exceeded") || lower.Contains("exceed"))
                return LLMQuestionType.ThresholdWarning;

            if (lower.Contains("which") || lower.Contains("what approach") || lower.Contains("choose") || lower.Contains("select"))
                return LLMQuestionType.Selection;

            if (lower.Contains("should i") || lower.Contains("should we") || lower.Contains("do you think"))
                return LLMQuestionType.Confirmation;

            // Default to Clarification for open-ended questions
            return LLMQuestionType.Clarification;
        }

        /// <summary>
        /// Extracts a hint from the question for auto-answering purposes.
        /// </summary>
        private static string? ExtractAutoAnswerHint(string questionText)
        {
            // Look for patterns like "(1) option A  (2) option B  (3) option C"
            var optionsMatch = Regex.Match(questionText, @"\([\d]\).*", RegexOptions.IgnoreCase);
            if (optionsMatch.Success)
                return optionsMatch.Value;

            return null;
        }
    }
}
