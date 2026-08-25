namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration of internal phase types for debug instruction processing.
    /// Each phase represents a distinct strategy attempt in the debugging workflow.
    /// </summary>
    public enum InternalPhaseType
    {
        /// <summary>
        /// Analysis phase: inspect code, logs, runtime state to understand the problem.
        /// </summary>
        Analysis = 0,

        /// <summary>
        /// Breakpoint phase: set breakpoints and inspect runtime state.
        /// </summary>
        Breakpoint = 1,

        /// <summary>
        /// Instrumentation phase: add logging, monitoring, or diagnostic output.
        /// </summary>
        Instrumentation = 2,

        /// <summary>
        /// Test phase: run tests to validate or reproduce the issue.
        /// </summary>
        Test = 3,

        /// <summary>
        /// Observation phase: gather data without modifying code.
        /// </summary>
        Observation = 4
    }
}
