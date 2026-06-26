namespace ContinueCore.Llm;
public static partial class StreamChatFunctions
{
    public static async AsyncGenerator<ChatMessage, PromptLog, object> llmStreamChat(ConfigHandler configHandler, AbortController abortController, Message<
     { messages :  ChatMessage [ ] ;  completionOptions :  import ( ".." ) . LLMFullCompletionOptions ;  title :  string ;  messageOptions ? :  import ( ".." ) . MessageOption ;  legacySlashCommandData ? :  { command :  import ( ".." ) . SlashCommandDescWithSource ;  input :  string ;  contextItems :  import ( ".." ) . ContextItemWithId [ ] ;  historyIndex :  number ;  selectedCode :  import ( ".." ) . RangeInFile [ ] ;  } ;  } >
    msg, IDE ide, IMessenger<ToCoreProtocol, FromCoreProtocol> messenger)
    {
        var { config } = await configHandler.loadConfig();
        if (!config)
        {
            throw "/* unknown: new Error(\"Config not loaded\") */";
        }

        if (config.experimental.readResponseTTS)
        {
            TTS.kill();
        }

        var {
    legacySlashCommandData,
    completionOptions,
    messages,
    messageOptions,
  } = msg.data;
        var model = config.selectedModelByRole.chat;
        if (!model)
        {
            throw "/* unknown: new Error(\"No chat model selected\") */";
        }

        var errorPromptLog = new
        {
            modelTitle = model.title ?? model.model,
            modelProvider = model.underlyingProviderName ?? "unknown",
            completion = "",
            prompt = "",
            completionOptions = SpreadMerge.Merge(msg.data.completionOptions, new { model = model.model })
        };
        try
        {
            if (legacySlashCommandData)
            {
                var { command, contextItems, historyIndex, input, selectedCode } = legacySlashCommandData;
                var slashCommand = config.slashCommands.find((object sc) => sc.name == command.name);
                if (!slashCommand)
                {
                    throw "/* unknown: new Error(`Unknown slash command ${command.name}`) */";
                }

                if (!slashCommand.run)
                {
                    console.error($"Slash command {command.name} ({command.source}) has no run function");
                    throw "/* unknown: new Error(`Slash command not found`) */";
                }

                var gen = slashCommand.run(new { input, history = messages, llm = model, contextItems, params = command.params, ide, addContextItem = (object item) => messenger.request("addContextItem", new { item, historyIndex }), selectedCode, config, fetch = (object url, object init) => fetchwithRequestOptions(url, SpreadMerge.Merge(init, new { signal = abortController.signal }), model.requestOptions), completionOptions, abortController });
                var next = await gen.next();
                while (!next.done)
                {
                    if (abortController.signal.aborted)
                    {
                        next = await gen.return(errorPromptLog);
                    }

                    if (next.value)
                    {
                        "/* unknown: yield {\r\n            role: \"assistant\",\r\n            content: next.value,\r\n          } */";
                    }

                    next = await gen.next();
                }

                if (!next.done)
                {
                    throw "/* unknown: new Error(\"Will never happen\") */";
                }

                return next.value;
            }
            else
            {
                var gen = model.streamChat(messages, abortController.signal, completionOptions, messageOptions);
                var next = await gen.next();
                while (!next.done)
                {
                    if (abortController.signal.aborted)
                    {
                        next = await gen.return(errorPromptLog);
                    }

                    var chunk = next.value;
                    "/* unknown: yield chunk */";
                    next = await gen.next();
                }

                if (config.experimental.readResponseTTS && "/* untranslatable binary op */")
                {
                    TTS.read(next.value.completion);
                }

                if (!next.done)
                {
                    throw "/* unknown: new Error(\"Will never happen\") */";
                }

                return next.value;
            }
        }
        catch (Exception)
        {
            throw error;
        }
    }
}