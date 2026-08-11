using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for LLM orchestration.
    /// Handles streaming completions, model capabilities, and token counting.
    /// </summary>
    public interface ILlmService
    {
        /// <summary>
        /// Streams a completion from the LLM service.
        /// </summary>
        /// <param name="messages">The conversation messages to send to the LLM.</param>
        /// <param name="options">Optional streaming options (temperature, max_tokens, etc.).</param>
        /// <param name="ct">Cancellation token to stop the stream.</param>
        /// <returns>An async enumerable of completion chunks.</returns>
        IAsyncEnumerable<CompletionChunk> StreamAsync(
            IEnumerable<ChatMessage> messages,
            StreamOptions? options = null,
            CancellationToken ct = default);

        /// <summary>
        /// Checks if a model supports streaming completion.
        /// </summary>
        /// <param name="modelId">The ID of the model to check.</param>
        /// <returns>True if the model supports streaming.</returns>
        bool SupportsStreaming(string modelId);

        /// <summary>
        /// Checks if a model supports function calling / tool use.
        /// </summary>
        /// <param name="modelId">The ID of the model to check.</param>
        /// <returns>True if the model supports function calling.</returns>
        bool SupportsFunctionCalling(string modelId);

        /// <summary>
        /// Gets the context window size for a model.
        /// </summary>
        /// <param name="modelId">The ID of the model.</param>
        /// <returns>The context window size in tokens.</returns>
        int GetContextWindowSize(string modelId);

        /// <summary>
        /// Counts tokens for a given text.
        /// </summary>
        /// <param name="text">The text to count tokens for.</param>
        /// <param name="modelId">The ID of the model to use for counting.</param>
        /// <returns>The number of tokens.</returns>
        Task<int> CountTokensAsync(string text, string modelId);

        /// <summary>
        /// Counts tokens for a list of messages.
        /// </summary>
        /// <param name="messages">The messages to count tokens for.</param>
        /// <param name="modelId">The ID of the model to use for counting.</param>
        /// <returns>The total number of tokens.</returns>
        Task<int> CountMessagesTokensAsync(IEnumerable<ChatMessage> messages, string modelId);

        /// <summary>
        /// Logs an interaction for analytics or debugging.
        /// </summary>
        /// <param name="log">The interaction log to record.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LogInteractionAsync(LlmInteractionLog log);

        /// <summary>
        /// Event raised when an error occurs in the LLM service.
        /// </summary>
        event EventHandler<LlmErrorEventArgs>? Error;
    }

    /// <summary>
    /// Options for LLM streaming requests.
    /// </summary>
    public class StreamOptions
    {
        /// <summary>
        /// Sampling temperature (0.0 to 2.0).
        /// </summary>
        public double? Temperature { get; set; }

        /// <summary>
        /// Maximum tokens to generate.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Top-p (nucleus) sampling parameter.
        /// </summary>
        public double? TopP { get; set; }

        /// <summary>
        /// System prompt or instructions.
        /// </summary>
        public string? SystemPrompt { get; set; }
    }

    /// <summary>
    /// Represents a logged LLM interaction.
    /// </summary>
    public class LlmInteractionLog
    {
        /// <summary>
        /// Unique identifier for this log entry.
        /// </summary>
        public string? Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// ID of the model used.
        /// </summary>
        public string? ModelId { get; set; }

        /// <summary>
        /// The messages sent to the LLM.
        /// </summary>
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

        /// <summary>
        /// The response received from the LLM.
        /// </summary>
        public CompletionChunk? Response { get; set; }

        /// <summary>
        /// Time taken for the request.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Timestamp of the interaction.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
