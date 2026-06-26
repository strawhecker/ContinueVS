namespace ContinueCore.Llm;
public static partial class AsyncEncoderFunctions
{
    public static string workerCodeFilePath(string workerFileName)
    {
        if (process.env.NODE_ENV == "test")
        {
            return System.IO.Path.Combine(__dirname, "llm", workerFileName);
        }

        return System.IO.Path.Combine(__dirname, workerFileName);
    }
}