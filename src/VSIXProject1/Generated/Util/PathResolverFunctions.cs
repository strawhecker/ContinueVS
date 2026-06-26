namespace ContinueCore.Util;
public static partial class PathResolverFunctions
{
    public static async Task<bool> isUriWithinWorkspace(IDE ide, string uri)
    {
        var workspaceDirs = await ide.getWorkspaceDirs();
        var { foundInDir } = findUriInDirs(uri, workspaceDirs);
        if (foundInDir != null)
        {
            return await ide.fileExists(uri);
        }

        return false;
    }

    public static async Task<ResolvedPath?> resolveInputPath(IDE ide, string inputPath)
    {
        var trimmedPath = inputPath.trim();
        if (trimmedPath.startsWith("file://"))
        {
            var displayPath = fileURLToPath(trimmedPath);
            var isWithinWorkspace = await isUriWithinWorkspace(ide, trimmedPath);
            return new
            {
                uri = trimmedPath,
                displayPath,
                isAbsolute = true,
                isWithinWorkspace
            };
        }

        var expandedPath = untildify(trimmedPath);
        var isAbsolute = path.isAbsolute(expandedPath) || expandedPath.startsWith("\\\\\\\\") || "/* unknown: /^[a-zA-Z]:/ */".test(expandedPath);
        if (isAbsolute)
        {
            var uri = pathToFileURL(expandedPath).href;
            var isWithinWorkspace = await isUriWithinWorkspace(ide, uri);
            return new
            {
                uri,
                displayPath = expandedPath,
                isAbsolute = true,
                isWithinWorkspace
            };
        }

        var workspaceUri = await resolveRelativePathInDir(expandedPath, ide);
        if (workspaceUri)
        {
            return new
            {
                uri = workspaceUri,
                displayPath = expandedPath,
                isAbsolute = false,
                isWithinWorkspace = true
            };
        }

        return null;
    }
}