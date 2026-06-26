namespace ContinueCore.Autocomplete.Constants;
public static partial class AutocompleteLanguageInfoFunctions
{
    public static AutocompleteLanguageInfo languageForFilepath(string fileUri)
    {
        var extension = getUriFileExtension(fileUri);
        return "/* unknown: LANGUAGES[extension] */" || Typescript;
    }
}