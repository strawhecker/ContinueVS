using Microsoft.VisualStudio.Shell;
using System;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Service to detect and warn about version downgrades.
    /// Called by Step 35 (installer) before switching versions.
    /// Shows a warning dialog if downgrade is detected.
    /// </summary>
    public class DowngradeWarningService
    {
        private readonly IVersionComparator _versionComparator;

        /// <summary>
        /// Initializes a new instance of the DowngradeWarningService.
        /// </summary>
        /// <param name="versionComparator">Version comparator implementation. If null, uses VersionComparator.</param>
        public DowngradeWarningService(IVersionComparator versionComparator = null)
        {
            _versionComparator = versionComparator ?? new VersionComparator();
        }

        /// <summary>
        /// Checks if targetVersion is older than currentVersion.
        /// If yes, shows a warning dialog and returns user's response.
        /// </summary>
        /// <param name="currentVersion">The currently active version (e.g., "2.1.0").</param>
        /// <param name="targetVersion">The version user is attempting to switch to (e.g., "2.0.0").</param>
        /// <returns>
        /// True if user confirmed the downgrade (or no downgrade detected).
        /// False if user cancelled the downgrade.
        /// </returns>
        public async Task<bool> CheckDowngradeAsync(string currentVersion, string targetVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(targetVersion))
                return true; // No downgrade detected; proceed

            // Check if this is a downgrade
            if (!_versionComparator.IsDowngrade(currentVersion, targetVersion))
                return true; // Upgrade or same version; proceed

            // Downgrade detected; show warning
            return await ShowDowngradeWarningAsync(currentVersion, targetVersion);
        }

        /// <summary>
        /// Shows a warning dialog for version downgrade.
        /// Returns true if user clicked "Yes", false if "No".
        /// </summary>
        private async Task<bool> ShowDowngradeWarningAsync(string currentVersion, string targetVersion)
        {
            bool userConfirmed = false;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var message = $"Downgrade Notice\n\n" +
                          $"You are about to downgrade from Continue Bridge v{currentVersion} to v{targetVersion}.\n\n" +
                          $"Downgraded versions have fewer handlers and features. " +
                          $"A restart of Visual Studio will be required.\n\n" +
                          $"Continue?";

            var result = System.Windows.MessageBox.Show(
                message,
                "Continue Bridge — Downgrade Warning",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            userConfirmed = result == System.Windows.MessageBoxResult.Yes;

            return userConfirmed;
        }
    }
}
