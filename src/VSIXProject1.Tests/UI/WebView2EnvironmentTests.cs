using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Web.WebView2.Core;

namespace ContinueVS.Tests.UI
{
    /// <summary>
    /// Unit tests for WebView2 CoreWebView2Environment creation and initialization.
    /// 
    /// This test class verifies the isolated behavior of environment creation without
    /// requiring a full UI control or WebView2 host. Used for b1 step verification.
    /// </summary>
    public class WebView2EnvironmentTests
    {
        /// <summary>
        /// STEP b1.8: Test isolated environment creation with proper folder handling.
        /// Verifies that CreateAsync() succeeds and produces a valid environment object.
        /// </summary>
        [Fact]
        public async Task CreateEnvironmentAsync_WithValidUserDataFolder_Succeeds()
        {
            // Arrange: Create a temporary folder for isolated testing
            var tempFolder = Path.Combine(Path.GetTempPath(), $"CV-WebView2-Test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);

            try
            {
                // Act: Create environment with temporary folder
                System.Diagnostics.Debug.WriteLine($"[b1-TEST] Creating environment with folder: {tempFolder}");

                var env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: tempFolder);

                // Assert: Environment object is created and accessible
                Assert.NotNull(env);
                Assert.False(string.IsNullOrEmpty(env.BrowserVersionString));
                System.Diagnostics.Debug.WriteLine($"[b1-TEST] Environment created successfully. BrowserVersion={env.BrowserVersionString}");
            }
            finally
            {
                // Cleanup: Remove temporary folder and contents
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, recursive: true);
                    System.Diagnostics.Debug.WriteLine($"[b1-TEST] Cleaned up temporary folder: {tempFolder}");
                }
            }
        }

        /// <summary>
        /// STEP b1.8: Test environment creation with default paths.
        /// Verifies that production folder paths work correctly.
        /// </summary>
        [Fact]
        public async Task CreateEnvironmentAsync_WithProductionPath_Succeeds()
        {
            // Arrange: Use production path similar to ContinueToolWindowControl
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ContinueVS_Test", "WebView2");

            // Ensure folder exists
            Directory.CreateDirectory(userDataFolder);

            try
            {
                // Act: Create environment with production-like path
                System.Diagnostics.Debug.WriteLine($"[b1-TEST] Creating environment with production path: {userDataFolder}");

                var env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder);

                // Assert: Environment is functional
                Assert.NotNull(env);
                Assert.NotNull(env.UserDataFolder);
                System.Diagnostics.Debug.WriteLine($"[b1-TEST] Production path environment created. UserDataFolder={env.UserDataFolder}");
            }
            finally
            {
                // Cleanup: Remove test folder
                if (Directory.Exists(userDataFolder))
                {
                    try
                    {
                        Directory.Delete(userDataFolder, recursive: true);
                        System.Diagnostics.Debug.WriteLine($"[b1-TEST] Cleaned up production test folder: {userDataFolder}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b1-TEST] Warning: Could not clean up folder - {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// STEP b1.8: Test environment creation idempotency.
        /// Verifies that multiple CreateAsync calls with same folder succeed.
        /// </summary>
        [Fact]
        public async Task CreateEnvironmentAsync_MultipleCallsSameFolder_Succeeds()
        {
            // Arrange
            var tempFolder = Path.Combine(Path.GetTempPath(), $"CV-WebView2-Idempotency-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);

            try
            {
                // Act & Assert: Create multiple environments with same folder
                System.Diagnostics.Debug.WriteLine($"[b1-TEST] Testing idempotency with folder: {tempFolder}");

                var env1 = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: tempFolder);
                Assert.NotNull(env1);
                System.Diagnostics.Debug.WriteLine("[b1-TEST] First environment creation succeeded");

                var env2 = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: tempFolder);
                Assert.NotNull(env2);
                System.Diagnostics.Debug.WriteLine("[b1-TEST] Second environment creation with same folder succeeded (idempotent)");
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, recursive: true);
                    System.Diagnostics.Debug.WriteLine($"[b1-TEST] Cleaned up idempotency test folder: {tempFolder}");
                }
            }
        }
    }
}
