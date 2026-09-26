using ContinueVS.Services.Interfaces;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Result of reading the IDE's active document state (gap95 ide_active_document).
    /// Carries the document path plus the current selection/cursor. Fail-soft: a null
    /// result (or empty fields) means the value could not be read from DTE.
    /// </summary>
    public class ActiveDocumentInfo
    {
        /// <summary>
        /// Full file path of the active document, or null/empty when none is open.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Current selection/cursor in the active document, or null when unavailable.
        /// </summary>
        public Selection? Selection { get; set; }
    }

    /// <summary>
    /// Result of reading a named Visual Studio Output window pane (gap95 ide_output_pane).
    /// Fail-soft: content is empty when the pane does not exist or cannot be read.
    /// </summary>
    public class OutputPaneInfo
    {
        /// <summary>
        /// Name of the output pane (e.g. "Build", "Debug").
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Text content currently in the pane.
        /// </summary>
        public string? Content { get; set; }
    }

    /// <summary>
    /// A single solution build configuration (gap95 ide_build_configuration).
    /// Read-only: name + platform plus whether it is the currently active configuration.
    /// </summary>
    public class BuildConfigInfo
    {
        /// <summary>
        /// Configuration name (e.g. "Debug", "Release").
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Platform name (e.g. "Any CPU").
        /// </summary>
        public string? Platform { get; set; }

        /// <summary>
        /// True when this is the currently active solution configuration.
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// The solution's startup/launch profile (gap95 ide_launch_profile), which feeds
    /// the gap94 debug_start tool so it knows what to launch when no project is given.
    /// Fail-soft: null on a read failure or when no solution is open.
    /// </summary>
    public class LaunchProfileInfo
    {
        /// <summary>
        /// Name of the solution's startup project, or null when none is set.
        /// </summary>
        public string? StartupProject { get; set; }

        /// <summary>
        /// Currently active launch profile name, or null when none/default.
        /// </summary>
        public string? LaunchProfile { get; set; }
    }
}
