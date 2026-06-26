namespace ContinueCore.Indexing.Test;
public static partial class IndexingFunctions
{
    public static async Task insertMockChunks()
    {
        var index = "/* unknown: new ChunkCodebaseIndex(\r\n    testIde.readFile.bind(testIde),\r\n    mockContinueServerClient,\r\n    1000,\r\n  ) */";
        addToTestDir("/* unknown: [[mockFilename, mockFileContents]] */");
        await updateIndexAndAwaitGenerator(index, "compute", mockMarkComplete);
        await updateIndexAndAwaitGenerator(index, "addTag", mockMarkComplete);
    }

    public static async Task updateIndexAndAwaitGenerator(CodebaseIndex index, keyof RefreshIndexResults  resultType, object markComplete, IndexTag? tag)
    {
        var computeGenerator = index.update(tag, SpreadMerge.Merge(mockResults, new { [resultType] = "/* unknown: [mockPathAndCacheKey] */" }), markComplete, "test-repo");
        while (!await computeGenerator.next().done)
        {
        }
    }
}