namespace ContinueCore.Commands.Slash.BuiltInLegacy;
public static partial class OnboardFunctions
{
    public static async Task<
     { uri :  string ;  type :  FileType ;  basename :  string ;  relativePath :  string ;  } [ ] >
    getEntriesFilteredByIgnore(string dir, IDE ide)
    {
        var ig = ignore().add(DEFAULT_IGNORE).add(getGlobalContinueIgArray());
        var entries = await ide.listDir(dir);
        var ignoreUri = joinPathsToUri(dir, ".gitignore");
        var fileExists = await ide.fileExists(ignoreUri);
        if (fileExists)
        {
            var gitIgnore = await ide.readFile(ignoreUri);
            var igPatterns = gitIgArrayFromFile(gitIgnore);
            ig.add(igPatterns);
        }

        var workspaceDirs = await ide.getWorkspaceDirs();
        var withRelativePaths = entries.filter(([string, FileType] entry) => "/* unknown: entry[1] */" == "/* unknown: 1 as FileType.File */" || "/* unknown: entry[1] */" == "/* unknown: 2 as FileType.Directory */").map("/* untranslatable arrow body */");
        return withRelativePaths.filter((
         { uri :  string ;  type :  FileType ;  basename :  string ;  relativePath :  string ;  }
        entry) => !ig.ignores(entry.relativePath));
    }

    public static async Task<string> gatherProjectContext(string workspaceDir, IDE ide)
    {
        var context = "";
        await exploreDirectory(workspaceDir);
        return context;
    }

    public static string createOnboardingPrompt(string context)
    {
        return $"
    As a helpful AI assistant, your task is to onboard a new developer to this project.
    Use the following context about the project structure, READMEs, and dependency files to create a comprehensive overview:

    {context}

    Please provide an overview of the project with the following guidelines:
    - Determine the most important folders in the project, at most 10
    - Go through each important folder step-by-step:
      - Explain what each folder does in isolation by summarzing the README or package.json file, if available
      - Mention the most popular or common packages used in that folder and their roles.
    - After covering individual folders, zoom out to explain at most 5 high-level insights about the project's architecture:
      - How different parts of the codebase fit together.
      - The overall project architecture or design patterns evident from the folder structure and dependencies.
    - Provide at most 5 additional insights on the project's architecture that weren't covered in the folder-by-folder breakdown.

    Your response should be structured, clear, and focused on giving the new developer both a detailed understanding of individual components and a high-level overview of the project as a whole.

    Here is an example of a valid response:

    ## Important folders

    ### /folder1
    - Description: Contains the main application logic.
    - Key packages: Express.js for routing, Mongoose for database operations.

    #### /folder1/folder2

    ## Project Architecture
    - The frontend is built using React and Redux for state management.
    - The backend is a Node.js application using Express.js for routing and Mongoose for database operations.
    - The application follows a Model-View-Controller (MVC) architecture.

    ## Additional Insights
    - The project is using a monorepo structure.
    - The project uses TypeScript for type checking.
  ";
    }
}