namespace ContinueCore.Commands.Slash;
public static partial class PromptBlockSlashCommandFunctions
{
    public static SlashCommandWithSource convertPromptBlockToSlashCommand(Prompt prompt)
    {
        return new
        {
            name = prompt.name,
            description = prompt.description ?? "",
            prompt = prompt.prompt,
            source = "yaml-prompt-block",
            sourceFile = prompt.sourceFile
        };
    }
}