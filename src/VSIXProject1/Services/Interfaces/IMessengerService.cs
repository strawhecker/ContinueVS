using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for message routing and communication.
    /// Handles request/response, fire-and-forget, and streaming message patterns.
    /// </summary>
    public interface IMessengerService
    {
        /// <summary>
        /// Sends a request and waits for a response.
        /// </summary>
        /// <typeparam name="TRequest">Type of the request data.</typeparam>
        /// <typeparam name="TResponse">Type of the response data.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="data">The request data.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The response data.</returns>
        Task<TResponse> RequestAsync<TRequest, TResponse>(
            string messageType,
            TRequest data,
            CancellationToken ct = default);

        /// <summary>
        /// Sends a message without expecting a response.
        /// </summary>
        /// <typeparam name="TData">Type of the message data.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="data">The message data.</param>
        void Send<TData>(string messageType, TData data);

        /// <summary>
        /// Registers a handler for a message type.
        /// </summary>
        /// <typeparam name="TData">Type of incoming message data.</typeparam>
        /// <typeparam name="TResponse">Type of response data.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="handler">The handler function that processes the message and returns a response.</param>
        void On<TData, TResponse>(
            string messageType,
            Func<TData, Task<TResponse>> handler);

        /// <summary>
        /// Streams messages (chunked responses).
        /// </summary>
        /// <typeparam name="TRequest">Type of the request data.</typeparam>
        /// <typeparam name="TChunk">Type of each chunk in the stream.</typeparam>
        /// <param name="messageType">The type/name of the message.</param>
        /// <param name="data">The request data.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An async enumerable of chunks.</returns>
        IAsyncEnumerable<TChunk> StreamAsync<TRequest, TChunk>(
            string messageType,
            TRequest data,
            CancellationToken ct = default);
    }
}
