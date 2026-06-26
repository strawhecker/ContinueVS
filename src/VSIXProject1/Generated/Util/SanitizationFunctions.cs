namespace ContinueCore.Util;
public static partial class SanitizationFunctions
{
    public static string sanitizeShellArgument(string arg)
    {
        var result = quote("/* unknown: [arg] */");
        return result is string ? result : arg;
    }

    public static bool validateGitHubRepoUrl(string repoName)
    {
        if (!repoName || repoName is not string)
        {
            return false;
        }

        var trimmed = repoName.trim();
        if (trimmed.length == 0L)
        {
            return false;
        }

        if (trimmed.includes(".."))
        {
            return false;
        }

        var dangerousChars = "/* unknown: [\";\", \"&\", \"|\", \"$\", \"`\", \"\\n\", \"\\r\", \"<\", \">\"] */";
        if (dangerousChars.some((string char) => trimmed.includes(char)))
        {
            return false;
        }

        return true;
    }
}