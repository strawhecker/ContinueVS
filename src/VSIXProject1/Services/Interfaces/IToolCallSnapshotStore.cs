#nullable enable

using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Verdict returned for a single read invocation keyed by file path (gap80).
    /// </summary>
    public enum ReadDedupVerdict
    {
        /// <summary>
        /// This is the first read of the path — store as v1 and return normally.
        /// </summary>
        FirstRead,

        /// <summary>
        /// A prior version of this path was already read. The new read's content differs,
        /// so it is retained as a new version (v2, v3, ...) while prior versions are marked
        /// Deleted for the LLM but remain accessible for version retention.
        /// </summary>
        NewVersionRetained,

        /// <summary>
        /// A prior version of this path was already read and this read yields identical
        /// content. The duplicate request + response is marked Deleted for the LLM; the
        /// retained prior version stays accessible.
        /// </summary>
        DuplicateSoftDeleted
    }

    /// <summary>
    /// Result of evaluating a read tool call against the version-retaining snapshot store.
    /// </summary>
    public class ReadDedupDecision
    {
        public ReadDedupVerdict Verdict { get; set; }

        /// <summary>
        /// Canonical path key for this read.
        /// </summary>
        public string? KeyPath { get; set; }

        /// <summary>
        /// The retained version number for this path (1 for first read, incremented per new version).
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// True when the caller should soft-delete this request and its response (dedup/NewVersion).
        /// </summary>
        public bool SuppressFromLlm => Verdict == ReadDedupVerdict.DuplicateSoftDeleted ||
                                       Verdict == ReadDedupVerdict.NewVersionRetained;
    }

    /// <summary>
    /// Version-retaining read snapshot store (gap80).
    ///
    /// Same path, first read → store version v1. Same path read again → mark v1 request +
    /// response Deleted for the LLM while retaining v1 as an accessible past version alongside
    /// the new v2. Reuses the ChangeBaseline snapshot shape (gap29_8_2) for byte snapshots the
    /// future restore path will need; we do not build a parallel snapshot system.
    /// </summary>
    public interface IToolCallSnapshotStore
    {
        /// <summary>
        /// Evaluates a read invocation and returns a decision with the retention version.
        /// The caller then:
        ///   - if <see cref="ReadDedupDecision.SuppressFromLlm"/> is true, marks the outgoing
        ///     request + its result message Deleted (excluded at serialize time, retained bytes).
        ///   - otherwise (FirstRead) records the content snapshot as v1.
        /// </summary>
        ReadDedupDecision EvaluateRead(ToolCall toolCall, string retrievedContent);

        /// <summary>
        /// Called after the read tool executes. When the verdict was FirstRead (or NewVersionRetained)
        /// it captures the byte snapshot using a ChangeBaseline so the future restore path has it.
        /// </summary>
        void RecordSnapshot(string keyPath, string content);

        /// <summary>
        /// Clears all retained versions for a new user action (per-action scope).
        /// </summary>
        void Reset();
    }
}
