using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for estimating and counting tokens in messages and content.
    /// Provides abstraction over token counting logic to support multiple implementations
    /// (heuristic-based, model-specific tokenizers, etc.).
    /// </summary>
    public interface ITokenCountingService
    {
        /// <summary>
        /// Count tokens in a single message synchronously.
        /// </summary>
        /// <param name="message">The message to count tokens for</param>
        /// <returns>Estimated token count for the message</returns>
        int CountMessageTokens(ChatMessage message);

        /// <summary>
        /// Count tokens in a collection of messages synchronously.
        /// </summary>
        /// <param name="messages">The messages to count tokens for</param>
        /// <returns>Estimated total token count for all messages</returns>
        int CountMessagesTokens(List<ChatMessage> messages);

        /// <summary>
        /// Estimate tokens for a future/preview message based on content string.
        /// Used for pre-emptive pruning and context budget planning.
        /// </summary>
        /// <param name="content">The message content to estimate tokens for</param>
        /// <returns>Estimated token count for the content</returns>
        int EstimateFutureMessageTokens(string content);

        /// <summary>
        /// Get the characters-per-token ratio used by this implementation.
        /// </summary>
        int CharactersPerToken { get; }
    }
}
