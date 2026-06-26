namespace ContinueCore.Tools;
public static partial class IndexFunctions
{
    public static
     { type :  "function" ;  function :  { name :  string ;  description ? :  string ;  parameters ? :  Record 
    serializeTool(Tool tool)
    {
        var { preprocessArgs, evaluateToolCallPolicy, ...rest } = tool;
        return rest;
    }
}