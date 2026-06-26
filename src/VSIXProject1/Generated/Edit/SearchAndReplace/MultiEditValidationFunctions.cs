namespace ContinueCore.Edit.SearchAndReplace;
public static partial class MultiEditValidationFunctions
{
    public static
     { edits :  EditOperation [ ] ;  }
    validateMultiEdit(object args)
    {
        if (args is not object || !args || !"/* untranslatable binary op */")
        {
            throw "/* unknown: new ContinueError(\r\n      ContinueErrorReason.MultiEditEditsArrayRequired,\r\n      \"invalid multi-edit args\",\r\n    ) */";
        }

        if (!Array.isArray(args.edits))
        {
            throw "/* unknown: new ContinueError(\r\n      ContinueErrorReason.MultiEditEditsArrayRequired,\r\n      \"edits array is required\",\r\n    ) */";
        }

        var { edits } = args;
        if (edits.length == 0L)
        {
            throw "/* unknown: new ContinueError(\r\n      ContinueErrorReason.MultiEditEditsArrayEmpty,\r\n      \"edits array must contain at least one edit\",\r\n    ) */";
        }

        for (var i = 0; i < edits.length; "/* unknown: i++ */")
        {
            var edit = "/* unknown: edits[i] */";
            validateSingleEdit(edit.old_string, edit.new_string, edit.replace_all, i);
            if (i > 0L && edit.old_string == "")
            {
                throw "/* unknown: new ContinueError(\r\n        ContinueErrorReason.FindAndReplaceNonFirstEmptyOldString,\r\n        `Edit at index ${i}: old_string cannot be empty. Only the first edit can have an empty old_string for insertion at the beginning of the file.`,\r\n      ) */";
            }
        }

        return new
        {
            edits
        };
    }
}