namespace ContinueCore.Llm.Utils;
public static partial class ExtractPathsFromCodeBlocksFunctions
{
    public static string[] extractPathsFromCodeBlocks(string content)
    {
        var paths = "/* unknown: [] */";
        var codeBlockStarts = content.match("/* unknown: /```[^\\n]+/g */") || "/* unknown: [] */";
        foreach (var const blockStart in codeBlockStarts)
        {
            var filenameMatches = blockStart.match("/* unknown: /([^\\s()```]+\\.[a-zA-Z0-9]+)/ */");
            if (filenameMatches && "/* unknown: filenameMatches[1] */")
            {
                var filename = "/* unknown: filenameMatches[1] */";
                if ("/* unknown: /\\.[a-zA-Z0-9]+$/ */".test(filename) && !filename.includes("://") && !paths.includes(filename))
                {
                    paths.push(filename);
                }
            }
        }

        return paths;
    }
}