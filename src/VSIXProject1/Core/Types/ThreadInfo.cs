namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Lightweight snapshot of a debugger thread (gap92_2 inspection surface).
    /// </summary>
    public sealed class ThreadInfo
    {
        /// <summary>
        /// EnvDTE thread id.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Thread display name (best effort).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// True when the thread is alive.
        /// </summary>
        public bool IsAlive { get; set; }

        /// <summary>
        /// True when the thread is frozen (suspended by the debugger).
        /// </summary>
        public bool IsFrozen { get; set; }

        /// <summary>
        /// Thread priority string (best effort).
        /// </summary>
        public string? Priority { get; set; }

        /// <summary>
        /// The suspend count for this thread.
        /// </summary>
        public int SuspendCount { get; set; }
    }
}
