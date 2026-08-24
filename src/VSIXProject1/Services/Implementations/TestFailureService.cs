using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Orchestrates iterative test failure analysis with diagnostic capture and logging.
    /// Enforces maximum 5 iterations to prevent runaway analysis loops.
    /// </summary>
    internal class TestFailureService : ITestFailureService
    {
        private readonly IIdeService _ideService;
        private readonly IBridgeLogger _logger;

        public TestFailureService(IIdeService ideService, IBridgeLogger logger)
        {
            if (ideService == null) throw new ArgumentNullException(nameof(ideService));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _ideService = ideService;
            _logger = logger;
        }

        public async Task<TestRunResult> AnalyzeFailureAsync(string testPath, int iteration, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(testPath))
                throw new ArgumentException("testPath must not be empty.", nameof(testPath));

            if (iteration >= 5)
            {
                var message = $"Test failure analysis exceeded maximum 5 iterations. Last iteration: {iteration}";
                Debug.WriteLine($"[gap29_2-iteration-limit] {message}");
                await _logger.WriteErrorAsync($"[gap29_2] {message}");
                throw new TestAnalysisException(message, iteration);
            }

            Debug.WriteLine($"[gap29_2-analyze-start] TestPath: {testPath}, Iteration: {iteration}");

            var options = new TestRunOptions(testPath)
            {
                Debug = true,
                Verbosity = 2,
                CurrentIteration = iteration
            };

            try
            {
                var result = await _ideService.RunTestAsync(testPath, options, ct);

                await _logger.WriteInfoAsync(
                    $"[gap29_2] Test analysis iteration {iteration + 1}/5: {testPath} - ExitCode={result.ExitCode}, Frames={result.FrameCount}");

                Debug.WriteLine($"[gap29_2-analyze-complete] Iteration: {iteration}, ExitCode: {result.ExitCode}");

                return result;
            }
            catch (OperationCanceledException ex)
            {
                await _logger.WriteErrorAsync($"[gap29_2] Test analysis cancelled: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                await _logger.WriteErrorAsync($"[gap29_2] Test analysis error (iteration {iteration}): {ex.Message}");
                throw;
            }
        }
    }
}
