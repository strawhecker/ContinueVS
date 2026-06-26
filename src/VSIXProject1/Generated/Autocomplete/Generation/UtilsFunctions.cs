namespace ContinueCore.Autocomplete.Generation;
public static partial class UtilsFunctions
{
    public static async AsyncGenerator<string, object, object> stopAfterMaxProcessingTime(AsyncGenerator<string, object, object> stream, double maxTimeMs, () =>  void  fullStop)
    {
        var startTime = Date.now();
        var checkInterval = 10L;
        var chunkCount = 0L;
        var totalCharCount = 0L;
        foreach (var const chunk in stream)
        {
            "/* unknown: yield chunk */";
            "/* unknown: chunkCount++ */";
            totalCharCount += chunk.length;
            if (chunkCount % checkInterval == 0L)
            {
                if (Date.now() - startTime > maxTimeMs)
                {
                    fullStop();
                    return;
                }
            }
        }
    }
}