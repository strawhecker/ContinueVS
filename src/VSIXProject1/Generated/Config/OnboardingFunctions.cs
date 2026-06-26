namespace ContinueCore.Config;
public static partial class OnboardingFunctions
{
    public static ConfigYaml setupBestConfig(ConfigYaml config)
    {
        return SpreadMerge.Merge(config, new { models = config.models });
    }

    public static ConfigYaml setupLocalConfig(ConfigYaml config)
    {
        return SpreadMerge.Merge(config, new { models = "/* unknown: [\r\n      {\r\n        name: LOCAL_ONBOARDING_CHAT_TITLE,\r\n        provider: \"ollama\",\r\n        model: LOCAL_ONBOARDING_CHAT_MODEL,\r\n        roles: [\"chat\", \"edit\", \"apply\"],\r\n      },\r\n      {\r\n        name: LOCAL_ONBOARDING_FIM_TITLE,\r\n        provider: \"ollama\",\r\n        model: LOCAL_ONBOARDING_FIM_MODEL,\r\n        roles: [\"autocomplete\"],\r\n      },\r\n      {\r\n        name: LOCAL_ONBOARDING_EMBEDDINGS_TITLE,\r\n        provider: \"ollama\",\r\n        model: LOCAL_ONBOARDING_EMBEDDINGS_MODEL,\r\n        roles: [\"embed\"],\r\n      },\r\n      ...(config.models ?? []),\r\n    ] */" });
    }

    public static ConfigYaml setupQuickstartConfig(ConfigYaml config)
    {
        return config;
    }

    public static ConfigYaml setupProviderConfig(ConfigYaml config, string provider, string apiKey)
    {
        var newModels;
        var existingModels = config.models ?? "/* unknown: [] */";
        var isSameModel = (object m, object n) => "/* untranslatable binary op */" && "/* untranslatable binary op */" && m.provider == n.provider && m.model == n.model;
        var updatedModels = existingModels.map("/* untranslatable arrow body */");
        var modelsToAdd = newModels.filter((object n) => !existingModels.some((object m) => isSameModel(m, n)));
        return SpreadMerge.Merge(config, new { models = "/* unknown: [...updatedModels, ...modelsToAdd] */" });
    }
}