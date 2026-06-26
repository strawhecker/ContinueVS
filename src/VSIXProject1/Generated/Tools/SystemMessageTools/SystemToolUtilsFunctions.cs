namespace ContinueCore.Tools.SystemMessageTools;
public static partial class SystemToolUtilsFunctions
{
    public static string closeTag(string openingTag)
    {
        return $"</{openingTag.slice(1L)}";
    }

    public static string[] splitAtCodeblocksAndNewLines(string content)
    {
        return content.split("/* unknown: /(```|\\n)/g */").filter(Boolean);
    }

    public static string randomLettersAndNumbers(double length)
    {
        var characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var result = "";
        for (var i = 0; i < length; "/* unknown: i++ */")
        {
            result += characters.charAt(Math.floor(Math.random() * characters.length));
        }

        return result;
    }

    public static string generateOpenAIToolCallId()
    {
        return $"call_{randomLettersAndNumbers(24L)}";
    }

    public static ToolCallDelta createDelta(string name, string args, string id)
    {
        return new
        {
            type = "function",
            function = new
            {
                name,
                arguments = args
            },
            id
        };
    }
}