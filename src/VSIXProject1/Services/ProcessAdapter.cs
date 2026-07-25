using System;
using System.Diagnostics;

namespace VSIXProject1.Services
{
    /// <summary>
    /// Interface for process operations to enable testing and dependency injection
    /// </summary>
    public interface IProcessAdapter
    {
        /// <summary>
        /// Gets whether the process has exited
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Waits for the process to exit
        /// </summary>
        /// <param name="millisecondsTimeout">Timeout in milliseconds</param>
        /// <returns>True if process exited before timeout; false otherwise</returns>
        bool WaitForExit(int millisecondsTimeout);

        /// <summary>
        /// Terminates the process
        /// </summary>
        void Kill();
    }

    /// <summary>
    /// Concrete adapter for System.Diagnostics.Process
    /// </summary>
    public class ProcessAdapter : IProcessAdapter
    {
        private readonly Process _process;

        public ProcessAdapter(Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));
            _process = process;
        }

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    // Process is not associated with a running process; treat as exited
                    return true;
                }
            }
        }

        public bool WaitForExit(int millisecondsTimeout)
        {
            try
            {
                return _process.WaitForExit(millisecondsTimeout);
            }
            catch (InvalidOperationException)
            {
                // Process is not associated with a running process
                return true;
            }
        }

        public void Kill()
        {
            try
            {
                _process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Process is not associated or already exited; safe to ignore
            }
        }
    }
}
