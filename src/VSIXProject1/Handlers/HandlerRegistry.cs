using System.Collections.Generic;

namespace ContinueVS.Handlers
{
    /// <summary>
    /// Registry of all required message handlers for b20 verification.
    /// Defines the canonical set of 19+ handlers that must be registered before
    /// any WebView message processing can occur (blocker on b4: bridge injection).
    /// </summary>
    internal static class RequiredHandlers
    {
        /// <summary>
        /// List of required message types for handler registration.
        /// These correspond to handlers that must be instantiated and registered
        /// in MessageDispatcher during ContinueToolWindowControl initialization.
        /// </summary>
        public static readonly IReadOnlyList<string> MessageTypes = new[]
        {
            // IDE Info (6 handlers)
            "bridge:getWorkspaceDirs",
            "bridge:getIdeInfo",
            "bridge:getIdeSettings",
            "bridge:getUniqueId",
            "bridge:isTelemetryEnabled",
            "bridge:isWorkspaceRemote",

            // File I/O (7 handlers)
            "bridge:readFile",
            "bridge:fileExists",
            "bridge:getOpenFiles",
            "bridge:writeFile",
            "bridge:saveFile",
            "bridge:openFile",
            "bridge:deleteFile",

            // Git & URL (2 handlers)
            "bridge:getBranch",
            "bridge:openUrl",

            // Context & Symbols (3 handlers)
            "bridge:getContextItems",
            "bridge:getSymbolsForFiles",
            "bridge:loadSubmenuItems",

            // Settings (2 handlers)
            "bridge:loadSettings",
            "bridge:applySettings",
        };

        /// <summary>
        /// Minimum expected handler count for successful initialization.
        /// If actual count is below this, initialization is incomplete.
        /// </summary>
        public const int MinimumHandlerCount = 19;
    }
}
