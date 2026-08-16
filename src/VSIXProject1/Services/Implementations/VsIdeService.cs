using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public Task<string> GetBranchAsync() => Task.FromResult(string.Empty);

        public Task<string> GetRepoNameAsync() => Task.FromResult(string.Empty);

        public Task<string> GetGitRootPathAsync() => Task.FromResult(string.Empty);

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

        public string? GetActiveFilepath() => null;

        public string? GetSelectedText() => null;

        public Selection? GetCursorSelection() => null;

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
            Debug.WriteLine("[gap8_3-ideservice-start] VsIdeService.OpenFileInEditorAsync called");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.WriteLine("[gap8_3-ideservice-null-path] File path is null or empty");
                return Task.CompletedTask;
            }

            try
            {
                Debug.WriteLine($"[gap8_3-ideservice-file] File path: {filePath}");

                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[gap8_3-ideservice-not-exists] File does not exist: {filePath}");
                    return Task.CompletedTask;
                }

                Debug.WriteLine("[gap8_3-ideservice-dte-call] Getting DTE from ServiceProvider");

                // Get the DTE (VS automation object) via the VS service provider
                // We'll invoke this synchronously via ThreadHelper since it requires UI thread access
                var dteTask = new System.Threading.Tasks.TaskCompletionSource<DTE>();

                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    try
                    {
                        // Get the package instance which has VS service provider access
                        if (ContinueVSPackage.Instance != null)
                        {
                            var dte = await ContinueVSPackage.Instance.GetServiceAsync(typeof(DTE)) as DTE;
                            dteTask.SetResult(dte);
                        }
                        else
                        {
                            Debug.WriteLine("[gap8_3-ideservice-pkg-null] ContinueVSPackage.Instance is null");
                            dteTask.SetResult(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[gap8_3-ideservice-dte-error] Error getting DTE: {ex.Message}");
                        dteTask.SetException(ex);
                    }
                });

                var dte = dteTask.Task.Result;

                if (dte == null)
                {
                    Debug.WriteLine("[gap8_3-ideservice-dte-null] DTE is null, cannot open file");
                    return Task.CompletedTask;
                }

                Debug.WriteLine("[gap8_3-ideservice-dte-opfile] Calling DTE.ItemOperations.OpenFile");
                dte.ItemOperations.OpenFile(filePath, Constants.vsViewKindTextView);

                Debug.WriteLine("[gap8_3-ideservice-complete] File opened successfully in editor");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_3-ideservice-error] Error opening file in editor: {ex.Message}");
                Debug.WriteLine($"[gap8_3-ideservice-error-stack] {ex.StackTrace}");
                return Task.CompletedTask;
            }
        }
    }
}
