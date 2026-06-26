namespace ContinueCore.Tools.Definitions;
public static partial class RunTerminalCommandFunctions
{
    public static string getPreferredShell()
    {
        var platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription();
        if (platform == "win32")
        {
            return "powershell.exe";
        }
    }
}