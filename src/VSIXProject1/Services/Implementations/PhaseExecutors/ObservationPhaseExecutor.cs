using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations.PhaseExecutors
{
    /// <summary>
    /// Executor for Observation phases.
    /// Observation phases gather data without modifying code.
    /// They produce zero changes and are marked as Completed.
    /// </summary>
    public class ObservationPhaseExecutor : IPhaseExecutor
    {
        private readonly IBridgeLogger? _logger;
        private readonly IInteractivePromptService? _promptService;

        public InternalPhaseType PhaseType => InternalPhaseType.Observation;

        public ObservationPhaseExecutor(IBridgeLogger? logger = null, IInteractivePromptService? promptService = null)
        {
            _logger = logger;
            _promptService = promptService;
        }

        public async Task<InternalPhaseExecution> ExecuteAsync(
            InternalPhase phase,
            ChangeStack changeStack,
            string targetDir,
            bool isInteractiveMode = false,
            CancellationToken cancellationToken = default)
        {
            if (phase == null)
                throw new ArgumentNullException(nameof(phase));
            if (changeStack == null)
                throw new ArgumentNullException(nameof(changeStack));
            if (string.IsNullOrWhiteSpace(targetDir))
                throw new ArgumentException("Target directory cannot be empty.", nameof(targetDir));

            if (_logger != null)
                await _logger.WriteDebugAsync($"ObservationPhaseExecutor: executing phase '{phase.Id}' - {phase.Description}");

            return new InternalPhaseExecution
            {
                Strategy = "Observation",
                Result = "Completed",
                ChangesAppliedCount = 0,
                ExecutedAt = DateTime.UtcNow
            };
        }
    }
}
