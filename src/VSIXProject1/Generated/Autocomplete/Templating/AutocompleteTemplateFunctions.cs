namespace ContinueCore.Autocomplete.Templating;
public static partial class AutocompleteTemplateFunctions
{
    public static AutocompleteTemplate getTemplateForModel(string model)
    {
        var lowerCaseModel = model.toLowerCase();
        if (lowerCaseModel.includes("mercury"))
        {
            return mercuryMultifileFimTemplate;
        }

        if (lowerCaseModel.includes("qwen") && lowerCaseModel.includes("coder"))
        {
            return qwenMultifileFimTemplate;
        }

        if (lowerCaseModel.includes("granite") && lowerCaseModel.includes("4"))
        {
            return granite4FimTemplate;
        }

        if (lowerCaseModel.includes("seed") && lowerCaseModel.includes("coder"))
        {
            return seedCoderFimTemplate;
        }

        if (lowerCaseModel.includes("starcoder") || lowerCaseModel.includes("star-coder") || lowerCaseModel.includes("starchat") || lowerCaseModel.includes("octocoder") || lowerCaseModel.includes("stable") || lowerCaseModel.includes("codeqwen") || lowerCaseModel.includes("qwen"))
        {
            return stableCodeFimTemplate;
        }

        if (lowerCaseModel.includes("codestral"))
        {
            return codestralMultifileFimTemplate;
        }

        if (lowerCaseModel.includes("codegemma"))
        {
            return codegemmaFimTemplate;
        }

        if (lowerCaseModel.includes("codellama"))
        {
            return codeLlamaFimTemplate;
        }

        if (lowerCaseModel.includes("deepseek"))
        {
            return deepseekFimTemplate;
        }

        if (lowerCaseModel.includes("codegeex"))
        {
            return codegeexFimTemplate;
        }

        if (lowerCaseModel.includes("gpt") || lowerCaseModel.includes("davinci-002") || lowerCaseModel.includes("claude") || lowerCaseModel.includes("granite3") || lowerCaseModel.includes("granite-3"))
        {
            return holeFillerTemplate;
        }

        return stableCodeFimTemplate;
    }
}