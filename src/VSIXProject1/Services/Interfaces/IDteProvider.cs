using System.Collections.Generic;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Abstraction over the Visual Studio DTE object.
    /// Allows tests to mock DTE without requiring Microsoft.VisualStudio.Interop assembly.
    /// </summary>
    public interface IDteProvider
    {
        /// <summary>
        /// Get the selected text from the active document, or empty string if none.
        /// </summary>
        string GetSelectedText();

        /// <summary>
        /// Get the content of the active document.
        /// </summary>
        string GetActiveDocumentContent();

        /// <summary>
        /// Get recently opened file paths (up to maxCount).
        /// </summary>
        System.Collections.Generic.List<string> GetRecentFiles(int maxCount);

        /// <summary>
        /// Get the full file path of the active document, or empty string if none.
        /// </summary>
        string GetActiveFilepath();

        /// <summary>
        /// Get the directory of the currently open solution, or empty string if none.
        /// </summary>
        string GetSolutionDirectory();

        /// <summary>
        /// Get the current cursor selection from the active document, or null if unavailable.
        /// </summary>
        Selection? GetCursorSelection();

        /// <summary>
        /// Get the full paths of all documents currently open in the IDE (viewer/tab) via
        /// <c>DTE.Documents</c>. Used to decide whether a plan file is still open in a viewer
        /// even when it is no longer the active document ("open elsewhere keeps the binding").
        /// </summary>
        System.Collections.Generic.List<string> GetOpenDocumentPaths();
    }
}

