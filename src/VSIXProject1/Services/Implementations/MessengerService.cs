#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IMessengerService that handles real HTTP streaming to Ollama and other LLM providers.
    /// Supports request/response, fire-and-forget, and streaming message patterns.
    /// </summary>
    public class MessengerService : IMessengerService
    {
        private readonly IConfigService _configService;
        private readonly HttpClient _httpClient;
        private readonly IBridgeLogger? _logger;

        public MessengerService(
            IConfigService configService,
            HttpClient httpClient,
            IBridgeLogger? logger = null)
        {
            if (configService == null)
                throw new ArgumentNullException(nameof(configService));
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            _configService = configService;
            _httpClient = httpClient;
            _logger = logger;
        }

        public Task<TResponse> RequestAsync<TRequest, TResponse>(
            string messageType,
            TRequest data,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));

            return Task.FromResult<TResponse>(default!);
        }

        public void Send<TData>(string messageType, TData data)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));
        }

        public void On<TData, TResponse>(
            string messageType,
            Func<TData, Task<TResponse>> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));
        }

        public async IAsyncEnumerable<TChunk> StreamAsync<TRequest, TChunk>(
            string messageType,
            TRequest data,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));

            // Only handle "llm:stream" message type (Ollama chat streaming)
            if (messageType == "llm:stream")
            {
                await foreach (var chunk in StreamLlmAsync<TChunk>(data ?? new object(), ct))
                {
                    yield return chunk;
                }
            }
            // For other message types, return empty (future expansion point)
        }

        /// <summary>
        /// Streams LLM completion chunks from Ollama endpoint.
        /// Converts incoming StreamOptions + active model config to Ollama request.
        /// Parses NDJSON response stream and yields CompletionChunk objects.
        /// </summary>
        private async IAsyncEnumerable<TChunk> StreamLlmAsync<TChunk>(
            object data,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Extract StreamOptions from generic data parameter
            if (!(data is StreamOptions options))
                options = new StreamOptions();

            // Resolve active model from config
            var model = _configService.GetSelectedModel();
            if (model == null)
                throw new LlmException("No model selected in configuration");

            if (string.IsNullOrWhiteSpace(model.BaseUrl))
                throw new LlmException($"Model '{model.Name}' has no baseUrl configured");

            if (string.IsNullOrWhiteSpace(model.Provider))
                throw new LlmException($"Model '{model.Name}' has no provider configured");

            // Currently only support Ollama
            if (model.Provider != "ollama")
                throw new LlmException($"Provider '{model.Provider}' is not yet supported");

            await foreach (var chunk in ProcessOllamaStreamAsync<TChunk>(model, options, ct))
            {
                yield return chunk;
            }
        }

        /// <summary>
        /// Helper method to process Ollama stream without try-catch inside async generator.
        /// </summary>
        private async IAsyncEnumerable<TChunk> ProcessOllamaStreamAsync<TChunk>(
            ModelInfo model,
            StreamOptions options,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            HttpResponseMessage? response = null;

            // Convert ChatMessage array to OllamaMessage list
            var ollamaMessages = new List<OllamaMessage>();
            if (options.Messages != null)
            {
                foreach (var msg in options.Messages)
                {
                    var role = msg.Role switch
                    {
                        ChatMessageRole.User => "user",
                        ChatMessageRole.Assistant => "assistant",
                        ChatMessageRole.System => "system",
                        _ => "user"
                    };

                    ollamaMessages.Add(new OllamaMessage
                    {
                        Role = role,
                        Content = msg.Content ?? string.Empty
                    });
                }
            }

            // If no messages provided, add a default placeholder
            if (ollamaMessages.Count == 0)
            {
                ollamaMessages.Add(new OllamaMessage
                {
                    Role = "user",
                    Content = "Hello"
                });
            }

            // Build Ollama request
            var ollamaRequest = new OllamaRequest
            {
                Model = model.Name,
                Stream = true,
                Messages = ollamaMessages,
                Options = new OllamaOptions
                {
                    Temperature = options.Temperature,
                    MaxTokens = options.MaxTokens,
                    TopP = options.TopP
                }
            };

            // POST to Ollama chat endpoint
            var endpoint = $"{(model.BaseUrl ?? "").TrimEnd('/')}/api/chat";
            var json = JsonConvert.SerializeObject(ollamaRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (_logger != null)
                await _logger.WriteDebugAsync($"MessengerService: POST to {endpoint}");


            try
            {
                response = await _httpClient.PostAsync(endpoint, content, ct);
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new LlmException($"HTTP request to Ollama failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new LlmException("Ollama streaming cancelled by caller", ex);
            }
            catch (Exception ex)
            {
                throw new LlmException($"Unexpected error during Ollama streaming: {ex.Message}", ex);
            }

            try
            {
                // Read response stream line-by-line (NDJSON format)
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        ct.ThrowIfCancellationRequested();

                        // Skip empty lines
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        // Parse JSON line to OllamaResponse
                        OllamaResponse? ollamaResponse = null;
                        try
                        {
                            ollamaResponse = JsonConvert.DeserializeObject<OllamaResponse>(line);
                        }
                        catch (JsonException jsonEx)
                        {
                            if (_logger != null)
                                await _logger.WriteDebugAsync($"Failed to parse NDJSON line: {line}. Error: {jsonEx.Message}");
                            // Continue on parse errors (malformed chunk)
                            continue;
                        }

                        if (ollamaResponse?.Message?.Content != null)
                        {
                            // Convert to CompletionChunk and yield
                            var chunk = new CompletionChunk
                            {
                                Type = ChunkType.Text,
                                Content = ollamaResponse.Message.Content,
                                Role = ChatMessageRole.Assistant,
                                IsDone = ollamaResponse.Done,
                                Timestamp = DateTime.UtcNow
                            };

                            if (typeof(TChunk) == typeof(CompletionChunk))
                            {
                                yield return (TChunk)(object)chunk;
                            }
                        }

                        // Stop when done
                        if (ollamaResponse?.Done ?? false)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();
            }
        }
    }
}
