namespace ContinueCore.Autocomplete.Snippets;
public static partial class GitDiffCacheFunctions
{
    public static GetDiffFn getDiffFn(IDE ide)
    {
        return () => ide.getDiff(true);
    }

    public static async Task<string[]> getDiffsFromCache(IDE ide)
    {
        var diffCache = GitDiffCache.getInstance(getDiffFn(ide));
        return await diffCache.get();
    }
}