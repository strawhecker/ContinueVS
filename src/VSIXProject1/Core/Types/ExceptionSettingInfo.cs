namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Snapshot of a single exception-handling rule / setting (gap92_2 inspection surface).
    /// Mirrors the EnvDTE exception break-state model.
    /// </summary>
    public sealed class ExceptionSettingInfo
    {
        /// <summary>
        /// Exception category/group name (e.g. "Common Language Runtime Exceptions") or the
        /// individual exception name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The break-state behavior for this exception setting.
        /// </summary>
        public ExceptionBehavior Behavior { get; set; }

        /// <summary>
        /// True when a breakpoint is set for this exception (the debugger will stop on it).
        /// </summary>
        public bool IsBreakWhenThrown { get; set; }
    }

    /// <summary>
    /// How the debugger treats an exception setting.
    /// </summary>
    public enum ExceptionBehavior
    {
        /// <summary>The debugger never breaks on this exception.</summary>
        Continue,
        /// <summary>The debugger breaks (first-chance) when this exception is thrown.</summary>
        Break,
        /// <summary>The debugger only notifies (second-chance handling).</summary>
        Notify,
        /// <summary>The behavior is unknown/unspecified.</summary>
        Unknown
    }
}
