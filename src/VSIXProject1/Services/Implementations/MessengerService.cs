#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
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
        private readonly IContextDumpService _contextDumpService;

        public MessengerService(
            IConfigService configService,
            HttpClient httpClient,
            IBridgeLogger? logger = null,
            IContextDumpService? contextDumpService = null)
        {
            if (configService == null)
                throw new ArgumentNullException(nameof(configService));
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            _configService = configService;
            _httpClient = httpClient;
            _logger = logger;
            _contextDumpService = contextDumpService ?? new NullContextDumpService();
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
            _ = LoggerService.Current.WriteDebugAsync("[MessengerService.ProcessLlmStreamAsync] Attempting to get selected model...");
            var model = _configService.GetSelectedModel();

            if (model == null)
            {
                _ = LoggerService.Current.WriteDebugAsync("[MessengerService.ProcessLlmStreamAsync] ERROR: model is null - No model selected in configuration");
                throw new LlmException("No model selected in configuration");
            }

            _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] Model selected: {model.Name} (Id:{model.Id}, Provider:{model.Provider}, BaseUrl:{model.BaseUrl})");

            if (string.IsNullOrWhiteSpace(model.BaseUrl))
            {
                _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] ERROR: Model '{model.Name}' has no baseUrl configured");
                throw new LlmException($"Model '{model.Name}' has no baseUrl configured");
            }

            if (string.IsNullOrWhiteSpace(model.Provider))
            {
                _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] ERROR: Model '{model.Name}' has no provider configured");
                throw new LlmException($"Model '{model.Name}' has no provider configured");
            }

            // Currently only support Ollama
            if (model.Provider != "ollama")
            {
                _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] ERROR: Provider '{model.Provider}' is not yet supported");
                throw new LlmException($"Provider '{model.Provider}' is not yet supported");
            }

             _ = LoggerService.Current.WriteDebugAsync("[MessengerService.ProcessLlmStreamAsync] Starting Ollama stream...");

            // Query Ollama for available models (for diagnostics)
            try
            {
                var tagsEndpoint = $"{(model.BaseUrl ?? "").TrimEnd('/')}/api/tags";
                _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] Querying Ollama models from {tagsEndpoint}...");
                var tagsResponse = await _httpClient.GetAsync(tagsEndpoint, ct);
                if (tagsResponse.IsSuccessStatusCode)
                {
                    var tagsJson = await tagsResponse.Content.ReadAsStringAsync();
                    _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] Available Ollama models: {tagsJson}");
                }
                else
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] Failed to query models: HTTP {(int)tagsResponse.StatusCode}");
                }
            }
            catch (Exception diagEx)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[MessengerService.ProcessLlmStreamAsync] Error querying models: {diagEx.Message}");
            }

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

            _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Message count: {ollamaMessages.Count}");
            foreach (var msg in ollamaMessages)
            {
                var contentPreview = msg.Content?.Substring(0, Math.Min(50, msg.Content?.Length ?? 0)) ?? "[null content]";
                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync]   - Role: {msg.Role}, Content: {contentPreview}...");
            }

            // Build Ollama request
            // Use OllamaModelId if available (actual Ollama model identifier), otherwise fall back to Name
            var ollamaModelId = !string.IsNullOrEmpty(model.OllamaModelId) ? model.OllamaModelId : model.Name;
            _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Model name: {model.Name}, OllamaModelId: {model.OllamaModelId}, Using: {ollamaModelId}");

            var ollamaRequest = new OllamaRequest
            {
                Model = ollamaModelId,
                Stream = true,
                Messages = ollamaMessages,
                Options = new OllamaOptions
                {
                    Temperature = options.Temperature,
                    MaxTokens = options.MaxTokens,
                    TopP = options.TopP
                }
            };

            _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Building request - Model: {ollamaRequest.Model}, Stream: {ollamaRequest.Stream}, Temperature: {ollamaRequest.Options.Temperature}");

            // Dump context before sending if debug flag is enabled
            if (options.Messages != null)
            {
                var messageList = options.Messages.ToList();
                await _contextDumpService.DumpContextBeforeSendAsync(messageList);
            }

            // POST to Ollama chat endpoint
            var endpoint = $"{(model.BaseUrl ?? "").TrimEnd('/')}/api/chat";
            var json = JsonConvert.SerializeObject(ollamaRequest);
            _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Endpoint: {endpoint}");
            _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Request JSON: {json.Substring(0, Math.Min(200, json.Length))}...");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (_logger != null)
                await _logger.WriteDebugAsync($"MessengerService: POST to {endpoint}");

            try
            {
                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Sending HTTP POST request to {endpoint}...");
                // ResponseHeadersRead prevents HttpClient from buffering the entire response body before returning.
                // Without it, PostAsync waits until all NDJSON chunks are received, defeating streaming.
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Response status code: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    // Read error response body while stream is still available
                    string responseBodyText = "[Unable to read response]";
                    try
                    {
                        responseBodyText = await response.Content.ReadAsStringAsync();
                    }
                    catch (Exception readEx)
                    {
                        _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Failed to read error response body: {readEx.Message}");
                    }

                    _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] ERROR - HTTP {(int)response.StatusCode}: {responseBodyText}");
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {responseBodyText}");
                }

                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Status code confirmed successful");
            }
            catch (HttpRequestException ex)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] HttpRequestException: {ex.Message}");
                throw new LlmException($"HTTP request to Ollama failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] TaskCanceledException: {ex.Message}");
                throw new LlmException(
                    $"Ollama request timeout or was cancelled. " +
                    $"Ensure Ollama is running at {model.BaseUrl}/api/chat and the model '{model.Name}' is loaded. " +
                    $"The request may have taken too long to complete.", ex);
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Unexpected exception: {ex.GetType().Name}: {ex.Message}");
                throw new LlmException($"Unexpected error during Ollama streaming: {ex.Message}", ex);
            }

            try
            {
                _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Starting to read response stream...");

                // Read response stream line-by-line (NDJSON format)
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string? line;
                    int lineCount = 0;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        ct.ThrowIfCancellationRequested();

                        // Skip empty lines
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        lineCount++;
                        _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Received line {lineCount}: {line}");

                        // Parse JSON line to OllamaResponse
                        OllamaResponse? ollamaResponse = null;
                        try
                        {
                            ollamaResponse = JsonConvert.DeserializeObject<OllamaResponse>(line);
                        }
                        catch (JsonException jsonEx)
                        {
                            _ = LoggerService.Current.WriteDebugAsync($"[ProcessOllamaStreamAsync] Failed to parse NDJSON line {lineCount}: {jsonEx.Message}");
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

    /// <summary>
    /// No-op implementation of IContextDumpService used as fallback.
    /// </summary>
    internal class NullContextDumpService : IContextDumpService
    {
        public Task DumpContextBeforeSendAsync(List<ChatMessage> messages, List<ContextItem>? selectedContext = null)
        {
            return Task.CompletedTask;
        }

        public Task DumpResponseAfterReceiveAsync(string responseContent)
        {
            return Task.CompletedTask;
        }
    }
}

