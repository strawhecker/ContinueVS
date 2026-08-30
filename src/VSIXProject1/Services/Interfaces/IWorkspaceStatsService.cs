using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Collects and caches a <see cref="WorkspaceStats"/> snapshot for system prompt injection.
    /// <see cref="GetStats"/> returns the cached snapshot immediately; <see cref="Refresh"/> re-collects.
    /// </summary>
    public interface IWorkspaceStatsService
    {
        /// <summary>
        /// Returns the most recently collected <see cref="WorkspaceStats"/> snapshot.
        /// Calls <see cref="Refresh"/> on first invocation if the cache is empty.
        /// </summary>
        WorkspaceStats GetStats();

        /// <summary>
        /// Synchronously re-collects all workspace fields and updates the cache.
        /// Must be called from a non-UI thread.
        /// </summary>
        void Refresh();
    }
}
