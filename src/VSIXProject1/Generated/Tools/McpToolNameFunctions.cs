namespace ContinueCore.Tools;
public static partial class McpToolNameFunctions
{
    public static string getMCPToolName(MCPServerStatus server, MCPTool tool)
    {
        return getToolNameFromMCPServer(server.name, tool.name);
    }

    public static string getToolNameFromMCPServer(string serverName, string toolName)
    {
        var serverPrefix = serverName.toLowerCase().replace("/* unknown: /[^a-z0-9]+/g */", "_").replace("/* unknown: /^_+|_+$/g */", "").replace("/* unknown: /_+/g */", "_");
        if (toolName.startsWith(serverPrefix))
        {
            return toolName;
        }

        return $"{serverPrefix}_{toolName}";
    }
}