namespace ContinueCore.Context.Retrieval;
public static partial class UtilFunctions
{
    public static Chunk[] deduplicateChunks(Chunk[] chunks)
    {
        return deduplicateArray(chunks, (Chunk a, Chunk b) => a.filepath == b.filepath && a.startLine == b.startLine && a.endLine == b.endLine);
    }
}