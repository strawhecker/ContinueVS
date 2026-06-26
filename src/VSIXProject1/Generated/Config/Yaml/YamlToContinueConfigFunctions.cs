namespace ContinueCore.Config.Yaml;
public static partial class YamlToContinueConfigFunctions
{
    public static RuleWithSource convertYamlRuleToContinueRule(Rule rule)
    {
        if (rule is string)
        {
            return new
            {
                rule = rule,
                source = "rules-block"
            };
        }
        else
        {
            return new
            {
                source = "rules-block",
                rule = rule.rule,
                globs = rule.globs,
                name = rule.name,
                description = rule.description,
                sourceFile = rule.sourceFile,
                alwaysApply = rule.alwaysApply,
                invokable = rule.invokable ?? false
            };
        }
    }

    public static InternalMcpOptions convertYamlMcpConfigToInternalMcpOptions(MCPServer config, object? globalRequestOptions)
    {
        var { connectionTimeout, faviconUrl, name, sourceFile } = config;
        var shared = new
        {
            id = name,
            name,
            faviconUrl = faviconUrl,
            timeout = connectionTimeout,
            sourceFile
        };
        if ("/* untranslatable binary op */")
        {
            var { args, command, cwd, env, type } = config;
            var stdioOptions = SpreadMerge.Merge(shared, new { type, command, args, cwd, env });
            return stdioOptions;
        }

        var { type, url, apiKey, requestOptions } = config;
        var httpSseConfig = SpreadMerge.Merge(shared, new { type, url, apiKey, requestOptions = mergeConfigYamlRequestOptions(requestOptions, globalRequestOptions) });
        return httpSseConfig;
    }
}