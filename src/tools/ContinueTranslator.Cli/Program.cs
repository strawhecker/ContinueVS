string? repo = null, tag = null, outDir = null, generatedDir = null, rejectedDir = null;
bool postTranslateCleanGenerated = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--repo" && i + 1 < args.Length) repo = args[++i];
    else if (args[i] == "--tag" && i + 1 < args.Length) tag = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
    else if (args[i] == "--generated" && i + 1 < args.Length) generatedDir = args[++i];
    else if (args[i] == "--rejected" && i + 1 < args.Length) rejectedDir = args[++i];
    else if (args[i] == "--post-translate-clean-generated") postTranslateCleanGenerated = true;
}

if (args.Length > 0 && (args[^1] == "--generated" || args[^1] == "--rejected"))
{
    Console.Error.WriteLine("Error: --generated and --rejected require path arguments.");
    return 1;
}

if (string.IsNullOrWhiteSpace(repo) ||
    string.IsNullOrWhiteSpace(tag) ||
    string.IsNullOrWhiteSpace(outDir))
{
    Console.Error.WriteLine("Usage: ContinueTranslator.Cli --repo <path> --tag <tag> --out <directory> [--generated <path>] [--rejected <path>] [--post-translate-clean-generated]");
    Console.Error.WriteLine("  --repo                            Path to local clone of forked Continue repository (required)");
    Console.Error.WriteLine("  --tag                             Git tag or branch to check out before scanning (required)");
    Console.Error.WriteLine("  --out                             Output directory for generated C# files (required)");
    Console.Error.WriteLine("  --generated                       optional: promote translated files to the specified Generated/ directory");
    Console.Error.WriteLine("  --rejected                        optional: write rejected files to the specified rejected/ directory (auto-determined if --generated is used)");
    Console.Error.WriteLine("  --post-translate-clean-generated  optional: delete Generated/ directory after translation completes (useful for testing)");
    return 1;
}

var options = new ContinueTranslator.Cli.TranslationOptions(repo, tag, outDir, generatedDir, rejectedDir, postTranslateCleanGenerated);

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
