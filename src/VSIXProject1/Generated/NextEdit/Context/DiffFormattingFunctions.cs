namespace ContinueCore.NextEdit.Context;
public static partial class DiffFormattingFunctions
{
    public static DiffMetadata extractMetadataFromUnifiedDiff(string unifiedDiff)
    {
        var metadata = new
        {
        };
        var lines = unifiedDiff.split("\\n");
        if (lines.length >= 2L)
        {
            var oldFileMatch = "/* unknown: lines[0] */".match("/* unknown: /^--- (a\\/)?(.+?)(?:\\t(.+))?$/ */");
            if (oldFileMatch)
            {
                metadata.oldFilename = "/* unknown: oldFileMatch[2] */";
                metadata.oldTimestamp = "/* unknown: oldFileMatch[3] */";
                if (metadata.oldFilename == "/dev/null")
                {
                    metadata.isNew = true;
                }
            }

            var newFileMatch = "/* unknown: lines[1] */".match("/* unknown: /^\\+\\+\\+ (b\\/)?(.+?)(?:\\t(.+))?$/ */");
            if (newFileMatch)
            {
                metadata.newFilename = "/* unknown: newFileMatch[2] */";
                metadata.newTimestamp = "/* unknown: newFileMatch[3] */";
                if (metadata.newFilename == "/dev/null")
                {
                    metadata.isDeleted = true;
                }
            }

            if (metadata.oldFilename && metadata.newFilename && metadata.oldFilename != "/dev/null" && metadata.newFilename != "/dev/null" && metadata.oldFilename != metadata.newFilename)
            {
                metadata.isRename = true;
            }
        }

        metadata.hunks = "/* unknown: [] */";
        var hunkHeaderRegex = "/* unknown: /^@@ -(\\d+)(?:,(\\d+))? \\+(\\d+)(?:,(\\d+))? @@(?:\\s(.*))?$/ */";
        var currentHunk = null;
        var oldLineNumber = 0L;
        var newLineNumber = 0L;
        for (var i = 2; i < lines.length; "/* unknown: i++ */")
        {
            var line = "/* unknown: lines[i] */";
            var hunkMatch = line.match(hunkHeaderRegex);
            if (hunkMatch)
            {
                currentHunk = new
                {
                    oldStart = parseInt("/* unknown: hunkMatch[1] */", 10L),
                    oldCount = "/* unknown: hunkMatch[2] */" ? parseInt("/* unknown: hunkMatch[2] */", 10L) : 1L,
                    newStart = parseInt("/* unknown: hunkMatch[3] */", 10L),
                    newCount = "/* unknown: hunkMatch[4] */" ? parseInt("/* unknown: hunkMatch[4] */", 10L) : 1L,
                    header = "/* unknown: hunkMatch[5] */",
                    lines = "/* unknown: [] */"
                };
                oldLineNumber = currentHunk.oldStart;
                newLineNumber = currentHunk.newStart;
                metadata.hunks.push(currentHunk);
            }

            if (line.includes("Binary files") || line.includes("GIT binary patch"))
            {
                metadata.isBinary = true;
            }
        }

        return metadata;
    }
}