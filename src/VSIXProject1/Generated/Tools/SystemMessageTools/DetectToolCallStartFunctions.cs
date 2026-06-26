namespace ContinueCore.Tools.SystemMessageTools;
public static partial class DetectToolCallStartFunctions
{
    public static
     { isInToolCall :  boolean ;  isInPartialStart :  boolean ;  modifiedBuffer :  string ;  }
    detectToolCallStart(string buffer, SystemMessageToolsFramework toolCallFramework)
    {
        var starts = toolCallFramework.acceptedToolCallStarts;
        var modifiedBuffer = buffer;
        var isInToolCall = false;
        var isInPartialStart = false;
        var lowerCaseBuffer = buffer.toLowerCase();
        for (var i = 0; i < starts.length; "/* unknown: i++ */")
        {
            var [start, replacement] = "/* unknown: starts[i] */";
            if (lowerCaseBuffer.startsWith(start))
            {
                if (i != 0L)
                {
                    modifiedBuffer = buffer.replace("/* unknown: new RegExp(start, \"i\") */", replacement);
                }

                isInToolCall = true;
            }
        }

        return new
        {
            isInToolCall,
            isInPartialStart,
            modifiedBuffer
        };
    }
}