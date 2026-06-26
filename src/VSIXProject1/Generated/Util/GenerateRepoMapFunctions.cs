namespace ContinueCore.Util;
public static partial class GenerateRepoMapFunctions
{
    public static async Task<string> generateRepoMap(ILLM llm, IDE ide, RepoMapOptions options)
    {
        var generator = "/* unknown: new RepoMapGenerator(llm, ide, options) */";
        return generator.generate();
    }
}