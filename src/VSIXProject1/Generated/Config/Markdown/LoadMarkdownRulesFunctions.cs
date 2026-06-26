namespace ContinueCore.Config.Markdown;
public static partial class LoadMarkdownRulesFunctions
{
    public static async Task<
     { rules :  RuleWithSource [ ] ;  errors :  ConfigValidationError [ ] ;  } >
    loadMarkdownRules(IDE ide)
    {
        var errors = "/* unknown: [] */";
        var rules = "/* unknown: [] */";
        var workspaceDirs = await ide.getWorkspaceDirs();
        foreach (var const workspaceDir in workspaceDirs)
        {
            var agentFileFound = false;
            foreach (var const fileName in SUPPORTED_AGENT_FILES)
            {
                try
                {
                    var agentFileUri = joinPathsToUri(workspaceDir, fileName);
                    var exists = await ide.fileExists(agentFileUri);
                    if (exists)
                    {
                        var agentContent = await ide.readFile(agentFileUri);
                        var rule = markdownToRule(agentContent, new { uriType = "file", fileUri = agentFileUri });
                        rules.push(SpreadMerge.Merge(rule, new { source = "agentFile", sourceFile = agentFileUri, alwaysApply = true }));
                        agentFileFound = true;
                    }
                }
            }

            if (agentFileFound)
            {
            }
        }

        var dirsToCheck = "/* unknown: [RULES_DIR_NAME, PROMPTS_DIR_NAME] */";
        foreach (var const dirName in dirsToCheck)
        {
            try
            {
                var markdownFiles = await getAllDotContinueDefinitionFiles(ide, new { includeGlobal = true, includeWorkspace = true, fileExtType = "markdown" }, dirName);
                var mdFiles = markdownFiles.filter((
                 { path :  string ;  content :  string ;  }
                file) => file.path.endsWith(".md"));
                foreach (var const file in mdFiles)
                {
                    try
                    {
                        var rule = markdownToRule(file.content, new { uriType = "file", fileUri = file.path });
                        if (!rule.invokable)
                        {
                            rules.push(SpreadMerge.Merge(rule, new { source = "rules-block", sourceFile = file.path }));
                        }
                    }
                    catch (Exception)
                    {
                        errors.push(new { fatal = false, message = $"Failed to parse markdown rule file {file.path}: {e is Error ? e.message : e}" });
                    }
                }
            }
            catch (Exception)
            {
                errors.push(new { fatal = false, message = $"Error loading markdown rule files from {dirName}: {e is Error ? e.message : e}" });
            }
        }

        return new
        {
            rules,
            errors
        };
    }
}