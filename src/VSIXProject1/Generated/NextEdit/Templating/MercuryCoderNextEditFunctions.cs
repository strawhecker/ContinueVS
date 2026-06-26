namespace ContinueCore.NextEdit.Templating;
public static partial class MercuryCoderNextEditFunctions
{
    public static string recentlyViewedCodeSnippetsBlock(
     { filepath :  string ;  content :  string ;  } [ ] 
    recentlyViewedCodeSnippets)
    {
        return recentlyViewedCodeSnippets.reduce("/* untranslatable arrow body */", "");
    }

    public static string currentFileContentBlock(string currentFileContent, double editableRegionStartLine, double editableRegionEndLine, Position cursorPosition)
    {
        var currentFileContentLines = currentFileContent.split("\\n");
        var insertedCursorLines = insertCursorToken(currentFileContentLines, cursorPosition, MERCURY_CURSOR);
        var instrumentedLines = "/* unknown: [\r\n    ...insertedCursorLines.slice(0, editableRegionStartLine),\r\n    MERCURY_CODE_TO_EDIT_OPEN,\r\n    ...insertedCursorLines.slice(\r\n      editableRegionStartLine,\r\n      editableRegionEndLine + 1,\r\n    ),\r\n    MERCURY_CODE_TO_EDIT_CLOSE,\r\n    ...insertedCursorLines.slice(editableRegionEndLine + 1),\r\n  ] */";
        return instrumentedLines.join("\\n");
    }

    public static string editHistoryBlock(string[] editDiffHistory)
    {
        return editDiffHistory.map((string diff) => diff.split("\\n").slice(2L).join("\\n")).join("\\n");
    }

    public static string mercuryNextEditTemplateBuilder(
     { filepath :  string ;  code :  string ;  } [ ] 
    recentlyViewedCodeSnippets, string currentFileContent, string codeToEdit, Range codeToEditRange, Position cursorPosition, string editDiffHistory)
    {
        return "";
    }
}