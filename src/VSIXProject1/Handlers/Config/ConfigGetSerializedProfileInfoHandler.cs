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
                            selectedModelByRole = new
                            {
                                chat = firstModel,
                                edit = firstModel,
                                apply = firstModel,
                                summarize = firstModel
                            }
                        }
                    },
                    profileId = "local",
                    profiles = new[] { new { id = "local", title = "Local" } }
                });
            return Task.CompletedTask;
        }
    }
}
