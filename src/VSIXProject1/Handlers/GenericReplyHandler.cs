using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers
{
    /// <summary>
    /// A generic handler that replies with a fixed payload. Useful for stub/placeholder handlers.
    /// </summary>
    internal sealed class GenericReplyHandler : IMessageHandler
    {
        private readonly IGuiReplyProvider _guiReply;
        private readonly object _response;

        public GenericReplyHandler(IGuiReplyProvider guiReply, object response)
        {
            _guiReply = guiReply;
            _response = response;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            _guiReply.SendReplyToGui(message.MessageType, message.MessageId, _response);
            return Task.CompletedTask;
        }
    }
}
