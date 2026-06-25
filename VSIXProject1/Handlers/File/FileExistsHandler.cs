using ContinueVS.IPC;
using ContinueVS.UI;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.File
{
    internal sealed class FileExistsHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public FileExistsHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var filepath = message.Data?.Value<string>("filepath");
            bool exists = !string.IsNullOrEmpty(filepath) && System.IO.File.Exists(filepath);

            _control.SendReplyToGui(message.MessageType, message.MessageId, exists);
            return Task.CompletedTask;
        }
    }
}
