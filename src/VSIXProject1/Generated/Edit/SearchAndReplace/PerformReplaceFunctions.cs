namespace ContinueCore.Edit.SearchAndReplace;
public static partial class PerformReplaceFunctions
{
    public static string getLeadingIndent(string text)
    {
        var lines = text.split("\\n");
        foreach (var const line in lines)
        {
            if (line.trim().length > 0L)
            {
                var match = line.match("/* unknown: /^(\\s*)/ */");
                return match ? "/* unknown: match[1] */" : "";
            }
        }

        return "";
    }

    public static string getLineIndentAtPosition(string fileContent, double position)
    {
        var lineStart = fileContent.lastIndexOf("\\n", position - 1L) + 1L;
        var lineEnd = fileContent.indexOf("\\n", lineStart);
        var line = fileContent.substring(lineStart, lineEnd == -1L ? fileContent.length : lineEnd);
        var match = line.match("/* unknown: /^(\\s*)/ */");
        return match ? "/* unknown: match[1] */" : "";
    }

    public static string adjustReplacementIndentation(string fileContent, SearchMatchResult match, string oldString, string newString)
    {
        if (match.strategyName == "exactMatch" || match.strategyName == "emptySearch")
        {
            return newString;
        }

        var matchedIndent = getLineIndentAtPosition(fileContent, match.startIndex);
        var oldIndent = getLeadingIndent(oldString);
        if (matchedIndent == oldIndent)
        {
            return newString;
        }

        var lines = newString.split("\\n");
        var adjusted = lines.map("/* untranslatable arrow body */");
        return adjusted.join("\\n");
    }

    public static string executeFindAndReplace(string fileContent, string oldString, string newString, bool replaceAll, double? editIndex)
    {
        var matches = findSearchMatches(fileContent, oldString);
        if (matches.length == 0L)
        {
            throw "/* unknown: new ContinueError(\r\n      ContinueErrorReason.FindAndReplaceOldStringNotFound,\r\n      `Edit at index ${editIndex}: string not found in file: \"${oldString}\"`,\r\n    ) */";
        }

        if (replaceAll)
        {
            var result = fileContent;
            for (var i = matches.length - 1; i >= 0L; "/* unknown: i-- */")
            {
                var match = "/* unknown: matches[i] */";
                var adjustedNew = adjustReplacementIndentation(result, match, oldString, newString);
                result = result.substring(0L, match.startIndex) + adjustedNew + result.substring(match.endIndex);
            }

            return result;
        }
        else
        {
            if (matches.length > 1L)
            {
                throw "/* unknown: new ContinueError(\r\n        ContinueErrorReason.FindAndReplaceMultipleOccurrences,\r\n        `Edit at index ${editIndex}: String \"${oldString}\" appears ${matches.length} times in the file. Either provide a more specific string with surrounding context to make it unique, or use replace_all=true to replace all occurrences.`,\r\n      ) */";
            }

            var match = "/* unknown: matches[0] */";
            var adjustedNew = adjustReplacementIndentation(fileContent, match, oldString, newString);
            return fileContent.substring(0L, match.startIndex) + adjustedNew + fileContent.substring(match.endIndex);
        }
    }

    public static string executeMultiFindAndReplace(string fileContent, EditOperation[] edits)
    {
        var result = fileContent;
        for (var editIndex = 0; editIndex < edits.length; "/* unknown: editIndex++ */")
        {
            var edit = "/* unknown: edits[editIndex] */";
            result = executeFindAndReplace(result, edit.old_string, edit.new_string, edit.replace_all ?? false, editIndex);
        }

        return result;
    }
}