string? repo = null, tag = null, outDir = null, generatedDir = null;

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--repo") repo = args[++i];
    else if (args[i] == "--tag") tag = args[++i];
    else if (args[i] == "--out") outDir = args[++i];
    else if (args[i] == "--generated") generatedDir = args[++i];
}

if (args.Length > 0 && args[^1] == "--generated")
{
    Console.Error.WriteLine("Error: --generated requires a path argument.");
    return 1;
}

if (string.IsNullOrWhiteSpace(repo) ||
    string.IsNullOrWhiteSpace(tag) ||
    string.IsNullOrWhiteSpace(outDir))
{
    Console.Error.WriteLine("Usage: ContinueTranslator.Cli --repo <path> --tag <tag> --out <directory> [--generated <path>]");
    Console.Error.WriteLine("  --repo        Path to local clone of forked Continue repository (required)");
    Console.Error.WriteLine("  --tag         Git tag or branch to check out before scanning (required)");
    Console.Error.WriteLine("  --out         Output directory for generated C# files (required)");
    Console.Error.WriteLine("  --generated   optional: promote translated files to the specified Generated/ directory");
    return 1;
}

var options = new ContinueTranslator.Cli.TranslationOptions(repo, tag, outDir, generatedDir);

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
