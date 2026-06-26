namespace ContinueCore.Tools.Implementations;
public static partial class ReadFileLimitFunctions
{
    public static async Task throwIfFileExceedsHalfOfContext(string filepath, string content, ILLM? model)
    {
        if (model)
        {
            var tokens = await countTokensAsync(content, model.title);
            var tokenLimit = model.contextLength / 2L;
            if (tokens > tokenLimit)
            {
                throw "/* unknown: new ContinueError(\r\n        ContinueErrorReason.FileTooLarge,\r\n        `File ${filepath} is too large (${tokens} tokens vs ${tokenLimit} token limit). Try another approach`,\r\n      ) */";
            }
        }
    }
}