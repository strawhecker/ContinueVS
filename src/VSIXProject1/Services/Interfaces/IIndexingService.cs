using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for codebase indexing.
    /// Handles indexing, progress tracking, and searching of the codebase.
    /// </summary>
    public interface IIndexingService
    {
        /// <summary>
        /// Starts the indexing process.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartIndexingAsync();

        /// <summary>
        /// Pauses the current indexing operation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PauseIndexingAsync();

        /// <summary>
        /// Resumes a paused indexing operation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ResumeIndexingAsync();

        /// <summary>
        /// Cancels the current indexing operation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CancelIndexingAsync();

        /// <summary>
        /// Gets the current indexing status and progress.
        /// </summary>
        /// <returns>The current IndexingProgressUpdate.</returns>
        IndexingProgressUpdate GetCurrentStatus();

        /// <summary>
        /// Checks if a file has been indexed.
        /// </summary>
        /// <param name="filepath">The path to the file to check.</param>
        /// <returns>True if the file has been indexed.</returns>
        Task<bool> IsIndexedAsync(string filepath);

        /// <summary>
        /// Searches the index for matching code symbols.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>An enumerable of matching code symbols.</returns>
        Task<IEnumerable<CodeSymbol>> SearchIndexAsync(string query, int maxResults);

        /// <summary>
        /// Rebuilds the entire index from scratch.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RebuildIndexAsync();

        /// <summary>
        /// Event raised when indexing progress is updated.
        /// </summary>
        event EventHandler<IndexingProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// Event raised when an error occurs during indexing.
        /// </summary>
        event EventHandler<IndexingErrorEventArgs>? Error;
    }
}
