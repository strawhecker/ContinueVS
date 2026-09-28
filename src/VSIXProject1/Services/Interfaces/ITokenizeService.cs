using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// gap98: Performs an out-of-band token count against the LLM server's /tokenize endpoint.
    ///
    /// The request sends the assembled context as the "prompt" (same shape as the /tokenize curl:
    /// <c>{"model": "...", "prompt": "..."}</c>) but this call is one-off and out-of-band: it
    /// NEVER becomes part of the chat history, never starts a tool, never asks a question, and
    /// never triggers a new response. The caller consumes the returned token count only.
    /// </summary>
    public interface ITokenizeService
    {
        /// <summary>
        /// Requests a token count for the given context from the LLM server's /tokenize endpoint.
        /// </summary>
        /// <param name="model">The active model; its BaseUrl and Name are used for the request.</param>
        /// <param name="context">The context (messages or prompt text) to count tokens for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The number of tokens as reported by the server, or null when unavailable.</returns>
        Task<int?> CountTokensAsync(ModelInfo? model, IEnumerable<ChatMessage> context, CancellationToken ct = default);
    }
}
