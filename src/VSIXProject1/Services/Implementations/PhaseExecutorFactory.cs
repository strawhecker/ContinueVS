using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations.PhaseExecutors;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Factory for creating phase executors based on phase type.
    /// Maps InternalPhaseType to corresponding executor instances.
    /// </summary>
    public class PhaseExecutorFactory
    {
        private readonly Dictionary<InternalPhaseType, IPhaseExecutor> _executors;

        public PhaseExecutorFactory(
            IChangeStackService changeStackService,
            IBridgeLogger? logger = null)
        {
            if (changeStackService == null)
                throw new ArgumentNullException(nameof(changeStackService));

            _executors = new Dictionary<InternalPhaseType, IPhaseExecutor>
            {
                { InternalPhaseType.Analysis, new AnalysisPhaseExecutor(logger) },
                { InternalPhaseType.Observation, new ObservationPhaseExecutor(logger) },
                { InternalPhaseType.Instrumentation, new InstrumentationPhaseExecutor(changeStackService, logger) },
                // Future executors: Breakpoint, Test
            };
        }

        /// <summary>
        /// Gets the executor for a given phase type.
        /// </summary>
        /// <param name="phaseType">The phase type to get an executor for.</param>
        /// <returns>The executor instance, or null if phase type is not yet supported.</returns>
        public IPhaseExecutor? GetExecutor(InternalPhaseType phaseType)
        {
            _executors.TryGetValue(phaseType, out var executor);
            return executor;
        }
    }
}
