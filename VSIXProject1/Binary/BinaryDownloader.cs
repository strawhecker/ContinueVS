using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Binary
{
    /// <summary>
    /// Downloads the continue-binary executable from the GitHub release if it is not
    /// already present in the local cache directory.
    /// </summary>
    internal static class BinaryDownloader
    {
        // Final v2.0.0 release asset on GitHub.
        private const string DownloadUrl =
            "https://github.com/continuedev/continue/releases/download/v2.0.0-vscode/continue-binary-win32-x64.exe";

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
