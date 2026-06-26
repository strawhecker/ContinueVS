namespace ContinueCore.Utils;
public static partial class StreamMarkdownUtilsFunctions
{
    public static bool shouldStopAtMarkdownBlock(MarkdownBlockStateTracker stateTracker, double currentIndex)
    {
        return stateTracker.shouldStopAtPosition(currentIndex);
    }

    public static
     { newSeenFirstFence :  boolean ;  shouldSkip :  boolean ;  }
    processBlockNesting(string line, bool seenFirstFence, (line :  string )  =>  boolean shouldRemoveLineBeforeStart)
    {
        if (!seenFirstFence && shouldRemoveLineBeforeStart(line))
        {
            return new
            {
                newSeenFirstFence = false,
                shouldSkip = true
            };
        }

        if (!seenFirstFence)
        {
            return new
            {
                newSeenFirstFence = true,
                shouldSkip = false
            };
        }

        return new
        {
            newSeenFirstFence = seenFirstFence,
            shouldSkip = false
        };
    }

    public static async LineStream stopAtLinesWithMarkdownSupport(LineStream lines, string filename)
    {
        if (!isMarkdownFile(filename))
        {
            foreach (var const line in lines)
            {
                if (line.trim() == "```")
                {
                    return;
                }

                "/* unknown: yield line */";
            }

            return;
        }

        var allLines = "/* unknown: [] */";
        foreach (var const line in lines)
        {
            allLines.push(line);
        }

        var source = allLines.join("\\n");
        if (!source.match("/* unknown: /```(\\w*|.*)(md|markdown|gfm|github-markdown)/ */"))
        {
            var foundStandaloneBackticks = false;
            for (var i = 0; i < allLines.length; "/* unknown: i++ */")
            {
                if ("/* unknown: allLines[i] */".trim() == "```")
                {
                    for (var j = 0; j < i; "/* unknown: j++ */")
                    {
                        "/* unknown: yield allLines[j] */";
                    }

                    foundStandaloneBackticks = true;
                    return;
                }
            }

            if (!foundStandaloneBackticks)
            {
                foreach (var const line in allLines)
                {
                    "/* unknown: yield line */";
                }
            }

            return;
        }

        var stateTracker = "/* unknown: new MarkdownBlockStateTracker(allLines) */";
        for (var i = 0; i < allLines.length; "/* unknown: i++ */")
        {
            if (stateTracker.shouldStopAtPosition(i))
            {
                for (var j = 0; j < i; "/* unknown: j++ */")
                {
                    "/* unknown: yield allLines[j] */";
                }

                return;
            }
        }

        foreach (var const line in allLines)
        {
            "/* unknown: yield line */";
        }
    }
}