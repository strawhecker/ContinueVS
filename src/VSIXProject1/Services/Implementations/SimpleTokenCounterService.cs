using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Heuristic-based token counter implementation.
    /// Estimates tokens using character-based approximation and message wrapper overhead.
    /// 
    /// Heuristic:
    /// - 1 token ≈ ~4 characters (tunable via CharactersPerToken)
    /// - Each message adds ~50 tokens for wrapper overhead (metadata, formatting, role tags)
    /// - Empty messages count as minimum 5 tokens
    /// </summary>
    public class SimpleTokenCounterService : ITokenCountingService
    {
        private const int MessageWrapperTokens = 50;
        private const int MinTokensPerMessage = 5;

        /// <summary>
        /// Get or set the characters-per-token ratio (default: 4).
        /// Adjust to fine-tune token estimation accuracy for specific models.
        /// </summary>
        public int CharactersPerToken { get; set; } = 4;

        /// <summary>
        /// Count tokens in a single message.
        /// Formula: (content.Length / CharactersPerToken) + MessageWrapperTokens, minimum MinTokensPerMessage
        /// </summary>
        public int CountMessageTokens(ChatMessage message)
        {
            if (message == null)
                return 0;

            int contentTokens = Math.Max(1, message.Content.Length / CharactersPerToken);
            int totalTokens = Math.Max(MinTokensPerMessage, contentTokens + MessageWrapperTokens);

            return totalTokens;
        }

        /// <summary>
        /// Count tokens in a collection of messages by summing individual message counts.
        /// </summary>
        public int CountMessagesTokens(List<ChatMessage> messages)
        {
            if (messages == null || messages.Count == 0)
                return 0;

            int totalTokens = 0;
            foreach (var message in messages)
            {
                int messageTokens = CountMessageTokens(message);
                totalTokens += messageTokens;
            }

            return totalTokens;
        }

        /// <summary>
        /// Estimate tokens for a future message content string.
        /// Used for pre-emptive pruning: estimate how many tokens a new message will consume
        /// before adding it to the session.
        /// </summary>
        public int EstimateFutureMessageTokens(string content)
        {
            if (string.IsNullOrEmpty(content))
                return MinTokensPerMessage;

            int contentTokens = Math.Max(1, content.Length / CharactersPerToken);
            int totalTokens = Math.Max(MinTokensPerMessage, contentTokens + MessageWrapperTokens);

            return totalTokens;
        }
    }
}
