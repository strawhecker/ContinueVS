using System.Threading.Tasks;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for loading and managing system prompts for different chat modes.
    /// Supports both file-based (editable) and fallback (hardcoded) prompts.
    /// </summary>
    public interface ISystemPromptService
    {
        /// <summary>
        /// Asynchronously loads system prompts from the config file.
        /// Creates the config file if it doesn't exist using defaults.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task LoadAsync();

        /// <summary>
        /// Gets the system prompt for the specified mode.
        /// Falls back to hardcoded defaults if the file is unavailable or corrupted.
        /// </summary>
        /// <param name="mode">The chat mode name (e.g., "ask", "agent", "plan").</param>
        /// <returns>The system prompt string for the mode, or a default if not found.</returns>
        string GetPromptForMode(string mode);

        /// <summary>
        /// Reloads prompts from the config file, bypassing the cache.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ReloadAsync();

        /// <summary>
        /// Ensures the config file exists, creating it with defaults if necessary.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task EnsureConfigFileExistsAsync();
    }
}
