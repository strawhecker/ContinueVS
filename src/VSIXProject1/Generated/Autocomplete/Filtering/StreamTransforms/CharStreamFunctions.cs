namespace ContinueCore.Autocomplete.Filtering.StreamTransforms;
public static partial class CharStreamFunctions
{
    public static async AsyncGenerator<string, object, object> onlyWhitespaceAfterEndOfLine(AsyncGenerator<string, object, object> stream, string[] endOfLine, () =>  void  fullStop)
    {
        var pending = "";
        foreach (var let chunk in stream)
        {
            chunk = pending + chunk;
            pending = "";
            for (var i = 0; i < chunk.length - 1L; "/* unknown: i++ */")
            {
                if (endOfLine.includes("/* unknown: chunk[i] */") && "/* unknown: chunk[i + 1] */".trim() == "/* unknown: chunk[i + 1] */")
                {
                    "/* unknown: yield chunk.slice(0, i + 1) */";
                    fullStop();
                    return;
                }
            }

            if (endOfLine.includes("/* unknown: chunk[chunk.length - 1] */"))
            {
                pending = "/* unknown: chunk[chunk.length - 1] */";
                "/* unknown: yield chunk.slice(0, chunk.length - 1) */";
            }
            else
            {
                "/* unknown: yield chunk */";
            }
        }

        "/* unknown: yield pending */";
    }

    public static async AsyncGenerator<string, void, object> noFirstCharNewline(AsyncGenerator<string, object, object> stream)
    {
        var first = true;
        foreach (var const char in stream)
        {
            if (first)
            {
                first = false;
                if (char.startsWith("\\n") || char.startsWith("\\r"))
                {
                    return;
                }
            }

            "/* unknown: yield char */";
        }
    }

    public static async AsyncGenerator<string, object, object> stopAtStopTokens(AsyncGenerator<string, object, object> stream, string[] stopTokens)
    {
        if (stopTokens.length == 0L)
        {
            foreach (var const char in stream)
            {
                "/* unknown: yield char */";
            }

            return;
        }

        var maxStopTokenLength = Math.max("/* unknown: ...stopTokens.map((token) => token.length) */");
        var buffer = "";
        foreach (var const chunk in stream)
        {
            buffer += chunk;
            while (buffer.length >= maxStopTokenLength)
            {
                var found = false;
                foreach (var const stopToken in stopTokens)
                {
                    if (buffer.startsWith(stopToken))
                    {
                        found = true;
                        return;
                    }
                }

                if (!found)
                {
                    "/* unknown: yield buffer[0] */";
                    buffer = buffer.slice(1L);
                }
            }
        }

        stopTokens.forEach((string token) => buffer = buffer.replace(token, ""));
        foreach (var const char in buffer)
        {
            "/* unknown: yield char */";
        }
    }

    public static async AsyncGenerator<string, object, object> stopAtStartOf(AsyncGenerator<string, object, object> stream, string suffix, double? sequenceLength)
    {
        if (suffix.length < sequenceLength)
        {
            foreach (var const chunk in stream)
            {
                "/* unknown: yield chunk */";
            }

            return;
        }

        var targetPart = suffix.trimStart().slice(0L, Math.floor(sequenceLength * 1.5));
        var buffer = "";
        foreach (var const chunk in stream)
        {
            buffer += chunk;
            if (buffer.length >= sequenceLength && targetPart.includes(buffer))
            {
                return;
            }

            while (buffer.length > sequenceLength)
            {
                "/* unknown: yield buffer[0] */";
                buffer = buffer.slice(1L);
            }
        }

        if (buffer.length > 0L)
        {
            "/* unknown: yield buffer */";
        }
    }
}