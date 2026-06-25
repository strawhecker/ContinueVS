using ContinueVS.IPC;
using ContinueVS.UI;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.File
{
    internal sealed class ReadFileHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ReadFileHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var filepath = message.Data?.Value<string>("filepath");

            if (string.IsNullOrEmpty(filepath))
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, "");
                return Task.CompletedTask;
            }

            string contents;
            try
            {
                contents = System.IO.File.ReadAllText(filepath, Encoding.UTF8);
            }
            catch (IOException)
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, "");
                return Task.CompletedTask;
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, contents);
            return Task.CompletedTask;
        }
    }
}
