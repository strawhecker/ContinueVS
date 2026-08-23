using System;
using System.Threading.Tasks;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IModeService that manages chat mode changes and propagates events (gap27_3).
    /// Delegates to ISessionService to fire mode-change notifications to all subscribers.
    /// </summary>
    public class ModeService : IModeService
    {
        private readonly ISessionService _sessionService;

        /// <summary>
        /// Initializes a new instance of ModeService.
        /// </summary>
        /// <param name="sessionService">The session service to delegate mode changes to.</param>
        public ModeService(ISessionService sessionService)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        }

        /// <summary>
        /// Sets the current chat mode via SessionService, which fires SessionChanged event (gap27_3).
        /// </summary>
        public async Task SetModeAsync(int newMode)
        {
            await _sessionService.SetCurrentModeAsync(newMode);
        }
    }
}
