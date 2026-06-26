namespace ContinueCore.Diff;
public static partial class UtilFunctions
{
    public static bool linesMatchPerfectly(string lineA, string lineB)
    {
        return lineA == lineB && lineA != "";
    }

    public static bool linesMatch(string lineA, string lineB, double? linesBetween)
    {
        if ("/* unknown: [\"}\", \"*\", \"});\", \"})\"] */".includes(lineA.trim()))
        {
            return lineA.trim() == lineB.trim();
        }

        var d = distance(lineA, lineB);
        return d / Math.max(lineA.length, lineB.length) <= Math.max(0L, 0.48 - linesBetween * 0.06) || lineA.trim() == lineB.trim() && lineA.trim() != "";
    }

    public static MatchLineResult matchLine(string newLine, string[] oldLines, bool? permissiveAboutIndentation)
    {
        if (newLine.trim() == "" && "/* unknown: oldLines[0] */".trim() == "")
        {
            return new
            {
                matchIndex = 0L,
                isPerfectMatch = true,
                newLine = newLine.trim()
            };
        }

        var isEndBracket = END_BRACKETS.includes(newLine.trim());
        for (var i = 0; i < oldLines.length; "/* unknown: i++ */")
        {
            var oldLineTrimmed = "/* unknown: oldLines[i] */".trimEnd();
            var newLineTrimmed = newLine.trimEnd();
            if (i > 4L && isEndBracket)
            {
                return new
                {
                    matchIndex = -1L,
                    isPerfectMatch = false,
                    newLine
                };
            }

            if (linesMatchPerfectly(newLineTrimmed, oldLineTrimmed))
            {
                return new
                {
                    matchIndex = i,
                    isPerfectMatch = true,
                    newLine
                };
            }

            if (linesMatch(newLineTrimmed, oldLineTrimmed, i))
            {
                if (newLineTrimmed.trimStart() == oldLineTrimmed.trimStart() && permissiveAboutIndentation || newLine.trim().length > 8L)
                {
                    return new
                    {
                        matchIndex = i,
                        isPerfectMatch = true,
                        newLine = "/* unknown: oldLines[i] */"
                    };
                }

                return new
                {
                    matchIndex = i,
                    isPerfectMatch = false,
                    newLine
                };
            }
        }

        return new
        {
            matchIndex = -1L,
            isPerfectMatch = false,
            newLine
        };
    }

    public static async LineStream streamLines(AsyncGenerator<string | ChatMessage, object, object> streamCompletion, bool? log)
    {
        var allLines = "/* unknown: [] */";
        var buffer = "";
        try
        {
            foreach (var const update in streamCompletion)
            {
                var chunk = update is string ? update : renderChatMessage(update);
                buffer += chunk;
                var lines = buffer.split("\\n");
                buffer = lines.pop() ?? "";
                foreach (var const line in lines)
                {
                    "/* unknown: yield line */";
                    allLines.push(line);
                }
            }

            if (buffer.length > 0L)
            {
                "/* unknown: yield buffer */";
                allLines.push(buffer);
            }
        }
    }

    public static async AsyncGenerator<T, object, object> generateLines<T>(T[] lines)
    {
        foreach (var const line in lines)
        {
            "/* unknown: yield line */";
        }
    }
}