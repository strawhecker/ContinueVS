#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ContinueVS.Tests.Fixtures
{
    /// <summary>
    /// xUnit fixture that tracks and cleans up spawned processes.
    /// 
    /// Automatically kills all tracked processes when the fixture is disposed,
    /// preventing zombie processes from tests.
    /// 
    /// Usage:
    ///   [Collection("Process Cleanup Collection")]
    ///   public class MyTests : ICollectionFixture&lt;ProcessCleanupFixture&gt;
    ///   {
    ///       private readonly ProcessCleanupFixture _processFixture;
    ///       
    ///       public MyTests(ProcessCleanupFixture processFixture)
    ///       {
    ///           _processFixture = processFixture;
    ///       }
    ///       
    ///       [Fact]
    ///       public void TestSpawnsProcess()
    ///       {
    ///           var process = new Process
    ///           {
    ///               StartInfo = new ProcessStartInfo("cmd.exe")
    ///               {
    ///                   UseShellExecute = false
    ///               }
    ///           };
    ///           process.Start();
    ///           _processFixture.TrackProcess(process);
    ///           
    ///           // Test logic...
    ///           // Process will be killed automatically in Dispose()
    ///       }
    ///   }
    /// </summary>
    public class ProcessCleanupFixture : IDisposable
    {
        /// <summary>
        /// List of processes being tracked for cleanup.
        /// </summary>
        private readonly List<Process> _trackedProcesses = new List<Process>();

        /// <summary>
        /// Flag to track disposal state.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Registers a process for automatic cleanup.
        /// </summary>
        /// <param name="process">The process to track and eventually kill</param>
        public void TrackProcess(Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            lock (_trackedProcesses)
            {
                _trackedProcesses.Add(process);
            }
        }

        /// <summary>
        /// Gets the count of currently tracked processes.
        /// </summary>
        public int TrackedProcessCount
        {
            get
            {
                lock (_trackedProcesses)
                {
                    return _trackedProcesses.Count;
                }
            }
        }

        /// <summary>
        /// Kills all tracked processes.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method that kills all tracked processes.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_trackedProcesses)
                {
                    foreach (var process in _trackedProcesses)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                            }
                        }
                        catch
                        {
                            // Best effort cleanup; process may have already exited
                        }
                        finally
                        {
                            process?.Dispose();
                        }
                    }

                    _trackedProcesses.Clear();
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Finalizer to ensure cleanup if Dispose() is not called.
        /// </summary>
        ~ProcessCleanupFixture()
        {
            Dispose(false);
        }
    }
}
