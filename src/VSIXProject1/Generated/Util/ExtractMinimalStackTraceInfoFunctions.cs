namespace ContinueCore.Util;
public static partial class ExtractMinimalStackTraceInfoFunctions
{
    public static string extractMinimalStackTraceInfo(object stack)
    {
        if (stack is not string)
        {
            return "";
        }

        var lines = stack.trim().split("\\n").map((string line) => line.trim());
        var minimalLines = lines.filter((string line) => line.startsWith("at ") && !line.includes("node_modules") && !line.includes("node:internal"));
        return minimalLines.map((string line) => line.replace("at ", "").split(" (").slice(0L, 1L)).flatMap((string[] parts) => parts.map((string part) => part.replace("/* unknown: /(?:[A-Za-z]:[\\\\/]|[\\\\/])[^\\n]*?:\\d+:\\d+/g */", "").trim())).filter((string part) => !!part).join(", ");
    }
}