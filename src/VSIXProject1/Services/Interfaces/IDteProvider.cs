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

        /// <summary>
        /// Get the <c>EnvDTE.Debugger</c> automation object for the current debug session,
        /// or <c>null</c> when no debugger is available. Must be called on the UI thread.
        /// Every <c>Debugger</c> call in the debug stack flows through this accessor (gap92_1)
        /// so no EnvDTE debug access bypasses it.
        /// </summary>
        EnvDTE.Debugger? GetDebugger();

        /// <summary>
        /// Get the name of the solution's current startup project, or <c>null</c> when none is set.
        /// Reads <c>DTE.SolutionBuild.StartupProjects</c> (UI thread). Used by the debug lifecycle
        /// (gap94) to decide what <c>Debug.Start</c> launches when no explicit project is given.
        /// Pure read — keeps <c>IDebuggerService</c> decoupled from <c>SolutionBuild</c>. Fail-soft.
        /// </summary>
        string? GetStartupProjectName();

        // ------------------------------------------------------------------
        // gap95 — Non-debug DTE tools for the troubleshooting loop.
        // All accessors are UI-thread guarded and fail-soft (benign values on
        // COM/failure), so the IDE tools never throw into the tool loop.
        // ------------------------------------------------------------------

        /// <summary>
        /// Get the active document's path and current selection/cursor, or <c>null</c> when no
        /// document is active. Reads <c>DTE.ActiveDocument</c> + <c>TextSelection</c> (UI thread).
        /// (gap95 ide_active_document.)
        /// </summary>
        ContinueVS.Core.Types.ActiveDocumentInfo? GetActiveDocumentInfo();

        /// <summary>
        /// Open a file in the IDE editor and activate the document tab, returning the opened
        /// path or <c>null</c>/empty on failure. Reads <c>DTE.ItemOperations.OpenFile</c> +
        /// <c>Document.Activate</c> (UI thread). (gap95 ide_open_file.)
        /// </summary>
        string? OpenFileInIde(string filePath);

        /// <summary>
        /// Move the editor cursor to the given file:line and return the resulting position, or
        /// <c>false</c> on failure. Uses <c>Edit.GoTo</c> (UI thread). (gap95 ide_navigate_to.)
        /// </summary>
        bool NavigateTo(string filePath, int line);

        /// <summary>
        /// Invoke Go-To-Definition on the current selection and return the resulting active
        /// document location, or <c>null</c> when it could not be resolved. Uses
        /// <c>Edit.GoToDefinition</c> (UI thread). (gap95 ide_goto_definition / ide_find_symbol.)
        /// </summary>
        ContinueVS.Core.Types.ActiveDocumentInfo? GotoDefinition();

        /// <summary>
        /// Build the solution (or a single named project when provided), returning whether the
        /// build was invoked. Uses <c>SolutionBuild</c> (UI thread). (gap95 ide_build.)
        /// </summary>
        bool BuildSolution(string? projectName);

        /// <summary>
        /// Read the active solution build configuration. Returns a nullable single config
        /// describing the active configuration (name/platform), or <c>null</c> when unavailable.
        /// (gap95 ide_build_configuration.)
        /// </summary>
        ContinueVS.Core.Types.BuildConfigInfo? GetActiveBuildConfiguration();

        /// <summary>
        /// Read the solution's startup project and active launch profile (gap95 ide_launch_profile).
        /// Fail-soft: null when no solution is open. Feeds gap94 debug_start.
        /// </summary>
        ContinueVS.Core.Types.LaunchProfileInfo? GetLaunchProfile();

        /// <summary>
        /// Read the text content of a named Output window pane (e.g. "Build"), or <c>null</c>/empty
        /// when the pane does not exist or cannot be read. Uses <c>DTE.ToolWindows.OutputWindow</c>
        /// (UI thread). (gap95 ide_output_pane.)
        /// </summary>
        ContinueVS.Core.Types.OutputPaneInfo? GetOutputPane(string paneName);
    }
}

