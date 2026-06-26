namespace ContinueCore.Context.Mcp;
public static partial class MCPConnectionFunctions
{
    public static bool is401Error(object error)
    {
        return error is SseError && error.code == 401L || error is Error && error.message.includes("401") || error is Error && error.message.includes("Unauthorized");
    }
}