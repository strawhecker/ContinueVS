namespace ContinueCore.Tools.Implementations;
public static partial class GrepSearchFunctions
{
    public static ContextItem[] splitGrepResultsByFile(string content)
    {
        var matches = "/* unknown: [...content.matchAll(/^\\.\\/([^\\n]+)$/gm)] */";
        var contextItems = "/* unknown: [] */";
        for (var i = 0; i < matches.length; "/* unknown: i++ */")
        {
            var match = "/* unknown: matches[i] */";
            var filepath = "/* unknown: match[1] */";
            var startIndex = "/* unknown: match.index! */";
            var endIndex = i < matches.length - 1L ? "/* unknown: matches[i + 1].index! */" : content.length;
            var fileContent = content.substring(startIndex, endIndex).replace("/* unknown: /^\\.\\/[^\\n]+\\n/ */", "").trim();
            if (fileContent)
            {
                contextItems.push(new { name = $"Search results in {filepath}", description = $"Grep search results from {filepath}", content = fileContent, uri = new { type = "file", value = filepath } });
            }
        }

        return contextItems;
    }
}