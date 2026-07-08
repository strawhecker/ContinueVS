using System;

namespace ContinueVS.Services
{
    /// <summary>
    /// Semantic version comparison using System.Version.
    /// Handles pre-release versions by stripping suffixes.
    /// </summary>
    public class VersionComparator : IVersionComparator
    {
        /// <summary>
        /// Compares two version strings.
        /// Returns: 1 if v1 > v2, -1 if v1 < v2, 0 if equal or invalid.
        /// Handles pre-release versions by stripping suffix before comparison.
        /// </summary>
        public int CompareVersions(string version1, string version2)
        {
            if (string.IsNullOrWhiteSpace(version1) || string.IsNullOrWhiteSpace(version2))
                return 0;

            try
            {
                var v1 = ParseVersion(version1);
                var v2 = ParseVersion(version2);

                if (v1 == null || v2 == null)
                    return 0;

                return v1.CompareTo(v2);
            }
            catch
            {
                // Invalid version format; treat as equal
                return 0;
            }
        }

        /// <summary>
        /// Determines if targetVersion is older (lower) than currentVersion.
        /// </summary>
        public bool IsDowngrade(string currentVersion, string targetVersion)
        {
            return CompareVersions(currentVersion, targetVersion) > 0;
        }

        /// <summary>
        /// Parse version string, stripping pre-release suffix.
        /// "2.1.0-beta" → "2.1.0"
        /// "2.0.0" → "2.0.0"
        /// Returns null if parsing fails.
        /// </summary>
        private Version ParseVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return null;

            // Strip pre-release suffix (everything after '-')
            var cleanVersion = versionString.Split('-')[0].Trim();

            if (Version.TryParse(cleanVersion, out var version))
                return version;

            return null;
        }
    }
}
