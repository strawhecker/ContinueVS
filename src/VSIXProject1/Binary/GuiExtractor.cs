using System;
using System.IO;
using System.Threading.Tasks;

namespace ContinueVS.Binary
{
    /// <summary>
    /// Copies the bundled Continue React GUI (<c>gui/</c>) to
    /// <c>%APPDATA%\ContinueVS\gui\</c> on first use.
    /// Copy is skipped when <c>%APPDATA%\ContinueVS\gui\index.html</c> already exists.
    /// </summary>
    internal static partial class GuiExtractor
    {
        /// <summary>The folder where GUI assets are extracted on disk.</summary>
        public static string GuiRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ContinueVS", "gui");

        /// <summary>
        /// Absolute path to the sentinel file. Presence indicates the copy is complete.
        /// </summary>
        public static string IndexHtmlPath => Path.Combine(GuiRoot, "index.html");

        /// <summary>
        /// Absolute path to the GUI assets bundled inside the VSIX.
        /// </summary>
        private static string BundledGuiPath
        {
            get
            {
                var assemblyLocation = typeof(GuiExtractor).Assembly.Location;
                var assemblyDir = Path.GetDirectoryName(assemblyLocation);
                var bundledPath = Path.Combine(assemblyDir!, "gui");
                System.Diagnostics.Debug.WriteLine($"[GuiExtractor.BundledGuiPath] assemblyLocation={assemblyLocation}");
                System.Diagnostics.Debug.WriteLine($"[GuiExtractor.BundledGuiPath] assemblyDir={assemblyDir}");
                System.Diagnostics.Debug.WriteLine($"[GuiExtractor.BundledGuiPath] bundledPath={bundledPath}");
                System.Diagnostics.Debug.WriteLine($"[GuiExtractor.BundledGuiPath] DirectoryExists={Directory.Exists(bundledPath)}");
                return bundledPath;
            }
        }

        /// <summary>
        /// Ensures the Continue GUI assets are present on disk. No-op when
        /// <see cref="IndexHtmlPath"/> already exists.
        /// </summary>
        public static Task EnsureExtractedAsync()
        {
            // Check if we need to re-extract (if index.html exists, assume extraction is done)
            // However, if bridge-wrapper.js is missing, we need to re-extract because the
            // HTML structure changed (moved bridge-wrapper.js loading to <head>)
            string indexPath = IndexHtmlPath;
            string bridgeWrapperPath = Path.Combine(GuiRoot, "bridge-wrapper.js");
            bool indexExists = File.Exists(indexPath);
            bool wrapperExists = File.Exists(bridgeWrapperPath);

            System.Diagnostics.Debug.WriteLine($"[GuiExtractor.EnsureExtractedAsync] ENTRY");
            System.Diagnostics.Debug.WriteLine($"[GuiExtractor.EnsureExtractedAsync] indexPath={indexPath}");
            System.Diagnostics.Debug.WriteLine($"[GuiExtractor.EnsureExtractedAsync] bridgeWrapperPath={bridgeWrapperPath}");
            System.Diagnostics.Debug.WriteLine($"[GuiExtractor.EnsureExtractedAsync] indexExists={indexExists}, wrapperExists={wrapperExists}");

            if (indexExists && wrapperExists)
            {
                System.Diagnostics.Debug.WriteLine("[GuiExtractor.EnsureExtractedAsync] Both files exist - updating bridge-wrapper.js from bundled source");
                // Always overwrite bridge-wrapper.js to ensure latest version is deployed
                var bundledWrapper = Path.Combine(BundledGuiPath, "bridge-wrapper.js");
                if (File.Exists(bundledWrapper))
                {
                    File.Copy(bundledWrapper, bridgeWrapperPath, overwrite: true);
                    System.Diagnostics.Debug.WriteLine("[GuiExtractor.EnsureExtractedAsync] bridge-wrapper.js updated from bundled source");
                }
                return Task.CompletedTask;
            }

            System.Diagnostics.Debug.WriteLine("[GuiExtractor.EnsureExtractedAsync] Files missing - re-extracting...");

            // Re-extract if bridge-wrapper is missing (indicates old extraction)
            Directory.CreateDirectory(GuiRoot);

            // Delete old extracted files to force fresh copy
            if (!wrapperExists && Directory.Exists(GuiRoot))
            {
                // Only delete if bridge-wrapper is missing to avoid unnecessary deletions
                System.Diagnostics.Debug.WriteLine($"[GuiExtractor.EnsureExtractedAsync] Deleting old GUI root: {GuiRoot}");
                try
                {
                    Directory.Delete(GuiRoot, recursive: true);
                    Directory.CreateDirectory(GuiRoot);
                    System.Diagnostics.Debug.WriteLine("[GuiExtractor.EnsureExtractedAsync] Old directory deleted");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GuiExtractor.EnsureExtractedAsync] Error deleting directory: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[GuiExtractor.EnsureExtractedAsync] Copying from {BundledGuiPath} to {GuiRoot}");
            CopyDirectory(BundledGuiPath, GuiRoot);
            System.Diagnostics.Debug.WriteLine("[GuiExtractor.EnsureExtractedAsync] Extraction complete");
            return Task.CompletedTask;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }
}
