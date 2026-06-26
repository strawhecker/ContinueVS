namespace ContinueCore.Config.Markdown;
public static partial class UtilsFunctions
{
    public static string[] createRelativeRuleFilePathParts(string ruleName)
    {
        var safeRuleName = sanitizeRuleName(ruleName);
        return "/* unknown: [\".continue\", \"rules\", `${safeRuleName}.${RULE_FILE_EXTENSION}`] */";
    }

    public static string createRelativeRuleFilePath(string ruleName)
    {
        return createRelativeRuleFilePathParts(ruleName).join("/");
    }

    public static string createRuleFilePath(string workspaceDir, string ruleName)
    {
        return joinPathsToUri(workspaceDir, "/* unknown: ...createRelativeRuleFilePathParts(ruleName) */");
    }
}