using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Collects runtime workspace fields from <see cref="IIdeService"/> and <see cref="IDebuggerService"/>
    /// and caches a <see cref="WorkspaceStats"/> snapshot for system prompt injection.
    /// This class is a singleton; <see cref="Refresh"/> is called once per prompt-build from a non-UI thread.
    /// </summary>
    public sealed class WorkspaceStatsService : IWorkspaceStatsService
    {
        private readonly IIdeService _ideService;
        private readonly IDebuggerService _debuggerService;

        // Optional test seam: when non-null, used as git root instead of calling IIdeService.GetGitRootPathAsync()
        private readonly string? _testGitRoot;

        private WorkspaceStats? _stats;

        public WorkspaceStatsService(IIdeService ideService, IDebuggerService debuggerService, string? testGitRoot = null)
        {
            _ideService = ideService ?? throw new ArgumentNullException(nameof(ideService));
            _debuggerService = debuggerService ?? throw new ArgumentNullException(nameof(debuggerService));
            _testGitRoot = testGitRoot;
        }

        /// <inheritdoc/>
        public WorkspaceStats GetStats()
        {
            if (_stats == null)
                Refresh();
            return _stats!;
        }

        /// <inheritdoc/>
        public void Refresh()
        {
            var s = new WorkspaceStats();

            s.ActiveFile = CollectActiveFile();
            s.GitBranch = CollectGitBranch();

            var gitRoot = CollectGitRoot();

            s.GitRemote = CollectGitRemote(gitRoot);
            s.SolutionPath = CollectSolutionPath(gitRoot);
            s.TargetFrameworks = CollectTargetFrameworks(gitRoot);
            s.Shell = CollectShell();
            CollectDebugState(s);
            s.CompletedGaps = CollectCompletedGaps(gitRoot);

            _stats = s;
        }

        private string CollectActiveFile()
        {
            try
            {
#pragma warning disable VSTHRD002
                var path = _ideService.GetActiveDocumentPathAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                return string.IsNullOrWhiteSpace(path) ? "none" : path;
            }
            catch { return "none"; }
        }

        private string CollectGitBranch()
        {
            try
            {
#pragma warning disable VSTHRD002
                var branch = _ideService.GetBranchAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                return string.IsNullOrWhiteSpace(branch) ? "unknown" : branch;
            }
            catch { return "unknown"; }
        }

        private string CollectGitRoot()
        {
            if (_testGitRoot != null)
                return _testGitRoot;
            try
            {
#pragma warning disable VSTHRD002
                var root = _ideService.GetGitRootPathAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                return string.IsNullOrWhiteSpace(root) ? string.Empty : root;
            }
            catch { return string.Empty; }
        }

        private static string CollectGitRemote(string gitRoot)
        {
            if (string.IsNullOrWhiteSpace(gitRoot) || !Directory.Exists(gitRoot))
                return "none";
            try
            {
                var psi = new ProcessStartInfo("git", "remote get-url origin")
                {
                    WorkingDirectory = gitRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    var output = (proc?.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
                    proc?.WaitForExit();
                    return string.IsNullOrWhiteSpace(output) ? "none" : output;
                }
            }
            catch { return "none"; }
        }

        private static string CollectSolutionPath(string gitRoot)
        {
            if (string.IsNullOrWhiteSpace(gitRoot) || !Directory.Exists(gitRoot))
                return "none";
            try
            {
                var slnx = Directory.GetFiles(gitRoot, "*.slnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (slnx != null) return slnx;
                var sln = Directory.GetFiles(gitRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                return sln ?? "none";
            }
            catch { return "none"; }
        }

        private static string CollectTargetFrameworks(string gitRoot)
        {
            if (string.IsNullOrWhiteSpace(gitRoot) || !Directory.Exists(gitRoot))
                return "unknown";
            try
            {
                var tfms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var csproj in Directory.GetFiles(gitRoot, "*.csproj", SearchOption.AllDirectories))
                {
                    try
                    {
                        var doc = XDocument.Load(csproj);
                        foreach (var el in doc.Descendants())
                        {
                            if (el.Name.LocalName == "TargetFramework" || el.Name.LocalName == "TargetFrameworks")
                            {
                                foreach (var tfm in el.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    var t = tfm.Trim();
                                    if (!string.IsNullOrEmpty(t)) tfms.Add(t);
                                }
                            }
                        }
                    }
                    catch { /* skip malformed csproj */ }
                }
                return tfms.Count > 0 ? string.Join(",", tfms.OrderBy(x => x)) : "unknown";
            }
            catch { return "unknown"; }
        }

        private static string CollectShell()
        {
            if (Environment.GetEnvironmentVariable("PSModulePath") != null)
                return "powershell.exe";
            if (Environment.GetEnvironmentVariable("COMSPEC") != null)
                return "cmd.exe";
            return "powershell.exe";
        }

        private void CollectDebugState(WorkspaceStats s)
        {
            try
            {
#pragma warning disable VSTHRD002
                var state = _debuggerService.GetCurrentStateAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                if (state == null)
                {
                    s.DebugMode = "none";
                    s.BreakLocation = "none";
                    return;
                }
                if (state.IsRunning)
                {
                    s.DebugMode = "run";
                    s.BreakLocation = "none";
                }
                else
                {
                    s.DebugMode = "break";
                    s.BreakLocation = (!string.IsNullOrWhiteSpace(state.CurrentFile) && state.CurrentLine > 0)
                        ? $"{state.CurrentFile}:{state.CurrentLine}"
                        : "none";
                }
            }
            catch
            {
                s.DebugMode = "none";
                s.BreakLocation = "none";
            }
        }

        private static readonly Regex CompletedGapPattern =
            new Regex(@"###\s+(gap\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string CollectCompletedGaps(string gitRoot)
        {
            if (string.IsNullOrWhiteSpace(gitRoot) || !Directory.Exists(gitRoot))
                return "none";
            try
            {
                var sessionContextPath = Path.Combine(gitRoot, "docs", "session-context.md");
                if (!File.Exists(sessionContextPath))
                    return "none";

                var gaps = new List<string>();
                foreach (var line in File.ReadLines(sessionContextPath))
                {
                    // Line must contain the ✅ character and a gap ID on the same or nearby line
                    if (line.Contains("\u2705"))
                    {
                        var match = CompletedGapPattern.Match(line);
                        if (match.Success)
                            gaps.Add(match.Groups[1].Value);
                    }
                }
                return gaps.Count > 0 ? string.Join(",", gaps) : "none";
            }
            catch { return "none"; }
        }
    }
}
