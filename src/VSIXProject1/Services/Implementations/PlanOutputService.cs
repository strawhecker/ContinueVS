using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// File-based implementation of IPlanOutputService.
    /// gap43_2: Persists Plan mode LLM output to ~/.continueVS/plans/.
    /// gap84: Title-driven filename: {SafeFileStem}_{yyyyMMdd_HHmmss}.md.
    /// Accepts an optional continueDir constructor parameter for test isolation.
    /// </summary>
    public class PlanOutputService : IPlanOutputService
    {
        private readonly string _plansDirectory;

        private static readonly string DefaultContinueDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".continueVS");

        private static readonly char[] InvalidFileNameChars =
            Path.GetInvalidFileNameChars();

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

        /// <summary>
        /// Converts a plan title into a safe file stem suitable for a file name.
        /// Trims; strips invalid Windows path chars; collapses whitespace to '_';
        /// trims trailing dots/spaces; caps at ~60 chars; returns "plan" if empty.
        /// </summary>
        /// <param name="title">The title to sanitize.</param>
        /// <returns>A safe file stem.</returns>
        internal static string ToSafeFileStem(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "plan";
            }

            var sanitized = title.Trim();

            foreach (var c in InvalidFileNameChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "_");
            }

            // Collapse runs of whitespace (and underscores) into a single underscore.
            sanitized = Regex.Replace(sanitized, @"[\s_]+", "_");

            // Trim trailing dots and underscores/spaces.
            sanitized = sanitized.TrimEnd('.', '_', ' ');

            if (sanitized.Length == 0)
            {
                return "plan";
            }

            if (sanitized.Length > 60)
            {
                sanitized = sanitized.Substring(0, 60).TrimEnd('_');
                if (sanitized.Length == 0)
                {
                    return "plan";
                }
            }

            return sanitized;
        }

        /// <inheritdoc/>
        public async Task<string> SavePlanAsync(string title, string content, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Plan content cannot be null or whitespace.", nameof(content));
            }

            if (!Directory.Exists(_plansDirectory))
            {
                Directory.CreateDirectory(_plansDirectory);
            }

            var stem = ToSafeFileStem(title);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{stem}_{timestamp}.md";
            var filePath = Path.Combine(_plansDirectory, fileName);

            await Task.Run(() => File.WriteAllText(filePath, content), cancellationToken);

            LoggerService.Current.WriteDebug($"[gap84] Plan persisted: {filePath}");
            return filePath;
        }
    }
}
