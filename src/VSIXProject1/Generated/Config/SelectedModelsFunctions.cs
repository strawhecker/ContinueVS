namespace ContinueCore.Config;
public static partial class SelectedModelsFunctions
{
    public static ContinueConfig rectifySelectedModelsFromGlobalContext(ContinueConfig continueConfig, string profileId)
    {
        var configCopy = continueConfig;
        var globalContext = "/* unknown: new GlobalContext() */";
        var currentSelectedModels = globalContext.get("selectedModelsByProfileId");
        var currentForProfile = "/* unknown: currentSelectedModels?.[profileId] */" ?? new
        {
        };
        var fellBack = false;
        var roles = "/* unknown: [\r\n    \"autocomplete\",\r\n    \"apply\",\r\n    \"edit\",\r\n    \"embed\",\r\n    \"rerank\",\r\n    \"chat\",\r\n  ] */";
        foreach (var const role in roles)
        {
            var newModel = null;
            var currentSelection = "/* unknown: currentForProfile[role] */" ?? null;
            if (currentSelection)
            {
                var match = "/* unknown: continueConfig.modelsByRole[role] */".find((ILLM m) => m.title == currentSelection);
                if (match)
                {
                    newModel = match;
                }
            }

            if (!newModel && "/* unknown: continueConfig.modelsByRole[role] */".length > 0L)
            {
                newModel = "/* unknown: continueConfig.modelsByRole[role][0] */";
            }

            if (!currentSelection == newModel.title ?? null)
            {
                fellBack = true;
            }

            if (role == "apply" && newModel.getConfigurationStatus() != LLMConfigurationStatuses.VALID)
            {
            }

            "/* unknown: configCopy.selectedModelByRole[role] */" = newModel;
        }

        if (fellBack)
        {
            globalContext.update("selectedModelsByProfileId", SpreadMerge.Merge(currentSelectedModels, new { [profileId] = Object.fromEntries(Object.entries(configCopy.selectedModelByRole).map(([string, ILLM | null ] [key, value]) => "/* unknown: [\r\n          key,\r\n          value?.title ?? null,\r\n        ] */")) }));
        }

        return configCopy;
    }
}