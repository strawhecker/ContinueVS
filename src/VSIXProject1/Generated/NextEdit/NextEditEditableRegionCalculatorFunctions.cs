namespace ContinueCore.NextEdit;
public static partial class NextEditEditableRegionCalculatorFunctions
{
    public static async Task<RangeInFile[]?> getNextEditableRegion(EditableRegionStrategy strategy, object ctx)
    {
    }

    public static RangeInFile[]? naiveJump(object ctx)
    {
        var { fileLines, filepath } = ctx;
        if (!fileLines || !filepath)
        {
            console.warn("Missing required context for naive jump");
            return null;
        }

        return "/* unknown: [\r\n    {\r\n      filepath,\r\n      range: {\r\n        start: { line: 0, character: 0 },\r\n        end: {\r\n          line: fileLines.length - 1,\r\n          character: fileLines.at(-1).length,\r\n        },\r\n      },\r\n    },\r\n  ] */";
    }

    public static RangeInFile[]? slidingJump(object ctx)
    {
        var { fileLines, filepath, modelName, currentCursorPos } = ctx;
        if (!fileLines || !filepath || !modelName || !currentCursorPos)
        {
            console.warn("Missing required context for sliding jump");
            return null;
        }

        var topMargin = "/* unknown: MODEL_WINDOW_SIZES[modelName as NEXT_EDIT_MODELS] */".topMargin;
        var bottomMargin = "/* unknown: MODEL_WINDOW_SIZES[modelName as NEXT_EDIT_MODELS] */".bottomMargin;
        var windowSize = topMargin + bottomMargin + 1L;
        if (fileLines.length <= windowSize)
        {
            return "/* unknown: [\r\n      {\r\n        filepath,\r\n        range: {\r\n          start: { line: 0, character: 0 },\r\n          end: {\r\n            line: fileLines.length - 1,\r\n            character: fileLines[fileLines.length - 1].length,\r\n          },\r\n        },\r\n      },\r\n    ] */";
        }

        var ranges = "/* unknown: [] */";
        var cursorLine = currentCursorPos.line;
        var firstWindowStart = Math.max(0L, cursorLine - topMargin);
        var firstWindowEnd = Math.min(fileLines.length - 1L, cursorLine + bottomMargin);
        ranges.push(new { filepath, range = new { start = new { line = firstWindowStart, character = 0L }, end = new { line = firstWindowEnd, character = "/* unknown: fileLines[firstWindowEnd] */".length } } });
        var slidingStep = Math.max(1L, Math.floor(windowSize / 2L));
        var currentStartDown = firstWindowEnd + 1L;
        var currentStartUp = firstWindowStart - slidingStep;
        while (currentStartDown < fileLines.length || currentStartUp >= 0L)
        {
            if (currentStartDown < fileLines.length)
            {
                var windowStart = currentStartDown;
                var windowEnd = Math.min(windowStart + windowSize - 1L, fileLines.length - 1L);
                ranges.push(new { filepath, range = new { start = new { line = windowStart, character = 0L }, end = new { line = windowEnd, character = "/* unknown: fileLines[windowEnd] */".length } } });
                currentStartDown += slidingStep;
            }

            if (currentStartUp >= 0L)
            {
                var windowStart = Math.max(0L, currentStartUp);
                var windowEnd = Math.min(windowStart + windowSize - 1L, fileLines.length - 1L);
                ranges.push(new { filepath, range = new { start = new { line = windowStart, character = 0L }, end = new { line = windowEnd, character = "/* unknown: fileLines[windowEnd] */".length } } });
                currentStartUp -= slidingStep;
            }
        }

        return ranges;
    }

    public static async Task<RangeInFile[]?> rerankJump(
     { fileContent :  string ;  query :  string ;  filepath :  string ;  reranker :  ILLM ;  chunkSize :  number ;  }
    ctx)
    {
        try
        {
            var { fileContent, query, filepath, reranker, chunkSize = 5 } = ctx;
            if (!fileContent || !query || !filepath || !reranker)
            {
                console.warn("Missing required context for rerank jump:", !fileContent, !query, !filepath, !reranker);
                return null;
            }

            var lines = fileContent.split("\\n");
            var chunks = "/* unknown: [] */";
            for (var i = 0; i < lines.length; i += Math.floor(chunkSize / 2L))
            {
                var endLine = Math.min(i + chunkSize - 1L, lines.length - 1L);
                var chunkContent = lines.slice(i, endLine + 1L).join("\\n");
                if (chunkContent == "")
                {
                }

                chunks.push(new { content = chunkContent, startLine = i, endLine = endLine, digest = $"chunk-{i}-{endLine}", filepath = filepath, index = i });
            }

            var scores = await reranker.rerank(query, chunks);
            chunks.sort((Chunk a, Chunk b) => "/* unknown: scores[chunks.indexOf(b)] */" - "/* unknown: scores[chunks.indexOf(a)] */");
            var chunkIndex = Math.min(2L, chunks.length - 1L);
            var mostRelevantChunk = "/* unknown: chunks[chunkIndex] */";
            return "/* unknown: [\r\n      {\r\n        filepath,\r\n        range: {\r\n          start: { line: mostRelevantChunk.startLine, character: 0 },\r\n          end: {\r\n            line: mostRelevantChunk.endLine,\r\n            character: lines[mostRelevantChunk.endLine].length,\r\n          },\r\n        },\r\n      },\r\n    ] */";
        }
        catch (Exception)
        {
            console.error("Error in rerank jump:", error);
            return null;
        }
    }

    public static async Task<RangeInFile[]?> staticRerankJump(
     { oldFileContent :  string ;  newFileContent :  string ;  completionRange :  Range ;  filepath :  string ;  ide :  IDE ;  reranker ? :  ILLM ;  chunkSize ? :  number ;  }
    ctx)
    {
        try
        {
            var { oldFileContent, newFileContent, completionRange, filepath, ide } = ctx;
            if (!oldFileContent || !newFileContent || !completionRange || !filepath || !ide)
            {
                console.warn("Missing required context for static rerank jump:", !oldFileContent, !newFileContent, !completionRange, !filepath, !ide);
                return null;
            }

            var oldAst = await getAst(filepath, oldFileContent);
            if (!oldAst)
            {
            }

            var newAst = await getAst(filepath, newFileContent);
            if (!newAst)
            {
            }

            var changedNodes = compareAsts(oldAst, newAst);
            if (!changedNodes || changedNodes.length == 0L)
            {
            }

            var nodeQueue = changedNodes.sort((
             { oldNode :  Parser . SyntaxNode | null ;  newNode :  Parser . SyntaxNode | null ;  depth :  number ;  }
            a,  { oldNode :  Parser . SyntaxNode | null ;  newNode :  Parser . SyntaxNode | null ;  depth :  number ;  }
            b) => a.depth - b.depth);
            console.log("nodeQueue:", nodeQueue.map((
             { oldNode :  Parser . SyntaxNode | null ;  newNode :  Parser . SyntaxNode | null ;  depth :  number ;  }
            node) => new { oldText = node.oldNode.text || "", newText = node.newNode.text || "", oldType = node.oldNode.type || "", newType = node.newNode.type || "", depth = node.depth }));
            var targetNode = null;
            while (nodeQueue.length > 0L && !targetNode)
            {
                var candidate = nodeQueue.shift();
                if (candidate && candidate.oldNode && candidate.oldNode.type != "program")
                {
                    targetNode = candidate.oldNode;
                }
            }

            if (!targetNode)
            {
            }

            var nodeText = getNodeText(targetNode);
            if (!nodeText || nodeText.trim() == "")
            {
            }

            var references = "/* unknown: [] */";
            try
            {
                var nodePosition = getNodePosition(targetNode);
                if (nodePosition)
                {
                    var symbols = await ide.getDocumentSymbols(filepath);
                    var filteredSymbols = symbols.filter((import ( ".." ) . DocumentSymbol symbol) => !doRangesOverlap(symbol.range, completionRange));
                    if (!ctx.reranker)
                    {
                        console.warn("No reranker available for static jump symbol ranking");
                        return null;
                    }

                    var symbolChunks = filteredSymbols.map((import ( ".." ) . DocumentSymbol symbol) => new { content = symbol.name, startLine = symbol.range.start.line, endLine = symbol.range.end.line, digest = $"symbol-{symbol.name}-{symbol.range.start.line}", filepath = filepath, index = symbol.range.start.line });
                    if (symbolChunks.length == 0L)
                    {
                        console.warn("No symbols found for ranking");
                        return null;
                    }

                    var scores = await ctx.reranker.rerank(nodeText, symbolChunks);
                    symbolChunks.sort((Chunk a, Chunk b) => "/* unknown: scores[symbolChunks.indexOf(b)] */" - "/* unknown: scores[symbolChunks.indexOf(a)] */");
                    var mostRelevantSymbol = "/* unknown: symbolChunks[0] */";
                    var originalSymbol = filteredSymbols.find((import ( ".." ) . DocumentSymbol symbol) => symbol.range.start.line == mostRelevantSymbol.startLine && symbol.range.end.line == mostRelevantSymbol.endLine);
                    if (originalSymbol)
                    {
                        references = "/* unknown: [\r\n            {\r\n              filepath,\r\n              range: originalSymbol.range,\r\n            },\r\n          ] */";
                    }
                }
            }
            catch (Exception)
            {
                console.warn("Failed to use IDE references, falling back to text search:", e);
            }

            if (references.length == 0L)
            {
                references = findTextOccurrences(oldFileContent, nodeText).map((Range range) => new { filepath, range });
            }

            var currentFileReferences = references.filter((RangeInFile ref) => ref.filepath == filepath);
            if (currentFileReferences.length > 0L)
            {
                return "/* unknown: [currentFileReferences[0]] */";
            }

            return null;
        }
        catch (Exception)
        {
            console.error("Error in static jump:", error);
            return null;
        }
    }

    public static async Task<RangeInFile[]?> staticJump(
     { cursorPosition :  { line :  number ;  character :  number ;  } ;  filepath :  string ;  ide :  IDE ;  }
    ctx)
    {
        try
        {
            var { cursorPosition, filepath, ide } = ctx;
            if (!cursorPosition || !filepath || !ide)
            {
                console.warn("Missing required context for static jump:", !cursorPosition, !filepath, !ide);
                return null;
            }

            var tree = await DocumentHistoryTracker.getInstance().getMostRecentAst(filepath);
            if (!tree)
            {
            }

            var point = new
            {
                row = cursorPosition.line,
                column = cursorPosition.character
            };
            var nodeAtCursor = tree.rootNode.descendantForPosition(point);
            if (!nodeAtCursor)
            {
                console.log("No node found at cursor position");
                return null;
            }

            var identifierNode = findClosestIdentifierNode(nodeAtCursor);
            if (!identifierNode)
            {
                console.log("No identifier node found near cursor position");
                return null;
            }

            var references = await ide.getReferences(new { filepath, position = new { line = identifierNode.startPosition.row, character = identifierNode.startPosition.column } });
            if (!references || references.length == 0L)
            {
                console.log($"No references found for identifier: {identifierNode.text}");
                return null;
            }

            return references.length > 1L ? references.slice(1L) : null;
        }
        catch (Exception)
        {
            console.error("Error in staticJump:", error);
            return null;
        }
    }

    public static object findClosestIdentifierNode(object node)
    {
        if (!node)
        {
        }

        if (isIdentifierNode(node))
        {
        }

        if (isDeclarationNode(node))
        {
        }

        var parent = node.parent;
        if (parent && isIdentifierNode(parent))
        {
            return parent;
        }

        if (parent)
        {
            if (isDeclarationNode(parent))
            {
            }

            for (var i = 0; i < parent.childCount; ++i)
            {
                var sibling = parent.child(i);
                if (sibling && isIdentifierNode(sibling))
                {
                    return sibling;
                }
            }
        }

        return findClosestIdentifierNode(parent);
    }

    public static object findLeftmostIdentifier(Parser.SyntaxNode node)
    {
        if (isIdentifierNode(node))
        {
        }

        for (var i = 0; i < node.childCount; ++i)
        {
            var child = node.child(i);
            if (child)
            {
                var result = findLeftmostIdentifier(child);
                if (result)
                {
                }
            }
        }

        return null;
    }

    public static bool isIdentifierNode(Parser.SyntaxNode node)
    {
        var nodeType = node.type;
        if (nodeType == "identifier")
        {
        }

        if (nodeType.includes("identifier"))
        {
        }

        var specialIdentifiers = "/* unknown: [\"name\", \"constant\"] */";
        return specialIdentifiers.includes(nodeType);
    }

    public static bool isDeclarationNode(Parser.SyntaxNode node)
    {
        var nodeType = node.type;
        if (nodeType.endsWith("_declaration"))
        {
        }

        if (nodeType.endsWith("_definition"))
        {
        }

        if (nodeType.endsWith("_item"))
        {
        }

        var declarationTypes = "/* unknown: [\r\n    // Python.\r\n    \"function_definition\",\r\n    \"class_definition\",\r\n    \"async_function_definition\",\r\n    \"decorated_definition\",\r\n\r\n    // Ruby.\r\n    \"method\",\r\n    \"class\",\r\n    \"module\",\r\n    \"singleton_method\",\r\n\r\n    // Java.\r\n    \"variable_declarator\",\r\n    \"local_variable_declaration\",\r\n\r\n    // Go.\r\n    \"short_var_declaration\",\r\n\r\n    // General\r\n    \"method_definition\",\r\n  ] */";
        return declarationTypes.includes(nodeType);
    }

    public static
     { oldNode :  Parser . SyntaxNode | null ;  newNode :  Parser . SyntaxNode | null ;  depth :  number ;  } [ ] 
    compareAsts(Parser.Tree oldAst, Parser.Tree newAst)
    {
        var changedNodes = "/* unknown: [] */";
        traverse(oldAst.rootNode, newAst.rootNode);
        return changedNodes;
    }

    public static string getNodeText(Parser.SyntaxNode node)
    {
        if (!node)
        {
        }

        return node.text;
    }

    public static Position? getNodePosition(Parser.SyntaxNode node)
    {
        if (!node)
        {
        }

        return new
        {
            line = node.startPosition.row,
            character = node.startPosition.column
        };
    }

    public static Range[] findTextOccurrences(string text, string searchText)
    {
        var results = "/* unknown: [] */";
        var lines = text.split("\\n");
        for (var lineIndex = 0; lineIndex < lines.length; "/* unknown: lineIndex++ */")
        {
            var line = "/* unknown: lines[lineIndex] */";
            var charIndex = 0L;
            while (charIndex < line.length)
            {
                var foundIndex = line.indexOf(searchText, charIndex);
                if (foundIndex == -1L)
                {
                }

                results.push(new { start = new { line = lineIndex, character = foundIndex }, end = new { line = lineIndex, character = foundIndex + searchText.length } });
                charIndex = foundIndex + 1L;
            }
        }

        return results;
    }

    public static bool isRangeWithin(Range innerRange, Range outerRange)
    {
        var startWithin = innerRange.start.line > outerRange.start.line || innerRange.start.line == outerRange.start.line && innerRange.start.character >= outerRange.start.character;
        var endWithin = innerRange.end.line < outerRange.end.line || innerRange.end.line == outerRange.end.line && innerRange.end.character <= outerRange.end.character;
        return startWithin && endWithin;
    }

    public static bool doRangesOverlap(Range range1, Range range2)
    {
        var range1StartsAfterRange2Ends = range1.start.line > range2.end.line || range1.start.line == range2.end.line && range1.start.character > range2.end.character;
        var range2StartsAfterRange1Ends = range2.start.line > range1.end.line || range2.start.line == range1.end.line && range2.start.character > range1.end.character;
        return !range1StartsAfterRange2Ends || range2StartsAfterRange1Ends;
    }

    public static bool doesUpperPartOverlap(Range range1, Range range2)
    {
        var range1StartsBeforeRange2Ends = range1.start.line < range2.end.line || range1.start.line == range2.end.line && range1.start.character <= range2.end.character;
        var range1StartsBeforeRange2Starts = range1.start.line < range2.start.line || range1.start.line == range2.start.line && range1.start.character < range2.start.character;
        return range1StartsBeforeRange2Ends && range1StartsBeforeRange2Starts;
    }

    public static bool doesLowerPartOverlap(Range range1, Range range2)
    {
        var range1StartsInsideRange2 = range1.start.line > range2.start.line || range1.start.line == range2.start.line && range1.start.character >= range2.start.character && range1.start.line < range2.end.line || range1.start.line == range2.end.line && range1.start.character < range2.end.character;
        var range1EndsAfterRange2 = range1.end.line > range2.end.line || range1.end.line == range2.end.line && range1.end.character > range2.end.character;
        return range1StartsInsideRange2 && range1EndsAfterRange2;
    }

    public static bool doesRangePartiallyOverlap(Range range1, Range range2)
    {
        var upperPartOverlap = range1.start.line < range2.start.line || range1.start.line == range2.start.line && range1.start.character < range2.start.character && range1.end.line > range2.start.line || range1.end.line == range2.start.line && range1.end.character > range2.start.character && range1.end.line < range2.end.line || range1.end.line == range2.end.line && range1.end.character <= range2.end.character;
        var lowerPartOverlap = range1.start.line > range2.start.line || range1.start.line == range2.start.line && range1.start.character >= range2.start.character && range1.start.line < range2.end.line || range1.start.line == range2.end.line && range1.start.character < range2.end.character && range1.end.line > range2.end.line || range1.end.line == range2.end.line && range1.end.character > range2.end.character;
        return upperPartOverlap || lowerPartOverlap;
    }

    public static void printChunks(Chunk[] chunks)
    {
        console.log("chunks:", System.Text.Json.JsonSerializer.Serialize(chunks.map((Chunk chunk) => new { content = chunk.content, startLine = chunk.startLine, endLine = chunk.endLine }), null, 2L));
    }
}