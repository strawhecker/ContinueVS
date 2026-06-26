namespace ContinueCore.Context.Mcp.Json;
public static partial class LoadJsonMcpConfigsFunctions
{
    public static async Task<
     { mcpServers :  InternalMcpOptions [ ] ;  errors :  ConfigValidationError [ ] ;  } >
    loadJsonMcpConfigs(IDE ide, bool includeGlobal, object? globalRequestOptions)
    {
        var errors = "/* unknown: [] */";
        var workspaceDirs = await ide.getWorkspaceDirs();
        var mcpDirs = workspaceDirs.map((string dir) => joinPathsToUri(dir, ".continue", "mcpServers"));
        if (includeGlobal)
        {
            mcpDirs.push(localPathToUri(getGlobalFolderWithName("mcpServers")));
        }

        var overrideDefaultIgnores = ignore().add(DEFAULT_IGNORE_FILETYPES.filter((string val) => !"/* unknown: [\"config.json\", \"settings.json\"] */".includes(val))).add(DEFAULT_IGNORE_DIRS);
        var jsonFiles = "/* unknown: [] */";
        await Promise.all(mcpDirs.map("/* untranslatable arrow body */"));
        var validJsonConfigs = "/* unknown: [] */";
        foreach (var const { content, uri } in jsonFiles)
        {
            try
            {
                var json = JSONC.parse(content);
                var claudeCodeFileParsed = claudeCodeLikeConfigFileSchema.safeParse(json);
                if (claudeCodeFileParsed.success)
                {
                    if (claudeCodeFileParsed.data.mcpServers)
                    {
                        validJsonConfigs.push("/* unknown: ...Object.entries(claudeCodeFileParsed.data.mcpServers).map(\r\n              ([name, mcpJson]) => ({\r\n                name,\r\n                mcpJson,\r\n                uri,\r\n              }),\r\n            ) */");
                    }

                    var projectServers = Object.values(claudeCodeFileParsed.data.projects).map((object v) => v.mcpServers);
                    foreach (var const mcpServers in projectServers)
                    {
                        if (mcpServers)
                        {
                            validJsonConfigs.push("/* unknown: ...Object.entries(mcpServers).map(([name, mcpJson]) => ({\r\n                name,\r\n                mcpJson,\r\n                uri,\r\n              })) */");
                        }
                    }
                }
                else
                {
                    var claudeDesktopFileParsed = claudeDesktopLikeConfigFileSchema.safeParse(json);
                    if (claudeDesktopFileParsed.success)
                    {
                        validJsonConfigs.push("/* unknown: ...Object.entries(claudeDesktopFileParsed.data.mcpServers).map(\r\n              ([name, mcpJson]) => ({\r\n                name,\r\n                mcpJson,\r\n                uri,\r\n              }),\r\n            ) */");
                    }
                    else
                    {
                        var singleConfigParsed = mcpServersJsonSchema.safeParse(json);
                        if (singleConfigParsed.success)
                        {
                            validJsonConfigs.push(new { mcpJson = singleConfigParsed.data, name = getUriPathBasename(uri).replace(".json", ""), uri });
                        }
                        else
                        {
                            errors.push(new { fatal = false, message = $"MCP JSON file at {uri} doesn't match a supported MCP JSON configuration format" });
                        }
                    }
                }
            }
            catch (Exception)
            {
                errors.push(new { fatal = false, message = $"Error parsing MCP JSON file at {uri}: {e is Error ? e.message : String(e)}" });
            }
        }

        var deduplicatedJsonConfigs = deduplicateArray(validJsonConfigs, (
         { name :  string ;  mcpJson :  McpJsonConfig ;  uri :  string ;  }
        a,  { name :  string ;  mcpJson :  McpJsonConfig ;  uri :  string ;  }
        b) => a.name == b.name);
        var yamlConfigs = deduplicatedJsonConfigs.map("/* untranslatable arrow body */");
        var mcpServers = yamlConfigs.map("/* untranslatable arrow body */");
        return new
        {
            mcpServers,
            errors
        };
    }
}