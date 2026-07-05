string? repo = null, tag = null, outDir = null, generatedDir = null, rejectedDir = null, singleFile = null, singleOutput = null;
bool postTranslateCleanGenerated = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--repo" && i + 1 < args.Length) repo = args[++i];
    else if (args[i] == "--tag" && i + 1 < args.Length) tag = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
    else if (args[i] == "--generated" && i + 1 < args.Length) generatedDir = args[++i];
    else if (args[i] == "--rejected" && i + 1 < args.Length) rejectedDir = args[++i];
    else if (args[i] == "--post-translate-clean-generated") postTranslateCleanGenerated = true;
    else if (args[i] == "--single-file" && i + 1 < args.Length) singleFile = args[++i];
    else if (args[i] == "--single-output" && i + 1 < args.Length) singleOutput = args[++i];
}

if (args.Length > 0 && (args[^1] == "--generated" || args[^1] == "--rejected" || args[^1] == "--single-file" || args[^1] == "--single-output"))
{
    Console.Error.WriteLine("Error: --generated, --rejected, --single-file, and --single-output require path arguments.");
    return 1;
}

// Single-file mode: --single-file and --single-output together override all other parameters
if (singleFile is not null && singleOutput is not null)
{
    if (!File.Exists(singleFile))
    {
        Console.Error.WriteLine($"Error: --single-file path does not exist: {singleFile}");
        return 1;
    }
    if (!Directory.Exists(singleOutput))
    {
        try
        {
            Directory.CreateDirectory(singleOutput);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to create --single-output directory: {singleOutput}");
            Console.Error.WriteLine($"Details: {ex.Message}");
            return 1;
        }
    }

    var options = new ContinueTranslator.Cli.TranslationOptions(
        null,
        null,
        singleOutput,
        null,
        null,
        false,
        singleFile);

    try
    {
        new ContinueTranslator.Cli.PipelineRunner().Run(options);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 2;
    }

    return 0;
}

// Standard mode: all traditional parameters required
if (string.IsNullOrWhiteSpace(repo) ||
    string.IsNullOrWhiteSpace(tag) ||
    string.IsNullOrWhiteSpace(outDir))
{
    Console.Error.WriteLine("Usage: ContinueTranslator.Cli --repo <path> --tag <tag> --out <directory> [--generated <path>] [--rejected <path>] [--post-translate-clean-generated]");
    Console.Error.WriteLine("       ContinueTranslator.Cli --single-file <path.ts> --single-output <directory>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Standard mode:");
    Console.Error.WriteLine("  --repo                            Path to local clone of forked Continue repository (required)");
    Console.Error.WriteLine("  --tag                             Git tag or branch to check out before scanning (required)");
    Console.Error.WriteLine("  --out                             Output directory for generated C# files (required)");
    Console.Error.WriteLine("  --generated                       optional: promote translated files to the specified Generated/ directory");
    Console.Error.WriteLine("  --rejected                        optional: write rejected files to the specified rejected/ directory (auto-determined if --generated is used)");
    Console.Error.WriteLine("  --post-translate-clean-generated  optional: delete Generated/ directory after translation completes (useful for testing)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Single-file mode (overrides all other parameters except --single-output):");
    Console.Error.WriteLine("  --single-file <path>              Full path to a single .ts file to translate");
    Console.Error.WriteLine("  --single-output <path>            Full path to the output folder (required with --single-file)");
    return 1;
}

var standardOptions = new ContinueTranslator.Cli.TranslationOptions(repo, tag, outDir, generatedDir, rejectedDir, postTranslateCleanGenerated);

try
{
    new ContinueTranslator.Cli.PipelineRunner().Run(standardOptions);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

return 0;
