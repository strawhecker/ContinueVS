using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services
{
    /// <summary>
    /// Service to discover and enumerate available npm bridge versions from the src/versions/ directory.
    /// Each version directory (v2.0.0, v2.1.0, etc.) must contain a manifest.json file.
    /// </summary>
    public class VersionSelectorService
    {
        private readonly string _versionsBasePath;

        public VersionSelectorService(string? versionsBasePath = null)
        {
            if (versionsBasePath != null)
            {
                _versionsBasePath = versionsBasePath;
            }
            else
            {
                // Default: derive from extension assembly location
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var extensionDir = Path.GetDirectoryName(assemblyPath);
                _versionsBasePath = Path.Combine(extensionDir ?? "", "..\\..\\..\\versions");
            }
        }

        /// <summary>
        /// Discovers all available bridge versions from the versions directory.
        /// Returns a list of version strings (e.g., ["2.0.0", "2.1.0"]).
        /// </summary>
        public virtual List<string> GetAvailableVersions()
        {
            var versions = new List<string>();

            try
            {
                if (!Directory.Exists(_versionsBasePath))
                {
                    return versions;
                }

                // Look for directories matching pattern vX.Y.Z
                var versionDirs = Directory.GetDirectories(_versionsBasePath, "v*");

                foreach (var dir in versionDirs)
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName == null || !dirName.StartsWith("v"))
                        continue;

                    var versionString = dirName.Substring(1); // Remove leading 'v'

                    // Validate that manifest.json exists in this version directory
                    var manifestPath = Path.Combine(dir, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        // Optional: validate manifest structure
                        if (IsValidManifest(manifestPath))
                        {
                            versions.Add(versionString);
                        }
                    }
                }

                // Sort versions in descending order (newest first)
                versions.Sort(CompareVersions);
                versions.Reverse();
            }
            catch (Exception)
            {
                // If any error occurs during discovery, return empty list
                // The caller should handle gracefully with a default version
            }

            return versions;
        }

        /// <summary>
        /// Checks if a specific version exists and has a valid manifest.
        /// </summary>
        public virtual bool IsVersionAvailable(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            var versionDir = Path.Combine(_versionsBasePath, $"v{version}");
            var manifestPath = Path.Combine(versionDir, "manifest.json");

            try
            {
                return File.Exists(manifestPath) && IsValidManifest(manifestPath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the manifest.json for a specific version and returns the parsed content.
        /// </summary>
        public JObject? GetVersionManifest(string version)
        {
            if (!IsVersionAvailable(version))
                return null;

            try
            {
                var versionDir = Path.Combine(_versionsBasePath, $"v{version}");
                var manifestPath = Path.Combine(versionDir, "manifest.json");
                var json = File.ReadAllText(manifestPath);
                return JObject.Parse(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets metadata about a specific version from its manifest.
        /// </summary>
        public VersionMetadata? GetVersionMetadata(string version)
        {
            var manifest = GetVersionManifest(version);
            if (manifest == null)
                return null;

            try
            {
                return new VersionMetadata
                {
                    Version = version,
                    ContinueVersion = manifest["continueVersion"]?.Value<string>(),
                    ReleaseDate = manifest["releaseDate"]?.Value<string>(),
                    Status = manifest["status"]?.Value<string>() ?? "unknown",
                    IsStable = (manifest["status"]?.Value<string>() ?? "").Equals("stable", StringComparison.OrdinalIgnoreCase)
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Validates that a manifest.json file exists and has minimal required structure.
        /// </summary>
        private bool IsValidManifest(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                    return false;

                var json = File.ReadAllText(manifestPath);
                var manifest = JObject.Parse(json);

                // Require at least "version" field in manifest
                return manifest["version"] != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Compares two version strings semantically (e.g., "2.0.0" vs "2.1.0").
        /// Returns -1 if v1 < v2, 0 if equal, 1 if v1 > v2.
        /// </summary>
        private int CompareVersions(string v1, string v2)
        {
            if (!Version.TryParse(v1, out var ver1) || !Version.TryParse(v2, out var ver2))
            {
                return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
            }

            return ver1.CompareTo(ver2);
        }
    }

    /// <summary>
    /// Metadata about a bridge version extracted from its manifest.json.
    /// </summary>
    public class VersionMetadata
    {
        public string? Version { get; set; }
        public string? ContinueVersion { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Status { get; set; }
        public bool IsStable { get; set; }

        public override string ToString()
        {
            return $"Version {Version} (Continue {ContinueVersion}, {Status})";
        }
    }
}
