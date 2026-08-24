using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a recorded occurrence of an error, including occurrence count and manual groupings.
    /// </summary>
    public class ErrorOccurrence
    {
        /// <summary>
        /// The error fingerprint data.
        /// </summary>
        [JsonProperty("errorFingerprint")]
        public ErrorFingerprint ErrorFingerprint { get; private set; }

        /// <summary>
        /// Number of times this error has been recorded in the session.
        /// </summary>
        [JsonProperty("occurrenceCount")]
        public int OccurrenceCount { get; private set; }

        /// <summary>
        /// Timestamp of the last occurrence.
        /// </summary>
        [JsonProperty("lastOccurrenceTime")]
        public DateTime LastOccurrenceTime { get; private set; }

        /// <summary>
        /// Set of fingerprints manually grouped with this error.
        /// </summary>
        [JsonProperty("groupedFingerprints")]
        public HashSet<string> GroupedFingerprints { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ErrorOccurrence class.
        /// </summary>
        /// <param name="errorFingerprint">The error fingerprint data.</param>
        public ErrorOccurrence(ErrorFingerprint errorFingerprint)
        {
            ErrorFingerprint = errorFingerprint ?? throw new ArgumentNullException(nameof(errorFingerprint));
            OccurrenceCount = 1;
            LastOccurrenceTime = DateTime.UtcNow;
            GroupedFingerprints = new HashSet<string>();
        }

        /// <summary>
        /// Increments the occurrence count and updates the last occurrence time.
        /// </summary>
        public void IncrementCount()
        {
            OccurrenceCount++;
            LastOccurrenceTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds a fingerprint to the grouped fingerprints set.
        /// </summary>
        /// <param name="fingerprint">The fingerprint to group with this error.</param>
        public void AddGroupedFingerprint(string fingerprint)
        {
            if (!string.IsNullOrEmpty(fingerprint))
            {
                GroupedFingerprints.Add(fingerprint);
            }
        }

        /// <summary>
        /// Checks if a fingerprint is in the grouped fingerprints set.
        /// </summary>
        /// <param name="fingerprint">The fingerprint to check.</param>
        /// <returns>True if the fingerprint is grouped, false otherwise.</returns>
        public bool IsGroupedWith(string fingerprint)
        {
            return !string.IsNullOrEmpty(fingerprint) && GroupedFingerprints.Contains(fingerprint);
        }

        /// <summary>
        /// Returns a string representation of the occurrence.
        /// </summary>
        public override string ToString()
        {
            return $"{ErrorFingerprint} | Occurrences: {OccurrenceCount}";
        }
    }
}
