namespace ContinueCore.Commands.Slash.BuiltInLegacy;
public static partial class ReviewFunctions
{
    public static string getLastUserHistory(ChatMessage[] history)
    {
        var lastUserHistory = history.reverse().find((ChatMessage message) => message.role == "user");
        if (!lastUserHistory)
        {
            return "";
        }

        if (Array.isArray(lastUserHistory.content))
        {
            return lastUserHistory.content.reduce((string acc,  { type :  string ;  text ? :  string ;  }
            current) => current.type == "text" && current.text ? acc + current.text : acc, "");
        }

        return lastUserHistory.content is string ? lastUserHistory.content : "";
    }
}