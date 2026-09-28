using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// gap98: Real implementation of <see cref="ITokenizeService"/> that calls the LLM server's
    /// /tokenize endpoint.
    ///
    /// The request is a one-off, out-of-band counting call: the assembled context is sent as the
    /// "prompt" and the returned token count is consumed by the caller. The call NEVER writes into
    /// the session, chat history, or any store — it is purely a counting request, so it cannot
    /// hang the UI or trigger a new response.
    /// </summary>
    public sealed class TokenizeService : ITokenizeService
    {
        private readonly HttpClient _httpClient;

        public TokenizeService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <inheritdoc />
        public async Task<int?> CountTokensAsync(ModelInfo? model, IEnumerable<ChatMessage> context, CancellationToken ct = default)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.BaseUrl))
            {
                LoggerService.Current.WriteDebug("[TokenizeService] No model or BaseUrl available; returning null token count");
                return null;
            }

            // Build a plain-text prompt from the context. This is the same conceptual payload as
            // the /tokenize curl: a "model" plus a "prompt" string. Out-of-band only — nothing here
            // is written to the session or history.
            var promptBuilder = new StringBuilder();
            if (context != null)
            {
                foreach (var msg in context)
                {
                    if (msg.IsDeleted)
                        continue;
                    var roleLabel = msg.Role switch
                    {
                        ChatMessageRole.User => "user",
                        ChatMessageRole.Assistant => "assistant",
                        ChatMessageRole.System => "system",
                        ChatMessageRole.Tool => "tool",
                        _ => "user"
                    };
                    promptBuilder.Append(roleLabel).Append(": ").Append(msg.Content ?? string.Empty).Append("\n");
                }
            }

            if (promptBuilder.Length == 0)
            {
                return 0;
            }

            try
            {
                var endpoint = $"{(model.BaseUrl ?? "").TrimEnd('/')}/tokenize";
                var requestObj = new Dictionary<string, object>
                {
                    { "model", model.Name ?? model.Id ?? "unknown" },
                    { "prompt", promptBuilder.ToString() }
                };

                var json = JsonConvert.SerializeObject(requestObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

                // Add API key header if provided (some vLLM instances don't require it)
                if (!string.IsNullOrWhiteSpace(model.ApiKey) && model.ApiKey != "not-required")
                {
                    request.Headers.Add("Authorization", $"Bearer {model.ApiKey}");
                }

                LoggerService.Current.WriteDebug($"[TokenizeService] POST token count request to {endpoint} (out-of-band, no history write)");

                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cancellation.CancelAfter(TimeSpan.FromSeconds(10));

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellation.Token);
                }
                catch (TaskCanceledException tce)
                {
                    LoggerService.Current.WriteWarning($"[TokenizeService] tokenize request cancelled/timed out: {tce.Message}");
                    return null;
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        LoggerService.Current.WriteDebug($"[TokenizeService] tokenize HTTP {(int)response.StatusCode}");
                        return null;
                    }

                    var body = await response.Content.ReadAsStringAsync();
                    return TryParseTokenCount(body);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteWarning($"[TokenizeService] tokenize failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Best-effort parsing of a /tokenize response. vLLM returns <c>{"count": N, "token_ids": [...]}</c>;
        /// fall back to the count of token_ids when present.
        /// </summary>
        private static int? TryParseTokenCount(string body)
        {
            try
            {
                var obj = JObject.Parse(body);
                var count = obj["count"]?.Value<int?>();
                if (count.HasValue)
                    return count.Value;

                var tokenIds = obj["token_ids"] as JArray;
                if (tokenIds != null)
                    return tokenIds.Count;

                var tokens = obj["tokens"] as JArray;
                if (tokens != null)
                    return tokens.Count;
            }
            catch (JsonException)
            {
                // fall through
            }
            return null;
        }
    }
}
