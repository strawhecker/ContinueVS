using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for persistent error storage and querying.
    /// Provides methods to store, retrieve, query, cleanup, and export error records.
    /// </summary>
    public interface IErrorRepository
    {
        /// <summary>
        /// Initializes the error repository (creates directory, loads index if exists).
        /// Must be called before any other operations.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Stores an error record to disk and updates the in-memory index.
        /// </summary>
        /// <param name="record">The error record to store.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StoreErrorAsync(ErrorRecord record);

        /// <summary>
        /// Retrieves all errors of a specific exception type.
        /// </summary>
        /// <param name="exceptionType">The exception type to filter by (e.g., "System.NullReferenceException").</param>
        /// <returns>List of matching ErrorRecord objects.</returns>
        Task<IEnumerable<ErrorRecord>> GetErrorsByTypeAsync(string exceptionType);

        /// <summary>
        /// Retrieves all errors with a specific fingerprint.
        /// </summary>
        /// <param name="fingerprint">The error fingerprint to filter by.</param>
        /// <returns>List of matching ErrorRecord objects.</returns>
        Task<IEnumerable<ErrorRecord>> GetErrorsByFingerprintAsync(string fingerprint);

        /// <summary>
        /// Retrieves all errors within a time range.
        /// </summary>
        /// <param name="startTime">Start of the time range (inclusive).</param>
        /// <param name="endTime">End of the time range (inclusive).</param>
        /// <returns>List of matching ErrorRecord objects.</returns>
        Task<IEnumerable<ErrorRecord>> GetErrorsByTimeRangeAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// Automatically deletes all errors older than the specified number of days.
        /// Called during initialization and can be invoked manually for cleanup.
        /// </summary>
        /// <param name="days">Number of days; errors older than this are deleted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteErrorsOlderThanAsync(int days);

        /// <summary>
        /// Exports all stored errors as a JSON file.
        /// </summary>
        /// <param name="outputPath">The file path where JSON should be written.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExportAsJsonAsync(string outputPath);

        /// <summary>
        /// Exports all stored errors as a CSV file.
        /// </summary>
        /// <param name="outputPath">The file path where CSV should be written.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExportAsCsvAsync(string outputPath);

        /// <summary>
        /// Gets the total count of stored errors across all fingerprints.
        /// </summary>
        /// <returns>The total error count.</returns>
        Task<int> GetTotalErrorCountAsync();
    }
}
