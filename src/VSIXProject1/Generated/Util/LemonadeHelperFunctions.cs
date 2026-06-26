namespace ContinueCore.Util;
public static partial class LemonadeHelperFunctions
{
    public static async Task<bool> isLemonadeInstalled()
    {
        if (process.platform == "win32")
        {
            return "/* unknown: new Promise((resolve, _reject) => {\r\n      exec(\"where.exe lemonade-server\", (error, _stdout, _stderr) => {\r\n        resolve(!error);\r\n      });\r\n    }) */";
        }

        try
        {
            var response = await fetch("http://localhost:8000/api/v1/health", new { method = "GET", signal = AbortSignal.timeout(3000L) });
            if (response.ok)
            {
                var data = await response.json();
                return data.status == "ok";
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<object> startLocalLemonade(IDE ide)
    {
        var startCommand;
        if (startCommand)
        {
            return ide.runCommand(startCommand, new { reuseTerminal = true, terminalName = "Start Lemonade" });
        }
    }
}