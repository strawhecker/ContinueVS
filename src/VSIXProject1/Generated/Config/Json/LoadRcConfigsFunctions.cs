namespace ContinueCore.Config.Json;
public static partial class LoadRcConfigsFunctions
{
    public static async Task<ContinueRcJson[]> getWorkspaceRcConfigs(IDE ide)
    {
        try
        {
            var workspaces = await ide.getWorkspaceDirs();
            var rcFiles = await Promise.all(workspaces.map("/* untranslatable arrow body */"));
            return rcFiles.flat().map((string file) => "/* unknown: JSONC.parse(file) as unknown as ContinueRcJson */");
        }
        catch (Exception)
        {
            console.debug("Failed to load workspace configs: ", e);
            return "/* unknown: [] */";
        }
    }
}