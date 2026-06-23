using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.IPC
{
    /// <summary>
    /// WebSocket client that connects to the continue-binary's IDE endpoint
    /// (<c>ws://localhost:{port}/ide</c>) and implements the Continue message protocol.
    ///
    /// Usage:
    ///   1. Subscribe to <see cref="MessageReceived"/> before calling <see cref="ConnectAsync"/>.
    ///   2. Call <see cref="SendAsync"/> to push messages to the binary.
    ///   3. Call <see cref="SendRequestAsync{T}"/> for request/response correlation.
    /// </summary>
    internal sealed class ContinueClient : IDisposable
    {
        // -----------------------------------------------------------------
        // Fields
        // -----------------------------------------------------------------
        private ClientWebSocket?    _ws;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim   _sendLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JToken?>> _pending
            = new ConcurrentDictionary<string, TaskCompletionSource<JToken?>>();

        private bool _disposed;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Fires for every inbound message from the binary.</summary>
        public event EventHandler<Message>? MessageReceived;

        /// <summary>Fires when the socket disconnects unexpectedly.</summary>
        public event EventHandler? Disconnected;

        /// <summary>Whether the WebSocket connection is currently open.</summary>
        public bool IsConnected => _ws?.State == WebSocketState.Open;

        /// <summary>
        /// Opens the WebSocket connection to the binary and begins the receive loop.
        /// Throws if the connection cannot be established within the timeout.
        /// </summary>
        public async Task ConnectAsync(int port, CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ws  = new ClientWebSocket();

            var uri = new Uri($"ws://localhost:{port}/ide");
            await _ws.ConnectAsync(uri, _cts.Token);

            // Send IDE info handshake.
            await SendAsync("getIdeInfo", new IdeInfo(), _cts.Token);

            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        }

        /// <summary>Sends a fire-and-forget message to the binary.</summary>
        public async Task SendAsync(string messageType, object data, CancellationToken cancellationToken)
        {
            var msg = new Message
            {
                MessageType = messageType,
                MessageId   = Guid.NewGuid().ToString(),
                Data        = JToken.FromObject(data),
            };
            await SendRawAsync(msg, cancellationToken);
        }

        /// <summary>
        /// Sends a pre-built message as-is (preserving the caller-supplied MessageId).
        /// Used by <see cref="IdeCallbackHandler"/> to reply with the original request ID.
        /// </summary>
        internal async Task SendRawMessageAsync(Message msg, CancellationToken cancellationToken)
        {
            await SendRawAsync(msg, cancellationToken);
        }

        /// <summary>
        /// Sends a request and waits for the binary to echo a response with the same
        /// <c>messageId</c>.  Returns the response data token.
        /// </summary>
        public async Task<JToken?> SendRequestAsync(
            string messageType,
            object data,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            var id  = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<JToken?>();
            _pending[id] = tcs;

            cancellationToken.Register(() => tcs.TrySetCanceled());

            var msg = new Message { MessageType = messageType, MessageId = id, Data = JToken.FromObject(data) };
            await SendRawAsync(msg, cancellationToken);

            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
            using (var cts = new CancellationTokenSource(effectiveTimeout))
                cts.Token.Register(() => tcs.TrySetCanceled());

            try
            {
                return await tcs.Task;
            }
            finally
            {
                _pending.TryRemove(id, out _);
            }
        }

        // -----------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------

        private async Task SendRawAsync(Message msg, CancellationToken cancellationToken)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not connected.");

            var json  = JsonConvert.SerializeObject(msg);
            var bytes = Encoding.UTF8.GetBytes(json);
            var seg   = new ArraySegment<byte>(bytes);

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await _ws.SendAsync(seg, WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[65536];

            try
            {
                while (!cancellationToken.IsCancellationRequested
                       && _ws!.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        var seg = new ArraySegment<byte>(buffer);
                        result = await _ws.ReceiveAsync(seg, cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            Disconnected?.Invoke(this, EventArgs.Empty);
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    DispatchMessage(sb.ToString());
                }
            }
            catch (OperationCanceledException) { /* expected on shutdown */ }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                    Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DispatchMessage(string json)
        {
            Message? msg;
            try
            {
                msg = JsonConvert.DeserializeObject<Message>(json);
            }
            catch
            {
                return;
            }

            if (msg == null) return;

            // Complete any waiting request.
            if (!string.IsNullOrEmpty(msg.MessageId)
                && _pending.TryGetValue(msg.MessageId, out var tcs))
            {
                tcs.TrySetResult(msg.Data);
            }

            MessageReceived?.Invoke(this, msg);
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts?.Cancel();
            try { _ws?.Dispose(); } catch { /* best-effort */ }
            _sendLock.Dispose();
            _cts?.Dispose();
        }
    }
}
