#nullable enable

using System;
using System.IO;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Fixtures
{
    /// <summary>
    /// Test factory that roots a <see cref="SessionService"/> in a unique temporary
    /// directory (<c>%TEMP%\ContinueVS-Test-Session-{guid}</c>) instead of the real
    /// user folder (<c>~/.continueVS/sessions</c>).
    ///
    /// Every unit test that constructs a <see cref="SessionService"/> for persistence
    /// must go through this factory so session files are never written into real user
    /// data. The temporary directory is removed when <see cref="Dispose"/> is called.
    /// </summary>
    public sealed class TempSessionServiceFactory : IDisposable
    {
        /// <summary>
        /// The full path to the temporary directory backing this factory.
        /// Multiple services created from the same factory share this directory
        /// (needed where save/load across instances must resolve to one location).
        /// </summary>
        public string DirectoryPath { get; }

        private bool _disposed;

        /// <summary>
        /// Creates a factory backed by a fresh, unique temporary directory.
        /// </summary>
        public TempSessionServiceFactory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "ContinueVS-Test-Session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        /// <summary>
        /// Creates a <see cref="SessionService"/> writing to the factory's temporary
        /// directory via the 2-arg constructor, guaranteeing test isolation.
        /// </summary>
        public SessionService Create(ITokenCountingService tokenCountingService)
        {
            if (tokenCountingService == null)
            {
                throw new ArgumentNullException(nameof(tokenCountingService));
            }

            return new SessionService(tokenCountingService, DirectoryPath);
        }

        /// <summary>
        /// Removes the temporary directory and all session files created by tests.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing && Directory.Exists(DirectoryPath))
            {
                try
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup; never throw from Dispose.
                }
            }

            _disposed = true;
        }
    }
}
