using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for generating error fingerprints and tracking recurring errors.
    /// Supports deduplication via fingerprinting and manual error grouping.
    /// </summary>
    public interface IErrorFingerprintService
    {
        /// <summary>
        /// Generates a fingerprint from a parsed stack trace.
        /// Fingerprint is based on exception type and top 3 stack frames.
        /// </summary>
        /// <param name="parseResult">The parsed stack trace result.</param>
        /// <returns>An ErrorFingerprint containing the hash and frame summaries.</returns>
        Task<ErrorFingerprint> GenerateFingerprintAsync(ParseResult parseResult);

        /// <summary>
        /// Records an error occurrence in the session cache.
        /// Increments occurrence count if error is already known.
        /// </summary>
        /// <param name="fingerprint">The error fingerprint to record.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordErrorAsync(ErrorFingerprint fingerprint);

        /// <summary>
        /// Gets the occurrence count for a specific error fingerprint in the current session.
        /// </summary>
        /// <param name="fingerprint">The fingerprint string to query.</param>
        /// <returns>The number of occurrences (0 if unknown error).</returns>
        Task<int> GetOccurrenceCountAsync(string fingerprint);

        /// <summary>
        /// Checks if an error fingerprint is known (has been recorded before in this session).
        /// </summary>
        /// <param name="fingerprint">The fingerprint string to check.</param>
        /// <returns>True if the error has been seen before, false if new.</returns>
        Task<bool> GetIsKnownErrorAsync(string fingerprint);

        /// <summary>
        /// Manually groups two errors together, marking them as related.
        /// Grouping is bidirectional: if A is grouped with B, then B is grouped with A.
        /// </summary>
        /// <param name="fingerprint1">The first fingerprint.</param>
        /// <param name="fingerprint2">The second fingerprint.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GroupErrorsAsync(string fingerprint1, string fingerprint2);

        /// <summary>
        /// Retrieves all fingerprints manually grouped with a given fingerprint.
        /// </summary>
        /// <param name="fingerprint">The fingerprint to query for grouped errors.</param>
        /// <returns>A collection of grouped fingerprint strings.</returns>
        Task<IReadOnlyCollection<string>> GetGroupedFingerprintsAsync(string fingerprint);
    }
}
