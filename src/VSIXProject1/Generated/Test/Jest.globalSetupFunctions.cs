namespace ContinueCore.Test;
public static partial class Jest.globalSetupFunctions
{
    public static async Task (anonymous)()
    {
        process.env.CONTINUE_GLOBAL_DIR = System.IO.Path.Combine(__dirname, ".continue-test");
        if (System.IO.File.Exists(process.env.CONTINUE_GLOBAL_DIR))
        {
            fs.rmSync(process.env.CONTINUE_GLOBAL_DIR, new { recursive = true, force = true });
        }
    }
}