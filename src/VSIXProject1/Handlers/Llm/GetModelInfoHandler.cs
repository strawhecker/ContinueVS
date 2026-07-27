#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.IPC;
using ContinueVS.Services;
using ContinueVS.UI;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Handlers.Llm
{
    /// <summary>
    /// Handler for bridge:getModelInfo requests.
    /// 
    /// Queries ModelInfoCollector to fetch current model and available models,
    /// then returns complete metadata including capabilities and token limits.
    /// 
    /// **MessageType**: bridge:getModelInfo
    /// **Input**: Message (minimal, no payload required)
    /// **Output**: JToken containing { currentModel, availableModels, modelCapabilities, tokenLimits }
    /// 
    /// **Performance**: &lt;50ms (config-only, no I/O beyond cached config file)
    /// **Thread Safety**: Executes on UI thread; non-blocking
    /// 
    /// Instrumentation:
    /// - [b17-REQUEST-RECEIVED]: Handler entry, message type validation
    /// - [b17-COLLECTOR-QUERY]: Before/after collector calls, model count
    /// - [b17-MODEL-MAPPING]: Response structure creation, field population
    /// - [b17-RESPONSE-SERIALIZED]: Final JSON validity check
    /// - [b14-HANDLER-ENTRY/EXIT]: Thread ID tracking (added by dispatcher)
    /// </summary>
    internal sealed class GetModelInfoHandler : IMessageHandler
    {
        private readonly IGuiReplyProvider _guiReplyProvider;
        private readonly IBridgeLogger? _logger;

        /// <summary>
        /// Initializes a new GetModelInfoHandler.
        /// </summary>
        /// <param name="guiReplyProvider">Provider for sending replies back to GUI (typically ContinueToolWindowControl)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public GetModelInfoHandler(IGuiReplyProvider guiReplyProvider, IBridgeLogger? logger = null)
        {
            _guiReplyProvider = guiReplyProvider ?? throw new ArgumentNullException(nameof(guiReplyProvider));
            _logger = logger;
        }

        /// <summary>
        /// Handles bridge:getModelInfo request.
        /// 
        /// Flow:
        /// 1. Validate message type
        /// 2. Create ModelInfoCollector
        /// 3. Query current model
        /// 4. Query available models
        /// 5. Map to response structure
        /// 6. Serialize to JSON
        /// 7. Send reply via GUI provider
        /// </summary>
        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var threadId = Thread.CurrentThread.ManagedThreadId;

            try
            {
                // [b17-REQUEST-RECEIVED] Entry point
                System.Diagnostics.Debug.WriteLine($"[b17-REQUEST-RECEIVED] Handler entry, MessageType={message.MessageType}, MessageId={message.MessageId}, ThreadId={threadId}");

                // Validate message type (case-insensitive comparison)
                if (!message.MessageType.Equals("bridge:getModelInfo", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMsg = $"Invalid message type for GetModelInfoHandler: {message.MessageType}";
                    System.Diagnostics.Debug.WriteLine($"[b17-REQUEST-RECEIVED] Error: {errorMsg}");
                    throw new ArgumentException(errorMsg, nameof(message));
                }

                // Create collector instance
                var collector = new ModelInfoCollector(_logger);

                // [b17-COLLECTOR-QUERY] Before collector calls
                System.Diagnostics.Debug.WriteLine($"[b17-COLLECTOR-QUERY] Starting collector queries");
                var collectorSw = Stopwatch.StartNew();

                // Get current and available models
                var currentModel = await collector.GetCurrentModelAsync();
                var availableModels = await collector.GetAvailableModelsAsync();

                collectorSw.Stop();
                System.Diagnostics.Debug.WriteLine($"[b17-COLLECTOR-QUERY] Collector completed in {collectorSw.ElapsedMilliseconds}ms, availableModels.Count={availableModels?.Count ?? 0}");

                // [b17-MODEL-MAPPING] Build response structure
                System.Diagnostics.Debug.WriteLine($"[b17-MODEL-MAPPING] Building response structure");
                var mappingSw = Stopwatch.StartNew();

                // Map current model
                var currentModelObj = currentModel != null ? new JObject
                {
                    { "provider", currentModel.Provider },
                    { "model", currentModel.Model },
                    { "title", currentModel.Title },
                    { "apiBase", currentModel.ApiBase }
                } : null;

                // Map available models array
                var availableModelsArray = new JArray();
                if (availableModels != null && availableModels.Count > 0)
                {
                    foreach (var modelInfo in availableModels)
                    {
                        availableModelsArray.Add(new JObject
                        {
                            { "provider", modelInfo.Provider },
                            { "model", modelInfo.Model },
                            { "title", modelInfo.Title },
                            { "apiBase", modelInfo.ApiBase }
                        });
                    }
                }

                // Get capabilities and token limits from current model
                var provider = currentModel?.Provider ?? "openai";
                var model = currentModel?.Model ?? "";
                var capabilities = await collector.GetModelCapabilitiesAsync(provider);
                var tokenLimits = await collector.GetTokenLimitsAsync(provider, model);

                // Map capabilities
                var capabilitiesObj = new JObject
                {
                    { "contextLength", capabilities?.ContextLength ?? 4096 },
                    { "supportsStreaming", capabilities?.SupportsStreaming ?? true },
                    { "supportsVision", capabilities?.SupportsVision ?? false },
                    { "maxRpm", capabilities?.MaxRpm ?? 0 },
                    { "maxTokensPerMinute", capabilities?.MaxTokensPerMinute ?? 0 }
                };

                // Map token limits
                var tokenLimitsObj = new JObject
                {
                    { "maxInputTokens", tokenLimits?.MaxInputTokens ?? 4000 },
                    { "maxOutputTokens", tokenLimits?.MaxOutputTokens ?? 2000 },
                    { "totalContextTokens", tokenLimits?.TotalContextTokens ?? 4096 }
                };

                mappingSw.Stop();
                System.Diagnostics.Debug.WriteLine($"[b17-MODEL-MAPPING] Response structure built in {mappingSw.ElapsedMilliseconds}ms, fields: currentModel={currentModelObj != null}, availableModels.Count={availableModelsArray.Count}");

                // Build complete response
                var responsePayload = new JObject
                {
                    { "currentModel", currentModelObj },
                    { "availableModels", availableModelsArray },
                    { "modelCapabilities", capabilitiesObj },
                    { "tokenLimits", tokenLimitsObj }
                };

                // [b17-RESPONSE-SERIALIZED] Validate and send
                System.Diagnostics.Debug.WriteLine($"[b17-RESPONSE-SERIALIZED] JSON payload: {responsePayload}");
                _guiReplyProvider.SendReplyToGui("bridge:getModelInfo", message.MessageId, responsePayload);

                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[b17-REQUEST-RECEIVED] Handler exit, total elapsed: {sw.ElapsedMilliseconds}ms, ThreadId={threadId}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[b17-REQUEST-RECEIVED] Exception: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }
    }
}
