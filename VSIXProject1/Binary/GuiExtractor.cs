using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace ContinueVS.Binary
{
    /// <summary>
    /// Extracts the Continue React GUI (<c>extension/gui/**</c>) from a local VSIX file
    /// or downloads the latest package from the VS Code Marketplace.
    /// Extraction is skipped when <c>%APPDATA%\ContinueVS\gui\index.html</c> already exists.
    /// </summary>
    internal static partial class GuiExtractor
    {
        private const string MarketplaceUrl =
            "https://marketplace.visualstudio.com/_apis/public/gallery/publishers/Continue/vsextensions/continue/latest/vspackage";

        private const string GuiPrefix = "extension/gui/";

        private static readonly string GuiRoot =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ContinueVS", "gui");

        /// <summary>
        /// Absolute path to the sentinel file. Presence indicates extraction is complete.
        /// </summary>
        public static string IndexHtmlPath => Path.Combine(GuiRoot, "index.html");

        /// <summary>
        /// Ensures the Continue GUI assets are present on disk. No-op when
        /// <see cref="IndexHtmlPath"/> already exists.
        /// </summary>
        /// <param name="localVsixPath">
        /// Optional absolute path to a local VSIX (e.g. from the Options page).
        /// Falls back to downloading from the Marketplace when <see langword="null"/>
        /// or the path does not exist on disk.
        /// </param>
        public static async Task EnsureExtractedAsync(string? localVsixPath = null)
        {
            if (File.Exists(IndexHtmlPath))
                return;

            Directory.CreateDirectory(GuiRoot);

            string? vsixPath = ResolveLocal(localVsixPath)
                              ?? await DownloadVsixAsync().ConfigureAwait(false);

            if (vsixPath != null)
                ExtractGui(vsixPath);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static string? ResolveLocal(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return File.Exists(path) ? path : null;
        }

        private static async Task<string> DownloadVsixAsync()
        {
            var dest = Path.Combine(Path.GetTempPath(), "continue-latest.vsix");

            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(5);
                http.DefaultRequestHeaders.Add(
                    "User-Agent",
                    "ContinueVS/1.0 (+https://github.com/strawhecker/ContinueVS)");

                var data = await http.GetByteArrayAsync(MarketplaceUrl).ConfigureAwait(false);
                File.WriteAllBytes(dest, data);
            }

            return dest;
        }

        private static void ExtractGui(string vsixPath)
        {
            using (var fs = File.OpenRead(vsixPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.StartsWith(GuiPrefix, StringComparison.Ordinal))
                        continue;

                    var relative = entry.FullName.Substring(GuiPrefix.Length);
                    if (string.IsNullOrEmpty(relative))
                        continue;

                    var destPath = Path.Combine(
                        GuiRoot,
                        relative.Replace('/', Path.DirectorySeparatorChar));

                    // Directory entry — ensure it exists and move on
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    var destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    using (var src = entry.Open())
                    using (var dst = new FileStream(
                        destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        src.CopyTo(dst);
                    }
                }
            }
        }
    }
}
