using System;
using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the current runtime state during debugging.
    /// Includes local variables, callstack, watches, and execution status.
    /// </summary>
    public class RuntimeState
    {
        /// <summary>
        /// Local variables in current scope (key: variable name, value: value representation).
        /// </summary>
        public Dictionary<string, string> Locals { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Callstack frames (list of method names and file:line info).
        /// </summary>
        public List<CallStackFrame> CallStack { get; set; } = new List<CallStackFrame>();

        /// <summary>
        /// Watch expressions and their current values (key: expression, value: value representation).
        /// </summary>
        public Dictionary<string, string> Watches { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Current thread identifier.
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// True if execution is paused at breakpoint; false if running.
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Current line number where execution is paused.
        /// </summary>
        public int CurrentLine { get; set; }

        /// <summary>
        /// Current file path where execution is paused.
        /// </summary>
        public string? CurrentFile { get; set; }

        /// <summary>
        /// Timestamp when state was captured.
        /// </summary>
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a single frame in the callstack.
    /// </summary>
    public class CallStackFrame
    {
        /// <summary>
        /// Method name.
        /// </summary>
        public string? MethodName { get; set; }

        /// <summary>
        /// File path.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Line number (1-based).
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Frame index (0 = current frame).
        /// </summary>
        public int FrameIndex { get; set; }
    }
}
