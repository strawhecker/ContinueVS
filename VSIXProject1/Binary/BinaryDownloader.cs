using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Binary
{
    /// <summary>
    /// Downloads the Continue VS Code VSIX from the VS Marketplace and extracts
    /// the win32-x64 binary from inside it.
    ///
    /// The VSIX is a ZIP archive.  The binary is always at the path:
    ///   <c>extension/core/win32-x64/continue-binary.exe</c>
    /// </summary>
    internal static class BinaryDownloader
    {
        // VS Code Marketplace download URL for the win32-x64 platform VSIX.
        private const string VsixDownloadUrl =
            "https://marketplace.visualstudio.com/_apis/public/gallery/publishers/Continue" +
            "/vsextensions/continue/2.0.0/vspackage?targetPlatform=win32-x64";

        // Path of the binary inside the VSIX ZIP.
        private const string BinaryEntryPath =
            "extension/core/win32-x64/continue-binary.exe";

        /// <summary>
        /// Ensures <paramref name="binaryDestination"/> exists, downloading and
        /// extracting it from the VS Marketplace VSIX if necessary.
        /// </summary>
        internal static async Task EnsureAsync(
            string binaryDestination,
            IVsStatusbar? statusBar,
            CancellationToken cancellationToken)
        {
            if (File.Exists(binaryDestination))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(binaryDestination)!);

            var vsixPath = binaryDestination + ".vsix";
            try
            {
                await DownloadVsixAsync(vsixPath, statusBar, cancellationToken);
                await SetStatusAsync(statusBar, "Continue: extracting AI engine…");
                ExtractBinaryFromVsix(vsixPath, binaryDestination);
                await SetStatusAsync(statusBar, "Continue: AI engine ready.");
            }
            finally
            {
                if (File.Exists(vsixPath))
                    File.Delete(vsixPath);
            }
        }

        // -----------------------------------------------------------------
        // Download
        // -----------------------------------------------------------------

        private static async Task DownloadVsixAsync(
            string vsixPath,
            IVsStatusbar? statusBar,
            CancellationToken cancellationToken)
        {
            await SetStatusAsync(statusBar, "Continue: downloading AI engine from VS Marketplace…");

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
            using (var response = await client.GetAsync(
                       VsixDownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var tmpPath = vsixPath + ".download";

                try
                {
                    using (var src = await response.Content.ReadAsStreamAsync())
                    using (var dst = new FileStream(tmpPath, FileMode.Create, FileAccess.Write,
                                                    FileShare.None, 81920, useAsync: true))
                    {
                        var buffer = new byte[81920];
                        long downloaded = 0;
                        int read;

                        while ((read = await src.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await dst.WriteAsync(buffer, 0, read, cancellationToken);
                            downloaded += read;

                            if (total > 0)
                            {
                                var pct = (int)(downloaded * 100 / total);
                                await SetStatusAsync(statusBar,
                                    $"Continue: downloading AI engine… {pct}%");
                            }
                        }
                    }

                    File.Move(tmpPath, vsixPath);
                }
                catch
                {
                    if (File.Exists(tmpPath)) File.Delete(tmpPath);
                    throw;
                }
            }
        }

        // -----------------------------------------------------------------
        // Extract
        // -----------------------------------------------------------------

        private static void ExtractBinaryFromVsix(string vsixPath, string binaryDestination)
        {
            using (var zip = ZipFile.OpenRead(vsixPath))
            {
                var entry = zip.GetEntry(BinaryEntryPath);
                if (entry == null)
                    throw new FileNotFoundException(
                        $"Could not find '{BinaryEntryPath}' inside the downloaded VSIX. " +
                        "The Continue package structure may have changed.");

                using (var src = entry.Open())
                using (var dst = new FileStream(binaryDestination, FileMode.Create,
                                                FileAccess.Write, FileShare.None))
                {
                    src.CopyTo(dst);
                }
            }
        }

        // -----------------------------------------------------------------
        // Status bar helper
        // -----------------------------------------------------------------

        private static async Task SetStatusAsync(IVsStatusbar? bar, string text)
        {
            if (bar == null) return;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            bar.SetText(text);
        }
    }
}


        /// <summary>
        /// Ensures the binary exists at <paramref name="destinationPath"/>, downloading
        /// it if necessary.  Progress is reported to the VS status bar when
        /// <paramref name="statusBar"/> is non-null.
        /// </summary>
        internal static async Task EnsureAsync(
            string destinationPath,
            IVsStatusbar? statusBar,
            CancellationToken cancellationToken)
        {
            if (File.Exists(destinationPath))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await SetStatusAsync(statusBar, "Continue: downloading AI engine…");

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var response = await client.GetAsync(
                       DownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var tmpPath = destinationPath + ".download";

                try
                {
                    using (var src = await response.Content.ReadAsStreamAsync())
                    using (var dst = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    {
                        var buffer = new byte[81920];
                        long downloaded = 0;
                        int read;

                        while ((read = await src.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await dst.WriteAsync(buffer, 0, read, cancellationToken);
                            downloaded += read;

                            if (total > 0)
                            {
                                var pct = (int)(downloaded * 100 / total);
                                await SetStatusAsync(statusBar, $"Continue: downloading AI engine… {pct}%");
                            }
                        }
                    }

                    // Atomic rename so we never leave a partial binary.
                    File.Move(tmpPath, destinationPath);
                }
                catch
                {
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                    throw;
                }
            }

            await SetStatusAsync(statusBar, "Continue: AI engine ready.");
        }

        private static async Task SetStatusAsync(IVsStatusbar? bar, string text)
        {
            if (bar == null)
                return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            bar.SetText(text);
        }
    }
}
