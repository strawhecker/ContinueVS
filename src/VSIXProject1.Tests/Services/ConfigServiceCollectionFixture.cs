using Xunit;

namespace ContinueVS.Services.Tests
{
    /// <summary>
    /// Collection fixture to ensure ConfigServiceTests and PolicyPersistenceTests run sequentially.
    /// Both test classes access the shared config file at ~/.continueVS/continueVS.json,
    /// so they must not run in parallel to avoid file lock contention.
    /// </summary>
    [CollectionDefinition("ConfigService Collection")]
    public class ConfigServiceCollection : ICollectionFixture<ConfigServiceCollectionFixture>
    {
    }

    /// <summary>
    /// Marker class for coordinating sequential execution of config-dependent tests.
    /// </summary>
    public class ConfigServiceCollectionFixture
    {
    }
}
