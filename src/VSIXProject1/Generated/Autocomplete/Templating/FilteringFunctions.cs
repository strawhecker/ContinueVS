namespace ContinueCore.Autocomplete.Templating;
public static partial class FilteringFunctions
{
    public static (AutocompleteCodeSnippet | AutocompleteStaticSnippet ) [ ] filterSnippetsAlreadyInCaretWindow((AutocompleteCodeSnippet | AutocompleteStaticSnippet ) [ ] snippets, string caretWindow)
    {
        return snippets.filter((AutocompleteCodeSnippet | AutocompleteStaticSnippet s) => s.content.trim() != "" && !caretWindow.includes(s.content.trim()));
    }
}