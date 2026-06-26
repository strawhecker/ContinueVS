namespace ContinueCore.Commands.Slash;
public static partial class PromptFileSlashCommandFunctions
{
    public static SlashCommandWithSource? slashCommandFromPromptFile(string path, string content)
    {
        var { name, description, systemMessage, prompt, version } = parsePromptFile(path, content);
        return new
        {
            name,
            description,
            prompt,
            source = version == 1L ? "prompt-file-v1" : "prompt-file-v2",
            sourceFile = path,
            overrideSystemMessage = systemMessage
        };
    }
}