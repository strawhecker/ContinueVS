using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// File-based implementation of IPlanOutputService.
    /// gap43_2: Persists Plan mode LLM output to ~/.continueVS/plans/plan_{yyyyMMdd_HHmmss}.md.
    /// Accepts an optional continueDir constructor parameter for test isolation.
    /// </summary>
    public class PlanOutputService : IPlanOutputService
    {
        private readonly string _plansDirectory;

        private static readonly string DefaultContinueDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".continueVS");

        /// <summary>
        /// Initializes a new instance of PlanOutputService.
        /// </summary>
        /// <param name="continueDir">Optional override for the base ~/.continueVS directory (for testing).</param>
        public PlanOutputService(string? continueDir = null)
        {
            var baseDir = continueDir ?? DefaultContinueDir;
            _plansDirectory = Path.Combine(baseDir, "plans");
        }

        /// <inheritdoc/>
        public string GetPlansDirectory() => _plansDirectory;

        /// <inheritdoc/>
        public async Task<string> SavePlanAsync(string content, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Plan content cannot be null or whitespace.", nameof(content));
            }

            if (!Directory.Exists(_plansDirectory))
            {
                Directory.CreateDirectory(_plansDirectory);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"plan_{timestamp}.md";
            var filePath = Path.Combine(_plansDirectory, fileName);

            await Task.Run(() => File.WriteAllText(filePath, content), cancellationToken);

            System.Diagnostics.Debug.WriteLine($"[gap43_2] Plan persisted: {filePath}");
            return filePath;
        }
    }
}
