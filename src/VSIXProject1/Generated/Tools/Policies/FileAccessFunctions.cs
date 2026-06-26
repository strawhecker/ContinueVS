namespace ContinueCore.Tools.Policies;
public static partial class FileAccessFunctions
{
    public static ToolPolicy evaluateFileAccessPolicy(ToolPolicy basePolicy, bool isWithinWorkspace)
    {
        if (basePolicy == "disabled")
        {
            return "disabled";
        }

        if (isWithinWorkspace)
        {
            return basePolicy;
        }

        return "allowedWithPermission";
    }
}