namespace ContinueCore.ContinueServer.Stubs;
public static partial class HeadersFunctions
{
    public static async Task<
     { key :  string ;  timestamp :  string ;  v :  string ;  extensionVersion :  string ;  os :  string ;  uniqueId :  string ;  } >
    getHeaders()
    {
        return new
        {
            key = constants.c,
            timestamp = getTimestamp(),
            v = "1",
            extensionVersion = IdeInfoService.ideInfo.extensionVersion ?? "0.0.0",
            os = IdeInfoService.os ?? "Unknown",
            uniqueId = IdeInfoService.uniqueId ?? "None"
        };
    }
}