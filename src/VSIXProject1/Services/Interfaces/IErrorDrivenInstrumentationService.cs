using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for generating reactive instrumentation suggestions when exceptions occur.
    /// Queries historical error patterns and suggests targeted code additions to aid debugging.
    /// </summary>
    public interface IErrorDrivenInstrumentationService
    {
        /// <summary>
        /// Generates an instrumentation suggestion based on exception context and historical error patterns.
        /// </summary>
        /// <param name="exceptionType">The full exception type name (e.g., "System.NullReferenceException").</param>
        /// <param name="message">The exception message.</param>
        /// <param name="stackTrace">The exception stack trace.</param>
        /// <param name="filePath">The file path where the exception occurred.</param>
        /// <param name="lineNumber">The line number where the exception occurred.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// An InstrumentationSuggestion if a strategy can be generated; null if query fails, LLM errors, or inputs are invalid.
        /// Does not throw to caller; logs errors internally.
        /// </returns>
        Task<InstrumentationSuggestion?> SuggestInstrumentationAsync(
            string exceptionType,
            string message,
            string stackTrace,
            string filePath,
            int lineNumber,
            CancellationToken cancellationToken = default);
    }
}
