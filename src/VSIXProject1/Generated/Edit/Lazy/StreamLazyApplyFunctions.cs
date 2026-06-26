namespace ContinueCore.Edit.Lazy;
public static partial class StreamLazyApplyFunctions
{
    public static async AsyncGenerator<DiffLine, object, object> streamLazyApply(string oldCode, string filename, string newCode, ILLM llm, AbortController abortController)
    {
        var promptFactory = lazyApplyPromptForModel(llm.model, llm.providerName);
        if (!promptFactory)
        {
            throw "/* unknown: new Error(`Lazy apply not supported for model ${llm.model}`) */";
        }

        var promptMessages = promptFactory(oldCode, filename, newCode);
        var lazyCompletion = llm.streamChat(promptMessages, abortController.signal);
        var lazyCompletionLines = streamLines(lazyCompletion, true);
        lazyCompletionLines = stopAtLinesWithMarkdownSupport(lazyCompletionLines, filename);
        lazyCompletionLines = filterLeadingNewline(lazyCompletionLines);
        lazyCompletionLines = removeTrailingWhitespace(lazyCompletionLines);
        var lines = streamFillUnchangedCode(lazyCompletionLines, oldCode, replacementFunction);
        var oldLines = oldCode.split("/* unknown: /\\r?\\n/ */");
        var diffLines = streamDiff(oldLines, lines);
        diffLines = filterLeadingAndTrailingNewLineInsertion(diffLines);
        foreach (var const diffLine in diffLines)
        {
            "/* unknown: yield diffLine */";
        }
    }

    public static async LineStream streamFillUnchangedCode(LineStream lines, string oldCode, (oldCode :  string ,  linesBefore :  string [ ] ,  linesAfter :  string [ ] )  =>  AsyncGenerator < string >replacementFunction)
    {
        var newLines = "/* unknown: [] */";
        var buffer = "/* unknown: [] */";
        var waitingForBuffer = false;
        foreach (var const line in lines)
        {
            if (waitingForBuffer)
            {
                buffer.push(line);
                if (buffer.length >= BUFFER_LINES_BELOW)
                {
                    var replacementLines = replacementFunction(oldCode, newLines, buffer);
                    var replacement = "";
                    foreach (var const replacementLine in replacementLines)
                    {
                        "/* unknown: yield replacementLine */";
                        newLines.push(replacementLine);
                        replacement += replacementLine + "\\n";
                    }

                    foreach (var const bufferedLine in buffer)
                    {
                        "/* unknown: yield bufferedLine */";
                        newLines.push(bufferedLine);
                    }

                    waitingForBuffer = false;
                    buffer = "/* unknown: [] */";
                }
                else
                {
                }
            }

            if (line.includes(UNCHANGED_CODE))
            {
                waitingForBuffer = true;
            }
            else
            {
                "/* unknown: yield line */";
                newLines.push(line);
            }
        }

        if (waitingForBuffer)
        {
            var replacementLines = replacementFunction(oldCode, newLines, buffer);
            foreach (var const replacementLine in replacementLines)
            {
                "/* unknown: yield replacementLine */";
                newLines.push(replacementLine);
            }

            foreach (var const bufferedLine in buffer)
            {
                "/* unknown: yield bufferedLine */";
                newLines.push(bufferedLine);
            }
        }
    }
}