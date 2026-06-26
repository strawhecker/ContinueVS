namespace ContinueCore.Edit.Lazy;
public static partial class FindInAstFunctions
{
    public static object findInAst(Parser.SyntaxNode node, (node :  Parser . SyntaxNode )  =>  boolean criterion, (node :  Parser . SyntaxNode )  =>  boolean ?shouldRecurse)
    {
        var stack = "/* unknown: [node] */";
        while (stack.length > 0L)
        {
            var node = "/* unknown: stack.pop()! */";
            if (criterion(node))
            {
                return node;
            }

            if (shouldRecurse(node))
            {
                stack.push("/* unknown: ...node.children */");
            }
        }

        return null;
    }
}