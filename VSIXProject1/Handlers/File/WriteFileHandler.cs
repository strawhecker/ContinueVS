using ContinueVS.IPC;
using ContinueVS.UI;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.File
{
    internal sealed class WriteFileHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public WriteFileHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var path     = message.Data?.Value<string>("path");
            var contents = message.Data?.Value<string>("contents");

            if (string.IsNullOrWhiteSpace(path))
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
                return Task.CompletedTask;
            }

            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, contents ?? "", Encoding.UTF8);
            }
            catch (Exception)
            {
                // swallow silently
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
            return Task.CompletedTask;
        }
    }
}
