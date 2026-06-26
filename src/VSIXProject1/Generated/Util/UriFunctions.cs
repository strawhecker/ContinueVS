namespace ContinueCore.Util;
public static partial class UriFunctions
{
    public static string pathToUriPathSegment(string path)
    {
        var clean = path.replace("/* unknown: /[\\\\]/g */", "/");
        clean = clean.replace("/* unknown: /^\\// */", "");
        clean = clean.replace("/* unknown: /\\/$/ */", "");
        return clean.split("/").map((string part) => encodeURIComponent(part)).join("/");
    }

    public static object getCleanUriPath(string uri)
    {
        var path = URI.parse(uri).path ?? "";
        var clean = path.replace("/* unknown: /^\\// */", "");
        clean = clean.replace("/* unknown: /\\/$/ */", "");
        return clean;
    }

    public static
     { uri :  string ;  relativePathOrBasename :  string ;  foundInDir :  string  | null ;  }
    findUriInDirs(string uri, string[] dirUriCandidates)
    {
        var uriComps = URI.parse(uri);
        if (!uriComps.scheme)
        {
            throw "/* unknown: new Error(`Invalid uri: ${uri}`) */";
        }

        var uriPathParts = getCleanUriPath(uri).split("/");
        foreach (var const dir in dirUriCandidates)
        {
            var dirComps = URI.parse(dir);
            if (!dirComps.scheme)
            {
                throw "/* unknown: new Error(`Invalid uri: ${dir}`) */";
            }

            if (uriComps.scheme != dirComps.scheme)
            {
            }

            var dirPathParts = getCleanUriPath(dir).split("/");
            if (uriPathParts.length < dirPathParts.length)
            {
            }

            var allDirPartsMatch = true;
            for (var i = 0; i < dirPathParts.length; "/* unknown: i++ */")
            {
                if ("/* unknown: dirPathParts[i] */" != "/* unknown: uriPathParts[i] */")
                {
                    allDirPartsMatch = false;
                }
            }

            if (allDirPartsMatch)
            {
                var relativePath = uriPathParts.slice(dirPathParts.length).map(decodeURIComponent).join("/");
                return new
                {
                    uri,
                    relativePathOrBasename = relativePath,
                    foundInDir = dir
                };
            }
        }

        return new
        {
            uri,
            relativePathOrBasename = getUriPathBasename(uri),
            foundInDir = null
        };
    }

    public static string getUriPathBasename(string uri)
    {
        var path = getCleanUriPath(uri);
        var basename = path.split("/").pop() || "";
        return decodeURIComponent(basename);
    }

    public static string getFileExtensionFromBasename(string basename)
    {
        var parts = basename.split(".");
        if (parts.length < 2L)
        {
            return "";
        }

        return "/* unknown: parts.slice(-1)[0] */" ?? "".toLowerCase();
    }

    public static string getUriFileExtension(string uri)
    {
        var baseName = getUriPathBasename(uri);
        return getFileExtensionFromBasename(baseName);
    }

    public static string getLastNUriRelativePathParts(string[] dirUriCandidates, string uri, double n)
    {
        var { relativePathOrBasename } = findUriInDirs(uri, dirUriCandidates);
        return getLastNPathParts(relativePathOrBasename, n);
    }

    public static object joinPathsToUri(string uri, params string[]? pathSegments)
    {
        var baseUri = uri;
        if (baseUri.at(-1L) != "/")
        {
            baseUri += "/";
        }

        var segments = pathSegments.map((string segment) => pathToUriPathSegment(segment));
        return URI.resolve(baseUri, segments.join("/"));
    }

    public static object joinEncodedUriPathSegmentToUri(string uri, string pathSegment)
    {
        var baseUri = uri;
        if (baseUri.at(-1L) != "/")
        {
            baseUri += "/";
        }

        return URI.resolve(baseUri, pathSegment);
    }

    public static
     { uri :  string ;  uniquePath :  string ;  } [ ] 
    getShortestUniqueRelativeUriPaths(string[] uris, string[] dirUriCandidates)
    {
        var segmentCombinationsMap = "/* unknown: new Map<string, number>() */";
        var segmentsInfo = uris.map("/* untranslatable arrow body */");
        return segmentsInfo.map("/* untranslatable arrow body */");
    }

    public static string getLastNPathParts(string filepath, double n)
    {
        if (n <= 0L)
        {
            return "";
        }

        return filepath.split("/* unknown: /[\\\\/]/ */").slice(-n).join("/");
    }

    public static
     { uri :  string ;  relativePathOrBasename :  string ;  foundInDir :  string  | null ;  last2Parts :  string ;  baseName :  string ;  extension :  string ;  }
    getUriDescription(string uri, string[] dirUriCandidates)
    {
        var { relativePathOrBasename, foundInDir } = findUriInDirs(uri, dirUriCandidates);
        var baseName = getUriPathBasename(uri);
        var extension = getFileExtensionFromBasename(baseName);
        var last2Parts = getLastNUriRelativePathParts(dirUriCandidates, uri, 2L);
        return new
        {
            uri,
            relativePathOrBasename,
            foundInDir,
            last2Parts,
            baseName,
            extension
        };
    }
}