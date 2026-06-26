namespace ContinueCore.Edit.Lazy;
public static partial class UnifiedDiffApplyFunctions
{
    public static bool isUnifiedDiffFormat(string diff)
    {
        var lines = diff.trim().split("\\n");
        if (lines.length < 3L)
        {
            return false;
        }

        var hasHunkHeader = false;
        var hasValidContent = false;
        foreach (var const line in lines)
        {
            if (line.startsWith("---") || line.startsWith("+++"))
            {
            }
        }

        return hasHunkHeader && hasValidContent;
    }

    public static string[] extractBeforeLines(string[] hunkLines)
    {
        return hunkLines.filter((string line) => line.startsWith("-") || !line.startsWith("+")).map((string line) => line.substring(1L));
    }

    public static DiffLine[] applyUnifiedDiff(string sourceCode, string unifiedDiffText)
    {
        var sourceLines = sourceCode.split("/* unknown: /\\r?\\n/ */");
        var hunks = parseUnifiedDiff(unifiedDiffText);
        var diffResult = "/* unknown: [] */";
        var currentPos = 0L;
        foreach (var const hunk in hunks)
        {
            var hunkBeforeLines = extractBeforeLines(hunk.lines);
            var hunkStart = findHunkInSource(sourceLines, hunkBeforeLines, currentPos);
            if (hunkStart == -1L)
            {
                throw "/* unknown: new Error(\"Hunk could not be applied cleanly to source code.\") */";
            }

            for (var i = currentPos; i < hunkStart; "/* unknown: i++ */")
            {
                diffResult.push(new { type = "same", line = "/* unknown: sourceLines[i] */" });
            }

            var hunkSourcePos = hunkStart;
            foreach (var const dline in hunk.lines)
            {
                var srcLine = "/* unknown: sourceLines[hunkSourcePos] */";
                if (dline.startsWith("+"))
                {
                    diffResult.push(new { type = "new", line = dline.substring(1L) });
                }
            }

            currentPos = hunkSourcePos;
        }

        for (var i = currentPos; i < sourceLines.length; "/* unknown: i++ */")
        {
            diffResult.push(new { type = "same", line = "/* unknown: sourceLines[i] */" });
        }

        return diffResult;
    }

    public static Hunk[] parseUnifiedDiff(string diffText)
    {
        var lines = diffText.split("/* unknown: /\\r?\\n/ */");
        var hunks = "/* unknown: [] */";
        var currentHunk = null;
        foreach (var const line in lines)
        {
            if (line.startsWith("---") || line.startsWith("+++"))
            {
            }

            if (line.startsWith("@@"))
            {
                if (currentHunk)
                {
                    hunks.push(currentHunk);
                }

                currentHunk = new
                {
                    lines = "/* unknown: [] */"
                };
            }

            currentHunk.lines.push(line);
        }

        if (currentHunk)
        {
            hunks.push(currentHunk);
        }

        return hunks;
    }

    public static double findHunkInSource(string[] sourceLines, string[] hunkBeforeLines, double startIndex)
    {
        for (var i = startIndex; i <= sourceLines.length - hunkBeforeLines.length; "/* unknown: i++ */")
        {
            var match = true;
            for (var j = 0; j < hunkBeforeLines.length; "/* unknown: j++ */")
            {
                var sl = "/* unknown: sourceLines[i + j] */";
                var hl = "/* unknown: hunkBeforeLines[j] */";
                if (!linesMatch(sl, hl))
                {
                    match = false;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1L;
    }

    public static bool linesMatch(string a, string b)
    {
        var trimmedA = a.replace("/* unknown: /^\\s+/ */", "");
        var trimmedB = b.replace("/* unknown: /^\\s+/ */", "");
        return trimmedA == trimmedB;
    }
}