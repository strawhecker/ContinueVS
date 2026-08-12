using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
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
        private readonly IBridgeLogger? _logger;

        public event EventHandler<LlmErrorEventArgs>? Error;

        public LlmService(IMessengerService messengerService, IBridgeLogger? logger = null)
        {
            if (messengerService == null)
                throw new ArgumentNullException(nameof(messengerService));
            _messengerService = messengerService;
            _logger = logger;
        }

        public async IAsyncEnumerable<CompletionChunk> StreamAsync(
            IEnumerable<ChatMessage> messages,
            StreamOptions? options = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));

            if (_logger != null)
                await _logger.WriteDebugAsync("LlmService.StreamAsync (skeleton)");

            // Stub: Return empty stream
            await Task.Yield();
            yield break;
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
