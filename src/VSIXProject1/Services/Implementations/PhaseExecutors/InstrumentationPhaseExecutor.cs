using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations.PhaseExecutors
{
    /// <summary>
    /// Executor for Instrumentation phases.
    /// Instrumentation phases add logging, monitoring, or diagnostic output.
    /// They generate and apply code changes via the change stack.
    /// </summary>
    public class InstrumentationPhaseExecutor : IPhaseExecutor
    {
        private readonly IBridgeLogger? _logger;
        private readonly IChangeStackService _changeStackService;

        public InternalPhaseType PhaseType => InternalPhaseType.Instrumentation;

        public InstrumentationPhaseExecutor(
            IChangeStackService changeStackService,
            IBridgeLogger? logger = null)
        {
            _changeStackService = changeStackService ?? throw new ArgumentNullException(nameof(changeStackService));
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
                await _logger.WriteDebugAsync($"InstrumentationPhaseExecutor: executing phase '{phase.Id}' - {phase.Description}");

            int changesApplied = 0;

            try
            {
                // Mock: Generate one instrumentation change for demonstration
                var mockChange = new CodeChange
                {
                    ChangeId = Guid.NewGuid().ToString(),
                    NewContent = "// Instrumentation added\nConsole.WriteLine(\"Debug trace\");"
                };

                // Record change in the stack
                changeStack.RecordChange(mockChange);
                changeStack.MarkAsApplied(mockChange.ChangeId);
                changesApplied++;

                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstrumentationPhaseExecutor: applied {changesApplied} change(s)");

                return new InternalPhaseExecution
                {
                    Strategy = "Instrumentation",
                    Result = "Completed",
                    ChangesAppliedCount = changesApplied,
                    ExecutedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstrumentationPhaseExecutor: error - {ex.Message}");

                return new InternalPhaseExecution
                {
                    Strategy = "Instrumentation",
                    Result = "Failed",
                    ChangesAppliedCount = changesApplied,
                    ExecutedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
