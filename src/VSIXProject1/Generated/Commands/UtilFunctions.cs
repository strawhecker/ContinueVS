namespace ContinueCore.Commands;
public static partial class UtilFunctions
{
    public static ContextItemWithId rifWithContentsToContextItem(RangeInFileWithContents rif)
    {
        var basename = getUriPathBasename(rif.filepath);
        var { relativePathOrBasename, foundInDir, uri } = findUriInDirs(rif.filepath, window.workspacePaths ?? "/* unknown: [] */");
        var rangeStr = $"({rif.range.start.line + 1L}-{rif.range.end.line + 1L})";
        return new
        {
            content = rif.contents,
            name = $"{basename} {rangeStr}",
            description = $"{relativePathOrBasename} {rangeStr}",
            id = new
            {
                providerTitle = "code",
                itemId = uuidv4()
            },
            uri = new
            {
                type = "file",
                value = rif.filepath
            }
        };
    }

    public static RangeInFileWithContents ctxItemToRifWithContents(ContextItemWithId item, bool? linesOffByOne)
    {
        var startLine = 0L;
        var endLine = 0L;
        var adjustLines = linesOffByOne ? 1L : 0L;
        var nameSplit = item.name.split("(");
        if (nameSplit.length > 1L)
        {
            var lines = "/* unknown: nameSplit[1].split(\")\")[0] */".split("-");
            startLine = Number.parseInt("/* unknown: lines[0] */", 10L);
            if (startLine == 0L)
            {
                adjustLines = 0L;
            }

            startLine -= adjustLines;
            endLine = Number.parseInt("/* unknown: lines[1] */", 10L) - adjustLines;
        }

        var rif = new
        {
            filepath = item.uri.value || "",
            range = new
            {
                start = new
                {
                    line = startLine,
                    character = 0L
                },
                end = new
                {
                    line = endLine,
                    character = 0L
                }
            },
            contents = item.content
        };
        return rif;
    }
}