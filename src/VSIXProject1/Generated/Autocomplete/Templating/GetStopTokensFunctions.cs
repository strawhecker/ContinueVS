namespace ContinueCore.Autocomplete.Templating;
public static partial class GetStopTokensFunctions
{
    public static string[] getStopTokens(Partial completionOptions, AutocompleteLanguageInfo lang, string model)
    {
        var stopTokens = "/* unknown: [\r\n    ...(completionOptions?.stop || []),\r\n    // ...multilineStops,\r\n    ...commonStops,\r\n    ...(model.toLowerCase().includes(\"starcoder2\")\r\n      ? STARCODER2_T_ARTIFACTS\r\n      : []),\r\n    // ...lang.topLevelKeywords.map((word) => `\\n${word}`),\r\n  ] */";
        return stopTokens;
    }
}