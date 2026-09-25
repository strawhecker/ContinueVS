#nullable enable

using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Auditable record of a retire_from_context toggle operation (context pruning &amp;
    /// error supersession). These records are appended (never rewritten) to the idempotent
    /// reference file ~/.continueVS/retirements/retirements.jsonl, so the full trail is
    /// preserved even though the message itself is only toggled via the shared
    /// <see cref="ChatMessage.IsDeleted"/> tombstone (no new flag, no in-context pollution).
    ///
    /// The message's on-message tombstone is a bare bool and cannot carry the retirement
    /// metadata (reason, replaced_by_id, verbatim bytes); that metadata lives HERE, keyed by
    /// message id, so "who retired it / why / what replaced it / what it said" is always
    /// recoverable from the reference file alone.
    /// </summary>
    public class RetirementRecord
    {
        /// <summary>
        /// Tagged delta kind: "retire" when the message moved active → retired, or "unretire"
        /// when it moved retired → active. The reference file is append-only; a full toggle
        /// history accumulates as ordered lines (per the gap83 delta-log philosophy).
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "retire";

        /// <summary>
        /// The session the message belongs to, used to scope/route records.
        /// </summary>
        [JsonProperty("sessionId")]
        public string? SessionId { get; set; }

        /// <summary>
        /// The id of the message that was toggled.
        /// </summary>
        [JsonProperty("messageId")]
        public string? MessageId { get; set; }

        /// <summary>
        /// The reason supplied to the retire_from_context call — the state being moved to,
        /// e.g. "superseded by &lt;id&gt;", "redundant", "factually wrong",
        /// "pointer served its purpose".
        /// </summary>
        [JsonProperty("reason")]
        public string? Reason { get; set; }

        /// <summary>
        /// Optional id of the corrected/consolidated successor response, when one exists.
        /// </summary>
        [JsonProperty("replacedById")]
        public string? ReplacedById { get; set; }

        /// <summary>
        /// UTC timestamp of the toggle operation.
        /// </summary>
        [JsonProperty("timestampUtc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Verbatim JSON snapshot of the message at retire time, so the original content is
        /// preserved byte-for-byte in the reference file regardless of any later mutable
        /// changes to the live message (the tombstone only flips IsDeleted).
        /// </summary>
        [JsonProperty("verbatimJson")]
        public string? VerbatimJson { get; set; }
    }
}
