namespace ContinueCore.Util;
public static partial class TtsFunctions
{
    public static string sanitizeMessageForTTS(string message)
    {
        message = removeCodeBlocksAndTrim(message);
        message = message.replace("/* unknown: /\"/g */", "").replace("/* unknown: /`/g */", "").replace("/* unknown: /\\$/g */", "").replace("/* unknown: /\\\\/g */", "").replace("/* unknown: /[&|;()<>]/g */", "");
        message = message.trim().replace("/* unknown: /\\s+/g */", " ");
        return message;
    }
}