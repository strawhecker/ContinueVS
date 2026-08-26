using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IErrorDrivenInstrumentationService.
    /// Reacts to exceptions by querying historical error patterns and suggesting targeted instrumentation.
    /// </summary>
    public class ErrorDrivenInstrumentationService : IErrorDrivenInstrumentationService
    {
        private readonly IErrorRepository _errorRepository;
        private readonly IDebugStrategyGeneratorService _strategyGenerator;
        private readonly IBridgeLogger? _logger;

        public ErrorDrivenInstrumentationService(
            IErrorRepository errorRepository,
            IDebugStrategyGeneratorService strategyGenerator,
            IBridgeLogger? logger = null)
        {
            _errorRepository = errorRepository ?? throw new ArgumentNullException(nameof(errorRepository));
            _strategyGenerator = strategyGenerator ?? throw new ArgumentNullException(nameof(strategyGenerator));
            _logger = logger;
        }

        public async Task<InstrumentationSuggestion?> SuggestInstrumentationAsync(
            string exceptionType,
            string message,
            string stackTrace,
            string filePath,
            int lineNumber,
            CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(exceptionType))
                throw new ArgumentNullException(nameof(exceptionType), "Exception type cannot be null or empty");

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty");

            try
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"ErrorDrivenInstrumentation: starting suggestion for {exceptionType} at {filePath}:{lineNumber}");

                // Compute fingerprint from exception type and message
                var fingerprint = ComputeFingerprint(exceptionType, message);

                if (_logger != null)
                    await _logger.WriteDebugAsync($"ErrorDrivenInstrumentation: querying repository for fingerprint {fingerprint}");

                // Query ErrorRepository for historical matches
                var historicalErrors = await _errorRepository.GetErrorsByFingerprintAsync(fingerprint);
                var errorList = historicalErrors?.ToList() ?? new List<ErrorRecord>();

                if (_logger != null)
                    await _logger.WriteDebugAsync($"ErrorDrivenInstrumentation: found {errorList.Count} historical errors with matching fingerprint");

                // Build failure context from exception info
                var failureContext = BuildFailureContext(exceptionType, message, stackTrace, errorList);

                // Generate instrumentation strategy via LLM
                if (_logger != null)
                    await _logger.WriteDebugAsync("ErrorDrivenInstrumentation: calling strategy generator");

                var strategy = await _strategyGenerator.GenerateStrategyAsync(
                    instruction: $"Add diagnostic instrumentation for {exceptionType}",
                    failureContext: failureContext,
                    targetFile: filePath,
                    cancellationToken: cancellationToken);

                if (strategy == null)
                {
                    if (_logger != null)
                        await _logger.WriteDebugAsync("ErrorDrivenInstrumentation: strategy generator returned null");
                    return null;
                }

                if (_logger != null)
                    await _logger.WriteDebugAsync($"ErrorDrivenInstrumentation: strategy generated with {strategy.CodeSnippets.Count} snippets");

                // Wrap strategy in InstrumentationSuggestion
                var suggestion = new InstrumentationSuggestion
                {
                    ExceptionType = exceptionType,
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Reasoning = BuildReasoning(exceptionType, errorList.Count),
                    SuggestedStrategy = strategy,
                    ConfidenceScore = CalculateConfidence(errorList.Count),
                    MatchFingerprint = fingerprint,
                    GeneratedAt = DateTime.UtcNow
                };

                if (_logger != null)
                    await _logger.WriteDebugAsync("ErrorDrivenInstrumentation: suggestion generated successfully");

                return suggestion;
            }
            catch (OperationCanceledException)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync("ErrorDrivenInstrumentation: operation cancelled");
                return null;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"ErrorDrivenInstrumentation: exception during suggestion generation - {ex.Message}");
                return null;
            }
        }

        private string ComputeFingerprint(string exceptionType, string message)
        {
            // Compute simple fingerprint from exception type and message
            // Format: exceptionType:messageHash
            var messageHash = (message ?? string.Empty).GetHashCode();
            return $"{exceptionType}:{messageHash}";
        }

        private string BuildFailureContext(string exceptionType, string message, string stackTrace, List<ErrorRecord> historicalErrors)
        {
            var context = $@"Exception Type: {exceptionType}
Message: {message}
Stack Trace:
{stackTrace}";

            if (historicalErrors.Count > 0)
            {
                context += $"\n\nHistorical Similar Errors ({historicalErrors.Count} found):";
                foreach (var error in historicalErrors.Take(3))
                {
                    context += $"\n- {error.ExceptionType}: {error.ExceptionMessage}";
                }
            }

            return context;
        }

        private string BuildReasoning(string exceptionType, int matchCount)
        {
            if (matchCount == 0)
                return $"No prior {exceptionType} in history; generating generic diagnostic instrumentation.";

            return matchCount == 1
                ? $"Found 1 prior {exceptionType}; suggesting instrumentation based on that pattern."
                : $"Found {matchCount} prior {exceptionType} occurrences; suggesting aggregated instrumentation patterns.";
        }

        private double CalculateConfidence(int matchCount)
        {
            // Confidence increases with number of historical matches
            return Math.Min(1.0, 0.5 + (matchCount * 0.1));
        }
    }
}
