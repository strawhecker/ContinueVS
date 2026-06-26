namespace ContinueCore.Config.Markdown;
public static partial class LoadCodebaseRulesFunctions
{
    public static async Task<
     { rules :  RuleWithSource [ ] ;  errors :  ConfigValidationError [ ] ;  } >
    loadCodebaseRules(IDE ide)
    {
        var errors = "/* unknown: [] */";
        var rules = "/* unknown: [] */";
        try
        {
            var allFiles = await walkDirs(ide);
            var rulesMdFiles = allFiles.filter("/* untranslatable arrow body */");
            foreach (var const filePath in rulesMdFiles)
            {
                try
                {
                    var content = await ide.readFile(filePath);
                    var { relativePathOrBasename, foundInDir, uri } = findUriInDirs(filePath, await ide.getWorkspaceDirs());
                    if (foundInDir)
                    {
                        var lastSlashIndex = relativePathOrBasename.lastIndexOf("/");
                        var parentDir = relativePathOrBasename.substring(0L, lastSlashIndex);
                        var rule = markdownToRule(content, new { uriType = "file", fileUri = uri }, parentDir);
                        rules.push(SpreadMerge.Merge(rule, new { source = "colocated-markdown", sourceFile = filePath }));
                    }
                    else
                    {
                        console.warn($"Failed to load codebase rule {uri}: URI not found in workspace dirs");
                    }
                }
                catch (Exception)
                {
                    errors.push(new { fatal = false, message = $"Failed to parse colocated rule file {filePath}: {e is Error ? e.message : e}" });
                }
            }
        }
        catch (Exception)
        {
            errors.push(new { fatal = false, message = $"Error loading colocated rule files: {e is Error ? e.message : e}" });
        }

        return new
        {
            rules,
            errors
        };
    }
}