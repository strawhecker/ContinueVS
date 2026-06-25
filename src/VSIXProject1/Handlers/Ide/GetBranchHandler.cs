using ContinueVS.IPC;
using ContinueVS.UI;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class GetBranchHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public GetBranchHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var dir = message.Data?.Value<string>("dir");

            if (string.IsNullOrWhiteSpace(dir))
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, "");
                return;
            }

            var branch = await Task.Run(() => RunGitBranch(dir), cancellationToken);
            _control.SendReplyToGui(message.MessageType, message.MessageId, branch);
        }

        private static string RunGitBranch(string dir)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
                {
                    WorkingDirectory       = dir,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow         = true,
                };

                using (var process = Process.Start(psi))
                {
                    process.WaitForExit(5000);
                    return process.StandardOutput.ReadToEnd().Trim();
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
