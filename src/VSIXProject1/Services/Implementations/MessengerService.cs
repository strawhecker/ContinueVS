using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Handlers;
using ContinueVS.IPC;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IMessengerService that manages message routing and communication.
    /// Wraps the existing MessageDispatcher to provide generic request/response, fire-and-forget,
    /// and streaming message patterns.
    /// </summary>
    public class MessengerService : IMessengerService
    {
        private readonly MessageDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of MessengerService.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="telemetry">Optional telemetry collector for metrics.</param>
        public MessengerService(
            IBridgeLogger? logger = null,
            IBridgeTelemetryCollector? telemetry = null)
        {
            _dispatcher = new MessageDispatcher(logger, telemetry);
        }

        /// <summary>
        /// Sends a request and waits for a response.
        /// </summary>
        /// <typeparam name="TRequest">Type of the request data.</typeparam>
        /// <typeparam name="TResponse">Type of the response data.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="data">The request data.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The response data.</returns>
        /// <exception cref="ArgumentNullException">Thrown if messageType is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if serialization fails or no handler is registered.</exception>
        public async Task<TResponse> RequestAsync<TRequest, TResponse>(
            string messageType,
            TRequest data,
            CancellationToken ct = default)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            // Serialize request data to JToken
            JToken requestData;
            try
            {
                requestData = JToken.FromObject(data ?? new object());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize request data for message type '{messageType}'.",
                    ex);
            }

            // Create and dispatch message
            var message = new Message
            {
                MessageType = messageType,
                MessageId = Guid.NewGuid().ToString(),
                Data = requestData
            };

            try
            {
                await _dispatcher.DispatchAsync(message, ct);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Dispatch failed for message type '{messageType}'.",
                    ex);
            }

            // Deserialize response
            // Note: Current MessageDispatcher implementation does not return response data.
            // This is a stub that returns default(TResponse) until response routing is implemented.
            return default!;
        }

        /// <summary>
        /// Sends a message without expecting a response.
        /// </summary>
        /// <typeparam name="TData">Type of the message data.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="data">The message data.</param>
        public void Send<TData>(string messageType, TData data)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            // Serialize data to JToken
            JToken messageData;
            try
            {
                messageData = JToken.FromObject(data ?? new object());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Failed to serialize message data for type '{messageType}': {ex.Message}");
                return;
            }

            // Create message
            var message = new Message
            {
                MessageType = messageType,
                MessageId = Guid.NewGuid().ToString(),
                Data = messageData
            };

            // Fire-and-forget: dispatch without awaiting response
            _ = Task.Run(async () =>
            {
                try
                {
                    await _dispatcher.DispatchAsync(message, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Fire-and-forget dispatch error for message type '{messageType}': {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Registers a handler for a message type.
        /// </summary>
        /// <typeparam name="TData">Type of incoming message data.</typeparam>
        /// <typeparam name="TResponse">Type of response data.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="handler">The handler function that processes the message and returns a response.</param>
        /// <exception cref="ArgumentNullException">Thrown if messageType or handler is null.</exception>
        /// <exception cref="ArgumentException">Thrown if a handler is already registered for this message type.</exception>
        public void On<TData, TResponse>(
            string messageType,
            Func<TData, Task<TResponse>> handler)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            // Create adapter that wraps the generic handler in IMessageHandler
            var adapter = new GenericMessageHandlerAdapter<TData, TResponse>(handler);

            // Register with dispatcher
            _dispatcher.Register(messageType, adapter);
        }

        /// <summary>
        /// Streams messages (chunked responses).
        /// </summary>
        /// <typeparam name="TRequest">Type of the request data.</typeparam>
        /// <typeparam name="TChunk">Type of each chunk in the stream.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="data">The request data.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An async enumerable of chunks.</returns>
        public async IAsyncEnumerable<TChunk> StreamAsync<TRequest, TChunk>(
            string messageType,
            TRequest data,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            // TODO: Implement streaming support
            // Current MessageDispatcher does not support streaming response collection.
            // This stub returns an empty stream until streaming channels are wired.
            await Task.CompletedTask;
            yield break;
        }

        /// <summary>
        /// Adapter that wraps a generic async handler in the IMessageHandler interface.
        /// Handles deserialization of incoming message data and serialization of response data.
        /// </summary>
        private sealed class GenericMessageHandlerAdapter<TData, TResponse> : IMessageHandler
        {
            private readonly Func<TData, Task<TResponse>> _handler;

            public GenericMessageHandlerAdapter(Func<TData, Task<TResponse>> handler)
            {
                _handler = handler;
            }

            public async Task HandleAsync(Message message, CancellationToken cancellationToken)
            {
                try
                {
                    // Deserialize incoming message data to TData
                    TData? deserializedData = default;
                    if (message.Data != null)
                    {
                        try
                        {
                            deserializedData = message.Data.ToObject<TData>();
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                $"Failed to deserialize message data for type '{typeof(TData).Name}'.",
                                ex);
                        }
                    }

                    // Invoke the generic handler
                    var response = await _handler(deserializedData!);

                    // TODO: Send response back through message channel
                    // Current implementation does not route responses back to sender.
                    // Response routing will be implemented in a follow-up step.

                    await Task.CompletedTask;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Handler invocation failed for message type '{message.MessageType}'.",
                        ex);
                }
            }
        }
    }
}
