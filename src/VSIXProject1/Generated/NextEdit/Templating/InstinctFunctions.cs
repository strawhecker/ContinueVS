namespace ContinueCore.NextEdit.Templating;
public static partial class InstinctFunctions
{
    public static string contextSnippetsBlock(string contextSnippets)
    {
        var headerRegex = "/* unknown: /^(\\+\\+\\+\\+\\+ )(.*)/ */";
        var lines = contextSnippets.split("\\n");
        return lines.reduce("/* untranslatable arrow body */", "/* unknown: [] */").join("\\n");
    }

    public static string currentFileContentBlock(string currentFileContent, double windowStart, double windowEnd, double editableRegionStartLine, double editableRegionEndLine, Position cursorPosition)
    {
        var currentFileContentLines = currentFileContent.split("\\n");
        var insertedCursorLines = insertCursorToken(currentFileContentLines, cursorPosition, INSTINCT_USER_CURSOR_IS_HERE_TOKEN);
        var instrumentedLines = "/* unknown: [\r\n    ...insertedCursorLines.slice(windowStart, editableRegionStartLine),\r\n    INSTINCT_EDITABLE_REGION_START_TOKEN,\r\n    ...insertedCursorLines.slice(\r\n      editableRegionStartLine,\r\n      editableRegionEndLine + 1,\r\n    ),\r\n    INSTINCT_EDITABLE_REGION_END_TOKEN,\r\n    ...insertedCursorLines.slice(editableRegionEndLine + 1, windowEnd + 1),\r\n  ] */";
        return instrumentedLines.join("\\n");
    }

    public static string editHistoryBlock(string[] editDiffHistories)
    {
        if (!editDiffHistories.length)
        {
            return "";
        }

        var blocks = "/* unknown: [] */";
        foreach (var const editDiffHistory in editDiffHistories)
        {
            if (!editDiffHistory.trim())
            {
            }

            var diffSections = editDiffHistory.split("/* unknown: /^Index: /m */").filter((string section) => section.trim());
            foreach (var const section in diffSections)
            {
                var lines = section.split("\\n");
                var filename = "/* unknown: lines[0] */";
                var diffLines = lines.filter((string line) => !line.startsWith("---") && !line.startsWith("+++") && !line.startsWith("===") && line.trim() != "").slice(1L);
                var actualDiffContent = diffLines.filter((string line) => line.startsWith("@@") || line.startsWith("+") || line.startsWith("-") || line.startsWith(" "));
                if (actualDiffContent.length == 0L)
                {
                }

                var diffBlock = "/* unknown: [\r\n        `User edited file \"${filename}\"`,\r\n        \"\",\r\n        \"```diff\",\r\n        actualDiffContent.join(\"\\n\"),\r\n        \"```\",\r\n      ] */".join("\\n");
                blocks.push(diffBlock);
            }
        }

        return blocks.join("\\n");
    }
}