using System;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Options for running a test with diagnostic settings.
    /// </summary>
    public class TestRunOptions
    {
        /// <summary>
        /// Path to the test file or test method identifier.
        /// </summary>
        public string TestPath { get; set; }

        /// <summary>
        /// Enable debug mode (capture breakpoint hits, variable states).
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Verbosity level (0=quiet, 1=normal, 2=verbose, 3=very verbose).
        /// </summary>
        public int Verbosity { get; set; }

        /// <summary>
        /// File path to set a breakpoint before test execution.
        /// </summary>
        public string? BreakpointFile { get; set; }

        /// <summary>
        /// Line number for breakpoint (1-based).
        /// </summary>
        public int? BreakpointLine { get; set; }

        /// <summary>
        /// Maximum time allowed for test execution (default 30 seconds).
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Current iteration number (0-based) in failure analysis loop.
        /// </summary>
        public int CurrentIteration { get; set; }

        public TestRunOptions(string testPath)
        {
            TestPath = testPath ?? throw new ArgumentNullException(nameof(testPath));
            Debug = false;
            Verbosity = 1;
            CurrentIteration = 0;
        }
    }
}
