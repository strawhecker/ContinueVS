#nullable enable

using System;
using System.IO;

namespace ContinueVS.Tests.Fixtures
{
    /// <summary>
    /// xUnit fixture that provides a temporary directory for test isolation.
    /// 
    /// Each test class using [Collection("Temp Directory Collection")] receives
    /// a unique temp directory that is automatically cleaned up after the test.
    /// 
    /// Usage:
    ///   [Collection("Temp Directory Collection")]
    ///   public class MyTests : ICollectionFixture&lt;TempDirectoryFixture&gt;
    ///   {
    ///       private readonly TempDirectoryFixture _tempFixture;
    ///       
    ///       public MyTests(TempDirectoryFixture tempFixture)
    ///       {
    ///           _tempFixture = tempFixture;
    ///       }
    ///       
    ///       [Fact]
    ///       public void TestUsesTempDirectory()
    ///       {
    ///           var filePath = Path.Combine(_tempFixture.TempPath, "test.txt");
    ///           File.WriteAllText(filePath, "test content");
    ///           Assert.True(File.Exists(filePath));
    ///       }
    ///   }
    /// </summary>
    public class TempDirectoryFixture : IDisposable
    {
        /// <summary>
        /// The full path to the temporary directory for this fixture.
        /// </summary>
        public string TempPath { get; }

        /// <summary>
        /// Flag to track disposal state.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes the fixture by creating a unique temporary directory.
        /// </summary>
        public TempDirectoryFixture()
        {
            // Create a unique temp directory
            TempPath = Path.Combine(
                Path.GetTempPath(),
                $"ContinueVS-Test-{Guid.NewGuid()}");

            Directory.CreateDirectory(TempPath);
        }

        /// <summary>
        /// Cleans up the temporary directory and all its contents.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for proper cleanup pattern.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    if (Directory.Exists(TempPath))
                    {
                        Directory.Delete(TempPath, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup; don't throw from finalizer
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizer to ensure cleanup if Dispose() is not called.
        /// </summary>
        ~TempDirectoryFixture()
        {
            Dispose(false);
        }
    }
}
