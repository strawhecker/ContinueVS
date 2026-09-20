using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for persisting Plan mode output to ~/.continueVS/plans/.
    /// gap43_1: Writes completed assistant responses generated in Plan mode to disk.
    /// gap84: Title-driven filename via ToSafeFileStem + timestamp.
    /// </summary>
    public interface IPlanOutputService
    {
        /// <summary>
        /// Saves plan content to a title-derived, timestamped markdown file in the plans directory.
        /// </summary>
        /// <param name="title">A short, descriptive title used to derive the file stem (sanitized).</param>
        /// <param name="content">The plan text content to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The absolute file path of the saved plan file.</returns>
        /// <exception cref="System.ArgumentException">Thrown when content is null or whitespace.</exception>
        Task<string> SavePlanAsync(string title, string content, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the absolute path to the plans directory (~/.continueVS/plans/).
        /// </summary>
        string GetPlansDirectory();
    }
}
