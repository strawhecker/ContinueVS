namespace ContinueCore.Util;
public static partial class MessageContentFunctions
{
    public static string stripImages(MessageContent messageContent)
    {
        if (messageContent is string)
        {
            return messageContent;
        }

        return messageContent.filter((MessagePart part) => part.type == "text").map((TextMessagePart part) => "/* unknown: part as TextMessagePart */".text).join("\\n");
    }

    public static string renderChatMessage(ChatMessage message)
    {
    }

    public static string renderContextItems(ContextItem[] contextItems)
    {
        return contextItems.map((ContextItem item) => item.content).join("\\n\\n");
    }

    public static string renderContextItemsWithStatus(object[] contextItems)
    {
        return contextItems.map("/* untranslatable arrow body */").join("\\n\\n");
    }

    public static MessagePart[] normalizeToMessageParts(ChatMessage message)
    {
    }
}