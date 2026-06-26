namespace ContinueCore.Util;
public static partial class RangesFunctions
{
    public static string getRangeInString(string content, Range range)
    {
        var lines = content.split("\\n");
        if (range.start.line == range.end.line)
        {
            return "/* unknown: lines[range.start.line] */".substring(range.start.character, range.end.character) ?? "";
        }

        var firstLine = "/* unknown: lines[range.start.line] */".substring(range.start.character, "/* unknown: lines[range.start.line] */".length) ?? "";
        var middleLines = lines.slice(range.start.line + 1L, range.end.line);
        var lastLine = "/* unknown: lines[range.end.line] */".substring(0L, range.end.character) ?? "";
        return "/* unknown: [firstLine, ...middleLines, lastLine] */".join("\\n");
    }

    public static Range? intersection(Range a, Range b)
    {
        var startLine = Math.max(a.start.line, b.start.line);
        var endLine = Math.min(a.end.line, b.end.line);
        if (startLine > endLine)
        {
            return null;
        }

        if (startLine == endLine)
        {
            var startCharacter = Math.max(a.start.character, b.start.character);
            var endCharacter = Math.min(a.end.character, b.end.character);
            if (startCharacter > endCharacter)
            {
                return null;
            }

            return new
            {
                start = new
                {
                    line = startLine,
                    character = startCharacter
                },
                end = new
                {
                    line = endLine,
                    character = endCharacter
                }
            };
        }

        var startCharacter = startLine == a.start.line ? a.start.character : b.start.character;
        var endCharacter = endLine == a.end.line ? a.end.character : b.end.character;
        return new
        {
            start = new
            {
                line = startLine,
                character = startCharacter
            },
            end = new
            {
                line = endLine,
                character = endCharacter
            }
        };
    }

    public static Range union(Range a, Range b)
    {
        var start;
        if (a.start.line == b.start.line)
        {
            start = new
            {
                line = a.start.line,
                character = Math.min(a.start.character, b.start.character)
            };
        }

        var end;
        if (a.end.line == b.end.line)
        {
            end = new
            {
                line = a.end.line,
                character = Math.max(a.end.character, b.end.character)
            };
        }

        return new
        {
            start,
            end
        };
    }

    public static Position maxPosition(Position a, Position b)
    {
        if (a.line > b.line)
        {
            return a;
        }
    }

    public static Position minPosition(Position a, Position b)
    {
        if (a.line < b.line)
        {
            return a;
        }
    }
}