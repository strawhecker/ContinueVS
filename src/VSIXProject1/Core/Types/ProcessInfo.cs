namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Snapshot of the currently being-debugged process (gap92_2 inspection surface).
    /// </summary>
    public sealed class ProcessInfo
    {
        /// <summary>
        /// OS process id.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Process name (best effort).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// True when the process is actively being debugged.
        /// </summary>
        public bool IsBeingDebugged { get; set; }

        /// <summary>
        /// Count of programs loaded in the process (best effort).
        /// </summary>
        public int ProgramCount { get; set; }
    }
}
