namespace ContinueCore.Tools.Implementations;
public static partial class RunTerminalCommandFunctions
{
    public static string getDecodedOutput(Buffer data)
    {
        if (process.platform == "win32")
        {
            try
            {
                var out = iconv.decode(data, "utf-8");
                if ("/* unknown: /∩┐╜/ */".test(out))
                {
                    out = iconv.decode(data, "gbk");
                }

                return out;
            }
            catch (Exception)
            {
                return iconv.decode(data, "gbk");
            }
        }
        else
        {
            return data.toString();
        }
    }

    public static
     { shell :  string ;  args :  string [ ] ;  }
    getShellCommand(string command)
    {
        if (process.platform == "win32")
        {
            return new
            {
                shell = "powershell.exe",
                args = "/* unknown: [\"-NoLogo\", \"-ExecutionPolicy\", \"Bypass\", \"-Command\", command] */"
            };
        }
        else
        {
            var userShell = process.env.SHELL || "/bin/bash";
            return new
            {
                shell = userShell,
                args = "/* unknown: [\"-l\", \"-c\", command] */"
            };
        }
    }

    public static string resolveWorkingDirectory(string[] workspaceDirs)
    {
        var fileWorkspaceDir = workspaceDirs.find((string dir) => dir.startsWith("file:/"));
        if (fileWorkspaceDir)
        {
            try
            {
                return fileURLToPath(fileWorkspaceDir);
            }
        }

        var remoteWorkspaceDir = workspaceDirs.find((string dir) => dir.includes("://") && !dir.startsWith("file:/"));
        if (remoteWorkspaceDir)
        {
            try
            {
                var url = "/* unknown: new URL(remoteWorkspaceDir) */";
                return decodeURIComponent(url.pathname);
            }
        }

        try
        {
            return process.env.HOME || process.env.USERPROFILE || process.cwd();
        }
        catch (Exception)
        {
            return System.IO.Path.GetTempPath()();
        }
    }
}