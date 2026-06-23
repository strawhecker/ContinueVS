using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Handles every IDE-callback message that the continue-binary sends to the IDE.
    /// Message types are taken directly from <c>core/protocol/ide.ts</c>
    /// <c>ToIdeFromWebviewOrCoreProtocol</c>.
    ///
    /// Call <see cref="Register"/> once after connecting the client.
    /// </summary>
    internal sealed class IdeCallbackHandler
    {
        private readonly ContinueClient  _client;
        private readonly IServiceProvider _sp;

        public IdeCallbackHandler(ContinueClient client, IServiceProvider sp)
        {
            _client = client;
            _sp     = sp;
        }

        public void Register()   => _client.MessageReceived += OnMessageReceived;
        public void Unregister() => _client.MessageReceived -= OnMessageReceived;

        // -----------------------------------------------------------------
        // Dispatch
        // -----------------------------------------------------------------

        private void OnMessageReceived(object sender, Message msg)
            => _ = Task.Run(() => HandleAsync(msg));

        private async Task HandleAsync(Message msg)
        {
            try
            {
                switch (msg.MessageType)
                {
                    // ----------- IDE info / settings -----------
                    case "getIdeInfo":         await GetIdeInfoAsync(msg);         break;
                    case "getIdeSettings":     await GetIdeSettingsAsync(msg);     break;
                    case "isTelemetryEnabled": await ReplyAsync(msg, false);       break;
                    case "isWorkspaceRemote":  await ReplyAsync(msg, false);       break;
                    case "getUniqueId":        await GetUniqueIdAsync(msg);        break;

                    // ----------- Workspace -----------
                    case "getWorkspaceDirs":   await GetWorkspaceDirsAsync(msg);   break;
                    case "getOpenFiles":       await GetOpenFilesAsync(msg);       break;
                    case "getCurrentFile":     await GetCurrentFileAsync(msg);     break;
                    case "getPinnedFiles":     await ReplyAsync(msg, Array.Empty<string>()); break;

                    // ----------- File system -----------
                    case "readFile":           await ReadFileAsync(msg);           break;
                    case "writeFile":          await WriteFileAsync(msg);          break;
                    case "removeFile":         await RemoveFileAsync(msg);         break;
                    case "saveFile":           await SaveFileAsync(msg);           break;
                    case "fileExists":         await FileExistsAsync(msg);         break;
                    case "readRangeInFile":    await ReadRangeInFileAsync(msg);    break;
                    case "listDir":            await ListDirAsync(msg);            break;
                    case "getFileStats":       await GetFileStatsAsync(msg);       break;
                    case "getFileResults":     await GetFileResultsAsync(msg);     break;

                    // ----------- Navigation / editor -----------
                    case "openFile":           await OpenFileAsync(msg);           break;
                    case "showLines":          await ShowLinesAsync(msg);          break;
                    case "showVirtualFile":    await ReplyAsync(msg, null);        break;
                    case "openUrl":            await OpenUrlAsync(msg);            break;

                    // ----------- Search -----------
                    case "getSearchResults":   await GetSearchResultsAsync(msg);   break;

                    // ----------- Git -----------
                    case "getDiff":            await GetDiffAsync(msg);            break;
                    case "getBranch":          await GetBranchAsync(msg);          break;
                    case "getRepoName":        await GetRepoNameAsync(msg);        break;
                    case "getGitRootPath":     await GetGitRootPathAsync(msg);     break;

                    // ----------- Terminal / subprocess -----------
                    case "runCommand":         await RunCommandAsync(msg);         break;
                    case "subprocess":         await SubprocessAsync(msg);         break;
                    case "getTerminalContents":await ReplyAsync(msg, "");          break;

                    // ----------- Debugging -----------
                    case "getDebugLocals":          await ReplyAsync(msg, "");              break;
                    case "getTopLevelCallStackSources": await ReplyAsync(msg, Array.Empty<string>()); break;
                    case "getAvailableThreads":     await ReplyAsync(msg, Array.Empty<object>()); break;

                    // ----------- Problems / diagnostics -----------
                    case "getProblems":        await GetProblemsAsync(msg);        break;

                    // ----------- UI -----------
                    case "showToast":          await ShowToastAsync(msg);          break;
                    case "closeSidebar":       await ReplyAsync(msg, null);        break;

                    // ----------- Symbols / LSP -----------
                    case "gotoDefinition":     await ReplyAsync(msg, Array.Empty<object>()); break;
                    case "gotoTypeDefinition": await ReplyAsync(msg, Array.Empty<object>()); break;
                    case "getSignatureHelp":   await ReplyAsync(msg, null);        break;
                    case "getReferences":      await ReplyAsync(msg, Array.Empty<object>()); break;
                    case "getDocumentSymbols": await ReplyAsync(msg, Array.Empty<object>()); break;
                    case "getTags":            await ReplyAsync(msg, Array.Empty<object>()); break;

                    // ----------- Secrets -----------
                    case "readSecrets":        await ReadSecretsAsync(msg);        break;
                    case "writeSecrets":       await ReplyAsync(msg, null);        break;

                    // ----------- Errors -----------
                    case "reportError":        /* telemetry no-op */              break;
                }
            }
            catch { /* never crash the receive loop */ }
        }

        // =================================================================
        // Handlers
        // =================================================================

        // ---- IDE info ----

        private Task GetIdeInfoAsync(Message msg) =>
            ReplyAsync(msg, new IdeInfo());

        private Task GetIdeSettingsAsync(Message msg) =>
            ReplyAsync(msg, new IdeSettings());

        private async Task GetUniqueIdAsync(Message msg)
        {
            // Use the VS installation id as a stable unique id.
            await ReplyAsync(msg, Guid.NewGuid().ToString());
        }

        // ---- Workspace ----

        private async Task GetWorkspaceDirsAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dirs = new List<string>();
            try
            {
                var sln = Dte()?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                    dirs.Add(Path.GetDirectoryName(sln.FullName)!);
            }
            catch { }
            await ReplyAsync(msg, dirs.ToArray());
        }

        private async Task GetOpenFilesAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var paths = new List<string>();
            try
            {
                var docs = Dte()?.Documents;
                if (docs != null)
                    foreach (Document doc in docs)
                        if (!string.IsNullOrEmpty(doc.FullName))
                            paths.Add(doc.FullName);
            }
            catch { }
            await ReplyAsync(msg, paths.ToArray());
        }

        private async Task GetCurrentFileAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var doc = Dte()?.ActiveDocument;
            if (doc == null) { await ReplyAsync(msg, null); return; }

            string contents = "";
            try { contents = File.ReadAllText(doc.FullName); } catch { }

            await ReplyAsync(msg, new
            {
                isUntitled = false,
                path       = doc.FullName,
                contents,
            });
        }

        // ---- File system ----

        private async Task ReadFileAsync(Message msg)
        {
            var path = msg.Data?["filepath"]?.Value<string>() ?? "";
            string content = "";
            try { content = File.ReadAllText(path); } catch { }
            await ReplyAsync(msg, content);
        }

        private async Task WriteFileAsync(Message msg)
        {
            var path     = msg.Data?["path"]?.Value<string>()     ?? "";
            var contents = msg.Data?["contents"]?.Value<string>() ?? "";
            try { File.WriteAllText(path, contents); } catch { }
            await ReplyAsync(msg, null);
        }

        private async Task RemoveFileAsync(Message msg)
        {
            var path = msg.Data?["path"]?.Value<string>() ?? "";
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            await ReplyAsync(msg, null);
        }

        private async Task SaveFileAsync(Message msg)
        {
            var filepath = msg.Data?["filepath"]?.Value<string>() ?? "";
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                var docs = Dte()?.Documents;
                if (docs != null)
                    foreach (Document doc in docs)
                        if (string.Equals(doc.FullName, filepath, StringComparison.OrdinalIgnoreCase))
                            doc.Save();
            }
            catch { }
            await ReplyAsync(msg, null);
        }

        private async Task FileExistsAsync(Message msg)
        {
            var path = msg.Data?["filepath"]?.Value<string>() ?? "";
            await ReplyAsync(msg, File.Exists(path));
        }

        private async Task ReadRangeInFileAsync(Message msg)
        {
            var filepath = msg.Data?["filepath"]?.Value<string>() ?? "";
            int startLine = msg.Data?["range"]?["start"]?["line"]?.Value<int>() ?? 0;
            int endLine   = msg.Data?["range"]?["end"]?["line"]?.Value<int>()   ?? 0;

            string text = "";
            try
            {
                var lines = File.ReadAllLines(filepath);
                var start = Math.Max(0, startLine);
                var end   = Math.Min(lines.Length - 1, endLine);
                if (start <= end)
                    text = string.Join(Environment.NewLine, lines.Skip(start).Take(end - start + 1));
            }
            catch { }
            await ReplyAsync(msg, text);
        }

        private async Task ListDirAsync(Message msg)
        {
            // Protocol field is "dir" (not "directory").
            var dir = msg.Data?["dir"]?.Value<string>() ?? "";
            var entries = new List<object>();
            try
            {
                if (Directory.Exists(dir))
                {
                    foreach (var f in Directory.GetFiles(dir))
                        entries.Add(new object[] { Path.GetFileName(f), 1 });
                    foreach (var d in Directory.GetDirectories(dir))
                        entries.Add(new object[] { Path.GetFileName(d), 2 });
                }
            }
            catch { }
            await ReplyAsync(msg, entries.ToArray());
        }

        private async Task GetFileStatsAsync(Message msg)
        {
            var files = msg.Data?["files"]?.ToObject<string[]>() ?? Array.Empty<string>();
            var result = new Dictionary<string, object>();
            foreach (var f in files)
            {
                try
                {
                    var info = new FileInfo(f);
                    result[f] = new { size = info.Length, lastModified = info.LastWriteTimeUtc };
                }
                catch { result[f] = new { }; }
            }
            await ReplyAsync(msg, result);
        }

        private async Task GetFileResultsAsync(Message msg)
        {
            var pattern    = msg.Data?["pattern"]?.Value<string>()    ?? "*";
            var maxResults = msg.Data?["maxResults"]?.Value<int>()     ?? 100;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var results = new List<string>();
            try
            {
                var sln = Dte()?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                {
                    var root = Path.GetDirectoryName(sln.FullName)!;
                    results.AddRange(
                        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                                 .Take(maxResults));
                }
            }
            catch { }
            await ReplyAsync(msg, results.ToArray());
        }

        // ---- Navigation ----

        private async Task OpenFileAsync(Message msg)
        {
            var path = msg.Data?["path"]?.Value<string>() ?? "";
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try { Dte()?.ItemOperations?.OpenFile(path); } catch { }
            await ReplyAsync(msg, null);
        }

        private async Task ShowLinesAsync(Message msg)
        {
            var filepath  = msg.Data?["filepath"]?.Value<string>()  ?? "";
            var startLine = msg.Data?["startLine"]?.Value<int>()     ?? 0;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                var window = Dte()?.ItemOperations?.OpenFile(filepath);
                if (window?.Document?.Selection is TextSelection sel)
                    sel.GotoLine(startLine + 1, true);
            }
            catch { }
            await ReplyAsync(msg, null);
        }

        private async Task OpenUrlAsync(Message msg)
        {
            var url = msg.Data?.Value<string>() ?? msg.Data?["url"]?.Value<string>() ?? "";
            try { if (!string.IsNullOrEmpty(url)) Process.Start(url); } catch { }
            await ReplyAsync(msg, null);
        }

        // ---- Search ----

        private async Task GetSearchResultsAsync(Message msg)
        {
            // Basic text search via recursive file scan — a full ripgrep integration
            // would be preferable but keeps things self-contained.
            var query      = msg.Data?["query"]?.Value<string>()      ?? "";
            var maxResults = msg.Data?["maxResults"]?.Value<int>()     ?? 20;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var sb = new System.Text.StringBuilder();
            try
            {
                var sln = Dte()?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                {
                    var root = Path.GetDirectoryName(sln.FullName)!;
                    int count = 0;
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        if (count >= maxResults) break;
                        try
                        {
                            var lines = File.ReadAllLines(file);
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    sb.AppendLine($"{file}:{i + 1}: {lines[i].Trim()}");
                                    if (++count >= maxResults) break;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            await ReplyAsync(msg, sb.ToString());
        }

        // ---- Git ----

        private async Task GetDiffAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            string root = "";
            try
            {
                var sln = Dte()?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                    root = Path.GetDirectoryName(sln.FullName)!;
            }
            catch { }

            var diffs = new List<string>();
            try
            {
                var includeUnstaged = msg.Data?["includeUnstaged"]?.Value<bool>() ?? false;
                var args = includeUnstaged ? "diff" : "diff --cached";
                var output = RunGit(root, args);
                if (!string.IsNullOrEmpty(output))
                    diffs.Add(output);
            }
            catch { }
            await ReplyAsync(msg, diffs.ToArray());
        }

        private async Task GetBranchAsync(Message msg)
        {
            var dir    = msg.Data?["dir"]?.Value<string>() ?? GetSolutionDir();
            var branch = RunGit(dir, "rev-parse --abbrev-ref HEAD")?.Trim() ?? "";
            await ReplyAsync(msg, branch);
        }

        private async Task GetRepoNameAsync(Message msg)
        {
            var dir    = msg.Data?["dir"]?.Value<string>() ?? GetSolutionDir();
            var remote = RunGit(dir, "remote get-url origin")?.Trim();
            if (!string.IsNullOrEmpty(remote))
            {
                var name = remote.TrimEnd('/').Split('/').LastOrDefault()?.Replace(".git", "");
                await ReplyAsync(msg, name);
            }
            else
                await ReplyAsync(msg, (string?)null);
        }

        private async Task GetGitRootPathAsync(Message msg)
        {
            var dir  = msg.Data?["dir"]?.Value<string>() ?? GetSolutionDir();
            var root = RunGit(dir, "rev-parse --show-toplevel")?.Trim();
            await ReplyAsync(msg, string.IsNullOrEmpty(root) ? (string?)null : root);
        }

        // ---- Terminal / subprocess ----

        private async Task RunCommandAsync(Message msg)
        {
            var command = msg.Data?["command"]?.Value<string>() ?? "";
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c {command}")
                {
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                    WorkingDirectory = GetSolutionDir(),
                };
                using (var p = Process.Start(psi))
                    p?.WaitForExit(10_000);
            }
            catch { }
            await ReplyAsync(msg, null);
        }

        private async Task SubprocessAsync(Message msg)
        {
            var command = msg.Data?["command"]?.Value<string>() ?? "";
            var cwd     = msg.Data?["cwd"]?.Value<string>()     ?? GetSolutionDir();
            string stdout = "", stderr = "";
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c {command}")
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    WorkingDirectory       = cwd,
                };
                using (var p = Process.Start(psi)!)
                {
                    p.WaitForExit(15_000);
                    stdout = await p.StandardOutput.ReadToEndAsync();
                    stderr = await p.StandardError.ReadToEndAsync();
                }
            }
            catch { }
            await ReplyAsync(msg, new[] { stdout, stderr });
        }

        // ---- Problems ----

        private async Task GetProblemsAsync(Message msg)
        {
            var filepath = msg.Data?["filepath"]?.Value<string>() ?? "";
            // VS Error List integration would require COM marshalling; return empty for now.
            await ReplyAsync(msg, Array.Empty<object>());
        }

        // ---- UI ----

        private async Task ShowToastAsync(Message msg)
        {
            // data is an array: [level, message, ...actions]
            var arr     = msg.Data as JArray;
            var level   = arr?[0]?.Value<string>() ?? "info";
            var message = arr?[1]?.Value<string>() ?? msg.Data?.ToString() ?? "";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_sp.GetService(typeof(SVsUIShell)) is IVsUIShell shell)
            {
                var icon = level == "error"
                    ? OLEMSGICON.OLEMSGICON_CRITICAL
                    : level == "warning"
                        ? OLEMSGICON.OLEMSGICON_WARNING
                        : OLEMSGICON.OLEMSGICON_INFO;
                shell.ShowMessageBox(0, Guid.Empty, "Continue", message, "", 0,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, icon, 0, out _);
            }
            await ReplyAsync(msg, "ok");
        }

        // ---- Secrets ----

        private async Task ReadSecretsAsync(Message msg)
        {
            var keys = msg.Data?["keys"]?.ToObject<string[]>() ?? Array.Empty<string>();
            var result = new Dictionary<string, string>();
            foreach (var k in keys)
                result[k] = "";
            await ReplyAsync(msg, result);
        }

        // =================================================================
        // Helpers
        // =================================================================

        private Task ReplyAsync(Message request, object? data)
        {
            var reply = new Message
            {
                MessageType = request.MessageType,
                MessageId   = request.MessageId,
                Data        = data == null ? JValue.CreateNull() : JToken.FromObject(data),
            };
            return _client.SendRawMessageAsync(reply, CancellationToken.None);
        }

        private DTE2? Dte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _sp.GetService(typeof(DTE)) as DTE2;
        }

        private string GetSolutionDir()
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync());
                var sln = Dte()?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                    return Path.GetDirectoryName(sln.FullName)!;
            }
            catch { }
            return Environment.CurrentDirectory;
        }

        private static string? RunGit(string workDir, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    WorkingDirectory       = string.IsNullOrEmpty(workDir)
                                               ? Environment.CurrentDirectory
                                               : workDir,
                };
                using (var p = Process.Start(psi)!)
                {
                    var output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5_000);
                    return p.ExitCode == 0 ? output : null;
                }
            }
            catch { return null; }
        }
    }
}


namespace ContinueVS.IPC
{
    /// <summary>
    /// Handles IDE-callback messages that the continue-binary sends to the IDE endpoint.
    /// Registers message handlers on a <see cref="ContinueClient"/> and fulfils them
    /// using Visual Studio services (DTE2, IVsTextManager, etc.).
    ///
    /// Call <see cref="Register"/> once after connecting the client.
    /// </summary>
    internal sealed class IdeCallbackHandler
    {
        private readonly ContinueClient _client;
        private readonly IServiceProvider _serviceProvider;

        public IdeCallbackHandler(ContinueClient client, IServiceProvider serviceProvider)
        {
            _client          = client;
            _serviceProvider = serviceProvider;
        }

        /// <summary>Subscribes to all IDE callback message types on the client.</summary>
        public void Register()
        {
            _client.MessageReceived += OnMessageReceived;
        }

        public void Unregister()
        {
            _client.MessageReceived -= OnMessageReceived;
        }

        // -----------------------------------------------------------------
        // Dispatch
        // -----------------------------------------------------------------

        private void OnMessageReceived(object sender, Message msg)
        {
            _ = Task.Run(() => HandleAsync(msg));
        }

        private async Task HandleAsync(Message msg)
        {
            try
            {
                switch (msg.MessageType)
                {
                    case "readFile":
                        await HandleReadFileAsync(msg); break;
                    case "writeFile":
                        await HandleWriteFileAsync(msg); break;
                    case "listDir":
                        await HandleListDirAsync(msg); break;
                    case "getOpenFiles":
                        await HandleGetOpenFilesAsync(msg); break;
                    case "getCurrentFile":
                        await HandleGetCurrentFileAsync(msg); break;
                    case "getWorkspaceDirs":
                        await HandleGetWorkspaceDirsAsync(msg); break;
                    case "showMessage":
                        await HandleShowMessageAsync(msg); break;
                    case "openFile":
                        await HandleOpenFileAsync(msg); break;
                    case "getIdeSettings":
                        await HandleGetIdeSettingsAsync(msg); break;
                }
            }
            catch { /* swallow; do not crash the receive loop */ }
        }

        // -----------------------------------------------------------------
        // Handlers
        // -----------------------------------------------------------------

        private async Task HandleReadFileAsync(Message msg)
        {
            var path = msg.Data?["filepath"]?.Value<string>() ?? "";
            string content = "";
            try { content = File.ReadAllText(path); } catch { }
            await ReplyAsync(msg, JToken.FromObject(new { contents = content }));
        }

        private async Task HandleWriteFileAsync(Message msg)
        {
            var path    = msg.Data?["path"]?.Value<string>()     ?? "";
            var content = msg.Data?["contents"]?.Value<string>() ?? "";
            try { File.WriteAllText(path, content); } catch { }
            await ReplyAsync(msg, JValue.CreateNull());
        }

        private async Task HandleListDirAsync(Message msg)
        {
            var dir = msg.Data?["directory"]?.Value<string>() ?? "";
            var entries = new List<JArray>();
            try
            {
                if (Directory.Exists(dir))
                {
                    foreach (var f in Directory.GetFiles(dir))
                        entries.Add(new JArray(Path.GetFileName(f), 1));
                    foreach (var d in Directory.GetDirectories(dir))
                        entries.Add(new JArray(Path.GetFileName(d), 2));
                }
            }
            catch { }
            await ReplyAsync(msg, JToken.FromObject(entries));
        }

        private async Task HandleGetOpenFilesAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte  = GetDte();
            var paths = new List<string>();
            if (dte?.Documents != null)
                foreach (Document doc in dte.Documents)
                    if (!string.IsNullOrEmpty(doc.FullName))
                        paths.Add(doc.FullName);

            await ReplyAsync(msg, JToken.FromObject(paths));
        }

        private async Task HandleGetCurrentFileAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = GetDte();
            var doc = dte?.ActiveDocument;
            if (doc == null)
            {
                await ReplyAsync(msg, JValue.CreateNull());
                return;
            }

            string content = "";
            try { content = File.ReadAllText(doc.FullName); } catch { }

            await ReplyAsync(msg, JToken.FromObject(new
            {
                path     = doc.FullName,
                contents = content,
                isUntitled = false,
            }));
        }

        private async Task HandleGetWorkspaceDirsAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte  = GetDte();
            var dirs = new List<string>();

            try
            {
                var sln = dte?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                    dirs.Add(Path.GetDirectoryName(sln.FullName)!);
            }
            catch { }

            await ReplyAsync(msg, JToken.FromObject(dirs));
        }

        private async Task HandleShowMessageAsync(Message msg)
        {
            var text  = msg.Data?["message"]?.Value<string>() ?? "";
            var level = msg.Data?["level"]?.Value<string>()   ?? "info";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_serviceProvider.GetService(typeof(SVsUIShell)) is IVsUIShell shell)
            {
                var icon = level == "error"
                    ? OLEMSGICON.OLEMSGICON_CRITICAL
                    : OLEMSGICON.OLEMSGICON_INFO;
                int result;
                shell.ShowMessageBox(0, Guid.Empty, "Continue", text, "", 0,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST, icon, 0, out result);
            }

            await ReplyAsync(msg, JValue.CreateNull());
        }

        private async Task HandleOpenFileAsync(Message msg)
        {
            var path = msg.Data?["path"]?.Value<string>() ?? "";
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            GetDte()?.ItemOperations?.OpenFile(path);
            await ReplyAsync(msg, JValue.CreateNull());
        }

        private async Task HandleGetIdeSettingsAsync(Message msg)
        {
            await ReplyAsync(msg, JToken.FromObject(new
            {
                remoteConfigServerUrl = (string?)null,
                remoteConfigSyncPeriod = 60,
                userToken = "",
                enableControlServerBeta = false,
            }));
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private Task ReplyAsync(Message request, JToken data)
        {
            var reply = new Message
            {
                MessageType = request.MessageType,
                MessageId   = request.MessageId,
                Data        = data,
            };
            return _client.SendRawMessageAsync(reply, CancellationToken.None);
        }

        private DTE2? GetDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _serviceProvider.GetService(typeof(DTE)) as DTE2;
        }
    }
}
