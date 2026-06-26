namespace ContinueCore.Util;
public static partial class PathToUriFunctions
{
    public static object localPathToUri(string path)
    {
        if (path.startsWith("file://"))
        {
            console.warn("localPathToUri: path already starts with file://");
            return path;
        }

        var url = pathToFileURL(path);
        return URI.normalize(url.toString());
    }

    public static string localPathOrUriToPath(string localPathOrUri)
    {
        try
        {
            return fileURLToPath(localPathOrUri);
        }
        catch (Exception)
        {
            return localPathOrUri;
        }
    }
}