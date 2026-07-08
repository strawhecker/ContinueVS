namespace ContinueVS.Services
{
    /// <summary>
    /// Interface for semantic version comparison.
    /// Enables testability and future implementation swaps.
    /// </summary>
    public interface IVersionComparator
    {
        /// <summary>
        /// Compares two version strings.
        /// Returns: 1 if v1 > v2, -1 if v1 < v2, 0 if equal.
        /// Returns 0 (equal) if either version is invalid.
        /// </summary>
        int CompareVersions(string version1, string version2);

        /// <summary>
        /// Determines if version1 is older (lower) than version2.
        /// </summary>
        bool IsDowngrade(string currentVersion, string targetVersion);
    }
}
