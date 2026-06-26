namespace ContinueCore.Test;
public static partial class TestDirFunctions
{
    public static void setUpTestDir()
    {
        if (System.IO.File.Exists(TEST_DIR_PATH))
        {
            fs.rmSync(TEST_DIR_PATH, new { recursive = true });
        }

        System.IO.Directory.CreateDirectory(TEST_DIR_PATH);
    }

    public static void tearDownTestDir()
    {
        if (System.IO.File.Exists(TEST_DIR_PATH))
        {
            fs.rmSync(TEST_DIR_PATH, new { recursive = true });
        }
    }

    public static void addToTestDir((string  | [ string ,  string  ] ) [ ] pathsOrUris)
    {
        var paths = pathsOrUris.map("/* untranslatable arrow body */");
        foreach (var const p in paths)
        {
            var filepath = System.IO.Path.Combine(TEST_DIR_PATH, Array.isArray(p) ? "/* unknown: p[0] */" : p);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filepath), new { recursive = true });
            if (Array.isArray(p))
            {
                System.IO.File.WriteAllText(filepath, "/* unknown: p[1] */");
            }
        }
    }
}