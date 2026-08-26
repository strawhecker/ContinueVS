#nullable enable

namespace ContinueVS.Services.Implementations
{
    using ContinueVS.Core.Types;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Registry of auto-answer policies for LLM questions.
    /// Maps question types to default response strategies and answers.
    /// </summary>
    public static class AutoAnswerPolicyRegistry
    {
        /// <summary>
        /// Gets the default answer for a given question type and response strategy.
        /// </summary>
        /// <param name="questionType">The type of question being answered.</param>
        /// <param name="strategy">The response strategy to apply (Default, Conservative, Aggressive).</param>
        /// <returns>The auto-answer string; empty string if no policy applies.</returns>
        public static string GetDefaultAnswer(LLMQuestionType questionType, AutoAnswerResponse strategy)
        {
            var key = (questionType, strategy);

            if (_policies.TryGetValue(key, out var answer))
                return answer;

            // Fallback: if specific strategy not found, try Default strategy
            if (strategy != AutoAnswerResponse.Default)
            {
                var fallbackKey = (questionType, AutoAnswerResponse.Default);
                if (_policies.TryGetValue(fallbackKey, out var fallback))
                    return fallback;
            }

            return string.Empty;
        }

        /// <summary>
        /// Policy dictionary: (QuestionType, Strategy) -> Answer string.
        /// </summary>
        private static readonly Dictionary<(LLMQuestionType, AutoAnswerResponse), string> _policies =
            new Dictionary<(LLMQuestionType, AutoAnswerResponse), string>
            {
                // Clarification questions
                { (LLMQuestionType.Clarification, AutoAnswerResponse.Default), "Proceed with the most common approach." },
                { (LLMQuestionType.Clarification, AutoAnswerResponse.Conservative), "Use the safest, most tested approach." },
                { (LLMQuestionType.Clarification, AutoAnswerResponse.Aggressive), "Use the most efficient approach." },

                // Confirmation questions
                { (LLMQuestionType.Confirmation, AutoAnswerResponse.Default), "Yes, proceed with caution." },
                { (LLMQuestionType.Confirmation, AutoAnswerResponse.Conservative), "No, skip this step." },
                { (LLMQuestionType.Confirmation, AutoAnswerResponse.Aggressive), "Yes, proceed immediately." },

                // Selection questions
                { (LLMQuestionType.Selection, AutoAnswerResponse.Default), "Select option 1." },
                { (LLMQuestionType.Selection, AutoAnswerResponse.Conservative), "Select the safest option." },
                { (LLMQuestionType.Selection, AutoAnswerResponse.Aggressive), "Select the fastest option." },

                // Threshold warning questions
                { (LLMQuestionType.ThresholdWarning, AutoAnswerResponse.Default), "Continue with caution." },
                { (LLMQuestionType.ThresholdWarning, AutoAnswerResponse.Conservative), "Halt and review." },
                { (LLMQuestionType.ThresholdWarning, AutoAnswerResponse.Aggressive), "Continue without further review." }
            };
    }
}
