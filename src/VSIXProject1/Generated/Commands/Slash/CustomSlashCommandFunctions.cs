namespace ContinueCore.Commands.Slash;
public static partial class CustomSlashCommandFunctions
{
    public static SlashCommandWithSource convertCustomCommandToSlashCommand(CustomCommand customCommand)
    {
        var commandName = customCommand.name.startsWith("/") ? customCommand.name.substring(1L) : customCommand.name;
        return new
        {
            name = commandName,
            description = customCommand.description ?? "",
            prompt = customCommand.prompt,
            source = "json-custom-command",
            sourceFile = customCommand.sourceFile
        };
    }
}