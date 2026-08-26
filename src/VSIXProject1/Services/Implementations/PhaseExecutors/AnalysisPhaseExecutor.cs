using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations.PhaseExecutors
{
    /// <summary>
    /// Executor for Analysis phases.
    /// Analysis phases inspect code, logs, and runtime state.
    /// They produce zero changes and are marked as Completed.
    /// </summary>
    public class AnalysisPhaseExecutor : IPhaseExecutor
    {
        private readonly IBridgeLogger? _logger;

        public InternalPhaseType PhaseType => InternalPhaseType.Analysis;

        public AnalysisPhaseExecutor(IBridgeLogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<InternalPhaseExecution> ExecuteAsync(
            InternalPhase phase,
            ChangeStack changeStack,
            string targetDir,
            CancellationToken cancellationToken = default)
        {
            if (phase == null)
                throw new ArgumentNullException(nameof(phase));
            if (changeStack == null)
                throw new ArgumentNullException(nameof(changeStack));
            if (string.IsNullOrWhiteSpace(targetDir))
                throw new ArgumentException("Target directory cannot be empty.", nameof(targetDir));

            if (_logger != null)
                await _logger.WriteDebugAsync($"AnalysisPhaseExecutor: executing phase '{phase.Id}' - {phase.Description}");

            return new InternalPhaseExecution
            {
                Strategy = "Analysis",
                Result = "Completed",
                ChangesAppliedCount = 0,
                ExecutedAt = DateTime.UtcNow
            };
        }
    }
}
