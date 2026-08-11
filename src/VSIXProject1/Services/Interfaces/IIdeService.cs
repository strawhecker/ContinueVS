using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        // Git Operations
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
