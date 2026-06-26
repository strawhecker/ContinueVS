namespace ContinueCore.Config;
public static partial class GetWorkspaceContinueRuleDotFilesFunctions
{
    public static async Task<
     { rules :  RuleWithSource [ ] ;  errors :  ConfigValidationError [ ] ;  } >
    getWorkspaceContinueRuleDotFiles(IDE ide)
    {
        var dirs = await ide.getWorkspaceDirs();
        var errors = "/* unknown: [] */";
        var rules = "/* unknown: [] */";
        foreach (var const dir in dirs)
        {
            try
            {
                var dotFile = joinPathsToUri(dir, SYSTEM_PROMPT_DOT_FILE);
                var exists = await ide.fileExists(dotFile);
                if (exists)
                {
                    var content = await ide.readFile(dotFile);
                    rules.push(new { rule = content, sourceFile = dotFile, source = ".continuerules" });
                }
            }
            catch (Exception)
            {
                errors.push(new { fatal = false, message = $"Failed to load system prompt dot file from workspace {dir}: {e is Error ? e.message : e}" });
            }
        }

        return new
        {
            rules,
            errors
        };
    }
}