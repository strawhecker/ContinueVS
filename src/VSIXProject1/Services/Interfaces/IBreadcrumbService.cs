using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for recording and querying breadcrumb trail of application events.
    /// Breadcrumbs help trace application state changes before errors occur.
    /// </summary>
    public interface IBreadcrumbService
    {
        /// <summary>
        /// Records a breadcrumb event with timestamp, level, and message.
        /// Automatically masks sensitive data (API keys, passwords, tokens).
        /// </summary>
        /// <param name="message">The breadcrumb message to record.</param>
        /// <param name="level">The breadcrumb severity level (Info, Warning, Error).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RecordBreadcrumbAsync(string message, BreadcrumbLevel level);

        /// <summary>
        /// Retrieves all recorded breadcrumbs up to the specified limit.
        /// </summary>
        /// <param name="limit">Maximum number of breadcrumbs to return (default: 20).</param>
        /// <returns>A collection of breadcrumb records ordered by timestamp (oldest first).</returns>
        Task<IReadOnlyList<BreadcrumbRecord>> GetBreadcrumbsAsync(int limit = 20);

        /// <summary>
        /// Retrieves breadcrumbs filtered by severity level.
        /// </summary>
        /// <param name="level">The breadcrumb level to filter by.</param>
        /// <param name="limit">Maximum number of breadcrumbs to return (default: 20).</param>
        /// <returns>A collection of breadcrumb records matching the level, ordered by timestamp (oldest first).</returns>
        Task<IReadOnlyList<BreadcrumbRecord>> GetBreadcrumbsByLevelAsync(BreadcrumbLevel level, int limit = 20);

        /// <summary>
        /// Clears all recorded breadcrumbs for the current session.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ClearBreadcrumbsAsync();
    }
}
