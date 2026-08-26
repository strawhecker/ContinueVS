using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for applying instrumentation strategies to source files.
    /// Tracks all changes in ChangeStack for per-change rollback capability.
    /// </summary>
    public interface IInstrumentationService
    {
        /// <summary>
        /// Applies an instrumentation strategy to source files.
        /// Each code snippet becomes a CodeChange tracked in ChangeStack.
        /// </summary>
        /// <param name="strategy">The instrumentation strategy to apply.</param>
        /// <param name="changeStack">ChangeStack instance for tracking modifications.</param>
        /// <param name="targetDir">Root directory for locating target files.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of applied ChangeIds. Empty if strategy was null or application failed.</returns>
        Task<List<string>> ApplyStrategyAsync(
            InstrumentationStrategy? strategy,
            ChangeStack changeStack,
            string targetDir,
            CancellationToken cancellationToken = default);
    }
}
