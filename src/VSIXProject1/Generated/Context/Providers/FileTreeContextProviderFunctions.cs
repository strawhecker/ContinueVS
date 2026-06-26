namespace ContinueCore.Context.Providers;
public static partial class FileTreeContextProviderFunctions
{
    public static string formatFileTree(Directory tree, string? indentation)
    {
        var result = "";
        foreach (var const file in tree.files)
        {
            result += $"{indentation}{file}
";
        }

        foreach (var const directory in tree.directories)
        {
            result += $"{indentation}{directory.name}/
";
            result += formatFileTree(directory, $"{indentation}  ");
        }

        return result;
    }
}