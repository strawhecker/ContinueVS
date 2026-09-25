namespace ContinueVS.Core.Types
{
    /// <summary>
    /// State gate required by an inspection call (gap92_2). Mirrors which EnvDTE operations are
    /// meaningful in which debugger mode.
    /// </summary>
    public enum InspectionGate
    {
        /// <summary>
        /// Any live session (run or break). Used for thread/module/process/exception-setting/output reads.
        /// </summary>
        Any,

        /// <summary>
        /// Break mode only. Used for stack/frame/locals/arguments/this/statement and current-exception reads.
        /// </summary>
        Paused
    }
}
