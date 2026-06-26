namespace ContinueCore.Util;
public static partial class RepoUrlFunctions
{
    public static string normalizeRepoUrl(string url)
    {
        if (!url)
        {
        }

        var normalized = url.trim();
        if (normalized.startsWith("git@github.com:"))
        {
            normalized = normalized.replace("git@github.com:", "https://github.com/");
        }

        if (normalized.startsWith("ssh://git@github.com"))
        {
            normalized = normalized.replace("ssh://git@github.com/", "https://github.com/").replace("ssh://git@github.com:", "https://github.com/");
        }

        if (normalized.includes("/") && !"/* unknown: /^[a-z]+:\\/\\//i */".test(normalized) && !normalized.startsWith("git@"))
        {
            normalized = $"https://github.com/{normalized}";
        }

        if (normalized.endsWith("/"))
        {
            normalized = normalized.slice(0L, -1L);
        }

        if (normalized.endsWith(".git"))
        {
            normalized = normalized.slice(0L, -4L);
        }

        return normalized.toLowerCase();
    }
}