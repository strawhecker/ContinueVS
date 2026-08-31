using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        private readonly IConfigService? _configService;

        // Optional test seam: when non-null, used as git root instead of running git rev-parse
        private readonly string? _testGitRoot;
        // Optional test seam: when non-null, returned as branch instead of running git rev-parse
        private readonly string? _testGitBranch;

        private WorkspaceStats? _stats;

        // Resolved once at construction time from user config + environment probes.
        private readonly string _gitExe;

        public WorkspaceStatsService(
            IIdeService ideService,
            IDebuggerService debuggerService,
            IConfigService? configService = null,
            string? testGitRoot = null,
            string? testGitBranch = null)
        {
            _ideService = ideService ?? throw new ArgumentNullException(nameof(ideService));
            _debuggerService = debuggerService ?? throw new ArgumentNullException(nameof(debuggerService));
            _configService = configService;
            _testGitRoot = testGitRoot;
            _testGitBranch = testGitBranch;
            _gitExe = ResolveGitExe(GetUserConfiguredGitPath());
            System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] git resolved to: {_gitExe}");
        }

        private string? GetUserConfiguredGitPath()
        {
            try
            {
                // ConfigService.GetCurrentConfig() may throw if not initialized yet.
                // This can happen during DI setup before ServiceInitializer runs.
                // Return null to allow fallback resolution (system PATH, registry, etc.).
                var cfg = _configService?.GetCurrentConfig();
                return string.IsNullOrWhiteSpace(cfg?.GitPath) ? null : cfg!.GitPath;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not been initialized"))
            {
                // ConfigService not initialized yet; return null for fallback resolution
                System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] ConfigService not initialized during constructor, using fallback git resolution: {ex.Message}");
                return null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Resolves the git executable at startup using a priority-ordered probe chain:
        /// 1. User-configured path from config.json (highest priority)
        /// 2. VS-bundled git (ships with VS for source control)
        /// 3. Windows registry GitForWindows InstallPath
        /// 4. GIT_EXEC_PATH / GIT_INSTALL_ROOT environment variables
        /// 5. Walk the process PATH entries
        /// 6. Common Git-for-Windows install locations
        /// 7. Bare "git" fallback (works when git is already on PATH in the host process)
        /// </summary>
        private static string ResolveGitExe(string? userPath)
        {
            // 1. User override
            if (!string.IsNullOrWhiteSpace(userPath) && File.Exists(userPath))
            {
                System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] git: using user config path: {userPath}");
                return userPath!;
            }

            // 2. VS-bundled git — most reliable when devenv.exe has a stripped PATH
            //    VSAPPIDDIR points to the VS IDE directory (e.g. ...\Common7\IDE\)
            var vsAppIdDir = Environment.GetEnvironmentVariable("VSAPPIDDIR");
            if (!string.IsNullOrWhiteSpace(vsAppIdDir))
            {
                var vsBundled = Path.Combine(
                    vsAppIdDir.TrimEnd(Path.DirectorySeparatorChar),
                    "..", "..",
                    "CommonExtensions", "Microsoft", "TeamFoundation",
                    "Team Explorer", "Git", "cmd", "git.exe");
                try
                {
                    var full = Path.GetFullPath(vsBundled);
                    if (File.Exists(full))
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] git: using VS-bundled git: {full}");
                        return full;
                    }
                }
                catch { /* path resolution failed */ }
            }

            // 3. Windows registry: HKLM\SOFTWARE\GitForWindows → InstallPath
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\GitForWindows"))
                {
                    var installPath = key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(installPath))
                    {
                        foreach (var rel in new[] { @"bin\git.exe", @"cmd\git.exe" })
                        {
                            var candidate = Path.Combine(installPath, rel);
                            if (File.Exists(candidate))
                            {
                                System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] git: registry hit: {candidate}");
                                return candidate;
                            }
                        }
                    }
                }
            }
            catch { /* registry access failed */ }

            // 4. GIT_EXEC_PATH / GIT_INSTALL_ROOT environment variables
            foreach (var envVar in new[] { "GIT_EXEC_PATH", "GIT_INSTALL_ROOT" })
            {
                var val = Environment.GetEnvironmentVariable(envVar);
                if (string.IsNullOrWhiteSpace(val)) continue;
                foreach (var rel in new[] { "git.exe", @"bin\git.exe", @"cmd\git.exe" })
                {
                    try
                    {
                        var candidate = Path.Combine(val, rel);
                        if (File.Exists(candidate))
                        {
                            System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] git: env var {envVar} hit: {candidate}");
                            return candidate;
                        }
                    }
                    catch { }
                }
            }

            // 5. Walk the process PATH
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), "git.exe");
                    if (File.Exists(candidate))
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] git: PATH hit: {candidate}");
                        return candidate;
                    }
                }
                catch { }
            }

            // 6. Common Git-for-Windows install locations
            var common = new[]
            {
                @"C:\Program Files\Git\bin\git.exe",
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files (x86)\Git\bin\git.exe",
            };
            foreach (var c in common)
            {
                if (File.Exists(c))
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] git: common path hit: {c}");
                    return c;
                }
            }

            // 7. Bare fallback
            System.Diagnostics.Debug.WriteLine(
                "[WorkspaceStatsService] git: all probes failed; falling back to bare 'git'. " +
                "Set 'gitPath' in ~/.continueVS/config.json to override.");
            return "git";
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
            // Collect DTE-dependent values on the calling (UI) thread before
            // dispatching to a thread-pool thread.
            string activeFile = CollectActiveFile();
            string? solutionDir = CollectSolutionDir();

            // Dispatch remaining (git/filesystem/blocking) work to a thread-pool thread so the
            // inner .GetAwaiter().GetResult() calls never block the VS UI thread.
#pragma warning disable VSTHRD002
            Task.Run(() => RefreshCore(activeFile, solutionDir)).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }

        private void RefreshCore(string activeFile, string? solutionDir)
        {
            var s = new WorkspaceStats();

            s.ActiveFile = activeFile;

            var workDir = ResolveWorkDirFromFile(activeFile, solutionDir);
            s.GitBranch = CollectGitBranch(workDir);

            var gitRoot = CollectGitRoot(workDir);

            s.GitRemote = CollectGitRemote(gitRoot);
            // Try git root first for solution file; fall back to VS solution dir if not found
            s.SolutionPath = CollectSolutionPath(gitRoot, solutionDir);
            s.TargetFrameworks = CollectTargetFrameworks(gitRoot);
            s.Shell = CollectShell();
            CollectDebugState(s);
            s.CompletedGaps = CollectCompletedGaps(gitRoot);

            _stats = s;
            System.Diagnostics.Debug.WriteLine($"[WorkspaceStatsService] Refresh complete: ActiveFile={s.ActiveFile}, GitBranch={s.GitBranch}, SolutionPath={s.SolutionPath}");
        }

        private static string? ResolveWorkDirFromFile(string activeFile, string? solutionDir)
        {
            if (!string.IsNullOrWhiteSpace(activeFile) && activeFile != "none")
            {
                var dir = Path.GetDirectoryName(activeFile);
                if (dir != null && Directory.Exists(dir)) return dir;
            }

            // Active file unavailable (tool window focused) — fall back to solution directory.
            // _dte.Solution.FullName is always populated when a solution is open.
            if (!string.IsNullOrWhiteSpace(solutionDir) && Directory.Exists(solutionDir))
                return solutionDir;

            return FindGitRootFromAssembly() ?? Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Walks up the directory tree starting from the executing assembly location,
        /// returning the first directory that contains a <c>.git</c> folder.
        /// Returns <c>null</c> if no git root is found.
        /// </summary>
        private static string? FindGitRootFromAssembly()
        {
            try
            {
                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var dir = Path.GetDirectoryName(assemblyLocation);
                while (!string.IsNullOrEmpty(dir))
                {
                    if (Directory.Exists(Path.Combine(dir, ".git")))
                        return dir;
                    var parent = Path.GetDirectoryName(dir);
                    if (parent == dir) break; // reached filesystem root
                    dir = parent;
                }
            }
            catch { }
            return null;
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

        private string? CollectSolutionDir()
        {
            try { return _ideService.GetSolutionDirectory(); }
            catch { return null; }
        }

        private string CollectGitBranch(string? workDir)
        {
            if (_testGitBranch != null) return _testGitBranch;
            if (string.IsNullOrWhiteSpace(workDir)) return "unknown";
            try
            {
                var psi = new ProcessStartInfo(_gitExe, "rev-parse --abbrev-ref HEAD")
                {
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    var output = (proc?.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
                    proc?.WaitForExit();
                    return string.IsNullOrWhiteSpace(output) ? "unknown" : output;
                }
            }
            catch { return "unknown"; }
        }

        private string CollectGitRoot(string? workDir)
        {
            if (_testGitRoot != null)
                return _testGitRoot;
            if (string.IsNullOrWhiteSpace(workDir)) return string.Empty;
            try
            {
                var psi = new ProcessStartInfo(_gitExe, "rev-parse --show-toplevel")
                {
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    var output = (proc?.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
                    proc?.WaitForExit();
                    if (string.IsNullOrWhiteSpace(output)) return string.Empty;
                    return output.Replace('/', Path.DirectorySeparatorChar);
                }
            }
            catch { return string.Empty; }
        }

        private string CollectGitRemote(string gitRoot)
        {
            if (string.IsNullOrWhiteSpace(gitRoot) || !Directory.Exists(gitRoot))
                return "none";
            try
            {
                var psi = new ProcessStartInfo(_gitExe, "remote get-url origin")
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

        private static string CollectSolutionPath(string primaryRoot, string? fallbackRoot = null)
        {
            // Try primary root first (git root or VS solution dir)
            if (!string.IsNullOrWhiteSpace(primaryRoot) && Directory.Exists(primaryRoot))
            {
                try
                {
                    var slnx = Directory.GetFiles(primaryRoot, "*.slnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (slnx != null) return slnx;
                    var sln = Directory.GetFiles(primaryRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (sln != null) return sln;
                }
                catch { /* continue to fallback */ }
            }

            // If primary root found nothing, try fallback root (VS solution dir if git root was used)
            if (!string.IsNullOrWhiteSpace(fallbackRoot) && fallbackRoot != primaryRoot && Directory.Exists(fallbackRoot))
            {
                try
                {
                    var slnx = Directory.GetFiles(fallbackRoot, "*.slnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (slnx != null) return slnx;
                    var sln = Directory.GetFiles(fallbackRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (sln != null) return sln;
                }
                catch { /* fallthrough */ }
            }

            return "none";
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
