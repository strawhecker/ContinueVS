namespace ContinueCore.Llm.Rules;
public static partial class RulesUtilsFunctions
{
    public static string getRuleDisplayName(RuleMetadata rule)
    {
        if (rule.name)
        {
            return rule.name;
        }

        return getRuleSourceDisplayName(rule);
    }

    public static string getRuleSourceDisplayName(RuleMetadata rule)
    {
    }
}