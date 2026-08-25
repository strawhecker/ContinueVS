using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a transaction log of all source code changes applied during a debug session or phase.
    /// Supports per-change rollback; earlier changes survive failure of later ones.
    /// </summary>
    public class ChangeStack
    {
        /// <summary>
        /// Unique identifier for this change stack instance.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Complete history of all changes applied to this stack, in chronological order.
        /// </summary>
        [JsonProperty("history")]
        public List<CodeChange> History { get; set; } = new List<CodeChange>();

        /// <summary>
        /// Set of ChangeIds that have been successfully applied.
        /// Used to track which changes are currently active on disk.
        /// </summary>
        [JsonProperty("appliedChanges")]
        public List<string> AppliedChanges { get; set; } = new List<string>();

        /// <summary>
        /// Timestamp when this change stack was created.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Records a new change and its baseline in this stack's history.
        /// Does not modify the applied changes list; application is confirmed separately.
        /// </summary>
        /// <param name="change">The CodeChange to record.</param>
        public void RecordChange(CodeChange change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            History.Add(change);
        }

        /// <summary>
        /// Marks a change as successfully applied to disk.
        /// </summary>
        /// <param name="changeId">The ID of the change that was applied.</param>
        public void MarkAsApplied(string changeId)
        {
            if (string.IsNullOrEmpty(changeId))
            {
                throw new ArgumentException("Change ID cannot be null or empty.", nameof(changeId));
            }

            if (!AppliedChanges.Contains(changeId))
            {
                AppliedChanges.Add(changeId);
            }
        }

        /// <summary>
        /// Removes a change from the applied list (after rollback).
        /// </summary>
        /// <param name="changeId">The ID of the change to unmark.</param>
        public void UnmarkAsApplied(string changeId)
        {
            if (string.IsNullOrEmpty(changeId))
            {
                throw new ArgumentException("Change ID cannot be null or empty.", nameof(changeId));
            }

            AppliedChanges.Remove(changeId);
        }

        /// <summary>
        /// Retrieves the complete change history in chronological order.
        /// </summary>
        /// <returns>A copy of the history list.</returns>
        public List<CodeChange> GetChangeHistory()
        {
            return new List<CodeChange>(History);
        }

        /// <summary>
        /// Retrieves the list of currently applied change IDs.
        /// </summary>
        /// <returns>A copy of the applied changes list.</returns>
        public List<string> GetAppliedChanges()
        {
            return new List<string>(AppliedChanges);
        }

        /// <summary>
        /// Finds a change in history by its ID.
        /// </summary>
        /// <param name="changeId">The ID to search for.</param>
        /// <returns>The CodeChange if found, otherwise null.</returns>
        public CodeChange? FindChangeById(string changeId)
        {
            if (string.IsNullOrEmpty(changeId))
            {
                return null;
            }

            return History.FirstOrDefault(c => c.ChangeId == changeId);
        }

        /// <summary>
        /// Gets all changes that come after the specified change ID in the history.
        /// Used to determine what needs to be rolled back in a cascade rollback.
        /// </summary>
        /// <param name="changeId">The change ID to use as the pivot point.</param>
        /// <returns>List of changes that come after the specified change, in order.</returns>
        public List<CodeChange> GetChangesAfter(string changeId)
        {
            if (string.IsNullOrEmpty(changeId))
            {
                return new List<CodeChange>(History);
            }

            var index = History.FindIndex(c => c.ChangeId == changeId);
            if (index < 0)
            {
                return new List<CodeChange>();
            }

            return History.Skip(index + 1).ToList();
        }
    }
}
