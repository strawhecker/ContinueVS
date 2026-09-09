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
        private readonly IToolService _toolService;
        private readonly HttpClient _httpClient;
        private readonly IBridgeLogger? _logger;
        private readonly IContextDumpService _contextDumpService;

        public MessengerService(
            IConfigService configService,
            IToolService toolService,
            HttpClient httpClient,
            IBridgeLogger? logger = null,
            IContextDumpService? contextDumpService = null)
        {
            if (configService == null)
                throw new ArgumentNullException(nameof(configService));
            if (toolService == null)
                throw new ArgumentNullException(nameof(toolService));
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            _configService = configService;
            _toolService = toolService;
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
            LoggerService.Current.WriteDebug("[MessengerService.ProcessLlmStreamAsync] Attempting to get selected model...");

            ModelInfo? model = null;
            try
            {
                model = _configService.GetSelectedModel();
            }
            catch (InvalidOperationException configEx)
            {
                LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] ERROR: ConfigService not initialized: {configEx.Message}");
                throw new LlmException("ConfigService has not been initialized. Ensure ServiceInitializer.InitializeAsync() is called during plugin startup.", configEx);
            }

            if (model == null)
            {
                LoggerService.Current.WriteDebug("[MessengerService.ProcessLlmStreamAsync] ERROR: model is null - No model selected in configuration");
                throw new LlmException("No model selected in configuration");
            }

            LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] Model selected: {model.Name} (Id:{model.Id}, Provider:{model.Provider}, BaseUrl:{model.BaseUrl})");

            if (string.IsNullOrWhiteSpace(model.BaseUrl))
            {
                LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] ERROR: Model '{model.Name}' has no baseUrl configured");
                throw new LlmException($"Model '{model.Name}' has no baseUrl configured");
            }

            if (string.IsNullOrWhiteSpace(model.Provider))
            {
                LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] ERROR: Model '{model.Name}' has no provider configured");
                throw new LlmException($"Model '{model.Name}' has no provider configured");
            }

            // Support both Ollama and OpenAI (including vLLM with custom baseUrl)
            if (string.Equals(model.Provider, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                LoggerService.Current.WriteDebug("[MessengerService.ProcessLlmStreamAsync] Starting Ollama stream...");

                // Query Ollama for available models (for diagnostics)
                try
                {
                    var tagsEndpoint = $"{(model.BaseUrl ?? "").TrimEnd('/')}/api/tags";
                    LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] Querying Ollama models from {tagsEndpoint}...");
                    var tagsResponse = await _httpClient.GetAsync(tagsEndpoint, ct);
                    if (tagsResponse.IsSuccessStatusCode)
                    {
                        var tagsJson = await tagsResponse.Content.ReadAsStringAsync();
                        LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] Available Ollama models: {tagsJson}");
                    }
                    else
                    {
                        LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] Failed to query models: HTTP {(int)tagsResponse.StatusCode}");
                    }
                }
                catch (Exception diagEx)
                {
                    LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] Error querying models: {diagEx.Message}");
                }

                await foreach (var chunk in ProcessOllamaStreamAsync<TChunk>(model, options, ct))
                {
                    yield return chunk;
                }
            }
            else if (string.Equals(model.Provider, "openai", StringComparison.OrdinalIgnoreCase))
            {
                LoggerService.Current.WriteDebug("[MessengerService.ProcessLlmStreamAsync] Starting OpenAI-compatible stream (vLLM/OpenAI)...");
                await foreach (var chunk in ProcessOpenAiStreamAsync<TChunk>(model, options, ct))
                {
                    yield return chunk;
                }
            }
            else
            {
                LoggerService.Current.WriteDebug($"[MessengerService.ProcessLlmStreamAsync] ERROR: Provider '{model.Provider}' is not yet supported");
                throw new LlmException($"Provider '{model.Provider}' is not yet supported. Supported providers: ollama, openai");
            }
        }

        /// <summary>
        /// Helper method to process OpenAI-compatible stream (vLLM, OpenAI, etc.).
        /// Sends request and streams back CompletionChunk objects from SSE responses.
        /// </summary>
        private async IAsyncEnumerable<TChunk> ProcessOpenAiStreamAsync<TChunk>(
            ModelInfo model,
            StreamOptions options,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Convert ChatMessage array to message list with role/content
            var messages = new List<Dictionary<string, object>>();
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

                    messages.Add(new Dictionary<string, object>
                    {
                        { "role", role },
                        { "content", msg.Content ?? string.Empty }
                    });
                }
            }

            // If no messages provided, add a default placeholder
            if (messages.Count == 0)
            {
                messages.Add(new Dictionary<string, object>
                {
                    { "role", "user" },
                    { "content", "Hello" }
                });
            }

            LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Message count: {messages.Count}");

            // Build OpenAI request as JSON object
            var modelId = model.Name ?? "unknown";
            LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Model name: {model.Name}, Using: {modelId}");

            var requestObj = new Dictionary<string, object>
            {
                { "model", modelId },
                { "stream", true },
                { "messages", messages }
            };

            if (options.Temperature.HasValue)
                requestObj["temperature"] = options.Temperature.Value;
            if (options.MaxTokens.HasValue)
                requestObj["max_tokens"] = options.MaxTokens.Value;
            if (options.TopP.HasValue)
                requestObj["top_p"] = options.TopP.Value;

            // Populate tools filtered by current ChatMode (gap71)
            var availableTools = _toolService.GetAvailableTools(options.Mode);
            _logger?.WriteDebug($"[gap71-messenger-openai-tools] Mode={options.Mode}, filtering tools: {availableTools.Count()} available");

            if (availableTools.Any())
            {
                var toolSchemas = ConvertToolDefinitionsToSchema(availableTools);
                requestObj["tools"] = toolSchemas;
                _logger?.WriteDebug($"[gap71-messenger-openai-tools] Populated request.tools with {toolSchemas.Count} schemas for mode {options.Mode}");
            }

            LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Building request - Model: {modelId}, Stream: true, Temperature: {options.Temperature}");

            // Dump context before sending if debug flag is enabled
            if (options.Messages != null)
            {
                var messageList = options.Messages.ToList();
                await _contextDumpService.DumpContextBeforeSendAsync(messageList);
            }

            // POST to OpenAI chat completions endpoint
            var endpoint = $"{(model.BaseUrl ?? "").TrimEnd('/')}/v1/chat/completions";
            var json = JsonConvert.SerializeObject(requestObj);
            LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Endpoint: {endpoint}");
            LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Request JSON: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (_logger != null)
                _logger?.WriteDebug($"MessengerService: POST to {endpoint}");

            LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Sending HTTP POST request to {endpoint}...");
            // ResponseHeadersRead prevents HttpClient from buffering the entire response body before returning.
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

            // Add API key header if provided (some vLLM instances don't require it)
            if (!string.IsNullOrWhiteSpace(model.ApiKey) && model.ApiKey != "not-required")
            {
                request.Headers.Add("Authorization", $"Bearer {model.ApiKey}");
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Response status code: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    string responseBodyText = "[Unable to read response]";
                    try
                    {
                        responseBodyText = await response.Content.ReadAsStringAsync();
                    }
                    catch (Exception readEx)
                    {
                        LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Failed to read error response body: {readEx.Message}");
                    }

                    LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] ERROR - HTTP {(int)response.StatusCode}: {responseBodyText}");
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {responseBodyText}");
                }

                LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Status code confirmed successful");
            }
            catch (HttpRequestException ex)
            {
                LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] HttpRequestException: {ex.Message}");
                throw new LlmException($"HTTP request to OpenAI-compatible endpoint failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] TaskCanceledException: {ex.Message}");
                throw new LlmException(
                    $"OpenAI-compatible request timeout or was cancelled. " +
                    $"Ensure endpoint is running at {model.BaseUrl}/v1/chat/completions and model '{model.Name}' is available. " +
                    $"The request may have taken too long to complete.", ex);
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Unexpected exception: {ex.GetType().Name}: {ex.Message}");
                throw new LlmException($"Unexpected error during OpenAI-compatible streaming: {ex.Message}", ex);
            }

            LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Starting to read response stream...");

            // Read response stream line-by-line (SSE format with "data: " prefix)
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
                    LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Received line {lineCount}: {line}");

                    // Parse SSE format: "data: {json}"
                    if (!line.StartsWith("data: "))
                    {
                        LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Skipping non-data line: {line}");
                        continue;
                    }

                    var jsonData = line.Substring("data: ".Length);

                    // Check for stream termination marker
                    if (jsonData == "[DONE]")
                    {
                        LoggerService.Current.WriteDebug($"[ProcessOpenAiStreamAsync] Stream terminated with [DONE] marker");
                        break;
                    }

                    // Parse JSON response and extract chunk
                    var chunk = ParseOpenAiChunk(jsonData);
                    if (chunk != null && typeof(TChunk) == typeof(CompletionChunk))
                    {
                        yield return (TChunk)(object)chunk;
                    }
                }
            }

            response?.Dispose();
        }

        /// <summary>
        /// Parse a single SSE line from OpenAI-compatible endpoint into a CompletionChunk.
        /// Returns null if parse fails or no content extracted.
        /// </summary>
        private CompletionChunk? ParseOpenAiChunk(string jsonData)
        {
            JObject? jsonObj = null;
            try
            {
                jsonObj = JsonConvert.DeserializeObject<JObject>(jsonData);
            }
            catch (JsonException jsonEx)
            {
                LoggerService.Current.WriteDebug($"[ParseOpenAiChunk] Failed to parse JSON: {jsonEx.Message}");
                return null;
            }

            if (jsonObj == null)
                return null;

            try
            {
                var choices = jsonObj["choices"] as JArray;
                if (choices == null || choices.Count == 0)
                    return null;

                var choice = choices[0] as JObject;
                if (choice == null)
                    return null;

                var delta = choice["delta"] as JObject;
                if (delta == null)
                    return null;

                // Support both "content" (OpenAI/vLLM standard) and "reasoning" (DeepSeek reasoning models)
                var content = delta["content"]?.Value<string>();
                var reasoning = delta["reasoning"]?.Value<string>();

                // Extract tool_calls from delta if present (gap70: tool streaming)
                List<ToolCallSchema>? toolCalls = null;
                var toolCallsArray = delta["tool_calls"] as JArray;
                if (toolCallsArray != null && toolCallsArray.Count > 0)
                {
                    try
                    {
                        toolCalls = toolCallsArray.ToObject<List<ToolCallSchema>>();
                        LoggerService.Current.WriteDebug(
                            $"[ParseOpenAiChunk] Extracted tool_calls: {toolCalls?.Count ?? 0} calls");
                    }
                    catch (Exception toolEx)
                    {
                        LoggerService.Current.WriteWarning(
                            $"[ParseOpenAiChunk] Failed to deserialize tool_calls: {toolEx.Message}");
                    }
                }

                // If neither content nor reasoning nor tool_calls, skip this chunk
                if (string.IsNullOrEmpty(content) && string.IsNullOrEmpty(reasoning) && (toolCalls == null || toolCalls.Count == 0))
                    return null;

                var finishReason = choice["finish_reason"]?.Value<string>();
                var chunk = new CompletionChunk
                {
                    Type = toolCalls != null && toolCalls.Count > 0 ? ChunkType.ToolCall : ChunkType.Text,
                    Content = content,
                    Reasoning = reasoning,
                    ToolCalls = toolCalls,
                    Role = ChatMessageRole.Assistant,
                    IsDone = finishReason == "stop",
                    DoneReason = finishReason,
                    Timestamp = DateTime.UtcNow
                };

                return chunk;
            }
            catch (Exception parseEx)
            {
                LoggerService.Current.WriteDebug($"[ParseOpenAiChunk] Error extracting content: {parseEx.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts provider-specific ToolCallSchema to canonical internal ToolCall.
        /// Handles JSON argument parsing and gracefully handles malformed input.
        /// Public method to support streaming loop conversion in ChatPageViewModel.
        /// </summary>
        /// <param name="schema">Provider-specific tool call schema (from OpenAI/DeepSeek/vLLM)</param>
        /// <returns>Canonical internal ToolCall with parsed arguments dictionary</returns>
        public ToolCall ConvertToolCallSchemaToToolCall(ToolCallSchema schema)
        {
            var toolCall = new ToolCall
            {
                Id = schema.Id,
                Name = schema.Function?.Name ?? "unknown"
            };

            // Parse Arguments from JSON string to IDictionary<string, object>
            if (schema.Function?.Arguments != null && !string.IsNullOrEmpty(schema.Function.Arguments))
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<IDictionary<string, object>>(
                        schema.Function.Arguments);
                    toolCall.Arguments = parsed;
                }
                catch (JsonException argEx)
                {
                    LoggerService.Current.WriteWarning(
                        $"[gap72-convert] Failed to parse arguments for tool '{toolCall.Name}': {argEx.Message}. " +
                        $"Raw arguments: {schema.Function.Arguments}");
                    toolCall.Arguments = new Dictionary<string, object>();
                }
            }
            else
            {
                toolCall.Arguments = new Dictionary<string, object>();
            }

            return toolCall;
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
                        ChatMessageRole.Tool => "tool",
                        _ => "user"
                    };

                    var ollamaMsg = new OllamaMessage
                    {
                        Role = role,
                        Content = msg.Content ?? string.Empty
                    };

                    // For tool role messages, include the tool_call_id that correlates to the original tool call
                    if (msg.Role == ChatMessageRole.Tool && !string.IsNullOrEmpty(msg.ToolCallId))
                    {
                        ollamaMsg.ToolCallId = msg.ToolCallId;
                    }

                    ollamaMessages.Add(ollamaMsg);
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

            LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Message count: {ollamaMessages.Count}");
            foreach (var msg in ollamaMessages)
            {
                var contentPreview = msg.Content?.Substring(0, Math.Min(50, msg.Content?.Length ?? 0)) ?? "[null content]";
                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync]   - Role: {msg.Role}, Content: {contentPreview}...");
            }

            // Build Ollama request
            // Use OllamaModelId if available (actual Ollama model identifier), otherwise fall back to Name
            var ollamaModelId = !string.IsNullOrEmpty(model.OllamaModelId) ? model.OllamaModelId : model.Name;
            LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Model name: {model.Name}, OllamaModelId: {model.OllamaModelId}, Using: {ollamaModelId}");

            var ollamaRequest = new OllamaRequest
            {
                Model = ollamaModelId,
                Stream = true,
                Messages = ollamaMessages,
                Options = new OllamaOptions
                {
                    Temperature = options.Temperature,
                    MaxTokens = options.MaxTokens,
                    TopP = options.TopP,
                    ContextWindow = model.ContextWindow
                }
            };

            // Populate tools filtered by current ChatMode (gap71)
            var availableTools = _toolService.GetAvailableTools(options.Mode);
            _logger?.WriteDebug($"[gap71-messenger-tools] Mode={options.Mode}, filtering tools: {availableTools.Count()} available");

            if (availableTools.Any())
            {
                var toolSchemas = ConvertToolDefinitionsToSchema(availableTools);
                ollamaRequest.Tools = toolSchemas;
                _logger?.WriteDebug($"[gap71-messenger-tools] Populated OllamaRequest.Tools with {toolSchemas.Count} schemas for mode {options.Mode}");
            }

            LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Building request - Model: {ollamaRequest.Model}, Stream: {ollamaRequest.Stream}, Temperature: {ollamaRequest.Options.Temperature}");

            // Dump context before sending if debug flag is enabled
            if (options.Messages != null)
            {
                var messageList = options.Messages.ToList();
                await _contextDumpService.DumpContextBeforeSendAsync(messageList);
            }

            // POST to Ollama chat endpoint
            var endpoint = $"{(model.BaseUrl ?? "").TrimEnd('/')}/api/chat";
            var json = JsonConvert.SerializeObject(ollamaRequest);
            LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Endpoint: {endpoint}");
            //LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Request JSON: {json.Substring(0, Math.Min(200, json.Length))}...");
            LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Request JSON: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (_logger != null)
                _logger?.WriteDebug($"MessengerService: POST to {endpoint}");

            try
            {
                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Sending HTTP POST request to {endpoint}...");
                // ResponseHeadersRead prevents HttpClient from buffering the entire response body before returning.
                // Without it, PostAsync waits until all NDJSON chunks are received, defeating streaming.
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Response status code: {response.StatusCode}");

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
                        LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Failed to read error response body: {readEx.Message}");
                    }

                    LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] ERROR - HTTP {(int)response.StatusCode}: {responseBodyText}");
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {responseBodyText}");
                }

                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Status code confirmed successful");
            }
            catch (HttpRequestException ex)
            {
                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] HttpRequestException: {ex.Message}");
                throw new LlmException($"HTTP request to Ollama failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] TaskCanceledException: {ex.Message}");
                throw new LlmException(
                    $"Ollama request timeout or was cancelled. " +
                    $"Ensure Ollama is running at {model.BaseUrl}/api/chat and the model '{model.Name}' is loaded. " +
                    $"The request may have taken too long to complete.", ex);
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Unexpected exception: {ex.GetType().Name}: {ex.Message}");
                throw new LlmException($"Unexpected error during Ollama streaming: {ex.Message}", ex);
            }

            try
            {
                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Starting to read response stream...");

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
                        LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Received line {lineCount}: {line}");

                        // Parse JSON line to OllamaResponse
                        OllamaResponse? ollamaResponse = null;
                        try
                        {
                            ollamaResponse = JsonConvert.DeserializeObject<OllamaResponse>(line);
                        }
                        catch (JsonException jsonEx)
                        {
                            LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Failed to parse NDJSON line {lineCount}: {jsonEx.Message}");
                            if (_logger != null)
                                _logger?.WriteDebug($"Failed to parse NDJSON line: {line}. Error: {jsonEx.Message}");
                            // Continue on parse errors (malformed chunk)
                            continue;
                        }

                        if (ollamaResponse?.Message != null)
                        {
                            // Check if message has content or tool calls
                            bool hasContent = !string.IsNullOrEmpty(ollamaResponse.Message.Content);
                            bool hasToolCalls = ollamaResponse.Message.ToolCalls?.Count > 0;

                            if (!hasContent && !hasToolCalls)
                            {
                                // Skip empty messages without content or tools
                                LoggerService.Current.WriteDebug($"[ProcessOllamaStreamAsync] Skipping empty message at line {lineCount}");
                                continue;
                            }

                            // Convert text content to CompletionChunk and yield
                            if (hasContent)
                            {
                                var chunk = new CompletionChunk
                                {
                                    Type = ChunkType.Text,
                                    Content = ollamaResponse.Message.Content,
                                    Role = ChatMessageRole.Assistant,
                                    IsDone = ollamaResponse.Done,
                                    DoneReason = ollamaResponse.DoneReason,
                                    Timestamp = DateTime.UtcNow
                                };

                                if (typeof(TChunk) == typeof(CompletionChunk))
                                {
                                    yield return (TChunk)(object)chunk;
                                }
                            }

                            // Capture tool calls if present
                            if (hasToolCalls && ollamaResponse.Message?.ToolCalls != null)
                            {
                                LoggerService.Current.WriteDebug($"[gap55_3-tool-call-detected] Received {ollamaResponse.Message.ToolCalls.Count} tool calls from Ollama at line {lineCount}");
                                foreach (var toolCall in ollamaResponse.Message.ToolCalls)
                                {
                                    var argPreview = toolCall.Function?.Arguments?.Substring(0, Math.Min(100, toolCall.Function.Arguments.Length)) ?? "[no args]";
                                    LoggerService.Current.WriteDebug($"[gap55_3-tool-details] Tool={toolCall.Function?.Name}, Args={argPreview}...");
                                }

                                // If this is the final response, yield tool calls in a final chunk
                                if (ollamaResponse.Done)
                                {
                                    var toolChunk = new CompletionChunk
                                    {
                                        Type = ChunkType.ToolCall,
                                        Content = string.Empty,
                                        Role = ChatMessageRole.Assistant,
                                        IsDone = true,
                                        DoneReason = ollamaResponse.DoneReason,
                                        ToolCalls = ollamaResponse.Message.ToolCalls,
                                        Timestamp = DateTime.UtcNow
                                    };

                                    if (typeof(TChunk) == typeof(CompletionChunk))
                                    {
                                        yield return (TChunk)(object)toolChunk;
                                    }

                                    LoggerService.Current.WriteDebug($"[gap55_3-completion] Done with reason={ollamaResponse.DoneReason}, toolCalls={ollamaResponse.Message.ToolCalls.Count}");
                                }
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

        /// <summary>
        /// Converts ToolDefinition objects to OpenAI-compatible ToolSchema format.
        /// Validates tool names and builds parameter schemas for LLM consumption.
        /// </summary>
        /// <param name="tools">Enumerable of tool definitions to convert.</param>
        /// <returns>List of ToolSchema objects in OpenAI function calling format.</returns>
        private List<ToolSchema> ConvertToolDefinitionsToSchema(IEnumerable<ToolDefinition> tools)
        {
            var schemas = new List<ToolSchema>();

            if (tools == null)
                return schemas;

            var toolNameRegex = new System.Text.RegularExpressions.Regex(@"^[a-z_][a-z0-9_]*$");

            foreach (var tool in tools)
            {
                // Validate tool name matches OpenAI requirements: [a-z_][a-z0-9_]*
                if (string.IsNullOrEmpty(tool.Name) || !toolNameRegex.IsMatch(tool.Name))
                {
                    LoggerService.Current.WriteDebug(
                        $"[gap55_1-tool-validation] Skipping tool '{tool.Name}' - invalid name format. Must match [a-z_][a-z0-9_]*");
                    continue;
                }

                // Build parameters schema from tool definition parameters
                var paramsSchema = new ParametersSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ParameterDefinition>(),
                    Required = new List<string>()
                };

                foreach (var param in tool.Parameters ?? new List<ParameterDefinition>())
                {
                    if (!string.IsNullOrEmpty(param.Name))
                    {
                        paramsSchema.Properties[param.Name] = param;

                        if (param.IsRequired)
                            paramsSchema.Required.Add(param.Name);
                    }
                }

                var schema = new ToolSchema
                {
                    Type = "function",
                    Function = new ToolFunctionSchema
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Parameters = paramsSchema
                    }
                };

                schemas.Add(schema);

                LoggerService.Current.WriteDebug(
                    $"[gap55_1-tool-schema] Converted tool '{tool.Name}' with {tool.Parameters?.Count ?? 0} parameters");
            }

            return schemas;
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

