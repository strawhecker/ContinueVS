using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Visual Studio implementation of IIdeService.
    /// Provides IDE integration for file operations, git, LSP, and editor state.
    /// Full VS automation wiring is a future gap; this stub satisfies DI and returns safe defaults.
    /// </summary>
    internal class VsIdeService : IIdeService
    {
        private readonly IDteProvider _dteProvider;

        public VsIdeService(IDteProvider dteProvider)
        {
            _dteProvider = dteProvider ?? throw new ArgumentNullException(nameof(dteProvider));
        }

        #pragma warning disable CS0067 // Events are part of IIdeService contract; raised by future VS automation wiring
        public event EventHandler<FileChangedEventArgs>? FileChanged;
        public event EventHandler<ActiveFileChangedEventArgs>? ActiveFileChanged;
#pragma warning restore CS0067

        // File Operations

        public Task<string> ReadFileAsync(string filepath)
        {
            if (string.IsNullOrWhiteSpace(filepath))
                throw new ArgumentException("filepath must not be empty.", nameof(filepath));

            return Task.FromResult(File.Exists(filepath) ? File.ReadAllText(filepath) : string.Empty);
        }

        public Task WriteFileAsync(string filepath, string contents)
        {
            if (string.IsNullOrWhiteSpace(filepath))
                throw new ArgumentException("filepath must not be empty.", nameof(filepath));

            File.WriteAllText(filepath, contents);
            return Task.CompletedTask;
        }

        public Task<string> ReadRangeInFileAsync(string filepath, int startLine, int endLine)
        {
            if (string.IsNullOrWhiteSpace(filepath))
                throw new ArgumentException("filepath must not be empty.", nameof(filepath));

            if (!File.Exists(filepath))
                return Task.FromResult(string.Empty);

            var lines = File.ReadAllLines(filepath);
            var start = Math.Max(0, startLine - 1);
            var end = Math.Min(lines.Length - 1, endLine - 1);
            if (start > end)
                return Task.FromResult(string.Empty);

            return Task.FromResult(string.Join(Environment.NewLine, lines.Skip(start).Take(end - start + 1)));
        }

        public Task SaveFileAsync(string filepath)
        {
            // VS automation save will be wired in a future gap.
            return Task.CompletedTask;
        }

        public Task DeleteFileAsync(string filepath)
        {
            if (string.IsNullOrWhiteSpace(filepath))
                throw new ArgumentException("filepath must not be empty.", nameof(filepath));

            if (File.Exists(filepath))
                File.Delete(filepath);

            return Task.CompletedTask;
        }

        // Git Operations

        public Task<string> GetActiveDocumentPathAsync()
        {
            try
            {
                var path = _dteProvider.GetActiveFilepath();
                return Task.FromResult(string.IsNullOrWhiteSpace(path) ? "none" : path);
            }
            catch
            {
                return Task.FromResult("none");
            }
        }

        public Task<string> GetBranchAsync()
        {
            try
            {
                var workDir = ResolveGitWorkingDir();
                if (workDir == null) return Task.FromResult(string.Empty);
                var branch = RunGit("rev-parse --abbrev-ref HEAD", workDir);
                return Task.FromResult(branch);
            }
            catch { return Task.FromResult(string.Empty); }
        }

        public Task<string> GetRepoNameAsync()
        {
            try
            {
                var root = RunGitRootSync();
                if (string.IsNullOrWhiteSpace(root)) return Task.FromResult(string.Empty);
                return Task.FromResult(Path.GetFileName((root ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            }
            catch { return Task.FromResult(string.Empty); }
        }

        public Task<string> GetGitRootPathAsync()
        {
            try
            {
                var root = RunGitRootSync();
                return Task.FromResult(root ?? string.Empty);
            }
            catch { return Task.FromResult(string.Empty); }
        }

        private string? ResolveGitWorkingDir()
        {
            try
            {
                var activeFile = _dteProvider.GetActiveFilepath();
                if (!string.IsNullOrWhiteSpace(activeFile))
                {
                    var dir = Path.GetDirectoryName(activeFile);
                    if (dir != null && Directory.Exists(dir)) return dir;
                }
            }
            catch { /* fall through */ }
            return Directory.GetCurrentDirectory();
        }

        private string? RunGitRootSync()
        {
            var workDir = ResolveGitWorkingDir();
            if (workDir == null) return null;
            var result = RunGit("rev-parse --show-toplevel", workDir);
            // git outputs forward slashes on Windows; normalise to backslash
            return string.IsNullOrWhiteSpace(result) ? null : result.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string RunGit(string args, string workingDir)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    var output = proc?.StandardOutput.ReadToEnd()?.Trim() ?? string.Empty;
                    proc?.WaitForExit();
                    return output;
                }
            }
            catch { return string.Empty; }
        }

        // LSP Operations

        public Task<IEnumerable<Location>> GotoDefinitionAsync(Location location)
            => Task.FromResult(Enumerable.Empty<Location>());

        public Task<IEnumerable<Location>> GetReferencesAsync(Location location)
            => Task.FromResult(Enumerable.Empty<Location>());

        public Task<IEnumerable<DocumentSymbol>> GetDocumentSymbolsAsync(string filepath)
            => Task.FromResult(Enumerable.Empty<DocumentSymbol>());

        public Task<IEnumerable<Diagnostic>> GetProblemsAsync(string filepath)
            => Task.FromResult(Enumerable.Empty<Diagnostic>());

        // Subprocess Execution

        public Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd)
            => Task.FromResult((string.Empty, string.Empty));

        // Editor State

        public string? GetActiveFilepath() => _dteProvider.GetActiveFilepath();

        public string? GetSolutionDirectory() => _dteProvider.GetSolutionDirectory();

        public string? GetSelectedText()
        {
            Debug.WriteLine("[gap33-getselected] VsIdeService.GetSelectedText delegating to DteProvider");
            return _dteProvider.GetSelectedText();
        }

        public Selection? GetCursorSelection()
        {
            Debug.WriteLine("[gap33-getcursor] VsIdeService.GetCursorSelection delegating to DteProvider");
            return _dteProvider.GetCursorSelection();
        }

        // File Queries

        public bool FileExists(string filepath)
        {
            if (string.IsNullOrWhiteSpace(filepath))
                return false;

            return File.Exists(filepath);
        }

        public IEnumerable<string> GetWorkspaceFiles(string pattern = "*")
            => Enumerable.Empty<string>();

        // VS Editor Operations

        public Task OpenFileInEditorAsync(string filePath)
        {
            return OpenFileInEditorCoreAsync(filePath);
        }

#pragma warning disable VSTHRD109 // Multiple early returns don't switch threads; only called when needed
        private async Task OpenFileInEditorCoreAsync(string filePath)
#pragma warning restore VSTHRD109
        {
            Debug.WriteLine("[gap8_3-ideservice-start] VsIdeService.OpenFileInEditorAsync called");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.WriteLine("[gap8_3-ideservice-null-path] File path is null or empty");
                return;
            }

            try
            {
                Debug.WriteLine($"[gap8_3-ideservice-file] File path: {filePath}");

                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[gap8_3-ideservice-not-exists] File does not exist: {filePath}");
                    return;
                }

                Debug.WriteLine("[gap8_3-ideservice-dte-call] Getting DTE from ServiceProvider");

                // Get the DTE (VS automation object) via the VS service provider on the main thread
                DTE? dte = null;

                try
                {
                    // RunAsync runs on a background thread, but switches to main thread internally
                    await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        try
                        {
                            if (ContinueVSPackage.Instance != null)
                            {
                                dte = await ContinueVSPackage.Instance.GetServiceAsync(typeof(DTE)) as DTE;
                                if (dte == null)
                                {
                                    Debug.WriteLine("[gap8_3-ideservice-dte-null] GetServiceAsync returned null");
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[gap8_3-ideservice-pkg-null] ContinueVSPackage.Instance is null");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[gap8_3-ideservice-dte-error] Error getting DTE: {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[gap8_3-ideservice-dte-getservice-error] Failed to get DTE service: {ex.Message}");
                }

                if (dte == null)
                {
                    Debug.WriteLine("[gap8_3-ideservice-dte-null] DTE is null, cannot open file");
                    return;
                }

                Debug.WriteLine("[gap8_3-ideservice-dte-opfile] Calling DTE.ItemOperations.OpenFile");
                // Ensure we're on the UI thread before accessing DTE
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                dte.ItemOperations.OpenFile(filePath, Constants.vsViewKindTextView);

                Debug.WriteLine("[gap8_3-ideservice-complete] File opened successfully in editor");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_3-ideservice-error] Error opening file in editor: {ex.Message}");
                Debug.WriteLine($"[gap8_3-ideservice-error-stack] {ex.StackTrace}");
            }
        }

        // Test Runner Operations
        public async Task<TestRunResult> RunTestAsync(string testPath, TestRunOptions options, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(testPath))
                throw new ArgumentException("testPath must not be empty.", nameof(testPath));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var result = new TestRunResult();

            try
            {
                // Execute test via dotnet test subprocess
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(options.Timeout);

                var testCommand = $"dotnet test --filter FullyQualifiedName~{testPath}";
                if (options.Verbosity > 1)
                    testCommand += " --verbosity detailed";

                using var proc = new System.Diagnostics.Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"test --filter FullyQualifiedName~{testPath}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                proc.Start();

                // Capture output
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                var exitCodeTask = new TaskCompletionSource<int>();

                var waitTask = Task.Run(() =>
                {
                    proc.WaitForExit();
                    exitCodeTask.SetResult(proc.ExitCode);
                }, cts.Token);

                try
                {
                    await Task.WhenAny(waitTask, Task.Delay((int)options.Timeout.TotalMilliseconds, cts.Token));
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(); } catch { }
                    result.ExitCode = -1;
                    result.Message = $"Test execution timeout after {options.Timeout.TotalSeconds}s";
                    return result;
                }

                result.Stdout = await stdoutTask;
                result.Stderr = await stderrTask;
                result.ExitCode = proc.ExitCode;
                result.Message = result.ExitCode == 0 ? "Test passed" : "Test failed";
                result.FrameCount = 0;

                Debug.WriteLine($"[gap29_2-test-run] Test: {testPath}, ExitCode: {result.ExitCode}, Frames: {result.FrameCount}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap29_2-test-error] Error running test: {ex.Message}");
                result.ExitCode = -1;
                result.Message = $"Error: {ex.Message}";
                return result;
            }
        }

        // Debug Operations

        public async Task<RuntimeState?> InspectVariablesAsync(CancellationToken cancellationToken = default)
        {
            // This is a stub implementation. In a real scenario, this would use IDebuggerService.
            // For now, return null indicating no active debugger.
            return await Task.FromResult<RuntimeState?>(null);
        }

        public async Task<BreakpointInfo?> SetBreakpointAsync(string filePath, int lineNumber, string? condition = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath must not be empty.", nameof(filePath));
            if (lineNumber <= 0)
                throw new ArgumentException("lineNumber must be positive.", nameof(lineNumber));

            // This is a stub implementation.
            var info = new BreakpointInfo
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                IsEnabled = true,
                HitCount = 0,
                Condition = condition,
                BreakpointId = Guid.NewGuid().ToString()
            };

            return await Task.FromResult<BreakpointInfo?>(info);
        }

        public async Task<bool> ClearBreakpointAsync(string filePath, int lineNumber, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath must not be empty.", nameof(filePath));
            if (lineNumber <= 0)
                throw new ArgumentException("lineNumber must be positive.", nameof(lineNumber));

            // This is a stub implementation.
            return await Task.FromResult(true);
        }

        public async Task<RuntimeState?> StepAsync(DebugStepAction action, CancellationToken cancellationToken = default)
        {
            // This is a stub implementation.
            return await Task.FromResult<RuntimeState?>(null);
        }

        public async Task ResumeDebugAsync(CancellationToken cancellationToken = default)
        {
            // Enforce 30-second timeout for resume operation
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                try
                {
                    // This is a stub implementation.
                    await Task.Delay(100, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("Execution did not resume within 30 seconds.");
                }
            }
        }
    }
}
