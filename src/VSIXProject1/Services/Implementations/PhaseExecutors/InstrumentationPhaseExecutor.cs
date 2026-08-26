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
    /// Uses LLM-generated InstrumentationStrategy to guide code changes.
    /// </summary>
    public class InstrumentationPhaseExecutor : IPhaseExecutor
    {
        private readonly IBridgeLogger? _logger;
        private readonly IInteractivePromptService? _promptService;
        private readonly IChangeStackService _changeStackService;
        private readonly IDebugStrategyGeneratorService _strategyGeneratorService;
        private readonly IInstrumentationService _instrumentationService;

        public InternalPhaseType PhaseType => InternalPhaseType.Instrumentation;

        public InstrumentationPhaseExecutor(
            IChangeStackService changeStackService,
            IDebugStrategyGeneratorService strategyGeneratorService,
            IInstrumentationService instrumentationService,
            IBridgeLogger? logger = null,
            IInteractivePromptService? promptService = null)
        {
            _changeStackService = changeStackService ?? throw new ArgumentNullException(nameof(changeStackService));
            _strategyGeneratorService = strategyGeneratorService ?? throw new ArgumentNullException(nameof(strategyGeneratorService));
            _instrumentationService = instrumentationService ?? throw new ArgumentNullException(nameof(instrumentationService));
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
                await _logger.WriteDebugAsync($"InstrumentationPhaseExecutor: executing phase '{phase.Id}' - {phase.Description}");

            int changesApplied = 0;

            try
            {
                // Generate instrumentation strategy from phase description
                var strategy = await _strategyGeneratorService.GenerateStrategyAsync(
                    phase.Description,
                    failureContext: null,
                    targetFile: null,
                    cancellationToken);

                if (strategy == null)
                {
                    if (_logger != null)
                        await _logger.WriteDebugAsync("InstrumentationPhaseExecutor: strategy generation returned null");

                    return new InternalPhaseExecution
                    {
                        Strategy = "Instrumentation",
                        Result = "Completed",
                        ChangesAppliedCount = 0,
                        ExecutedAt = DateTime.UtcNow
                    };
                }

                // Apply strategy to source files
                var appliedChangeIds = await _instrumentationService.ApplyStrategyAsync(
                    strategy,
                    changeStack,
                    targetDir,
                    cancellationToken);

                changesApplied = appliedChangeIds.Count;

                if (_logger != null)
                    await _logger.WriteDebugAsync($"InstrumentationPhaseExecutor: applied {changesApplied} change(s)");

                return new InternalPhaseExecution
                {
                    Strategy = strategy.Description,
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
