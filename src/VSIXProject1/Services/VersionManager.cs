using System;
using Microsoft.Win32;

namespace ContinueVS.Services
{
    /// <summary>
    /// Service to manage the active bridge version preference.
    /// Loads and persists the selected version to the Visual Studio registry.
    /// </summary>
    public class VersionManager
    {
        private const string RegistryPath = @"Software\ContinueVS";
        private const string VersionKeyName = "ActiveBridgeVersion";
        private const string DefaultVersion = "2.0.0";

        private readonly VersionSelectorService _versionSelector;
        private string? _cachedVersion;

        public VersionManager(VersionSelectorService? versionSelector = null)
        {
            _versionSelector = versionSelector ?? new VersionSelectorService();
            _cachedVersion = null;
        }

        /// <summary>
        /// Gets the currently active bridge version.
        /// Loads from registry on first call, then caches the result.
        /// Validates that the stored version exists; falls back to default if not.
        /// </summary>
        public string GetActiveVersion()
        {
            // Return cached version if already loaded
            if (!string.IsNullOrEmpty(_cachedVersion))
            {
                return _cachedVersion!;
            }

            // Try to load from registry
            var registryVersion = LoadVersionFromRegistry();

            if (!string.IsNullOrEmpty(registryVersion) && _versionSelector.IsVersionAvailable(registryVersion!))
            {
                _cachedVersion = registryVersion;
                return _cachedVersion!;
            }

            // If registry version is invalid or missing, validate and use default
            if (_versionSelector.IsVersionAvailable(DefaultVersion))
            {
                _cachedVersion = DefaultVersion;
                // Attempt to persist the default version to registry for next time
                SaveVersionToRegistry(DefaultVersion);
                return _cachedVersion;
            }

            // If even the default isn't available, get the first available version
            var availableVersions = _versionSelector.GetAvailableVersions();
            if (availableVersions.Count > 0)
            {
                _cachedVersion = availableVersions[0];
                SaveVersionToRegistry(_cachedVersion);
                return _cachedVersion;
            }

            // Fallback if no versions are available (should not happen in normal operation)
            _cachedVersion = DefaultVersion;
            return _cachedVersion;
        }

        /// <summary>
        /// Sets the active bridge version (with validation).
        /// Only allows setting to versions that are known to be available.
        /// </summary>
        public bool SetActiveVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            // Validate that the requested version exists
            if (!_versionSelector.IsVersionAvailable(version))
            {
                return false;
            }

            _cachedVersion = version;
            SaveVersionToRegistry(version);
            return true;
        }

        /// <summary>
        /// Resets the active version back to the default.
        /// </summary>
        public void ResetToDefault()
        {
            _cachedVersion = DefaultVersion;
            SaveVersionToRegistry(DefaultVersion);
        }

        /// <summary>
        /// Clears the cached version to force a fresh load from registry on next call.
        /// Useful for testing or after registry updates.
        /// </summary>
        public void ClearCache()
        {
            _cachedVersion = null;
        }

        /// <summary>
        /// Loads the version string from the Visual Studio registry.
        /// Returns null if the registry key doesn't exist or can't be read.
        /// </summary>
        private string? LoadVersionFromRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null)
                    {
                        return null;
                    }

                    var value = key.GetValue(VersionKeyName);
                    return value?.ToString();
                }
            }
            catch (Exception)
            {
                // If any registry operation fails, return null to use default
                return null;
            }
        }

        /// <summary>
        /// Saves the version string to the Visual Studio registry.
        /// Creates the registry key if it doesn't exist.
        /// </summary>
        private void SaveVersionToRegistry(string version)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue(VersionKeyName, version, RegistryValueKind.String);
                    }
                }
            }
            catch (Exception)
            {
                // If registry write fails, we continue anyway
                // The version is cached in memory and will be used for this session
            }
        }
    }
}
