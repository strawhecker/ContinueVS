using System;
using ContinueVS.Core.Config;
using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ContinueVS.Handlers.Config
{
    internal sealed class ConfigGetSerializedProfileInfoHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;
        private readonly ConfigCache _cache;

        public ConfigGetSerializedProfileInfoHandler(ContinueToolWindowControl control, ConfigCache cache)
        {
            _control = control;
            _cache = cache;
            System.Diagnostics.Debug.WriteLine($"[c14-CACHE-INJECTED] cache instance={_cache.GetHashCode()}");
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"[c10-HANDLER-ENTRY] messageId={message.MessageId}");
            System.Diagnostics.Debug.WriteLine($"[c11-MESSAGE-DATA] {Newtonsoft.Json.JsonConvert.SerializeObject(message, Newtonsoft.Json.Formatting.Indented)}");
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
                        // Convert to JObject array first for consistent handling
                        var jModels = new List<Newtonsoft.Json.Linq.JObject>();
                        foreach (var modelToken in modelsArr)
                        {
                            var jModel = modelToken as Newtonsoft.Json.Linq.JObject;
                            if (jModel != null)
                            {
                                // Ensure each model has a 'name' field (copy from 'title' if missing)
                                if (!jModel.ContainsKey("name") && jModel.ContainsKey("title"))
                                {
                                    jModel["name"] = jModel["title"];
                                }
                                jModels.Add(jModel);
                            }
                        }
                        models = jModels.Cast<object>().ToArray();
                    }
                    var tabModel = configObj["tabAutocompleteModel"] as Newtonsoft.Json.Linq.JObject;
                    if (tabModel != null)
                    {
                        // Ensure tabModel also has 'name'
                        if (!tabModel.ContainsKey("name") && tabModel.ContainsKey("title"))
                        {
                            tabModel["name"] = tabModel["title"];
                        }
                        tabAutocompleteModel = tabModel;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigHandler] Error reading config.json: {ex.Message}");
            }

            var firstModel = models.Length > 0 ? models[0] : null;

            System.Diagnostics.Debug.WriteLine("[c16-BEFORE-CACHE-RETRIEVAL] About to retrieve tools from cache");

            // [c16-rev] Retrieve tools from cache with defensive null-guards
            object[] tools = new object[0];
            try
            {
                System.Diagnostics.Debug.WriteLine("[c16-RETRIEVAL-START] Beginning cache retrieval");
                System.Diagnostics.Debug.WriteLine($"[c16-CACHE-INSTANCE] _cache={(_cache != null ? "NOT NULL" : "NULL")}");

                if (_cache == null)
                {
                    System.Diagnostics.Debug.WriteLine("[c16-CACHE-NULL-WARN] Cache instance is null, using fallback");
                }
                else
                {
                    var snapshot = _cache.GetSnapshot();
                    System.Diagnostics.Debug.WriteLine($"[c16-SNAPSHOT-CHECK] snapshot != null: {snapshot != null}, Tools != null: {snapshot?.Tools != null}, Tools.Length: {snapshot?.Tools?.Length ?? 0}");

                    if (snapshot == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[c16-SNAPSHOT-NULL-WARN] GetSnapshot returned null, using fallback");
                    }
                    else if (snapshot.Tools == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[c16-TOOLS-NULL-WARN] snapshot.Tools is null, using fallback");
                        tools = new object[0];
                    }
                    else
                    {
                        tools = snapshot.Tools;
                        System.Diagnostics.Debug.WriteLine($"[c16-TOOLS-ASSIGNED] Assigned {tools.Length} tools from snapshot");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[c16-RETRIEVAL-ERROR] Exception during cache retrieval: {ex.Message}");
                tools = new object[0];
            }

            System.Diagnostics.Debug.WriteLine($"[c16-TOOLS-FROM-CACHE] count={tools?.Length ?? 0}");

            // [DEBUG] Log tools array structure before sending
            if (tools != null)
            {
                for (int i = 0; i < tools.Length; i++)
                {
                    var toolJson = Newtonsoft.Json.JsonConvert.SerializeObject(tools[i]);
                    System.Diagnostics.Debug.WriteLine($"[c16-TOOL-ITEM] index={i}: {toolJson}");
                }
            }

            // [c12-rev] Log complete response before sending to GUI
            var responseJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                new
                {
                    config = new
                    {
                        models = models,
                        tabAutocompleteModels = tabAutocompleteModel != null ? new[] { tabAutocompleteModel } : new object[0],
                        allowAnonymousTelemetry = true,
                        slashCommands = new object[0],
                        contextProviders = new object[0],
                        tools = tools,
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
                            chat = firstModel ?? new { name = "default", provider = "default" },
                            edit = firstModel ?? new { name = "default", provider = "default" },
                            apply = firstModel ?? new { name = "default", provider = "default" },
                            summarize = firstModel ?? new { name = "default", provider = "default" },
                            autocomplete = tabAutocompleteModel ?? new { name = "default", provider = "default" },
                            rerank = (object?)new { name = "default", provider = "default" },
                            embed = (object?)new { name = "default", provider = "default" },
                            subagent = (object?)new { name = "default", provider = "default" }
                        }
                    },
                    errors = (object?)null,
                    configLoadInterrupted = false
                },
                Newtonsoft.Json.Formatting.Indented);
            System.Diagnostics.Debug.WriteLine($"[c12-RESPONSE-JSON] {responseJson}");

            // [DEBUG] Log actual payload about to be sent (without result wrapper)
            var actualPayload = new
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
                    tools = tools,
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
                        chat = firstModel ?? new { name = "default", provider = "default" },
                        edit = firstModel ?? new { name = "default", provider = "default" },
                        apply = firstModel ?? new { name = "default", provider = "default" },
                        summarize = firstModel ?? new { name = "default", provider = "default" },
                        autocomplete = tabAutocompleteModel ?? new { name = "default", provider = "default" },
                        rerank = (object?)new { name = "default", provider = "default" },
                        embed = (object?)new { name = "default", provider = "default" },
                        subagent = (object?)new { name = "default", provider = "default" }
                    }
                },
                errors = (object?)null,
                configLoadInterrupted = false
            };
            var actualPayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(actualPayload, Newtonsoft.Json.Formatting.Indented);
            System.Diagnostics.Debug.WriteLine($"[c12-ACTUAL-PAYLOAD] {actualPayloadJson}");


            // Convert tools array to JArray for proper serialization
            var toolsJArray = tools != null ? Newtonsoft.Json.Linq.JArray.FromObject(tools) : new Newtonsoft.Json.Linq.JArray();

            // Ensure models is also properly serialized
            var modelsJArray = models != null ? Newtonsoft.Json.Linq.JArray.FromObject(models) : new Newtonsoft.Json.Linq.JArray();

            // Convert model role arrays
            var chatModelArray = firstModel != null ? Newtonsoft.Json.Linq.JArray.FromObject(new[] { firstModel }) : new Newtonsoft.Json.Linq.JArray();
            var applyModelArray = firstModel != null ? Newtonsoft.Json.Linq.JArray.FromObject(new[] { firstModel }) : new Newtonsoft.Json.Linq.JArray();
            var editModelArray = firstModel != null ? Newtonsoft.Json.Linq.JArray.FromObject(new[] { firstModel }) : new Newtonsoft.Json.Linq.JArray();
            var summarizeModelArray = firstModel != null ? Newtonsoft.Json.Linq.JArray.FromObject(new[] { firstModel }) : new Newtonsoft.Json.Linq.JArray();
            var autocompleteModelArray = tabAutocompleteModel != null ? Newtonsoft.Json.Linq.JArray.FromObject(new[] { tabAutocompleteModel }) : new Newtonsoft.Json.Linq.JArray();

            // Build response as JObject to ensure proper serialization (no undefined entries)
            var response = new Newtonsoft.Json.Linq.JObject();
            var result = new Newtonsoft.Json.Linq.JObject();
            var config = new Newtonsoft.Json.Linq.JObject();

            config["models"] = modelsJArray;
            config["tabAutocompleteModels"] = tabAutocompleteModel != null
                ? Newtonsoft.Json.Linq.JArray.FromObject(new[] { tabAutocompleteModel })
                : new Newtonsoft.Json.Linq.JArray();
            config["allowAnonymousTelemetry"] = true;
            config["slashCommands"] = new Newtonsoft.Json.Linq.JArray();
            config["contextProviders"] = new Newtonsoft.Json.Linq.JArray();
            config["tools"] = toolsJArray;
            config["mcpServerStatuses"] = new Newtonsoft.Json.Linq.JArray();
            config["rules"] = new Newtonsoft.Json.Linq.JArray();

            var modelsByRole = new Newtonsoft.Json.Linq.JObject();
            modelsByRole["chat"] = chatModelArray;
            modelsByRole["apply"] = applyModelArray;
            modelsByRole["edit"] = editModelArray;
            modelsByRole["summarize"] = summarizeModelArray;
            modelsByRole["autocomplete"] = autocompleteModelArray;
            modelsByRole["rerank"] = new Newtonsoft.Json.Linq.JArray();
            modelsByRole["embed"] = new Newtonsoft.Json.Linq.JArray();
            modelsByRole["subagent"] = new Newtonsoft.Json.Linq.JArray();
            config["modelsByRole"] = modelsByRole;

            var selectedModelByRole = new Newtonsoft.Json.Linq.JObject();
            if (firstModel != null)
            {
                selectedModelByRole["chat"] = Newtonsoft.Json.Linq.JToken.FromObject(firstModel);
                selectedModelByRole["edit"] = Newtonsoft.Json.Linq.JToken.FromObject(firstModel);
                selectedModelByRole["apply"] = Newtonsoft.Json.Linq.JToken.FromObject(firstModel);
                selectedModelByRole["summarize"] = Newtonsoft.Json.Linq.JToken.FromObject(firstModel);
            }
            else
            {
                var defaultModel = Newtonsoft.Json.Linq.JObject.FromObject(new { name = "default", provider = "default" });
                selectedModelByRole["chat"] = defaultModel;
                selectedModelByRole["edit"] = defaultModel;
                selectedModelByRole["apply"] = defaultModel;
                selectedModelByRole["summarize"] = defaultModel;
            }

            if (tabAutocompleteModel != null)
            {
                selectedModelByRole["autocomplete"] = Newtonsoft.Json.Linq.JToken.FromObject(tabAutocompleteModel);
            }
            else
            {
                selectedModelByRole["autocomplete"] = Newtonsoft.Json.Linq.JObject.FromObject(new { name = "default", provider = "default" });
            }

            selectedModelByRole["rerank"] = Newtonsoft.Json.Linq.JObject.FromObject(new { name = "default", provider = "default" });
            selectedModelByRole["embed"] = Newtonsoft.Json.Linq.JObject.FromObject(new { name = "default", provider = "default" });
            selectedModelByRole["subagent"] = Newtonsoft.Json.Linq.JObject.FromObject(new { name = "default", provider = "default" });
            config["selectedModelByRole"] = selectedModelByRole;

            result["config"] = config;
            result["errors"] = null;
            result["configLoadInterrupted"] = false;
            response["result"] = result;

            _control.SendReplyToGui(message.MessageType, message.MessageId, response);
            return Task.CompletedTask;
        }
    }
}
