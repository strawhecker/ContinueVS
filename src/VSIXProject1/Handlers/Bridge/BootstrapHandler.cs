using ContinueVS.IPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Bridge
{
    /// <summary>
    /// Bootstrap Handler - Responds to the initial React app bootstrap request.
    /// 
    /// This is the FIRST handler called by React after our continueVS bridge is injected.
    /// React sends a bridge:bootstrap message to negotiate capabilities and get the handler registry.
    /// 
    /// Without this handler, React gets stuck because it can't initialize!
    /// </summary>
    internal sealed class BootstrapHandler : IMessageHandler
    {
        private readonly MessageDispatcher _dispatcher;

        public BootstrapHandler(MessageDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"[BootstrapHandler] Received bootstrap request: {message.MessageId}");

            try
            {
                // Get the list of available handlers
                var handlerCount = _dispatcher.GetHandlerCount();

                // List of all handlers (should match HandlerRegistry.cs)
                var handlers = new[]
                {
                    "getWorkspaceDirs", "getIdeInfo", "getIdeSettings", "getUniqueId", "isTelemetryEnabled", "isWorkspaceRemote",
                    "readFile", "fileExists", "getOpenFiles", "writeFile", "saveFile", "openFile", "openUrl", "getBranch",
                    "context/getContextItems", "context/getSymbolsForFiles", "context/loadSubmenuItems", "context/addDocs", "context/removeDocs", "context/indexDocs",
                    "config/addOpenAiKey", "config/ideSettingsUpdate", "config/deleteModel", "config/getSerializedProfileInfo", "config/addModel", "config/addLocalWorkspaceBlock", "config/addGlobalRule", "config/deleteRule", "config/newPromptFile", "config/newAssistantFile", "config/refreshProfiles", "config/openProfile", "config/updateSharedConfig", "config/updateSelectedModel",
                    "llm/complete", "llm/streamChat", "llm/listModels", "llm/compileChat",
                    "bridge:getModelInfo", "getCurrentFile", "applyToFile", "acceptDiff", "rejectDiff",
                    "autocomplete/complete", "autocomplete/accept", "autocomplete/cancel",
                    "bridge:bootstrap" // This handler itself
                };

                System.Diagnostics.Debug.WriteLine($"[BootstrapHandler] Returning {handlerCount} available handlers");

                // Create bootstrap response with available handlers and bridge info
                var response = new
                {
                    success = true,
                    bridgeVersion = "2.0.0",
                    bridgeProtocolVersion = "1.0",
                    features = new
                    {
                        streaming = true,
                        subscriptions = false,
                        telemetry = false
                    },
                    handlers = handlers,
                    editorState = (object?)null,
                    ideVersion = "2026.1",
                    ideName = "Visual Studio"
                };

                System.Diagnostics.Debug.WriteLine($"[BootstrapHandler] Bootstrap response prepared: {handlerCount} handlers");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BootstrapHandler] ERROR: {ex}");
            }
        }
    }
}
