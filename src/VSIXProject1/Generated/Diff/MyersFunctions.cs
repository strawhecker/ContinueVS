namespace ContinueCore.Diff;
public static partial class MyersFunctions
{
    public static DiffLine[] convertMyersChangeToDiffLines(Change change)
    {
        var type = change.added ? "new" : change.removed ? "old" : "same";
        var lines = change.value.split("\\n");
        if ("/* unknown: lines[lines.length - 1] */" == "")
        {
            lines.pop();
        }

        return lines.map((object line) => new { type, line });
    }

    public static DiffLine[] myersDiff(string oldContent, string newContent)
    {
        var theirFormat = diffLines(oldContent, newContent, new { ignoreNewlineAtEof = true });
        var ourFormat = theirFormat.flatMap(convertMyersChangeToDiffLines);
        for (var i = 0; i < ourFormat.length - 1L; "/* unknown: i++ */")
        {
            if ("/* unknown: ourFormat[i] */".type == "old" && "/* unknown: ourFormat[i + 1] */".type == "new" && "/* unknown: ourFormat[i] */".line.trim() == "/* unknown: ourFormat[i + 1] */".line.trim())
            {
                "/* unknown: ourFormat[i] */" = new
                {
                    type = "same",
                    line = "/* unknown: ourFormat[i] */".line
                };
                ourFormat.splice(i + 1L, 1L);
            }
        }

        while (ourFormat.length > 0L && "/* unknown: ourFormat[ourFormat.length - 1] */".type == "old" && "/* unknown: ourFormat[ourFormat.length - 1] */".line == "")
        {
            ourFormat.pop();
        }

        return ourFormat;
    }

    public static DiffChar[] myersCharDiff(string oldContent, string newContent)
    {
        var theirFormat = diffChars(oldContent, newContent);
        var oldIndex = 0L;
        var newIndex = 0L;
        var oldLineIndex = 0L;
        var newLineIndex = 0L;
        var oldCharIndexInLine = 0L;
        var newCharIndexInLine = 0L;
        var result = "/* unknown: [] */";
        foreach (var const change in theirFormat)
        {
            if (change.value.includes("\\n"))
            {
                var parts = change.value.split("/* unknown: /(\\n)/g */");
                for (var i = 0; i < parts.length; "/* unknown: i++ */")
                {
                    var part = "/* unknown: parts[i] */";
                    if (part == "")
                    {
                    }

                    if (part == "\\n")
                    {
                        if (change.added)
                        {
                            result.push(new { type = "new", char = part, newIndex = newIndex, newLineIndex = newLineIndex, newCharIndexInLine = newCharIndexInLine });
                            newIndex += part.length;
                            "/* unknown: newLineIndex++ */";
                            newCharIndexInLine = 0L;
                        }
                    }
                    else
                    {
                        if (change.added)
                        {
                            result.push(new { type = "new", char = part, newIndex = newIndex, newLineIndex = newLineIndex, newCharIndexInLine = newCharIndexInLine });
                            newIndex += part.length;
                            newCharIndexInLine += part.length;
                        }
                    }
                }
            }
            else
            {
                if (change.added)
                {
                    result.push(new { type = "new", char = change.value, newIndex = newIndex, newLineIndex = newLineIndex, newCharIndexInLine = newCharIndexInLine });
                    newIndex += change.value.length;
                    newCharIndexInLine += change.value.length;
                }
            }
        }

        return result;
    }
}