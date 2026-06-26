namespace ContinueCore.Commands.Slash;
public static partial class RuleBlockSlashCommandFunctions
{
    public static SlashCommandWithSource convertRuleBlockToSlashCommand(RuleWithSource rule)
    {
        return new
        {
            name = rule.name || rule.rule.length > 20L ? rule.rule.substring(0L, 20L) + "..." : rule.rule,
            description = rule.description ?? "",
            prompt = rule.rule,
            source = "invokable-rule",
            sourceFile = rule.sourceFile,
            slug = rule.slug
        };
    }
}