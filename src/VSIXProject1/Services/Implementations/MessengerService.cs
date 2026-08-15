#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Stub implementation of IMessengerService used until the Node.js bridge is wired (gap4).
    /// All streaming calls return an empty sequence; handlers are stored but not dispatched.
    /// </summary>
    public class MessengerService : IMessengerService
    {
        public Task<TResponse> RequestAsync<TRequest, TResponse>(
            string messageType,
            TRequest data,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));

            return Task.FromResult<TResponse>(default!);
        }

        public void Send<TData>(string messageType, TData data)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));
        }

        public void On<TData, TResponse>(
            string messageType,
            Func<TData, Task<TResponse>> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));
        }

        public async IAsyncEnumerable<TChunk> StreamAsync<TRequest, TChunk>(
            string messageType,
            TRequest data,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType cannot be empty", nameof(messageType));

            await Task.CompletedTask;
            yield break;
        }
    }
}
