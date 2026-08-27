using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Exceptions;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Skeleton implementation of ILlmService.
    /// </summary>
#pragma warning disable CS0067 // Event is never used
    public class LlmService : ILlmService
    {
        private readonly IMessengerService _messengerService;
        private readonly IConfigService? _configService;
        private readonly IBridgeLogger? _logger;
        private List<CompletionChunk> _currentStreamBuffer = new List<CompletionChunk>();

        public event EventHandler<LlmErrorEventArgs>? Error;

        public LlmService(IMessengerService messengerService, IConfigService? configService = null, IBridgeLogger? logger = null)
        {
            if (messengerService == null)
                throw new ArgumentNullException(nameof(messengerService));
            _messengerService = messengerService;
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// Streams LLM completion chunks asynchronously for the given messages.
        /// Chunks are buffered internally and can be retrieved via GetStreamBuffer() for pause checkpoint capture (gap31_3).
        /// </summary>
        /// <param name="messages">Enumerable of chat messages to stream completion for.</param>
        /// <param name="options">Optional streaming options (model, temperature, etc.).</param>
        /// <param name="ct">Cancellation token for stopping the stream.</param>
        /// <returns>Async enumerable of completion chunks.</returns>
        /// <exception cref="ArgumentNullException">Thrown if messages is null.</exception>
        /// <exception cref="LlmException">Thrown if LLM streaming fails (connection, rate limit, model error, etc.). Check InnerException for details.</exception>
        public async IAsyncEnumerable<CompletionChunk> StreamAsync(
            IEnumerable<ChatMessage> messages,
            StreamOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));

            if (_logger != null)
                await _logger.WriteDebugAsync("LlmService.StreamAsync");

            // Merge messages into options
            var streamOptions = options ?? new StreamOptions();
            streamOptions.Messages = messages;

            // Delegate to messenger service for actual streaming
            await foreach (var chunk in _messengerService.StreamAsync<StreamOptions, CompletionChunk>(
                "llm:stream",
                streamOptions,
                ct))
            {
                // Buffer the chunk before yielding (gap31_3: execution state preservation)
                _currentStreamBuffer.Add(chunk);
                yield return chunk;
            }
        }

        /// <summary>
        /// Retrieves all buffered chunks from the current streaming session.
        /// Used by pause checkpointing (gap31_3) to capture streamed response state.
        /// Returns a copy of the buffer to prevent external modification.
        /// </summary>
        /// <returns>List of buffered CompletionChunk objects (copy of internal buffer).</returns>
        public List<CompletionChunk> GetStreamBuffer()
        {
            return new List<CompletionChunk>(_currentStreamBuffer);
        }

        /// <summary>
        /// Clears the stream buffer.
        /// Called before starting a new stream to ensure fresh buffer per stream session.
        /// </summary>
        public void ClearStreamBuffer()
        {
            _currentStreamBuffer.Clear();
        }

        public bool SupportsStreaming(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));
            return false;
        }

        public bool SupportsFunctionCalling(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));
            return false;
        }

        public int GetContextWindowSize(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));

            if (_configService != null)
            {
                var selectedModel = _configService.GetSelectedModel();
                if (selectedModel != null && selectedModel.ContextWindow > 0)
                    return selectedModel.ContextWindow;
            }

            return 4096;
        }

        public Task<int> CountTokensAsync(string text, string modelId)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));

            var count = (text.Length + 3) / 4;
            return Task.FromResult(count);
        }

        public Task<int> CountMessagesTokensAsync(IEnumerable<ChatMessage> messages, string modelId)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));

            var total = 0;
            foreach (var msg in messages)
            {
                if (!string.IsNullOrEmpty(msg.Content))
                    total += (msg.Content.Length + 3) / 4;
            }
            return Task.FromResult(total);
        }

        public Task LogInteractionAsync(LlmInteractionLog log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            return Task.CompletedTask;
        }
#pragma warning restore CS0067 // Event is never used
    }
}
