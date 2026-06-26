namespace ContinueCore.Indexing.Chunk;
public static partial class BasicFunctions
{
    public static async AsyncGenerator<ChunkWithoutID, object, object> basicChunker(string contents, double maxChunkSize)
    {
        if (contents.trim().length == 0L)
        {
            return;
        }

        var chunkContent = "";
        var chunkTokens = 0L;
        var startLine = 0L;
        var currLine = 0L;
        var lineTokens = await Promise.all(contents.split("\\n").map((string l) => new { line = l, tokenCount = await countTokensAsync(l) }));
        foreach (var const lt in lineTokens)
        {
            if (chunkTokens + lt.tokenCount > maxChunkSize - 5L)
            {
                "/* unknown: yield { content: chunkContent, startLine, endLine: currLine - 1 } */";
                chunkContent = "";
                chunkTokens = 0L;
                startLine = currLine;
            }

            if (lt.tokenCount < maxChunkSize)
            {
                chunkContent += $"{lt.line}
";
                chunkTokens += lt.tokenCount + 1L;
            }

            "/* unknown: currLine++ */";
        }

        "/* unknown: yield {\r\n    content: chunkContent,\r\n    startLine,\r\n    endLine: currLine - 1,\r\n  } */";
    }
}