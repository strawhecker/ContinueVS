namespace ContinueCore.Llm.Rules;
public static partial class GetSystemMessageWithRulesFunctions
{
    public static string getRuleId(RuleMetadata rule)
    {
        return rule.slug ?? rule.sourceFile ?? rule.name ?? rule.source;
    }
}