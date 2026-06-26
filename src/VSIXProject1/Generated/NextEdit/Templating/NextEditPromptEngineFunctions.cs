namespace ContinueCore.NextEdit.Templating;
public static partial class NextEditPromptEngineFunctions
{
    public static string getTemplateForModel(NEXT_EDIT_MODELS modelName)
    {
        var template = "/* unknown: NEXT_EDIT_MODEL_TEMPLATES[modelName] */";
        if (!template)
        {
            throw "/* unknown: new Error(`Model ${modelName} is not supported for next edit.`) */";
        }

        return template.template;
    }
}