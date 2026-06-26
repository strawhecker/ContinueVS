namespace ContinueCore.Context.Providers;
public static partial class GreptileContextProviderFunctions
{
    public static string getRemoteUrl(string absPath)
    {
        try
        {
            var remote = execSync($"git -C {absPath} remote get-url origin").toString().trim();
            return remote;
        }
        catch (Exception)
        {
            console.warn("Failed to get remote URL");
            return "";
        }
    }

    public static string getCurrentBranch(string absPath)
    {
        try
        {
            var branch = execSync($"git -C {absPath} rev-parse --abbrev-ref HEAD").toString().trim();
            return branch;
        }
        catch (Exception)
        {
            console.warn("Failed to get current branch");
            return "master";
        }
    }

    public static string extractRepoName(string remote)
    {
        if (remote.startsWith("http://") || remote.startsWith("https://"))
        {
            var parts = remote.split("/");
            if (parts.length >= 2L)
            {
                return "/* unknown: parts[parts.length - 2] */" + "/" + "/* unknown: parts[parts.length - 1] */".replace(".git", "");
            }
        }

        return "";
    }

    public static string getRemoteType(string remote)
    {
        if (remote.includes("github.com"))
        {
            return "github";
        }

        return "";
    }
}