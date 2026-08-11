using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for context retrieval and RAG.
    /// Handles context item gathering from multiple providers.
    /// </summary>
    public interface IContextService
    {
        /// <summary>
        /// Gets context items relevant to a query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="selectedCode">Optional code snippet for context.</param>
        /// <param name="maxItems">Maximum number of context items to return.</param>
        /// <returns>An enumerable of context items.</returns>
        Task<IEnumerable<ContextItem>> GetContextItemsAsync(
            string query,
            string? selectedCode = null,
            int maxItems = 10);

        /// <summary>
        /// Gets all enabled context providers.
        /// </summary>
        /// <returns>An enumerable of enabled context providers.</returns>
        IEnumerable<IContextProvider> GetEnabledProviders();

        /// <summary>
        /// Adds a context item manually.
        /// </summary>
        /// <param name="item">The context item to add.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddContextItemAsync(ContextItem item);

        /// <summary>
        /// Removes a context item.
        /// </summary>
        /// <param name="itemId">The ID of the context item to remove.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveContextItemAsync(string itemId);
    }

    /// <summary>
    /// Interface for context providers (plugins that provide context).
    /// </summary>
    public interface IContextProvider
    {
        /// <summary>
        /// Name of the provider.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets context items from this provider.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="selectedCode">Optional code snippet for context.</param>
        /// <param name="maxItems">Maximum number of items to return.</param>
        /// <returns>An enumerable of context items.</returns>
        Task<IEnumerable<ContextItem>> GetContextItemsAsync(
            string query,
            string? selectedCode = null,
            int maxItems = 10);
    }
}
