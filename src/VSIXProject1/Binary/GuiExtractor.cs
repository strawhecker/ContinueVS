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
        private static string BundledGuiPath =>
            Path.Combine(Path.GetDirectoryName(typeof(GuiExtractor).Assembly.Location)!, "gui");

        /// <summary>
        /// Ensures the Continue GUI assets are present on disk. No-op when
        /// <see cref="IndexHtmlPath"/> already exists.
        /// </summary>
        public static Task EnsureExtractedAsync()
        {
            if (File.Exists(IndexHtmlPath))
                return Task.CompletedTask;

            Directory.CreateDirectory(GuiRoot);
            CopyDirectory(BundledGuiPath, GuiRoot);
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
