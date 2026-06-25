using ContinueVS.IPC;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers
{
    /// <summary>
    /// Routes incoming WebView messages to the registered <see cref="IMessageHandler"/>
    /// for that message type.
    /// </summary>
    internal sealed class MessageDispatcher
    {
        private readonly Dictionary<string, IMessageHandler> _handlers =
            new Dictionary<string, IMessageHandler>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a handler for the given message type.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if a handler is already registered for <paramref name="messageType"/>.</exception>
        public void Register(string messageType, IMessageHandler handler)
        {
            if (_handlers.ContainsKey(messageType))
                throw new ArgumentException($"A handler is already registered for message type '{messageType}'.", nameof(messageType));

            _handlers[messageType] = handler;
        }

        /// <summary>
        /// Dispatches <paramref name="message"/> to the handler registered for its
        /// <see cref="Message.MessageType"/>. Unknown types are silently ignored.
        /// </summary>
        public Task DispatchAsync(Message message, CancellationToken cancellationToken)
        {
            if (_handlers.TryGetValue(message.MessageType, out var handler))
                return handler.HandleAsync(message, cancellationToken);

            return Task.CompletedTask;
        }
    }
}
