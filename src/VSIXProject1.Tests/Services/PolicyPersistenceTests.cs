using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Services.Tests
{
    /// <summary>
    /// Unit tests for policy persistence and restoration (gap27_16).
    /// Tests verify that continuation policy preferences are saved to and loaded from configuration.
    /// Note: Tests use try-finally for cleanup to ensure isolation despite shared config file access.
    /// </summary>
    [Collection("ConfigService Collection")]
    public class PolicyPersistenceTests : IDisposable
    {
        /// <summary>
        /// Cleans up the policy setting from config to ensure test isolation.
        /// Includes small delay to allow file handles to close.
        /// </summary>
        private static async Task CleanupPolicyAsync()
        {
            try
            {
                await Task.Delay(50); // Allow file handles to close
                var service = new ConfigService();
                await service.InitializeAsync();
                var config = service.GetCurrentConfig();
                if (config.CustomSettings.ContainsKey("defaultContinuationPolicy"))
                {
                    config.CustomSettings.Remove("defaultContinuationPolicy");
                    await service.SaveConfigAsync();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Disposes and cleans up.
        /// </summary>
        public void Dispose()
        {
            // Cleanup default policy on dispose
            _ = CleanupPolicyAsync();
        }

        /// <summary>
        /// Test 1: SavePolicy_Persists_To_Config
        /// Verifies that SaveDefaultPolicyAsync persists policy to config.json.
        /// </summary>
        [Fact]
        public async Task SavePolicy_Persists_To_Config()
        {
            try
            {
                // Arrange: Clean up initial state
                await CleanupPolicyAsync();

                var service = new ConfigService();
                await service.InitializeAsync();

                // Act: Save policy as Auto
                await service.SaveDefaultPolicyAsync(ContinuationPolicy.Auto);

                // Create new ConfigService instance to load same config file
                await Task.Delay(50);
                var service2 = new ConfigService();
                await service2.InitializeAsync();

                // Assert: Verify Auto policy was restored
                var restoredPolicy = await service2.GetDefaultPolicyAsync();
                Assert.Equal(ContinuationPolicy.Auto, restoredPolicy);
            }
            finally
            {
                // Cleanup
                await CleanupPolicyAsync();
            }
        }

        /// <summary>
        /// Test 2: GetPolicy_Returns_InteractiveByDefault
        /// Verifies that GetDefaultPolicyAsync returns Interactive when no policy is configured.
        /// </summary>
        [Fact]
        public async Task GetPolicy_Returns_InteractiveByDefault()
        {
            try
            {
                // Arrange: Create ConfigService and initialize (no prior policy saved)
                // Clear any previous policy from config to ensure clean state
                await CleanupPolicyAsync();

                var service = new ConfigService();
                await service.InitializeAsync();

                // Act: Call GetDefaultPolicyAsync without prior SaveDefaultPolicyAsync
                var policy = await service.GetDefaultPolicyAsync();

                // Assert: Returns Interactive as safe default
                Assert.Equal(ContinuationPolicy.Interactive, policy);
            }
            finally
            {
                // Cleanup
                await CleanupPolicyAsync();
            }
        }

        /// <summary>
        /// Test 3: RestorePolicy_On_Startup
        /// Verifies that policy saved in one ConfigService instance is available in another
        /// (simulating restart scenario).
        /// </summary>
        [Fact]
        public async Task RestorePolicy_On_Startup()
        {
            try
            {
                // Arrange: Clean up initial state
                await CleanupPolicyAsync();

                var service1 = new ConfigService();
                await service1.InitializeAsync();
                await service1.SaveDefaultPolicyAsync(ContinuationPolicy.Deferred);

                // Act: ConfigService 2 loads same config (simulating restart)
                await Task.Delay(50);
                var service2 = new ConfigService();
                await service2.InitializeAsync();
                var restoredPolicy = await service2.GetDefaultPolicyAsync();

                // Assert: Policy is restored
                Assert.Equal(ContinuationPolicy.Deferred, restoredPolicy);
            }
            finally
            {
                // Cleanup
                await CleanupPolicyAsync();
            }
        }

        /// <summary>
        /// Edge case: InvalidPolicy_DefaultsToInteractive
        /// Verifies that corrupted/invalid policy value defaults to Interactive.
        /// </summary>
        [Fact]
        public async Task InvalidPolicy_DefaultsToInteractive()
        {
            try
            {
                // Arrange: Create ConfigService and manually inject invalid policy value
                await CleanupPolicyAsync();

                var service = new ConfigService();
                await service.InitializeAsync();

                // Corrupt the config by setting invalid policy value
                var config = service.GetCurrentConfig();
                config.CustomSettings["defaultContinuationPolicy"] = "InvalidValue";

                // Act: Attempt to retrieve policy
                var policy = await service.GetDefaultPolicyAsync();

                // Assert: Falls back to Interactive
                Assert.Equal(ContinuationPolicy.Interactive, policy);
            }
            finally
            {
                // Cleanup
                await CleanupPolicyAsync();
            }
        }

        /// <summary>
        /// Edge case: AllPolicies_Persist_Correctly
        /// Verifies that all enum values (Auto, Interactive, Deferred) persist correctly.
        /// </summary>
        [Theory]
        [InlineData(ContinuationPolicy.Auto)]
        [InlineData(ContinuationPolicy.Interactive)]
        [InlineData(ContinuationPolicy.Deferred)]
        public async Task AllPolicies_Persist_Correctly(ContinuationPolicy policy)
        {
            try
            {
                // Arrange
                await CleanupPolicyAsync();

                var service1 = new ConfigService();
                await service1.InitializeAsync();

                // Act
                await service1.SaveDefaultPolicyAsync(policy);

                await Task.Delay(50);
                var service2 = new ConfigService();
                await service2.InitializeAsync();
                var restored = await service2.GetDefaultPolicyAsync();

                // Assert
                Assert.Equal(policy, restored);
            }
            finally
            {
                // Cleanup
                await CleanupPolicyAsync();
            }
        }
    }
}



