namespace ContinueCore.Commands.Slash.BuiltInLegacy;
public static partial class CmdFunctions
{
    public static bool commandIsPotentiallyDangerous(string command)
    {
        return command.includes("rm -rf") || command.includes("sudo") || command.includes("cd / ");
    }
}