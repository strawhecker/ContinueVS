namespace ContinueCore.Autocomplete.Classification;
public static partial class ShouldCompleteMultilineFunctions
{
    public static bool isMidlineCompletion(string prefix, string suffix)
    {
        return !suffix.startsWith("\\n");
    }

    public static bool shouldCompleteMultilineBasedOnLanguage(AutocompleteLanguageInfo language, string prefix, string suffix)
    {
        return language.useMultiline(new { prefix, suffix }) ?? true;
    }

    public static bool shouldCompleteMultiline(HelperVars helper)
    {
        if (helper.input.selectedCompletionInfo)
        {
            return true;
        }

        if (helper.lang.singleLineComment && "/* unknown: helper.fullPrefix\r\n      .split(\"\\n\")\r\n      .slice(-1)[0] */".trimStart().startsWith(helper.lang.singleLineComment))
        {
            return false;
        }

        return shouldCompleteMultilineBasedOnLanguage(helper.lang, helper.prunedPrefix, helper.prunedSuffix);
    }
}