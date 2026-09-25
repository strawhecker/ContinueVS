namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Snapshot of the current (most recent) exception being inspected (gap92_2 inspection surface).
    /// </summary>
    public sealed class ExceptionInfo
    {
        /// <summary>
        /// Exception type/name (e.g. "System.NullReferenceException").
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Exception numeric code (best effort).
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Exception description/message.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// True when this exception was encountered as a first-chance exception.
        /// </summary>
        public bool IsFirstChance { get; set; }

        /// <summary>
        /// True when this exception was encountered after (re)throwing (second chance).
        /// </summary>
        public bool IsSecondChance { get; set; }
    }
}
