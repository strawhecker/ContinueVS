namespace ContinueCore.Llm;
public static partial class GetAdjustedTokenCountFunctions
{
    public static double getAdjustedTokenCountFromModel(double baseTokens, string modelName)
    {
        var multiplier = 1L;
        var lowerModelName = modelName.toLowerCase() ?? "";
        if (lowerModelName.includes("claude"))
        {
            multiplier = ANTHROPIC_TOKEN_MULTIPLIER;
        }

        return Math.ceil(baseTokens * multiplier);
    }
}