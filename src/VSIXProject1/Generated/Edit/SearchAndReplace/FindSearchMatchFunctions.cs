namespace ContinueCore.Edit.SearchAndReplace;
public static partial class FindSearchMatchFunctions
{
    public static BasicMatchResult? exactMatch(string fileContent, string searchContent)
    {
        var exactIndex = fileContent.indexOf(searchContent);
        if (exactIndex != -1L)
        {
            return new
            {
                startIndex = exactIndex,
                endIndex = exactIndex + searchContent.length
            };
        }

        return null;
    }

    public static BasicMatchResult? trimmedMatch(string fileContent, string searchContent)
    {
        var trimmedSearchContent = searchContent.trim();
        var trimmedIndex = fileContent.indexOf(trimmedSearchContent);
        if (trimmedIndex != -1L)
        {
            return new
            {
                startIndex = trimmedIndex,
                endIndex = trimmedIndex + trimmedSearchContent.length
            };
        }

        return null;
    }

    public static BasicMatchResult? caseInsensitiveMatch(string fileContent, string searchContent)
    {
        var lowerFileContent = fileContent.toLowerCase();
        var lowerSearchContent = searchContent.toLowerCase();
        var index = lowerFileContent.indexOf(lowerSearchContent);
        if (index != -1L)
        {
            return new
            {
                startIndex = index,
                endIndex = index + searchContent.length
            };
        }

        return null;
    }

    public static BasicMatchResult? whitespaceIgnoredMatch(string fileContent, string searchContent)
    {
        var strippedFileContent = fileContent.replace("/* unknown: /\\s/g */", "");
        var strippedSearchContent = searchContent.replace("/* unknown: /\\s/g */", "");
        if (strippedSearchContent == "")
        {
            return null;
        }

        var strippedIndex = strippedFileContent.indexOf(strippedSearchContent);
        if (strippedIndex == -1L)
        {
            return null;
        }

        var originalStartIndex = -1L;
        var strippedCharCount = 0L;
        for (var i = 0; i < fileContent.length; "/* unknown: i++ */")
        {
            if (!"/* unknown: /\\s/ */".test("/* unknown: fileContent[i] */"))
            {
                if (strippedCharCount == strippedIndex)
                {
                    originalStartIndex = i;
                }

                "/* unknown: strippedCharCount++ */";
            }
        }

        if (originalStartIndex == -1L)
        {
            return null;
        }

        var originalEndIndex = originalStartIndex;
        var matchedNonWhitespaceChars = 0L;
        for (var i = originalStartIndex; i < fileContent.length; "/* unknown: i++ */")
        {
            if (!"/* unknown: /\\s/ */".test("/* unknown: fileContent[i] */"))
            {
                "/* unknown: matchedNonWhitespaceChars++ */";
                if (matchedNonWhitespaceChars == strippedSearchContent.length)
                {
                    originalEndIndex = i + 1L;
                }
            }

            originalEndIndex = i + 1L;
        }

        return new
        {
            startIndex = originalStartIndex,
            endIndex = originalEndIndex
        };
    }

    public static double jaroSimilarity(string s1, string s2)
    {
        if (s1 == s2)
        {
        }

        if (s1.length == 0L || s2.length == 0L)
        {
        }

        var matchDistance = Math.floor(Math.max(s1.length, s2.length) / 2L) - 1L;
        if (matchDistance < 0L)
        {
        }

        var s1Matches = "/* unknown: new Array(s1.length) */".fill(false);
        var s2Matches = "/* unknown: new Array(s2.length) */".fill(false);
        var matches = 0L;
        var transpositions = 0L;
        for (var i = 0; i < s1.length; "/* unknown: i++ */")
        {
            var start = Math.max(0L, i - matchDistance);
            var end = Math.min(i + matchDistance + 1L, s2.length);
            for (var j = start; j < end; "/* unknown: j++ */")
            {
                if ("/* unknown: s2Matches[j] */" || "/* unknown: s1[i] */" != "/* unknown: s2[j] */")
                {
                }

                "/* unknown: s1Matches[i] */" = true;
                "/* unknown: s2Matches[j] */" = true;
                "/* unknown: matches++ */";
            }
        }

        if (matches == 0L)
        {
        }

        var k = 0L;
        for (var i = 0; i < s1.length; "/* unknown: i++ */")
        {
            if (!"/* unknown: s1Matches[i] */")
            {
            }

            while (!"/* unknown: s2Matches[k] */")
            {
            }

            if ("/* unknown: s1[i] */" != "/* unknown: s2[k] */")
            {
            }

            "/* unknown: k++ */";
        }

        return matches / s1.length + matches / s2.length + matches - transpositions / 2L / matches / 3;
    }

    public static double jaroWinklerSimilarity(string s1, string s2, double? prefixScale)
    {
        var jaroSim = jaroSimilarity(s1, s2);
        if (jaroSim < 0.7)
        {
        }

        var prefixLength = 0L;
        var maxPrefix = Math.min(4L, Math.min(s1.length, s2.length));
        for (var i = 0; i < maxPrefix; "/* unknown: i++ */")
        {
            if ("/* unknown: s1[i] */" == "/* unknown: s2[i] */")
            {
                "/* unknown: prefixLength++ */";
            }
            else
            {
            }
        }

        return jaroSim + prefixLength * prefixScale * 1L - jaroSim;
    }

    public static BasicMatchResult? findFuzzyMatch(string fileContent, string searchContent, double? threshold)
    {
        var searchLines = searchContent.split("\\n");
        var fileLines = fileContent.split("\\n");
        var bestMatch = null;
        var bestSimilarity = 0L;
        var searchBlock = searchContent.trim();
        if (searchBlock.length > 5L)
        {
            for (var i = 0; i <= fileLines.length - searchLines.length; "/* unknown: i++ */")
            {
                var candidateLines = fileLines.slice(i, i + searchLines.length);
                var candidateBlock = candidateLines.join("\\n").trim();
                if (candidateBlock.length < 5L)
                {
                }

                var similarity = jaroWinklerSimilarity(searchBlock, candidateBlock);
                if (similarity >= threshold && similarity > bestSimilarity)
                {
                    var linesBeforeMatch = fileLines.slice(0L, i);
                    var startIndex = linesBeforeMatch.join("\\n").length + linesBeforeMatch.length > 0L ? 1L : 0L;
                    var endIndex = startIndex + candidateBlock.length;
                    bestMatch = new
                    {
                        startIndex,
                        endIndex
                    };
                    bestSimilarity = similarity;
                }
            }
        }

        for (var searchLineIdx = 0; searchLineIdx < searchLines.length; "/* unknown: searchLineIdx++ */")
        {
            var searchLine = "/* unknown: searchLines[searchLineIdx] */".trim();
            if (searchLine.length == 0L || searchLine.length < 3L)
            {
            }

            for (var fileLineIdx = 0; fileLineIdx < fileLines.length; "/* unknown: fileLineIdx++ */")
            {
                var fileLine = "/* unknown: fileLines[fileLineIdx] */".trim();
                if (fileLine.length == 0L || fileLine.length < 3L)
                {
                }

                var similarity = jaroWinklerSimilarity(searchLine, fileLine);
                if (similarity >= threshold && similarity > bestSimilarity)
                {
                    var linesBeforeMatch = fileLines.slice(0L, fileLineIdx);
                    var startIndex = linesBeforeMatch.join("\\n").length + linesBeforeMatch.length > 0L ? 1L : 0L;
                    var endIndex = startIndex + "/* unknown: fileLines[fileLineIdx] */".length;
                    bestMatch = new
                    {
                        startIndex,
                        endIndex
                    };
                    bestSimilarity = similarity;
                }
            }
        }

        return bestMatch;
    }

    public static SearchMatchResult? findSearchMatch(string fileContent, string searchContent)
    {
        var trimmedSearchContent = searchContent.trim();
        if (trimmedSearchContent == "")
        {
            return new
            {
                startIndex = 0L,
                endIndex = 0L,
                strategyName = "emptySearch"
            };
        }

        foreach (var const { strategy, name } in matchingStrategies)
        {
            var result = strategy(fileContent, searchContent);
            if (result != null)
            {
                return SpreadMerge.Merge(result, new { strategyName = name });
            }
        }

        return null;
    }

    public static SearchMatchResult[] findSearchMatches(string fileContent, string searchContent)
    {
        var matches = "/* unknown: [] */";
        if (searchContent.trim() == "")
        {
            return "/* unknown: [{ startIndex: 0, endIndex: 0, strategyName: \"emptySearch\" }] */";
        }

        var remainingContent = fileContent;
        var currentOffset = 0L;
        while (remainingContent.length > 0L)
        {
            var match = findSearchMatch(remainingContent, searchContent);
            if (match == null)
            {
            }

            var adjustedMatch = new
            {
                startIndex = match.startIndex + currentOffset,
                endIndex = match.endIndex + currentOffset,
                strategyName = match.strategyName
            };
            if (matches.length > 0L && adjustedMatch.startIndex <= "/* unknown: matches[matches.length - 1] */".startIndex)
            {
            }

            matches.push(adjustedMatch);
            currentOffset = adjustedMatch.endIndex;
            remainingContent = fileContent.slice(currentOffset);
        }

        return matches;
    }
}