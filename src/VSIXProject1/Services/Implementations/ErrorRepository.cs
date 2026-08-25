using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// File-based implementation of IErrorRepository.
    /// Stores error records as JSON files in ~/.continueVS/errors/ directory.
    /// Maintains an in-memory index for fast queries.
    /// Thread-safe using lock-based synchronization.
    /// </summary>
    public class ErrorRepository : IErrorRepository
    {
        private readonly IConfigService _configService;
        private readonly IBridgeLogger? _logger;
        private readonly object _lock = new object();
        private string? _errorsDirectory;
        private ConcurrentDictionary<string, List<ErrorRecord>>? _fingerprintIndex;
        private bool _initialized = false;
        private readonly string? _testErrorsDirectoryOverride;

        /// <summary>
        /// Initializes a new instance of ErrorRepository.
        /// </summary>
        public ErrorRepository(IConfigService configService, IBridgeLogger? logger = null, string? testErrorsDirectoryOverride = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
            _testErrorsDirectoryOverride = testErrorsDirectoryOverride;
        }

        /// <summary>
        /// Initializes the error repository: creates directory, loads existing errors into index, performs cleanup.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_logger != null)
                await _logger.WriteDebugAsync("ErrorRepository.InitializeAsync (start)");

            lock (_lock)
            {
                if (_initialized)
                    return;

                try
                {
                    // Use test override if provided; otherwise use production path
                    if (!string.IsNullOrEmpty(_testErrorsDirectoryOverride))
                    {
                        _errorsDirectory = _testErrorsDirectoryOverride;
                    }
                    else
                    {
                        // Get the config directory path from ConfigService
                        // ConfigService stores config in ~/.continueVS/, so errors go in ~/.continueVS/errors/
                        var continueDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".continueVS"
                        );
                        _errorsDirectory = Path.Combine(continueDir, "errors");
                    }

                    EnsureErrorsDirectory();
                    _fingerprintIndex = new ConcurrentDictionary<string, List<ErrorRecord>>();
                    ReloadIndex();
                    _initialized = true;

                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository initialized: {_errorsDirectory}");
                }
                catch (Exception ex)
                {
                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository.InitializeAsync failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Stores an error record to disk and updates the index.
        /// </summary>
        public async Task StoreErrorAsync(ErrorRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            lock (_lock)
            {
                if (!_initialized || _errorsDirectory == null || _fingerprintIndex == null)
                    throw new InvalidOperationException("ErrorRepository not initialized. Call InitializeAsync first.");

                try
                {
                    var fileName = BuildErrorFileName(record.Fingerprint, record.Timestamp);
                    var filePath = Path.Combine(_errorsDirectory, fileName);
                    var json = JsonConvert.SerializeObject(record, Formatting.Indented);
                    File.WriteAllText(filePath, json);

                    // Update index
                    _fingerprintIndex.AddOrUpdate(
                        record.Fingerprint,
                        new List<ErrorRecord> { record },
                        (key, existing) =>
                        {
                            existing.Add(record);
                            return existing;
                        }
                    );
                }
                catch (Exception ex)
                {
                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository.StoreErrorAsync failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Retrieves all errors of a specific exception type.
        /// </summary>
        public async Task<IEnumerable<ErrorRecord>> GetErrorsByTypeAsync(string exceptionType)
        {
            if (string.IsNullOrEmpty(exceptionType))
                return Enumerable.Empty<ErrorRecord>();

            lock (_lock)
            {
                if (!_initialized || _fingerprintIndex == null)
                    return Enumerable.Empty<ErrorRecord>();

                var results = new List<ErrorRecord>();
                foreach (var kvp in _fingerprintIndex)
                {
                    results.AddRange(kvp.Value.Where(e => e.ExceptionType == exceptionType));
                }
                return results;
            }
        }

        /// <summary>
        /// Retrieves all errors with a specific fingerprint.
        /// </summary>
        public async Task<IEnumerable<ErrorRecord>> GetErrorsByFingerprintAsync(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return Enumerable.Empty<ErrorRecord>();

            lock (_lock)
            {
                if (!_initialized || _fingerprintIndex == null)
                    return Enumerable.Empty<ErrorRecord>();

                if (_fingerprintIndex.TryGetValue(fingerprint, out var records))
                {
                    return records.ToList();
                }
                return Enumerable.Empty<ErrorRecord>();
            }
        }

        /// <summary>
        /// Retrieves all errors within a time range.
        /// </summary>
        public async Task<IEnumerable<ErrorRecord>> GetErrorsByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            lock (_lock)
            {
                if (!_initialized || _fingerprintIndex == null)
                    return Enumerable.Empty<ErrorRecord>();

                var results = new List<ErrorRecord>();
                foreach (var kvp in _fingerprintIndex)
                {
                    results.AddRange(
                        kvp.Value.Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
                    );
                }
                return results;
            }
        }

        /// <summary>
        /// Deletes all errors older than the specified number of days.
        /// </summary>
        public async Task DeleteErrorsOlderThanAsync(int days)
        {
            lock (_lock)
            {
                if (!_initialized || _errorsDirectory == null || _fingerprintIndex == null)
                    return;

                try
                {
                    var cutoffTime = DateTime.UtcNow.AddDays(-days);
                    var files = Directory.GetFiles(_errorsDirectory, "*.json");
                    var deletedCount = 0;

                    foreach (var file in files)
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var record = JsonConvert.DeserializeObject<ErrorRecord>(json);
                            if (record != null && record.Timestamp < cutoffTime)
                            {
                                File.Delete(file);
                                deletedCount++;

                                // Remove from index
                                if (_fingerprintIndex.TryGetValue(record.Fingerprint, out var records))
                                {
                                    records.RemoveAll(e => e.Timestamp == record.Timestamp && e.Fingerprint == record.Fingerprint);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (_logger != null)
                                _ = _logger.WriteDebugAsync($"ErrorRepository: Failed to process cleanup for {file}: {ex.Message}");
                        }
                    }

                    if (_logger != null && deletedCount > 0)
                        _ = _logger.WriteDebugAsync($"ErrorRepository cleanup: Deleted {deletedCount} errors older than {days} days");
                }
                catch (Exception ex)
                {
                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository.DeleteErrorsOlderThanAsync failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Exports all errors as JSON.
        /// </summary>
        public async Task ExportAsJsonAsync(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            lock (_lock)
            {
                if (!_initialized || _fingerprintIndex == null)
                    throw new InvalidOperationException("ErrorRepository not initialized.");

                try
                {
                    var allErrors = new List<ErrorRecord>();
                    foreach (var kvp in _fingerprintIndex)
                    {
                        allErrors.AddRange(kvp.Value);
                    }

                    var json = JsonConvert.SerializeObject(allErrors, Formatting.Indented);
                    File.WriteAllText(outputPath, json);

                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository: Exported {allErrors.Count} errors to {outputPath}");
                }
                catch (Exception ex)
                {
                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository.ExportAsJsonAsync failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Exports all errors as CSV.
        /// </summary>
        public async Task ExportAsCsvAsync(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            lock (_lock)
            {
                if (!_initialized || _fingerprintIndex == null)
                    throw new InvalidOperationException("ErrorRepository not initialized.");

                try
                {
                    var allErrors = new List<ErrorRecord>();
                    foreach (var kvp in _fingerprintIndex)
                    {
                        allErrors.AddRange(kvp.Value);
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine("Timestamp,Fingerprint,ExceptionType,ExceptionMessage,SessionId,UserNotes");

                    foreach (var error in allErrors.OrderBy(e => e.Timestamp))
                    {
                        var timestamp = error.Timestamp.ToString("O");
                        var fingerprint = CsvEscape(error.Fingerprint);
                        var exceptionType = CsvEscape(error.ExceptionType);
                        var exceptionMessage = CsvEscape(error.ExceptionMessage);
                        var sessionId = CsvEscape(error.SessionId);
                        var userNotes = CsvEscape(error.UserNotes);

                        sb.AppendLine($"{timestamp},{fingerprint},{exceptionType},{exceptionMessage},{sessionId},{userNotes}");
                    }

                    File.WriteAllText(outputPath, sb.ToString());

                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository: Exported {allErrors.Count} errors to {outputPath}");
                }
                catch (Exception ex)
                {
                    if (_logger != null)
                        _ = _logger.WriteDebugAsync($"ErrorRepository.ExportAsCsvAsync failed: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets the total count of stored errors.
        /// </summary>
        public async Task<int> GetTotalErrorCountAsync()
        {
            lock (_lock)
            {
                if (!_initialized || _fingerprintIndex == null)
                    return 0;

                return _fingerprintIndex.Values.Sum(list => list.Count);
            }
        }

        /// <summary>
        /// Ensures the errors directory exists.
        /// </summary>
        private void EnsureErrorsDirectory()
        {
            if (_errorsDirectory != null && !Directory.Exists(_errorsDirectory))
            {
                Directory.CreateDirectory(_errorsDirectory);
            }
        }

        /// <summary>
        /// Reloads the in-memory index from disk.
        /// Called during initialization.
        /// </summary>
        private void ReloadIndex()
        {
            try
            {
                if (_errorsDirectory == null || !Directory.Exists(_errorsDirectory))
                    return;

                var files = Directory.GetFiles(_errorsDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var record = JsonConvert.DeserializeObject<ErrorRecord>(json);
                        if (record != null && _fingerprintIndex != null)
                        {
                            _fingerprintIndex.AddOrUpdate(
                                record.Fingerprint,
                                new List<ErrorRecord> { record },
                                (key, existing) =>
                                {
                                    existing.Add(record);
                                    return existing;
                                }
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_logger != null)
                            _ = _logger.WriteDebugAsync($"ErrorRepository: Failed to load {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    _ = _logger.WriteDebugAsync($"ErrorRepository.ReloadIndex failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a unique filename for an error record.
        /// Format: {fingerprint}_{timestamp:yyyyMMddHHmmss}.json
        /// </summary>
        private string BuildErrorFileName(string fingerprint, DateTime timestamp)
        {
            var safeFp = new string(fingerprint.Take(16).ToArray());
            var timestampStr = timestamp.ToString("yyyyMMddHHmmss");
            return $"{safeFp}_{timestampStr}.json";
        }

        /// <summary>
        /// Sanitizes user notes to prevent path traversal and other attacks.
        /// </summary>
        private string SanitizeUserNotes(string notes)
        {
            if (string.IsNullOrEmpty(notes))
                return string.Empty;

            // Remove path traversal patterns
            var sanitized = notes.Replace("..", "").Replace("\\", "/");
            return sanitized;
        }

        /// <summary>
        /// Escapes a string for CSV format.
        /// </summary>
        private string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            if (value.Contains("\"") || value.Contains(",") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return "\"" + value + "\"";
        }
    }
}
