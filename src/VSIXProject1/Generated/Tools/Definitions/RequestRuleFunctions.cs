namespace ContinueCore.Tools.Definitions;
public static partial class RequestRuleFunctions
{
    public static string getAvailableRules(import ( "../.." ) . RuleWithSource [ ] rules)
    {
        var agentRequestedRules = rules.filter((import ( "../.." ) . RuleWithSource rule) => rule.alwaysApply == false && !rule.globs);
        if (agentRequestedRules.length == 0L)
        {
            return "No rules available.";
        }

        return agentRequestedRules.map((import ( "../.." ) . RuleWithSource rule) => $"{rule.name}: {rule.description}").join("\\n");
    }

    public static string getRequestRuleDescription(import ( "../.." ) . RuleWithSource [ ] rules)
    {
        var prefix = "Use this tool to retrieve additional 'rules' that contain more context/instructions based on their descriptions. Available rules:\\n";
        return prefix + getAvailableRules(rules);
    }

    public static string getRequestRuleSystemMessageDescription(import ( "../.." ) . RuleWithSource [ ] rules)
    {
        var prefix = $"To retrieve "rules" that contain more context/instructions based on their descriptions, use the {BuiltInToolNames.RequestRule} tool with the name of the rule. The available rules are:
";
        var availableRules = getAvailableRules(rules);
        var suffix = "\\n\\nFor example, you might respond with:";
        return prefix + availableRules + suffix;
    }
}