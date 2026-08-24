using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Service for generating and tracking error fingerprints.
    /// Maintains a session-scoped cache of error occurrences and supports manual grouping.
    /// </summary>
    public class ErrorFingerprintService : IErrorFingerprintService
    {
        private readonly ConcurrentDictionary<string, ErrorOccurrence> _errorCache;
        private readonly object _groupingLock = new object();

        /// <summary>
        /// Initializes a new instance of the ErrorFingerprintService class.
        /// </summary>
        public ErrorFingerprintService()
        {
            _errorCache = new ConcurrentDictionary<string, ErrorOccurrence>();
        }

        /// <summary>
        /// Generates a fingerprint from a parsed stack trace.
        /// Uses SHA256 hash of: ExceptionType | Frame0.Method | Frame0.File | Frame1.Method | Frame1.File | Frame2.Method | Frame2.File
        /// </summary>
        public async Task<ErrorFingerprint> GenerateFingerprintAsync(ParseResult parseResult)
        {
            if (parseResult == null)
                throw new ArgumentNullException(nameof(parseResult));

            var exceptionType = parseResult.Frames.FirstOrDefault()?.ExceptionType ?? "Unknown";
            var frames = parseResult.Frames.Take(3).ToList();

            // Build the fingerprint input string
            var sb = new StringBuilder();
            sb.Append(exceptionType);

            foreach (var frame in frames)
            {
                sb.Append("|");
                sb.Append(frame?.MethodName ?? "");
                sb.Append("|");
                sb.Append(frame?.FilePath ?? "");
            }

            // Pad with empty frames if fewer than 3
            for (int i = frames.Count; i < 3; i++)
            {
                sb.Append("||");
            }

            var fingerprintInput = sb.ToString();

            // Generate SHA256 hash
            var fingerprintHash = ComputeSha256(fingerprintInput);

            // Build frame summaries for display
            var frameSummaries = frames
                .Select(f => $"{f?.MethodName ?? "?"} ({f?.FilePath ?? "?"})")
                .ToArray();

            var fingerprint = new ErrorFingerprint(fingerprintHash, exceptionType, frameSummaries);
            return await Task.FromResult(fingerprint);
        }

        /// <summary>
        /// Records an error occurrence in the session cache.
        /// If the error is new, creates a new ErrorOccurrence. If duplicate, increments count.
        /// </summary>
        public async Task RecordErrorAsync(ErrorFingerprint fingerprint)
        {
            if (fingerprint == null)
                throw new ArgumentNullException(nameof(fingerprint));

            _errorCache.AddOrUpdate(
                fingerprint.Fingerprint,
                new ErrorOccurrence(fingerprint),
                (key, existing) =>
                {
                    existing.IncrementCount();
                    return existing;
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets the occurrence count for a specific error fingerprint.
        /// </summary>
        public async Task<int> GetOccurrenceCountAsync(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return 0;

            if (_errorCache.TryGetValue(fingerprint, out var occurrence))
            {
                return await Task.FromResult(occurrence.OccurrenceCount);
            }

            return await Task.FromResult(0);
        }

        /// <summary>
        /// Checks if an error fingerprint is known (has been recorded in this session).
        /// </summary>
        public async Task<bool> GetIsKnownErrorAsync(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return false;

            var isKnown = _errorCache.ContainsKey(fingerprint);
            return await Task.FromResult(isKnown);
        }

        /// <summary>
        /// Manually groups two errors together (bidirectional).
        /// If fingerprint1 and fingerprint2 don't exist yet, they are created as new errors.
        /// </summary>
        public async Task GroupErrorsAsync(string fingerprint1, string fingerprint2)
        {
            if (string.IsNullOrEmpty(fingerprint1) || string.IsNullOrEmpty(fingerprint2))
                return;

            lock (_groupingLock)
            {
                // Ensure both fingerprints exist in cache
                _errorCache.TryAdd(fingerprint1, new ErrorOccurrence(new ErrorFingerprint(fingerprint1, "GroupedError", Array.Empty<string>())));
                _errorCache.TryAdd(fingerprint2, new ErrorOccurrence(new ErrorFingerprint(fingerprint2, "GroupedError", Array.Empty<string>())));

                // Add bidirectional grouping
                if (_errorCache.TryGetValue(fingerprint1, out var occurrence1))
                {
                    occurrence1.AddGroupedFingerprint(fingerprint2);
                }

                if (_errorCache.TryGetValue(fingerprint2, out var occurrence2))
                {
                    occurrence2.AddGroupedFingerprint(fingerprint1);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves all fingerprints manually grouped with a given fingerprint.
        /// </summary>
        public async Task<IReadOnlyCollection<string>> GetGroupedFingerprintsAsync(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return new List<string>();

            if (_errorCache.TryGetValue(fingerprint, out var occurrence))
            {
                return await Task.FromResult(new List<string>(occurrence.GroupedFingerprints));
            }

            return await Task.FromResult(new List<string>());
        }

        /// <summary>
        /// Computes the SHA256 hash of the input string.
        /// </summary>
        private static string ComputeSha256(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
