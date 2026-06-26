namespace ContinueCore.Tools.SystemMessageTools;
public static partial class BuildToolsSystemMessageFunctions
{
    public static string addSystemMessageToolsToSystemMessage(SystemMessageToolsFramework framework, string baseSystemMessage, Tool[] systemMessageTools)
    {
        var systemMessage = baseSystemMessage;
        if (systemMessageTools.length > 0L)
        {
            var toolsSystemMessage = generateToolsSystemMessage(systemMessageTools, framework);
            systemMessage += $"

{toolsSystemMessage}";
        }

        return systemMessage;
    }
}