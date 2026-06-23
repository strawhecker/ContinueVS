using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
