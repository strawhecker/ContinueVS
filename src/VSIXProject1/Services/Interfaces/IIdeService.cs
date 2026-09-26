using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Represents a location in code (file and position).
    /// </summary>
    public class Location
    {
        /// <summary>
        /// File path.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Line number (1-based).
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Column number (1-based).
        /// </summary>
        public int Column { get; set; }
    }

    /// <summary>
    /// Represents a document symbol.
    /// </summary>
    public class DocumentSymbol
    {
        /// <summary>
        /// Name of the symbol.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Kind of symbol.
        /// </summary>
        public string? Kind { get; set; }

        /// <summary>
        /// Starting location.
        /// </summary>
        public Location? StartLocation { get; set; }

        /// <summary>
        /// Ending location.
        /// </summary>
        public Location? EndLocation { get; set; }

        /// <summary>
        /// Child symbols.
        /// </summary>
        public List<DocumentSymbol> Children { get; set; } = new List<DocumentSymbol>();
    }

    /// <summary>
    /// Represents a diagnostic (error, warning, etc.).
    /// </summary>
    public class Diagnostic
    {
        /// <summary>
        /// Severity of the diagnostic.
        /// </summary>
        public DiagnosticSeverity Severity { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Location of the diagnostic.
        /// </summary>
        public Location? Location { get; set; }

        /// <summary>
        /// Error code.
        /// </summary>
        public string? Code { get; set; }
    }

    /// <summary>
    /// Enumeration of diagnostic severities.
    /// </summary>
    public enum DiagnosticSeverity
    {
        /// <summary>
        /// Error severity.
        /// </summary>
        Error,

        /// <summary>
        /// Warning severity.
        /// </summary>
        Warning,

        /// <summary>
        /// Information severity.
        /// </summary>
        Information,

        /// <summary>
        /// Hint severity.
        /// </summary>
        Hint
    }

    /// <summary>
    /// Represents a selection in the editor.
    /// </summary>
    public class Selection
    {
        /// <summary>
        /// Start location of the selection.
        /// </summary>
        public Location? Start { get; set; }

        /// <summary>
        /// End location of the selection.
        /// </summary>
        public Location? End { get; set; }
    }

    /// <summary>
    /// Service interface for IDE abstraction and integration.
    /// Provides access to file operations, git, LSP, and editor state.
    /// </summary>
    public interface IIdeService
    {
        // File Operations
        /// <summary>
        /// Reads the entire contents of a file.
        /// </summary>
        /// <param name="filepath">The path to the file to read.</param>
        /// <returns>The file contents.</returns>
        Task<string> ReadFileAsync(string filepath);

        /// <summary>
        /// Writes contents to a file.
        /// </summary>
        /// <param name="filepath">The path to the file to write.</param>
        /// <param name="contents">The contents to write.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WriteFileAsync(string filepath, string contents);

        /// <summary>
        /// Reads a specific range of lines from a file.
        /// </summary>
        /// <param name="filepath">The path to the file.</param>
        /// <param name="startLine">Starting line number (1-based).</param>
        /// <param name="endLine">Ending line number (1-based).</param>
        /// <returns>The contents of the specified range.</returns>
        Task<string> ReadRangeInFileAsync(string filepath, int startLine, int endLine);

        /// <summary>
        /// Saves a file that is currently open in the editor.
        /// </summary>
        /// <param name="filepath">The path to the file to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveFileAsync(string filepath);

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="filepath">The path to the file to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteFileAsync(string filepath);

        /// <summary>
        /// Creates a new file with the specified contents.
        /// </summary>
        /// <param name="filepath">The path where the file should be created.</param>
        /// <param name="contents">The contents to write to the file.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CreateFileAsync(string filepath, string contents);

        /// <summary>
        /// Creates a new directory/folder.
        /// </summary>
        /// <param name="folderpath">The path where the folder should be created.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CreateFolderAsync(string folderpath);

        /// <summary>
        /// Lists files and folders in a directory.
        /// </summary>
        /// <param name="dirPath">The directory path to list.</param>
        /// <param name="recursive">If true, lists contents recursively.</param>
        /// <returns>An enumerable of file and folder paths.</returns>
        Task<IEnumerable<string>> ListDirectoryAsync(string dirPath, bool recursive = false);

        // Git Operations
        /// <summary>
        /// Gets the full path of the currently active document in the editor, or "none".
        /// </summary>
        /// <returns>Absolute file path of the active document, or "none" if none is open.</returns>
        Task<string> GetActiveDocumentPathAsync();

        /// <summary>
        /// Gets the contents of the currently open file in the IDE editor.
        /// </summary>
        /// <returns>The contents of the currently open file, or an empty string if no file is open.</returns>
        Task<string> ReadCurrentlyOpenFileAsync();

        /// <summary>
        /// Determines whether a document with the given path is currently open in the IDE viewer
        /// (any tab/window), not necessarily the active document. Used by active-plan binding to
        /// keep a plan bound while it is still open in a viewer but the active document is elsewhere.
        /// Best-effort: on failure this returns null (binding status unknown) rather than false,
        /// so callers do not silently clear a binding on an unavailable query.
        /// </summary>
        /// <param name="path">The absolute file path to check.</param>
        /// <returns>True if open, false if not open, null if status is unknown.</returns>
        Task<bool?> IsOpenInViewerAsync(string path);

        /// <summary>
        /// Gets the current git branch.
        /// </summary>
        /// <returns>The branch name.</returns>
        Task<string> GetBranchAsync();

        /// <summary>
        /// Gets the repository name.
        /// </summary>
        /// <returns>The repository name.</returns>
        Task<string> GetRepoNameAsync();

        /// <summary>
        /// Gets the git root directory path.
        /// </summary>
        /// <returns>The path to the git root directory.</returns>
        Task<string> GetGitRootPathAsync();

        // LSP Operations
        /// <summary>
        /// Goes to the definition of a symbol.
        /// </summary>
        /// <param name="location">The location of the symbol.</param>
        /// <returns>An enumerable of definition locations.</returns>
        Task<IEnumerable<Location>> GotoDefinitionAsync(Location location);

        /// <summary>
        /// Gets all references to a symbol.
        /// </summary>
        /// <param name="location">The location of the symbol.</param>
        /// <returns>An enumerable of reference locations.</returns>
        Task<IEnumerable<Location>> GetReferencesAsync(Location location);

        /// <summary>
        /// Gets all symbols in a document.
        /// </summary>
        /// <param name="filepath">The file path.</param>
        /// <returns>An enumerable of document symbols.</returns>
        Task<IEnumerable<DocumentSymbol>> GetDocumentSymbolsAsync(string filepath);

        /// <summary>
        /// Gets all problems/diagnostics in a file.
        /// </summary>
        /// <param name="filepath">The file path.</param>
        /// <returns>An enumerable of diagnostics.</returns>
        Task<IEnumerable<Diagnostic>> GetProblemsAsync(string filepath);

        // Subprocess Execution
        /// <summary>
        /// Runs a subprocess command.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="cwd">The working directory.</param>
        /// <returns>A tuple of (stdout, stderr).</returns>
        Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd);

        // Editor State
        /// <summary>
        /// Gets the path of the currently active file.
        /// </summary>
        /// <returns>The file path, or null if no file is active.</returns>
        string? GetActiveFilepath();

        /// <summary>
        /// Gets the directory of the currently open solution.
        /// </summary>
        /// <returns>The solution directory path, or null if no solution is open.</returns>
        string? GetSolutionDirectory();

        /// <summary>
        /// Gets the currently selected text in the editor.
        /// </summary>
        /// <returns>The selected text, or null if nothing is selected.</returns>
        string? GetSelectedText();

        /// <summary>
        /// Gets the current cursor selection.
        /// </summary>
        /// <returns>The current selection.</returns>
        Selection? GetCursorSelection();

        // File Queries
        /// <summary>
        /// Checks if a file exists.
        /// </summary>
        /// <param name="filepath">The path to check.</param>
        /// <returns>True if the file exists.</returns>
        bool FileExists(string filepath);

        /// <summary>
        /// Gets all workspace files matching a pattern.
        /// </summary>
        /// <param name="pattern">The glob pattern (e.g., "*.cs").</param>
        /// <returns>An enumerable of matching file paths.</returns>
        IEnumerable<string> GetWorkspaceFiles(string pattern = "*");

        // VS Editor Operations
        /// <summary>
        /// Opens a file in the Visual Studio editor.
        /// </summary>
        /// <param name="filePath">The path to the file to open.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenFileInEditorAsync(string filePath);

        // Gap95 IDE diagnostic/loop tools (non-debug DTE reads/actions)
        /// <summary>
        /// Gets the active document's path and current selection/cursor, or null when none is active.
        /// </summary>
        /// <returns>Active document info, or null.</returns>
        Task<ActiveDocumentInfo?> GetActiveDocumentInfoAsync();

        /// <summary>
        /// Opens a file in the IDE editor and activates its tab.
        /// </summary>
        /// <param name="filePath">The path of the file to open.</param>
        /// <returns>The opened path, or null/empty on failure.</returns>
        Task<string?> OpenFileInIdeAsync(string filePath);

        /// <summary>
        /// Moves the editor cursor to the given file:line.
        /// </summary>
        /// <param name="filePath">The path of the file to navigate in.</param>
        /// <param name="line">1-based line number to move to.</param>
        /// <returns>True if the navigation was issued.</returns>
        Task<bool> NavigateToAsync(string filePath, int line);

        /// <summary>
        /// Invokes Go-To-Definition on the current selection and reports the result location.
        /// </summary>
        /// <returns>The resulting active document location, or null.</returns>
        Task<ActiveDocumentInfo?> GotoDefinitionAsync();

        /// <summary>
        /// Builds the solution, or a named project when provided.
        /// </summary>
        /// <param name="projectName">Optional project name; empty builds the whole solution.</param>
        /// <returns>True if the build was invoked.</returns>
        Task<bool> BuildSolutionAsync(string? projectName);

        /// <summary>
        /// Reads the active solution build configuration.
        /// </summary>
        /// <returns>The active build configuration, or null.</returns>
        Task<BuildConfigInfo?> GetActiveBuildConfigurationAsync();

        /// <summary>
        /// Reads the solution startup project and active launch profile.
        /// </summary>
        /// <returns>Launch profile info, or null.</returns>
        Task<LaunchProfileInfo?> GetLaunchProfileAsync();

        /// <summary>
        /// Reads the text content of a named Output window pane.
        /// </summary>
        /// <param name="paneName">The output pane name (e.g. "Build").</param>
        /// <returns>The pane content, or null/empty when unavailable.</returns>
        Task<OutputPaneInfo?> GetOutputPaneAsync(string paneName);

        // Test Runner Operations
        /// <summary>
        /// Runs a test and captures diagnostic output (stdout, stderr, stack frames).
        /// </summary>
        /// <param name="testPath">Path or identifier of the test to run.</param>
        /// <param name="options">Test run options (debug mode, verbosity, timeout, iteration count).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Test run result with output and parsed frames.</returns>
        Task<TestRunResult> RunTestAsync(string testPath, TestRunOptions options, CancellationToken ct = default);

        /// <summary>
        /// Runs a terminal command and returns the output.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <returns>Standard output and error from the command.</returns>
        Task<string> RunCommandAsync(string command);

        /// <summary>
        /// Gets the current git diff.
        /// </summary>
        /// <returns>The unified diff format of current changes.</returns>
        Task<string> GetDiffAsync();

        /// <summary>
        /// Gets compiler problems (errors, warnings).
        /// </summary>
        /// <returns>Formatted list of problems.</returns>
        Task<string> GetProblemsAsync();

        /// <summary>
        /// Gets git status of the repository.
        /// </summary>
        /// <returns>Git status output.</returns>
        Task<string> GetGitStatusAsync();

        /// <summary>
        /// Gets git diff for a specific file.
        /// </summary>
        /// <param name="filePath">Path to the file (optional).</param>
        /// <returns>Unified diff for the file.</returns>
        Task<string> GetGitDiffAsync(string filePath);

        /// <summary>
        /// Gets git commit log.
        /// </summary>
        /// <param name="maxCommits">Maximum number of commits to return.</param>
        /// <returns>Commit history.</returns>
        Task<string> GetGitLogAsync(int maxCommits);

        /// <summary>
        /// Creates a git commit.
        /// </summary>
        /// <param name="message">Commit message.</param>
        /// <returns>The commit hash.</returns>
        Task<string> CreateGitCommitAsync(string message);

        /// <summary>
        /// Opens a file in the IDE.
        /// </summary>
        /// <param name="filepath">Path to the file to open.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenFileAsync(string filepath);

        // Events
        /// <summary>
        /// Event raised when a file changes on disk.
        /// </summary>
        event EventHandler<FileChangedEventArgs>? FileChanged;

        /// <summary>
        /// Event raised when the active file changes.
        /// </summary>
        event EventHandler<ActiveFileChangedEventArgs>? ActiveFileChanged;
    }
}
