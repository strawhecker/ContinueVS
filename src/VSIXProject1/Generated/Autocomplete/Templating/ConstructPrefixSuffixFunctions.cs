namespace ContinueCore.Autocomplete.Templating;
public static partial class ConstructPrefixSuffixFunctions
{
    public static
     { prefix :  string ;  suffix :  string ;  }
    constructInitialPrefixSuffix(AutocompleteInput input, string fileContents)
    {
        var lang = languageForFilepath(input.filepath);
        var fileLines = fileContents.split("\\n");
        var prefix = getRangeInString(fileContents, new { start = new { line = 0L, character = 0L }, end = input.selectedCompletionInfo.range.start ?? input.pos }) + input.selectedCompletionInfo.text ?? "";
        if (input.injectDetails)
        {
            var lines = prefix.split("\\n");
            prefix = $"{lines.slice(0L, -1L).join("\\n")}
{lang.singleLineComment} {input.injectDetails.split("\\n").join($"
{lang.singleLineComment} ")}
{"/* unknown: lines[lines.length - 1] */"}";
        }

        var suffix = getRangeInString(fileContents, new { start = input.pos, end = new { line = fileLines.length - 1L, character = Number.MAX_SAFE_INTEGER } });
        return new
        {
            prefix,
            suffix
        };
    }
}