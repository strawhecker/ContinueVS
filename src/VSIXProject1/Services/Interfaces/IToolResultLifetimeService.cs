#nullable enable

using System.Collections.Generic;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// gap80_1: Tool-result lifetime engine — decides when an in-flight tool-result message is
    /// superseded, invalidated, or stale, and what is never allowed to happen (silent time-based
    /// removal). Operates on the in-memory tool-result surface (the current turn's Messages role
    /// Tool entries), because tool results are NOT persisted to the session (gap73).
    ///
    /// Distinct from <see cref="IToolCallSnapshotStore"/> (gap80 version retention for read
    /// dedup). This service applies the coverage-aware supersession, mutation-invalidates-priors,
    /// directory-snapshot staleness, and failed-mutation pivot rules to the collected results
    /// before they are re-sent to the LLM.
    /// </summary>
    public interface IToolResultLifetimeService
    {
        /// <summary>
        /// Applies lifetime rules to the given tool-result messages, mutating each message's
        /// <see cref="ChatMessage.FreshnessState"/> / <see cref="ChatMessage.IsDeleted"/> /
        /// <see cref="ChatMessage.Content"/> in place. Called with the results collected for the
        /// NEXT LLM iteration AFTER this iteration's tools have been executed.
        /// </summary>
        /// <param name="toolResults">The tool-result messages collected for the next iteration.</param>
        void ApplyLifetime(List<ChatMessage> toolResults);
    }
}
