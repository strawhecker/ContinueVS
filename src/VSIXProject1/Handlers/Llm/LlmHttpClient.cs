using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Llm
{
    internal static class LlmHttpClient
    {
        private static readonly HttpClient _client = new HttpClient();

        /// <summary>
        /// Sends a completion request to the configured LLM provider and returns the response text.
        /// </summary>
        internal static async Task<string> CompleteAsync(
            LlmModelConfig model,
            string prompt,
            CancellationToken cancellationToken)
        {
            var provider = (model.Provider ?? "").ToLowerInvariant();
            if (provider == "anthropic")
                return await AnthropicCompleteAsync(model, prompt, cancellationToken).ConfigureAwait(false);

            return await OpenAiCompleteAsync(model, prompt, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> OpenAiCompleteAsync(
            LlmModelConfig model,
            string prompt,
            CancellationToken cancellationToken)
        {
            var baseUrl = (model.ApiBase ?? "https://api.openai.com/v1").TrimEnd('/');
            var url = baseUrl + "/chat/completions";

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(model.ApiKey))
                request.Headers.Add("Authorization", "Bearer " + model.ApiKey);

            var bodyJson = JsonConvert.SerializeObject(new
            {
                model = model.Model,
                messages = new[] { new { role = "user", content = prompt } },
                stream = false
            });
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using (var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jObj = JObject.Parse(responseJson);
                return jObj["choices"]?[0]?["message"]?["content"]?.Value<string>() ?? "";
            }
        }

        private static async Task<string> AnthropicCompleteAsync(
            LlmModelConfig model,
            string prompt,
            CancellationToken cancellationToken)
        {
            var baseUrl = (model.ApiBase ?? "https://api.anthropic.com").TrimEnd('/');
            var url = baseUrl + "/v1/messages";

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(model.ApiKey))
                request.Headers.Add("x-api-key", model.ApiKey);

            request.Headers.Add("anthropic-version", "2023-06-01");

            var bodyJson = JsonConvert.SerializeObject(new
            {
                model = model.Model,
                max_tokens = 1024,
                messages = new[] { new { role = "user", content = prompt } }
            });
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using (var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jObj = JObject.Parse(responseJson);
                return jObj["content"]?[0]?["text"]?.Value<string>() ?? "";
            }
        }

        private static async Task OpenAiStreamChatAsync(
            LlmModelConfig model,
            JArray messages,
            Action<string> onChunk,
            CancellationToken cancellationToken)
        {
            var baseUrl = (model.ApiBase ?? "https://api.openai.com/v1").TrimEnd('/');
            var url = baseUrl + "/chat/completions";

            System.Diagnostics.Debug.WriteLine($"[b24-OPENAI-CONFIG] Provider={model.Provider}, Model={model.Model}, ApiBase={model.ApiBase ?? "(null)"}");
            System.Diagnostics.Debug.WriteLine($"[b24-OPENAI-URL] Constructed URL={url}");
            System.Diagnostics.Debug.WriteLine($"[b24-OPENAI-MESSAGES] Message count={messages.Count}");

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(model.ApiKey))
                request.Headers.Add("Authorization", "Bearer " + model.ApiKey);

            var bodyJson = JsonConvert.SerializeObject(new
            {
                model = model.Model,
                messages = messages,
                stream = true
            });
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                {
                    while (true)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        if (line == "" || !line.StartsWith("data: ")) continue;
                        var data = line.Substring(6).Trim();
                        if (data == "[DONE]") break;
                        try
                        {
                            var jObj = JObject.Parse(data);
                            var chunk = jObj["choices"]?[0]?["delta"]?["content"]?.Value<string>();
                            if (!string.IsNullOrEmpty(chunk))
                                onChunk(chunk!);
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        private static async Task AnthropicStreamChatAsync(
            LlmModelConfig model,
            JArray messages,
            Action<string> onChunk,
            CancellationToken cancellationToken)
        {
            var baseUrl = (model.ApiBase ?? "https://api.anthropic.com").TrimEnd('/');
            var url = baseUrl + "/v1/messages";

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(model.ApiKey))
                request.Headers.Add("x-api-key", model.ApiKey);

            request.Headers.Add("anthropic-version", "2023-06-01");

            var bodyJson = JsonConvert.SerializeObject(new
            {
                model = model.Model,
                max_tokens = 1024,
                messages = messages,
                stream = true
            });
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                {
                    while (true)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        if (line == "" || !line.StartsWith("data: ")) continue;
                        var data = line.Substring(6).Trim();
                        try
                        {
                            var jObj = JObject.Parse(data);
                            var type = jObj["type"]?.Value<string>();
                            if (type == "content_block_delta")
                            {
                                var chunk = jObj["delta"]?["text"]?.Value<string>();
                                if (!string.IsNullOrEmpty(chunk))
                                    onChunk(chunk!);
                            }
                            else if (type == "message_stop")
                            {
                                break;
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        private static async Task OllamaStreamChatAsync(
            LlmModelConfig model,
            JArray messages,
            Action<string> onChunk,
            CancellationToken cancellationToken)
        {
            var baseUrl = (model.ApiBase ?? "http://localhost:11434").TrimEnd('/');
            var url = baseUrl + "/api/chat";

            System.Diagnostics.Debug.WriteLine($"[b24-OLLAMA-CONFIG] Provider={model.Provider}, Model={model.Model}, ApiBase={model.ApiBase ?? "(null)"}");
            System.Diagnostics.Debug.WriteLine($"[b24-OLLAMA-URL] Constructed URL={url}");
            System.Diagnostics.Debug.WriteLine($"[b24-OLLAMA-MESSAGES] Message count={messages.Count}");

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            var bodyJson = JsonConvert.SerializeObject(new
            {
                model = model.Model,
                messages = messages,
                stream = true
            });
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                {
                    while (true)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        if (line == "") continue;
                        try
                        {
                            var jObj = JObject.Parse(line);
                            var chunk = jObj["message"]?["content"]?.Value<string>();
                            if (!string.IsNullOrEmpty(chunk))
                                onChunk(chunk!);

                            // Check if this is the final message
                            var done = jObj["done"]?.Value<bool>() ?? false;
                            if (done) break;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[b24-OLLAMA-PARSE-ERROR] Failed to parse Ollama response line: {ex.Message}");
                        }
                    }
                }
            }
        }

            internal static async Task StreamChatAsync(
                LlmModelConfig model,
                JArray messages,
                Action<string> onChunk,
                CancellationToken cancellationToken)
            {
                var provider = (model.Provider ?? "").ToLowerInvariant();
                System.Diagnostics.Debug.WriteLine($"[b24-STREAM-DISPATCHER] Routing to provider={provider}");

                if (provider == "anthropic")
                    await AnthropicStreamChatAsync(model, messages, onChunk, cancellationToken).ConfigureAwait(false);
                else if (provider == "ollama")
                    await OllamaStreamChatAsync(model, messages, onChunk, cancellationToken).ConfigureAwait(false);
                else
                    await OpenAiStreamChatAsync(model, messages, onChunk, cancellationToken).ConfigureAwait(false);
            }
    }
}
