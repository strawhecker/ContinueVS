#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Default <see cref="IContextRetirementService"/>.
    ///
    /// Toggle mechanism: delegates to <see cref="ISessionService.SoftDeleteMessageAsync"/> /
    /// <see cref="ISessionService.UndeleteMessageAsync"/>, which flip the shared
    /// <see cref="ChatMessage.IsDeleted"/> tombstone. Because retiring reuses IsDeleted, every
    /// existing serialize/filter path that already excludes IsDeleted (PackageMessages, replay,
    /// etc.) automatically excludes retired messages — no new exclusion code is required.
    ///
    /// Reference file: append-only JSONL at ~/.continueVS/retirements/retirements.jsonl
    /// (mirroring the gap83 delta-log philosophy). Toggling re-emits a "retire"/"unretire" line;
    /// the file is never rewritten, so the auditable trail is never lost.
    /// </summary>
    public class ContextRetirementService : IContextRetirementService
    {
        private readonly ISessionService _sessionService;
        private readonly string _referenceDirectory;
        private readonly object _sync = new object();

        /// <summary>
        /// Computes the default reference directory: ~/.continueVS/retirements/
        /// </summary>
        private static string DefaultReferenceDirectory
        {
            get
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userProfile, ".continueVS", "retirements");
            }
        }

        /// <summary>
        /// Creates the service using the default ~/.continueVS/retirements/ directory.
        /// </summary>
        public ContextRetirementService(ISessionService sessionService)
            : this(sessionService, DefaultReferenceDirectory)
        {
        }

        /// <summary>
        /// Creates the service writing to an explicit reference directory (used by tests for
        /// isolation; the default constructor preserves DI compatibility).
        /// </summary>
        public ContextRetirementService(ISessionService sessionService, string referenceDirectory)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _referenceDirectory = referenceDirectory ?? throw new ArgumentNullException(nameof(referenceDirectory));
        }

        /// <inheritdoc/>
        public async Task RetireAsync(string sessionId, string messageId, string reason, string? replacedById, string? verbatimJson)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session id cannot be null or empty.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(messageId)) throw new ArgumentException("Message id cannot be null or empty.", nameof(messageId));

            // Reuse the shared IsDeleted tombstone to exclude from context (no new flag).
            await _sessionService.SoftDeleteMessageAsync(messageId);

            AppendRecord(new RetirementRecord
            {
                Type = "retire",
                SessionId = sessionId,
                MessageId = messageId,
                Reason = reason,
                ReplacedById = replacedById,
                TimestampUtc = DateTime.UtcNow,
                VerbatimJson = verbatimJson
            });
        }

        /// <inheritdoc/>
        public async Task UnretireAsync(string sessionId, string messageId, string reason)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session id cannot be null or empty.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(messageId)) throw new ArgumentException("Message id cannot be null or empty.", nameof(messageId));

            // Clear the shared IsDeleted tombstone to restore into context.
            await _sessionService.UndeleteMessageAsync(messageId);

            AppendRecord(new RetirementRecord
            {
                Type = "unretire",
                SessionId = sessionId,
                MessageId = messageId,
                Reason = reason,
                TimestampUtc = DateTime.UtcNow
            });
        }

        /// <inheritdoc/>
        public Task<bool> IsRetiredAsync(string sessionId, string messageId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session id cannot be null or empty.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(messageId)) throw new ArgumentException("Message id cannot be null or empty.", nameof(messageId));

            if (!File.Exists(ReferenceFilePath))
                return Task.FromResult(false);

            string? latest = null;
            lock (_sync)
            {
                var lines = File.ReadAllLines(ReferenceFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var rec = JsonConvert.DeserializeObject<RetirementRecord>(line);
                        if (rec != null &&
                            string.Equals(rec.MessageId, messageId, StringComparison.Ordinal) &&
                            string.Equals(rec.SessionId, sessionId, StringComparison.Ordinal))
                        {
                            latest = rec.Type;
                        }
                    }
                    catch
                    {
                        // Skip malformed lines; never let a bad line break the toggle query.
                    }
                }
            }

            return Task.FromResult(string.Equals(latest, "retire", StringComparison.Ordinal));
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<RetirementRecord>> GetRecordsAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session id cannot be null or empty.", nameof(sessionId));

            var result = new List<RetirementRecord>();
            if (!File.Exists(ReferenceFilePath))
                return Task.FromResult<IReadOnlyList<RetirementRecord>>(result);

            lock (_sync)
            {
                var lines = File.ReadAllLines(ReferenceFilePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var rec = JsonConvert.DeserializeObject<RetirementRecord>(line);
                        if (rec != null && string.Equals(rec.SessionId, sessionId, StringComparison.Ordinal))
                        {
                            result.Add(rec);
                        }
                    }
                    catch
                    {
                        // Skip malformed lines; never let a bad line break the reference read.
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<RetirementRecord>>(result);
        }

        /// <summary>
        /// Absolute path to the reference file (single shared file holding the audit trail).
        /// </summary>
        public string ReferenceFilePath => Path.Combine(_referenceDirectory, "retirements.jsonl");

        private void AppendRecord(RetirementRecord record)
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_referenceDirectory);
                using var writer = new StreamWriter(ReferenceFilePath, append: true, System.Text.Encoding.UTF8);
                writer.WriteLine(JsonConvert.SerializeObject(record, Formatting.None));
            }
        }
    }
}
