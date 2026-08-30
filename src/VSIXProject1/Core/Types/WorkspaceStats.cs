namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Flat snapshot of runtime workspace fields collected once per prompt-build.
    /// All fields fall back to "unknown" or "none" when the source is unavailable.
    /// </summary>
    public sealed class WorkspaceStats
    {
        /// <summary>Full path of the active document, or "none".</summary>
        public string ActiveFile { get; set; } = "none";

        /// <summary>Current git branch name, or "unknown".</summary>
        public string GitBranch { get; set; } = "unknown";

        /// <summary>Remote origin URL (git remote get-url origin), or "none".</summary>
        public string GitRemote { get; set; } = "none";

        /// <summary>Absolute path to the solution file (.slnx / .sln), or "none".</summary>
        public string SolutionPath { get; set; } = "none";

        /// <summary>Comma-separated unique TFMs found in all .csproj files, or "unknown".</summary>
        public string TargetFrameworks { get; set; } = "unknown";

        /// <summary>Active shell: "powershell.exe" or "cmd.exe".</summary>
        public string Shell { get; set; } = "powershell.exe";

        /// <summary>String name of the current chat mode, or "unknown".</summary>
        public string ChatMode { get; set; } = "unknown";

        /// <summary>"design" | "break" | "run" | "none".</summary>
        public string DebugMode { get; set; } = "none";

        /// <summary>"file:line" when paused at a breakpoint, or "none".</summary>
        public string BreakLocation { get; set; } = "none";

        /// <summary>Comma-separated gap IDs whose status is ✅ Complete, or "none".</summary>
        public string CompletedGaps { get; set; } = "none";
    }
}
