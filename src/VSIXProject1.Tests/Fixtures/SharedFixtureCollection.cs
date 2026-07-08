#nullable enable

using Xunit;

namespace ContinueVS.Tests.Fixtures
{
    /// <summary>
    /// xUnit collection definition that combines TempDirectoryFixture and ProcessCleanupFixture.
    /// 
    /// Enables test classes to use a shared fixture collection for safe parallel test execution.
    /// Tests in the same collection will not run in parallel; tests in different collections
    /// may run in parallel.
    /// 
    /// Usage:
    ///   [Collection("Bridge Test Collection")]
    ///   public class MyTests
    ///   {
    ///       private readonly TempDirectoryFixture _tempFixture;
    ///       private readonly ProcessCleanupFixture _processFixture;
    ///       
    ///       public MyTests(TempDirectoryFixture tempFixture, ProcessCleanupFixture processFixture)
    ///       {
    ///           _tempFixture = tempFixture;
    ///           _processFixture = processFixture;
    ///       }
    ///   }
    /// </summary>
    [CollectionDefinition("Bridge Test Collection")]
    public class SharedFixtureCollection : 
        ICollectionFixture<TempDirectoryFixture>,
        ICollectionFixture<ProcessCleanupFixture>
    {
        // This class is never instantiated; it only defines the collection for xUnit
    }

    /// <summary>
    /// Collection definition for tests requiring only a temporary directory.
    /// </summary>
    [CollectionDefinition("Temp Directory Collection")]
    public class TempDirectoryCollection : ICollectionFixture<TempDirectoryFixture>
    {
        // This class is never instantiated; it only defines the collection for xUnit
    }

    /// <summary>
    /// Collection definition for tests requiring process cleanup.
    /// </summary>
    [CollectionDefinition("Process Cleanup Collection")]
    public class ProcessCleanupCollection : ICollectionFixture<ProcessCleanupFixture>
    {
        // This class is never instantiated; it only defines the collection for xUnit
    }
}
