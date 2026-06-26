namespace ContinueCore.Edit.SearchAndReplace;
public static partial class ValidateArgsFunctions
{
    public static async Task<string> validateSearchAndReplaceFilepath(object filepath, IDE ide)
    {
        if (!filepath || filepath is not string)
        {
            throw "/* unknown: new ContinueError(\r\n      ContinueErrorReason.FindAndReplaceMissingFilepath,\r\n      \"filepath (string) is required\",\r\n    ) */";
        }

        var resolvedFilepath = await resolveRelativePathInDir(filepath, ide);
        if (!resolvedFilepath)
        {
            throw "/* unknown: new ContinueError(\r\n      ContinueErrorReason.FileNotFound,\r\n      `File ${filepath} does not exist`,\r\n    ) */";
        }

        return resolvedFilepath;
    }
}