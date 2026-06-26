namespace ContinueCore.Diff;
public static partial class StreamDiffFunctions
{
    public static async AsyncGenerator<DiffLine, object, object> streamDiff(string[] oldLines, LineStream newLines)
    {
        var oldLinesCopy = "/* unknown: [...oldLines] */";
        var seenIndentationMistake = false;
        var newLineResult = await newLines.next();
        while (oldLinesCopy.length > 0L && !newLineResult.done)
        {
            var { matchIndex, isPerfectMatch, newLine } = matchLine(newLineResult.value, oldLinesCopy, seenIndentationMistake);
            if (!seenIndentationMistake && newLineResult.value != newLine)
            {
                seenIndentationMistake = true;
            }

            var type;
            var isNewLine = matchIndex == -1L;
            if (isNewLine)
            {
                type = "new";
            }
            else
            {
                for (var i = 0; i < matchIndex; "/* unknown: i++ */")
                {
                    "/* unknown: yield { type: \"old\", line: oldLinesCopy.shift()! } */";
                }

                type = isPerfectMatch ? "same" : "old";
            }

            newLineResult = await newLines.next();
        }

        if (newLineResult.done && oldLinesCopy.length > 0L)
        {
            foreach (var const oldLine in oldLinesCopy)
            {
                "/* unknown: yield { type: \"old\", line: oldLine } */";
            }
        }

        if (!newLineResult.done && oldLinesCopy.length == 0L)
        {
            "/* unknown: yield { type: \"new\", line: newLineResult.value } */";
            foreach (var const newLine in newLines)
            {
                "/* unknown: yield { type: \"new\", line: newLine } */";
            }
        }
    }
}