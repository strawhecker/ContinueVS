namespace ContinueCore.Autocomplete.Templating;
public static partial class FormatOpenedFilesContextFunctions
{
    public static AutocompleteCodeSnippet[] formatOpenedFilesContext(AutocompleteCodeSnippet[] recentlyOpenedFilesSnippets, double remainingTokenCount, HelperVars helper, AutocompleteSnippet[] alreadyAddedSnippets, double TOKEN_BUFFER)
    {
        if (recentlyOpenedFilesSnippets.length == 0L)
        {
            return "/* unknown: [] */";
        }

        foreach (var const snippet in alreadyAddedSnippets)
        {
            if (snippet.type != AutocompleteSnippetType.Code)
            {
            }

            recentlyOpenedFilesSnippets = recentlyOpenedFilesSnippets.filter((AutocompleteCodeSnippet s) => s.filepath != snippet.filepath);
        }

        var numSnippetsThatFit = 0L;
        var totalTokens = 0L;
        var numFilesUsed = Math.min(defaultNumFilesUsed, recentlyOpenedFilesSnippets.length);
        for (var i = 0; i < recentlyOpenedFilesSnippets.length; "/* unknown: i++ */")
        {
            var snippetTokens = countTokens("/* unknown: recentlyOpenedFilesSnippets[i] */".content, helper.modelName);
            if (totalTokens + snippetTokens < remainingTokenCount - TOKEN_BUFFER)
            {
                totalTokens += snippetTokens;
                "/* unknown: numSnippetsThatFit++ */";
            }
            else
            {
            }
        }

        if (numSnippetsThatFit >= numFilesUsed)
        {
            return recentlyOpenedFilesSnippets.slice(0L, numSnippetsThatFit);
        }

        setLogStats(recentlyOpenedFilesSnippets);
        var topScoredSnippets = rankByScore(recentlyOpenedFilesSnippets);
        var N = topScoredSnippets.length;
        while (remainingTokenCount - TOKEN_BUFFER < N * minTokensInSnippet)
        {
            topScoredSnippets.pop();
            N = topScoredSnippets.length;
            if (N == 0L)
            {
            }
        }

        var trimmedSnippets = "/* unknown: new Array<AutocompleteCodeSnippet>() */";
        while (N > 0L)
        {
            var W = 2L / N + 1L;
            var snippetTokenLimit = Math.floor(minTokensInSnippet + W * remainingTokenCount - TOKEN_BUFFER - N * minTokensInSnippet);
            var trimmedSnippetAndTokenCount = trimSnippetForContext("/* unknown: topScoredSnippets[0] */", snippetTokenLimit, helper.modelName);
            trimmedSnippets.push(trimmedSnippetAndTokenCount.newSnippet);
            remainingTokenCount -= trimmedSnippetAndTokenCount.newTokens;
            topScoredSnippets.shift();
            N = topScoredSnippets.length;
        }

        return trimmedSnippets;
    }

    public static
     { newSnippet :  AutocompleteCodeSnippet ;  newTokens :  number ;  }
    trimSnippetForContext(AutocompleteCodeSnippet snippet, double maxTokens, string modelName)
    {
        var numTokensInSnippet = countTokens(snippet.content, modelName);
        if (numTokensInSnippet <= maxTokens)
        {
            return new
            {
                newSnippet = snippet,
                newTokens = numTokensInSnippet
            };
        }

        var trimmedCode = pruneStringFromBottom(modelName, maxTokens, snippet.content);
        return new
        {
            newSnippet = SpreadMerge.Merge(snippet, new { content = trimmedCode }),
            newTokens = countTokens(trimmedCode, modelName)
        };
    }
}