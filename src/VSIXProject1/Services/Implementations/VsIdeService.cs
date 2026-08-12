#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using VSIXProject1.Services;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IIdeService that wraps DTEAdapter and ProcessAdapter.
    /// Provides file operations, git integration, subprocess execution, and editor state access.
    /// </summary>
    public class VsIdeService : IIdeService
    {
        private readonly ContinueVS.Services.IDTEService? _dteService;
        private readonly IProcessAdapter? _processAdapter;
        private const int ProcessTimeoutMs = 30000; // 30 second timeout for subprocess operations

        /// <summary>
        /// Initializes a new instance of VsIdeService.
        /// </summary>
        /// <param name="dteService">The DTE service adapter for IDE integration.</param>
        /// <param name="processAdapter">The process adapter for subprocess execution. If null, subprocess operations will fail gracefully.</param>
        internal VsIdeService(ContinueVS.Services.IDTEService? dteService, IProcessAdapter? processAdapter = null)
        {
            _dteService = dteService;
            _processAdapter = processAdapter;
        }

        #region File Operations

        /// <summary>
        /// Reads the entire contents of a file.
        /// </summary>
        /// <param name="filepath">The path to the file to read.</param>
        /// <returns>The file contents.</returns>
        public async Task<string> ReadFileAsync(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath), "File path cannot be null or empty.");

            return await Task.Run(() =>
            {
                try
                {
                    return File.ReadAllText(filepath);
                }
                catch (Exception ex) when (!(ex is ArgumentNullException))
                {
                    throw new InvalidOperationException($"Failed to read file '{filepath}': {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Writes contents to a file.
        /// </summary>
        /// <param name="filepath">The path to the file to write.</param>
        /// <param name="contents">The contents to write.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task WriteFileAsync(string filepath, string contents)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath), "File path cannot be null or empty.");

            if (contents == null)
                throw new ArgumentNullException(nameof(contents), "Contents cannot be null.");

            await Task.Run(() =>
            {
                try
                {
                    var directory = Path.GetDirectoryName(filepath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(filepath, contents);
                }
                catch (Exception ex) when (!(ex is ArgumentNullException))
                {
                    throw new InvalidOperationException($"Failed to write file '{filepath}': {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Reads a specific range of lines from a file.
        /// </summary>
        /// <param name="filepath">The path to the file.</param>
        /// <param name="startLine">Starting line number (1-based).</param>
        /// <param name="endLine">Ending line number (1-based).</param>
        /// <returns>The contents of the specified range.</returns>
        public async Task<string> ReadRangeInFileAsync(string filepath, int startLine, int endLine)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath), "File path cannot be null or empty.");

            if (startLine < 1)
                throw new ArgumentException("Start line must be >= 1.", nameof(startLine));

            if (endLine < startLine)
                throw new ArgumentException("End line must be >= start line.", nameof(endLine));

            return await Task.Run(() =>
            {
                try
                {
                    var lines = File.ReadAllLines(filepath);
                    if (startLine > lines.Length)
                        return string.Empty;

                    var adjustedEnd = Math.Min(endLine, lines.Length);
                    var requestedLines = lines.Skip(startLine - 1).Take(adjustedEnd - startLine + 1);
                    return string.Join(Environment.NewLine, requestedLines);
                }
                catch (Exception ex) when (!(ex is ArgumentException))
                {
                    throw new InvalidOperationException(
                        $"Failed to read range (lines {startLine}-{endLine}) from file '{filepath}': {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Saves a file that is currently open in the editor.
        /// </summary>
        /// <param name="filepath">The path to the file to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SaveFileAsync(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath), "File path cannot be null or empty.");

            await Task.Run(() =>
            {
                try
                {
                    // Attempt to save the file via DTE if available
                    if (_dteService?.Solution != null)
                    {
                        // Note: Full DTE document save would require active UI thread access.
                        // For now, we delegate to filesystem write if the file exists.
                        if (File.Exists(filepath))
                        {
                            // File is already on disk; nothing to do here.
                            // In a full implementation, this would interact with the editor
                            // to save unsaved changes via DTE.EnvDTE.Document.Save()
                            return;
                        }
                    }

                    // Fallback: if file doesn't exist, it cannot be saved.
                    if (!File.Exists(filepath))
                        throw new FileNotFoundException($"File '{filepath}' does not exist.");
                }
                catch (Exception ex) when (!(ex is ArgumentNullException))
                {
                    throw new InvalidOperationException($"Failed to save file '{filepath}': {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="filepath">The path to the file to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DeleteFileAsync(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath), "File path cannot be null or empty.");

            await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(filepath))
                    {
                        File.Delete(filepath);
                    }
                }
                catch (Exception ex) when (!(ex is ArgumentNullException))
                {
                    throw new InvalidOperationException($"Failed to delete file '{filepath}': {ex.Message}", ex);
                }
            });
        }

        #endregion

        #region Git Operations

        /// <summary>
        /// Gets the current git branch.
        /// </summary>
        /// <returns>The branch name.</returns>
        public async Task<string> GetBranchAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var gitRoot = GetGitRootSync();
                    if (string.IsNullOrEmpty(gitRoot))
                        throw new InvalidOperationException("Not in a git repository.");

                    var (stdout, stderr) = RunProcessSync("git", "rev-parse --abbrev-ref HEAD", gitRoot);
                    if (!string.IsNullOrEmpty(stderr))
                        throw new InvalidOperationException($"Failed to get branch: {stderr}");

                    return stdout.Trim();
                }
                catch (Exception ex) when (!(ex is ArgumentException))
                {
                    throw new InvalidOperationException($"Failed to get git branch: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Gets the repository name.
        /// </summary>
        /// <returns>The repository name.</returns>
        public async Task<string> GetRepoNameAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var gitRoot = GetGitRootSync();
                    if (string.IsNullOrEmpty(gitRoot))
                        throw new InvalidOperationException("Not in a git repository.");

                    var repoName = new DirectoryInfo(gitRoot).Name;
                    return repoName;
                }
                catch (Exception ex) when (!(ex is ArgumentException))
                {
                    throw new InvalidOperationException($"Failed to get repository name: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Gets the git root directory path.
        /// </summary>
        /// <returns>The path to the git root directory.</returns>
        public async Task<string> GetGitRootPathAsync()
        {
            return await Task.Run(() => GetGitRootSync());
        }

        private string GetGitRootSync()
        {
            try
            {
                var currentDir = Environment.CurrentDirectory;
                var (stdout, stderr) = RunProcessSync("git", "rev-parse --show-toplevel", currentDir);

                if (!string.IsNullOrEmpty(stderr) || string.IsNullOrEmpty(stdout))
                    return string.Empty;

                return stdout.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region LSP Operations (Stubs)

        /// <summary>
        /// Goes to the definition of a symbol.
        /// </summary>
        /// <param name="location">The location of the symbol.</param>
        /// <returns>An enumerable of definition locations.</returns>
        public async Task<IEnumerable<Location>> GotoDefinitionAsync(Location location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location), "Location cannot be null.");

            // LSP implementation deferred; return empty for now
            return await Task.FromResult(Enumerable.Empty<Location>());
        }

        /// <summary>
        /// Gets all references to a symbol.
        /// </summary>
        /// <param name="location">The location of the symbol.</param>
        /// <returns>An enumerable of reference locations.</returns>
        public async Task<IEnumerable<Location>> GetReferencesAsync(Location location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location), "Location cannot be null.");

            // LSP implementation deferred; return empty for now
            return await Task.FromResult(Enumerable.Empty<Location>());
        }

        /// <summary>
        /// Gets all symbols in a document.
        /// </summary>
        /// <param name="filepath">The file path.</param>
        /// <returns>An enumerable of document symbols.</returns>
        public async Task<IEnumerable<DocumentSymbol>> GetDocumentSymbolsAsync(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath), "File path cannot be null or empty.");

            // LSP implementation deferred; return empty for now
            return await Task.FromResult(Enumerable.Empty<DocumentSymbol>());
        }

        /// <summary>
        /// Gets all problems/diagnostics in a file.
        /// </summary>
        /// <param name="filepath">The file path.</param>
        /// <returns>An enumerable of diagnostics.</returns>
        public async Task<IEnumerable<Diagnostic>> GetProblemsAsync(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
                throw new ArgumentNullException(nameof(filepath), "File path cannot be null or empty.");

            // LSP implementation deferred; return empty for now
            return await Task.FromResult(Enumerable.Empty<Diagnostic>());
        }

        #endregion

        #region Subprocess Execution

        /// <summary>
        /// Runs a subprocess command.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="cwd">The working directory.</param>
        /// <returns>A tuple of (stdout, stderr).</returns>
        public async Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentNullException(nameof(command), "Command cannot be null or empty.");

            if (string.IsNullOrEmpty(cwd))
                throw new ArgumentNullException(nameof(cwd), "Working directory cannot be null or empty.");

            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(cwd))
                        throw new InvalidOperationException($"Working directory '{cwd}' does not exist.");

                    // Parse command into executable and arguments
                    var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0)
                        throw new ArgumentException("Command cannot be empty.", nameof(command));

                    var executable = parts[0];
                    var args = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;

                    return RunProcessSync(executable, args, cwd);
                }
                catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException))
                {
                    throw new InvalidOperationException($"Failed to run subprocess '{command}' in '{cwd}': {ex.Message}", ex);
                }
            });
        }

        private (string stdout, string stderr) RunProcessSync(string executable, string args, string cwd)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executable;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.WorkingDirectory = cwd;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();

                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();

                    if (!process.WaitForExit(ProcessTimeoutMs))
                    {
                        process.Kill();
                        throw new InvalidOperationException(
                            $"Process '{executable}' timed out after {ProcessTimeoutMs}ms");
                    }

                    return (stdout, stderr);
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"Failed to execute '{executable} {args}' in '{cwd}': {ex.Message}", ex);
            }
        }

        #endregion

        #region Editor State

        /// <summary>
        /// Gets the path of the currently active file.
        /// </summary>
        /// <returns>The file path, or null if no file is active.</returns>
        public string? GetActiveFilepath()
        {
            try
            {
                if (_dteService?.Solution == null)
                    return null;

                // In a full implementation, this would access DTE.ActiveDocument.FullName
                // For now, return null as we cannot reliably access the active document
                // without being on the main UI thread.
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the currently selected text in the editor.
        /// </summary>
        /// <returns>The selected text, or null if nothing is selected.</returns>
        public string? GetSelectedText()
        {
            try
            {
                if (_dteService?.Solution == null)
                    return null;

                // In a full implementation, this would access the active text selection
                // via DTE.ActiveDocument.Selection.Text
                // For now, return null as we cannot reliably access this without UI thread access.
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the current cursor selection.
        /// </summary>
        /// <returns>The current selection.</returns>
        public Selection? GetCursorSelection()
        {
            try
            {
                if (_dteService?.Solution == null)
                    return null;

                // In a full implementation, this would access DTE.ActiveDocument.Selection
                // and extract the start/end locations (line, column)
                // For now, return null as we cannot reliably access this without UI thread access.
                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region File Queries

        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="filepath">The path to check.</param>
        /// <returns>True if the file exists.</returns>
        public bool FileExists(string filepath)
        {
            try
            {
                if (string.IsNullOrEmpty(filepath))
                    return false;

                return File.Exists(filepath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all workspace files matching a pattern.
        /// </summary>
        /// <param name="pattern">The glob pattern (e.g., "*.cs").</param>
        /// <returns>An enumerable of matching file paths.</returns>
        public IEnumerable<string> GetWorkspaceFiles(string pattern = "*")
        {
            try
            {
                if (string.IsNullOrEmpty(pattern))
                    pattern = "*";

                var gitRoot = GetGitRootSync();
                if (string.IsNullOrEmpty(gitRoot))
                {
                    // Fallback to current directory if not in a git repo
                    gitRoot = Environment.CurrentDirectory;
                }

                // Simple glob pattern matching for common cases (*.ext)
                if (pattern.StartsWith("*."))
                {
                    var extension = pattern.Substring(1); // Include the dot
                    return Directory.EnumerateFiles(gitRoot, pattern, SearchOption.AllDirectories)
                        .Where(f => !f.Contains("\\.git"))
                        .ToList();
                }

                // Direct glob
                try
                {
                    return Directory.EnumerateFiles(gitRoot, pattern, SearchOption.AllDirectories)
                        .Where(f => !f.Contains("\\.git"))
                        .ToList();
                }
                catch
                {
                    return Enumerable.Empty<string>();
                }
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a file changes on disk.
        /// </summary>
        public event EventHandler<FileChangedEventArgs>? FileChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Event raised when the active file changes.
        /// </summary>
        public event EventHandler<ActiveFileChangedEventArgs>? ActiveFileChanged
        {
            add { }
            remove { }
        }

        #endregion
    }
}
