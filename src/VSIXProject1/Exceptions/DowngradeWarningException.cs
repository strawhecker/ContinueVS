using System;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when version downgrade is detected and user cancels.
    /// Used for step 35 (installer) error handling.
    /// </summary>
    public class DowngradeWarningException : Exception
    {
        /// <summary>Gets the current version the user was on.</summary>
        public string CurrentVersion { get; }

        /// <summary>Gets the target version the user attempted to switch to.</summary>
        public string TargetVersion { get; }

        /// <summary>
        /// Initializes a new instance of the DowngradeWarningException.
        /// </summary>
        public DowngradeWarningException(string currentVersion, string targetVersion)
            : base($"Downgrade from {currentVersion} to {targetVersion} was cancelled by user.")
        {
            CurrentVersion = currentVersion;
            TargetVersion = targetVersion;
        }

        /// <summary>
        /// Initializes a new instance with a custom message.
        /// </summary>
        public DowngradeWarningException(string currentVersion, string targetVersion, string message)
            : base(message)
        {
            CurrentVersion = currentVersion;
            TargetVersion = targetVersion;
        }
    }
}
