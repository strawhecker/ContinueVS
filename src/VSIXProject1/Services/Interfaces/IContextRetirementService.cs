#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Mediates the retire_from_context tool: it reuses the shared
    /// <see cref="ChatMessage.IsDeleted"/> tombstone (via ISessionService.SoftDeleteMessageAsync /
    /// UndeleteMessageAsync — no new flag, no new exclusion code) for the active↔retired toggle,
    /// and persists an auditable <see cref="RetirementRecord"/> to the append-only reference file
    /// so the trail (reason, replaced_by_id, verbatim content) is never lost even though the
    /// message is no longer pulled into context.
    /// </summary>
    public interface IContextRetirementService
    {
        /// <summary>
        /// Retires a message: sets IsDeleted = true on it and appends a "retire" record.
        /// </summary>
        /// <param name="sessionId">The id of the current session.</param>
        /// <param name="messageId">The id of the message to retire.</param>
        /// <param name="reason">Short justification supplied by the LLM (the state being moved to).</param>
        /// <param name="replacedById">Optional id of the successor response that supersedes it.</param>
        /// <param name="verbatimJson">Verbatim JSON snapshot of the message to preserve in the reference file.</param>
        Task RetireAsync(string sessionId, string messageId, string reason, string? replacedById, string? verbatimJson);

        /// <summary>
        /// Un-retires a message: clears IsDeleted = false and appends an "unretire" record.
        /// </summary>
        Task UnretireAsync(string sessionId, string messageId, string reason);

        /// <summary>
        /// Gets whether the given message currently has a live (retired) state per the most
        /// recent record: true when the newest record for that message is a "retire".
        /// </summary>
        Task<bool> IsRetiredAsync(string sessionId, string messageId);

        /// <summary>
        /// Gets all retirement records for a session, oldest to newest (full toggle history).
        /// </summary>
        Task<IReadOnlyList<RetirementRecord>> GetRecordsAsync(string sessionId);
    }
}
