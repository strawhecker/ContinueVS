using System;
using System.IO;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Services.Tests
{
    /// <summary>
    /// Unit tests for policy persistence and restoration (gap27_16).
    /// Tests verify that continuation policy preferences are saved to and loaded from configuration.
    ///
    /// Each test uses a unique temp directory so it is fully isolated from the real user
    /// config (~/.continueVS/continueVS.json) and from other tests that share it, eliminating
    /// file-lock/state races that could cause intermittent failures.
    /// </summary>
    [Collection("ConfigService Collection")]
    public class PolicyPersistenceTests : IDisposable
    {
        private readonly string _testConfigDir;

        public PolicyPersistenceTests()
        {
            // Create a unique temp directory for this test instance to isolate config file I/O.
            _testConfigDir = Path.Combine(Path.GetTempPath(), "ContinueVSTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testConfigDir);
        }

        /// <summary>
        /// Creates a ConfigService pointing at the test's isolated temp directory.
        /// </summary>
        private ConfigService CreateConfigService()
        {
            return new ConfigService(null, _testConfigDir);
        }

        /// <summary>
        /// Cleans up the isolated temp directory and ensures no file handles are left open.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testConfigDir))
                {
                    Directory.Delete(_testConfigDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Test 1: SavePolicy_Persists_To_Config
        /// Verifies that SaveDefaultPolicyAsync persists policy to config.json.
        /// </summary>
        [Fact]
        public async Task SavePolicy_Persists_To_Config()
        {
            // Arrange: Create ConfigService in isolated temp dir
            var service = CreateConfigService();
            await service.InitializeAsync();

            // Act: Save policy as Auto
            await service.SaveDefaultPolicyAsync(ContinuationPolicy.Auto);

            // Create new ConfigService instance to load same config file
            var service2 = CreateConfigService();
            await service2.InitializeAsync();

            // Assert: Verify Auto policy was restored
            var restoredPolicy = await service2.GetDefaultPolicyAsync();
            Assert.Equal(ContinuationPolicy.Auto, restoredPolicy);
        }

        /// <summary>
        /// Test 2: GetPolicy_Returns_InteractiveByDefault
        /// Verifies that GetDefaultPolicyAsync returns Interactive when no policy is configured.
        /// </summary>
        [Fact]
        public async Task GetPolicy_Returns_InteractiveByDefault()
        {
            // Arrange: Create ConfigService in isolated temp dir (no prior policy saved)
            var service = CreateConfigService();
            await service.InitializeAsync();

            // Act: Call GetDefaultPolicyAsync without prior SaveDefaultPolicyAsync
            var policy = await service.GetDefaultPolicyAsync();

            // Assert: Returns Interactive as safe default
            Assert.Equal(ContinuationPolicy.Interactive, policy);
        }

        /// <summary>
        /// Test 3: RestorePolicy_On_Startup
        /// Verifies that policy saved in one ConfigService instance is available in another
        /// (simulating restart scenario).
        /// </summary>
        [Fact]
        public async Task RestorePolicy_On_Startup()
        {
            // Arrange: Create ConfigService and save policy
            var service1 = CreateConfigService();
            await service1.InitializeAsync();
            await service1.SaveDefaultPolicyAsync(ContinuationPolicy.Deferred);

            // Act: ConfigService 2 loads same config (simulating restart)
            var service2 = CreateConfigService();
            await service2.InitializeAsync();
            var restoredPolicy = await service2.GetDefaultPolicyAsync();

            // Assert: Policy is restored
            Assert.Equal(ContinuationPolicy.Deferred, restoredPolicy);
        }

        /// <summary>
        /// Edge case: InvalidPolicy_DefaultsToInteractive
        /// Verifies that corrupted/invalid policy value defaults to Interactive.
        /// </summary>
        [Fact]
        public async Task InvalidPolicy_DefaultsToInteractive()
        {
            // Arrange: Create ConfigService and manually inject invalid policy value
            var service = CreateConfigService();
            await service.InitializeAsync();

            // Corrupt the config by setting invalid policy value
            var config = service.GetCurrentConfig();
            config.CustomSettings["defaultContinuationPolicy"] = "InvalidValue";

            // Act: Attempt to retrieve policy
            var policy = await service.GetDefaultPolicyAsync();

            // Assert: Falls back to Interactive
            Assert.Equal(ContinuationPolicy.Interactive, policy);
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
            // Arrange
            var service1 = CreateConfigService();
            await service1.InitializeAsync();

            // Act
            await service1.SaveDefaultPolicyAsync(policy);

            var service2 = CreateConfigService();
            await service2.InitializeAsync();
            var restored = await service2.GetDefaultPolicyAsync();

            // Assert
            Assert.Equal(policy, restored);
        }
    }
}
