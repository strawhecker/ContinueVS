using System;
using System.Collections.Generic;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Result of running a test, including diagnostics and parsed stack frames.
    /// </summary>
    public class TestRunResult
    {
        /// <summary>
        /// Exit code from test execution (0 = success, non-zero = failure).
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Standard output from test execution.
        /// </summary>
        public string Stdout { get; set; }

        /// <summary>
        /// Standard error from test execution.
        /// </summary>
        public string Stderr { get; set; }

        /// <summary>
        /// Number of stack frames parsed from output.
        /// </summary>
        public int FrameCount { get; set; }

        /// <summary>
        /// List of parsed stack trace frames extracted from output.
        /// </summary>
        public List<StackTraceFrame> ParsedFrames { get; set; }

        /// <summary>
        /// Diagnostic message summarizing the test result.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Whether test execution succeeded (ExitCode == 0).
        /// </summary>
        public bool Succeeded => ExitCode == 0;

        public TestRunResult()
        {
            Stdout = string.Empty;
            Stderr = string.Empty;
            Message = string.Empty;
            ParsedFrames = new List<StackTraceFrame>();
        }

        public TestRunResult(int exitCode, string stdout, string stderr, string message = "")
        {
            ExitCode = exitCode;
            Stdout = stdout ?? string.Empty;
            Stderr = stderr ?? string.Empty;
            Message = message ?? string.Empty;
            ParsedFrames = new List<StackTraceFrame>();
            FrameCount = 0;
        }
    }
}
