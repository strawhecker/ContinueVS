using ContinueVS.IPC;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers
{
    /// <summary>
    /// Handles a single incoming WebView message type.
    /// </summary>
    internal interface IMessageHandler
    {
        Task HandleAsync(Message message, CancellationToken cancellationToken);
    }
}
