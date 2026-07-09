using Microsoft.VisualStudio.Shell;
using System;
using ContinueVS.Settings;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Extension methods for <see cref="IBridgeConfiguration"/> to export bridge feature flags
    /// and configuration to environment variables for the npm process.
    /// 
    /// Used during bridge startup (Step 45: BridgeLifecycleManager) to configure the Node.js
    /// Continue server process before spawning.
    /// </summary>
    internal static class BridgeConfigurationExtensions
    {
        /// <summary>
        /// Exports bridge feature flags to environment variables based on the current
        /// extension options and configuration state.
        /// 
        /// This method reads the <see cref="ContinueOptionsPage.EnableBridgeMode"/> setting
        /// and exports it as the <c>FEATURE_FLAG_BRIDGE_MODE</c> environment variable.
        /// The npm server process (core-server.js) checks this flag at startup and falls back
        /// to legacy translator if the flag is false.
        /// 
        /// Called by: <see cref="BridgeLifecycleManager"/> (Step 45) before spawning the
        /// npm process via <see cref="StdioTransport"/> (Step 19).
        /// </summary>
        /// <param name="configuration">The bridge configuration instance.</param>
        /// <remarks>
        /// Environment variable mapping:
        /// - <c>FEATURE_FLAG_BRIDGE_MODE</c> = "true"|"false" (from ContinueOptionsPage.EnableBridgeMode)
        /// 
        /// The npm process interprets these as follows:
        /// - "true" → use npm-based bridge for all operations
        /// - "false" → fall back to legacy translator binary
        /// 
        /// This method is safe to call multiple times; it simply overwrites the environment
        /// variable with the current setting.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
        internal static void ExportBridgeFlagsAsEnvironmentVariables(this IBridgeConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Read EnableBridgeMode from the options page
            bool enableBridgeMode = ReadEnableBridgeModeFromOptions();

            // Export as environment variable (npm process expects "true" or "false" string)
            Environment.SetEnvironmentVariable("FEATURE_FLAG_BRIDGE_MODE", enableBridgeMode ? "true" : "false");
        }

        /// <summary>
        /// Reads the EnableBridgeMode setting from the ContinueOptionsPage.
        /// 
        /// If the options page is not yet initialized (unlikely during normal startup),
        /// or if running in a test environment without VS shell assemblies available,
        /// defaults to true (bridge mode enabled).
        /// </summary>
        /// <returns>The value of ContinueOptionsPage.EnableBridgeMode, or true if not available.</returns>
        private static bool ReadEnableBridgeModeFromOptions()
        {
            try
            {
                if (ContinueVSPackage.Instance == null)
                {
                    // Package not yet initialized; default to true
                    return true;
                }

                var optionsPage = ContinueVSPackage.Instance.GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage;
                if (optionsPage == null)
                {
                    // Options page not available; default to true
                    return true;
                }

                return optionsPage.EnableBridgeMode;
            }
            catch (System.IO.FileNotFoundException)
            {
                // Test environment without VS shell assemblies; default to true
                return true;
            }
            catch
            {
                // Any other error reading options; default to true (safe fallback)
                return true;
            }
        }
    }
}
