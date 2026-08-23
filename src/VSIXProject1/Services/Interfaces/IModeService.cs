using System.Threading.Tasks;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing chat mode changes and propagating mode-change events (gap27_3).
    /// Bridges UI mode selection to session-level mode state and event notification.
    /// </summary>
    public interface IModeService
    {
        /// <summary>
        /// Sets the current chat mode and propagates the change to all subscribers via SessionService.SessionChanged event.
        /// </summary>
        /// <param name="newMode">The chat mode to set as an integer (0=Ask, 1=Agent, 2=Plan).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetModeAsync(int newMode);
    }
}
