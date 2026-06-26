namespace ContinueCore.Llm.Utils;
public static partial class CalculateRequestCostFunctions
{
    public static CostBreakdown? calculateAnthropicCost(string model, Usage usage)
    {
        var normalizedModel = model.toLowerCase();
        var pricing = new
        {
            "claude-sonnet-4-6" = new
            {
                input = 3L,
                output = 15L,
                cacheWrite = 3.75,
                cacheRead = 0.3
            },
            "claude-opus-4-6" = new
            {
                input = 5L,
                output = 25L,
                cacheWrite = 6.25,
                cacheRead = 0.5
            },
            "claude-opus-4-5" = new
            {
                input = 5L,
                output = 25L,
                cacheWrite = 6.25,
                cacheRead = 0.5
            },
            "claude-3-opus" = new
            {
                input = 15L,
                output = 75L,
                cacheWrite = 18.75,
                cacheRead = 1.5
            },
            "claude-3-5-sonnet" = new
            {
                input = 3L,
                output = 15L,
                cacheWrite = 3.75,
                cacheRead = 0.3
            },
            "claude-3-5-haiku" = new
            {
                input = 0.8,
                output = 4L,
                cacheWrite = 1L,
                cacheRead = 0.08
            },
            "claude-3-haiku" = new
            {
                input = 0.25,
                output = 1.25,
                cacheWrite = 0.3,
                cacheRead = 0.03
            }
        };
        var sortedKeys = Object.keys(pricing).sort((string a, string b) => b.length - a.length);
        var modelPricing = null;
        foreach (var const prefix in sortedKeys)
        {
            if (normalizedModel.startsWith(prefix))
            {
                modelPricing = "/* unknown: pricing[prefix] */";
            }
        }

        if (!modelPricing)
        {
            return null;
        }

        var inputCost = usage.promptTokens / 1_000_000 * modelPricing.input;
        var outputCost = usage.completionTokens / 1_000_000 * modelPricing.output;
        var breakdownParts = "/* unknown: [] */";
        if (usage.promptTokens > 0L)
        {
            breakdownParts.push($"Input: {usage.promptTokens.toLocaleString()} tokens ├ù ${modelPricing.input}/MTok = ${inputCost.toFixed(6L)}");
        }

        if (usage.completionTokens > 0L)
        {
            breakdownParts.push($"Output: {usage.completionTokens.toLocaleString()} tokens ├ù ${modelPricing.output}/MTok = ${outputCost.toFixed(6L)}");
        }

        var cacheCost = 0L;
        if (usage.promptTokensDetails)
        {
            var { cachedTokens, cacheWriteTokens } = usage.promptTokensDetails;
            if (cacheWriteTokens && cacheWriteTokens > 0L)
            {
                var cacheWriteCost = cacheWriteTokens / 1_000_000 * modelPricing.cacheWrite;
                cacheCost += cacheWriteCost;
                breakdownParts.push($"Cache Write: {cacheWriteTokens.toLocaleString()} tokens ├ù ${modelPricing.cacheWrite}/MTok = ${cacheWriteCost.toFixed(6L)}");
            }

            if (cachedTokens && cachedTokens > 0L)
            {
                var cacheReadCost = cachedTokens / 1_000_000 * modelPricing.cacheRead;
                cacheCost += cacheReadCost;
                breakdownParts.push($"Cache Read: {cachedTokens.toLocaleString()} tokens ├ù ${modelPricing.cacheRead}/MTok = ${cacheReadCost.toFixed(6L)}");
            }
        }

        var totalCost = inputCost + outputCost + cacheCost;
        var breakdown = $"Model: {model}
";
        breakdown += breakdownParts.join("\\n");
        if (breakdownParts.length > 1L)
        {
            breakdown += $"
Total: ${totalCost.toFixed(6L)}";
        }

        return new
        {
            cost = totalCost,
            breakdown
        };
    }

    public static CostBreakdown? calculateOpenAICost(string model, Usage usage)
    {
        var normalizedModel = model.toLowerCase();
        var pricing = new
        {
            "gpt-4o-mini" = new
            {
                input = 0.15,
                output = 0.6
            },
            "gpt-4o" = new
            {
                input = 2.5,
                output = 10L
            },
            "gpt-4-turbo" = new
            {
                input = 10L,
                output = 30L
            },
            "gpt-3.5-turbo-0125" = new
            {
                input = 0.5,
                output = 1.5
            },
            "gpt-3.5-turbo-1106" = new
            {
                input = 1L,
                output = 2L
            },
            "gpt-3.5-turbo" = new
            {
                input = 1.5,
                output = 2L
            },
            "gpt-4" = new
            {
                input = 30L,
                output = 60L
            }
        };
        var sortedKeys = Object.keys(pricing).sort((string a, string b) => b.length - a.length);
        var modelPricing = null;
        foreach (var const prefix in sortedKeys)
        {
            if (normalizedModel.startsWith(prefix))
            {
                modelPricing = "/* unknown: pricing[prefix] */";
            }
        }

        if (!modelPricing)
        {
            return null;
        }

        var inputCost = usage.promptTokens / 1_000_000 * modelPricing.input;
        var outputCost = usage.completionTokens / 1_000_000 * modelPricing.output;
        var breakdownParts = "/* unknown: [] */";
        if (usage.promptTokens > 0L)
        {
            breakdownParts.push($"Input: {usage.promptTokens.toLocaleString()} tokens ├ù ${modelPricing.input}/MTok = ${inputCost.toFixed(6L)}");
        }

        if (usage.completionTokens > 0L)
        {
            breakdownParts.push($"Output: {usage.completionTokens.toLocaleString()} tokens ├ù ${modelPricing.output}/MTok = ${outputCost.toFixed(6L)}");
        }

        var totalCost = inputCost + outputCost;
        var breakdown = $"Model: {model}
";
        breakdown += breakdownParts.join("\\n");
        if (breakdownParts.length > 1L)
        {
            breakdown += $"
Total: ${totalCost.toFixed(6L)}";
        }

        return new
        {
            cost = totalCost,
            breakdown
        };
    }

    public static CostBreakdown? calculateRequestCost(string provider, string model, Usage usage)
    {
    }
}