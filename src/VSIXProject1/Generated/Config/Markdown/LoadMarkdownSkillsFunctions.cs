namespace ContinueCore.Config.Markdown;
public static partial class LoadMarkdownSkillsFunctions
{
    public static async Task<string[]> getClaudeSkillsDir(IDE ide)
    {
        var fullDirs = await ide.getWorkspaceDirs().map((string dir) => joinPathsToUri(dir, ".claude", SKILLS_DIR));
        fullDirs.push(localPathToUri(getGlobalFolderWithName(SKILLS_DIR)));
        return await Promise.all(fullDirs.map("/* untranslatable arrow body */")).flat();
    }

    public static async Task<
     { skills :  Skill [ ] ;  errors :  ConfigValidationError [ ] ;  } >
    loadMarkdownSkills(IDE ide)
    {
        var errors = "/* unknown: [] */";
        var skills = "/* unknown: [] */";
        try
        {
            var yamlAndMarkdownFileUris = "/* unknown: [\r\n      ...(\r\n        await getAllDotContinueDefinitionFiles(\r\n          ide,\r\n          {\r\n            includeGlobal: true,\r\n            includeWorkspace: true,\r\n            fileExtType: \"markdown\",\r\n          },\r\n          SKILLS_DIR,\r\n        )\r\n      ).map((file) => file.path),\r\n      ...(await getClaudeSkillsDir(ide)),\r\n    ] */";
            var skillFiles = yamlAndMarkdownFileUris.filter((string path) => path.endsWith("SKILL.md"));
            var workspaceDirs = await ide.getWorkspaceDirs();
            foreach (var const fileUri in skillFiles)
            {
                try
                {
                    var content = await ide.readFile(fileUri);
                    var { frontmatter, markdown } = "/* unknown: parseMarkdownRule(\r\n          content,\r\n        ) as unknown as { frontmatter: Skill; markdown: string } */";
                    var validatedFrontmatter = skillFrontmatterSchema.parse(frontmatter);
                    var filesInSkillsDirectory = await walkDir(fileUri.substring(0L, fileUri.lastIndexOf("/")), ide, new { source = "get skill files" }).filter((string file) => !file.endsWith("SKILL.md"));
                    var foundRelativeUri = findUriInDirs(fileUri, workspaceDirs);
                    skills.push(SpreadMerge.Merge(validatedFrontmatter, new { content = markdown, path = foundRelativeUri.foundInDir ? foundRelativeUri.relativePathOrBasename : fileUri, files = filesInSkillsDirectory }));
                }
                catch (Exception)
                {
                    errors.push(new { fatal = false, message = $"Failed to parse markdown skill file: {error is Error ? error.message : error}" });
                }
            }
        }
        catch (Exception)
        {
            errors.push(new { fatal = false, message = $"Error loading markdown skill files: {err is Error ? err.message : err}" });
        }

        return new
        {
            skills,
            errors
        };
    }
}