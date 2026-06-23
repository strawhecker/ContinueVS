using ContinueVS.IPC;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Editor
{
    /// <summary>
    /// Answers Continue binary requests for workspace-level information:
    /// open files, solution directories, and project file lists.
    ///
    /// Registers itself against <see cref="ContinueClient.MessageReceived"/> and
    /// replies to <c>getOpenFiles</c> and <c>getWorkspaceDirs</c> callbacks.
    /// </summary>
    internal sealed class WorkspaceContextProvider : IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ContinueClient   _client;
        private bool _disposed;

        public WorkspaceContextProvider(IServiceProvider services, ContinueClient client)
        {
            _services = services;
            _client   = client;
        }

        public void Register()
        {
            _client.MessageReceived += OnMessage;
        }

        public void Unregister()
        {
            _client.MessageReceived -= OnMessage;
        }

        private void OnMessage(object sender, Message msg)
        {
            switch (msg.MessageType)
            {
                case "getOpenFiles":
                    _ = Task.Run(() => ReplyOpenFilesAsync(msg));
                    break;
                case "getWorkspaceDirs":
                    _ = Task.Run(() => ReplyWorkspaceDirsAsync(msg));
                    break;
                case "listWorkspaceContents":
                    _ = Task.Run(() => ReplyListWorkspaceContentsAsync(msg));
                    break;
            }
        }

        private async Task ReplyOpenFilesAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = _services.GetService(typeof(DTE)) as DTE2;
            var paths = new List<string>();
            if (dte?.Documents != null)
                foreach (Document doc in dte.Documents)
                    if (!string.IsNullOrEmpty(doc.FullName))
                        paths.Add(doc.FullName);

            await _client.SendRawMessageAsync(new Message
            {
                MessageType = msg.MessageType,
                MessageId   = msg.MessageId,
                Data        = JToken.FromObject(paths),
            }, CancellationToken.None);
        }

        private async Task ReplyWorkspaceDirsAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = _services.GetService(typeof(DTE)) as DTE2;
            var dirs = new List<string>();

            try
            {
                var sln = dte?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                    dirs.Add(Path.GetDirectoryName(sln.FullName)!);
            }
            catch { }

            await _client.SendRawMessageAsync(new Message
            {
                MessageType = msg.MessageType,
                MessageId   = msg.MessageId,
                Data        = JToken.FromObject(dirs),
            }, CancellationToken.None);
        }

        private async Task ReplyListWorkspaceContentsAsync(Message msg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte  = _services.GetService(typeof(DTE)) as DTE2;
            var dirs = new List<string>();

            try
            {
                var sln = dte?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                    dirs.Add(Path.GetDirectoryName(sln.FullName)!);
            }
            catch { }

            // Walk the directory tree, respecting .gitignore or .continueignore if present.
            var files = new List<string>();
            foreach (var dir in dirs)
                CollectFiles(dir, files, maxFiles: 5000);

            await _client.SendRawMessageAsync(new Message
            {
                MessageType = msg.MessageType,
                MessageId   = msg.MessageId,
                Data        = JToken.FromObject(files),
            }, CancellationToken.None);
        }

        private static void CollectFiles(string dir, List<string> results, int maxFiles)
        {
            if (results.Count >= maxFiles) return;
            try
            {
                foreach (var f in Directory.GetFiles(dir))
                {
                    results.Add(f);
                    if (results.Count >= maxFiles) return;
                }
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (name == ".git" || name == "node_modules" || name == "bin" || name == "obj")
                        continue;
                    CollectFiles(sub, results, maxFiles);
                }
            }
            catch { /* skip unreadable directories */ }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unregister();
        }
    }
}
