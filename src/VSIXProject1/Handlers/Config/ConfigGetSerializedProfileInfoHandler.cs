using System;
using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Config
{
    internal sealed class ConfigGetSerializedProfileInfoHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ConfigGetSerializedProfileInfoHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"[c10-HANDLER-ENTRY] messageId={message.MessageId}");
            // Try to read models from ~/.continue/config.json
            object[] models = new object[0];
            object? tabAutocompleteModel = null;
            try
            {
                var configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".continue", "config.json");
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    var configObj = Newtonsoft.Json.Linq.JObject.Parse(json);
                    var modelsArr = configObj["models"] as Newtonsoft.Json.Linq.JArray;
                    if (modelsArr != null && modelsArr.Count > 0)
                    {
                        models = modelsArr.ToObject<object[]>() ?? new object[0];
                    }
                    var tabModel = configObj["tabAutocompleteModel"];
                    if (tabModel != null)
                    {
                        tabAutocompleteModel = tabModel;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigHandler] Error reading config.json: {ex.Message}");
            }

            var firstModel = models.Length > 0 ? models[0] : null;

            System.Diagnostics.Debug.WriteLine($"[c10-TOOLS-HARDCODED] returning empty array");
            _control.SendReplyToGui(message.MessageType, message.MessageId,
                new
                {
                    result = new
                    {
                        config = new
                        {
                            models = models,
                            tabAutocompleteModels = tabAutocompleteModel != null
                                ? new[] { tabAutocompleteModel }
                                : new object[0],
                            allowAnonymousTelemetry = true,
                            slashCommands = new object[0],
                            contextProviders = new object[0],
                            tools = new object[0],
                            mcpServerStatuses = new object[0],
                            rules = new object[0],
                            modelsByRole = new
                            {
                                chat = firstModel != null ? new[] { firstModel } : new object[0],
                                apply = firstModel != null ? new[] { firstModel } : new object[0],
                                edit = firstModel != null ? new[] { firstModel } : new object[0],
                                summarize = firstModel != null ? new[] { firstModel } : new object[0],
                                autocomplete = tabAutocompleteModel != null ? new[] { tabAutocompleteModel } : new object[0],
                                rerank = new object[0],
                                embed = new object[0],
                                subagent = new object[0]
                            },
                            selectedModelByRole = new
                            {
                                chat = firstModel,
                                edit = firstModel,
                                apply = firstModel,
                                summarize = firstModel,
                                autocomplete = tabAutocompleteModel,
                                rerank = (object?)null,
                                embed = (object?)null,
                                subagent = (object?)null
                            }
                        },
                        errors = (object?)null,
                        configLoadInterrupted = false
                    },
                    profileId = "local",
                    profiles = new[] { new { id = "local", title = "Local" } }
                });
            return Task.CompletedTask;
        }
    }
}
