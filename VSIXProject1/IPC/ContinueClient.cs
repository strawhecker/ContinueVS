using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Stdio IPC client for the continue-binary.
    ///
    /// The binary uses stdin/stdout for all communication.  Messages are
    /// newline-delimited JSON: <c>{"messageType":"…","messageId":"…","data":{…}}\r\n</c>
    ///
    /// Responses from the binary to our requests are wrapped:
    ///   <c>{ "done": bool, "content": T, "status": "success"|"error" }</c>
    ///
    /// IDE-side callbacks (binary → us) carry the raw data directly.
    /// We reply to them with the same messageId and the raw response value.
    /// </summary>
    internal sealed class ContinueClient : IDisposable
    {
        // -----------------------------------------------------------------
        // Fields
        // -----------------------------------------------------------------
        private Process?  _process;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        private readonly ConcurrentDictionary<string, PendingRequest> _pending
            = new ConcurrentDictionary<string, PendingRequest>();

        private bool _disposed;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Fires for every inbound message from the binary.</summary>
        public event EventHandler<Message>? MessageReceived;

        /// <summary>Fires when the binary process exits or its stdout closes.</summary>
        public event EventHandler? Disconnected;

        /// <summary>Whether the process is running and stdin is open.</summary>
        public bool IsConnected =>
            _process != null && !_process.HasExited && !_disposed;

        /// <summary>
        /// Attaches to an already-running <paramref name="process"/> and starts
        /// the stdout receive loop.
        /// </summary>
        public void Connect(Process process, CancellationToken cancellationToken)
        {
            _process = process;
            _cts     = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
        /// Sends a pre-built message with a caller-supplied MessageId.
        /// Used by <see cref="IdeCallbackHandler"/> to reply to binary callbacks.
        /// </summary>
        internal async Task SendRawMessageAsync(Message msg, CancellationToken cancellationToken)
        {
            await SendRawAsync(msg, cancellationToken);
        }

        /// <summary>
        /// Sends a request and waits for the binary's response
        /// (which wraps the payload in <c>{ done, content, status }</c>).
        /// Returns the inner <c>content</c> token when <c>done == true</c>.
        /// </summary>
        public async Task<JToken?> SendRequestAsync(
            string messageType,
            object data,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            var id  = Guid.NewGuid().ToString();
            var req = new PendingRequest();
            _pending[id] = req;

            cancellationToken.Register(() => req.Tcs.TrySetCanceled());

            var msg = new Message
            {
                MessageType = messageType,
                MessageId   = id,
                Data        = JToken.FromObject(data),
            };
            await SendRawAsync(msg, cancellationToken);

            using (var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30)))
                timeoutCts.Token.Register(() => req.Tcs.TrySetCanceled());

            try     { return await req.Tcs.Task; }
            finally { _pending.TryRemove(id, out _); }
        }

        // -----------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------

        private async Task SendRawAsync(Message msg, CancellationToken cancellationToken)
        {
            if (_process == null || _process.HasExited)
                throw new InvalidOperationException("Binary process is not running.");

            var json = JsonConvert.SerializeObject(msg) + "\r\n";

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await _process.StandardInput.WriteAsync(json);
                await _process.StandardInput.FlushAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                string? line;
                while (!cancellationToken.IsCancellationRequested
                       && (line = await _process!.StandardOutput.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        DispatchLine(line);
                }
            }
            catch (OperationCanceledException) { /* expected on shutdown */ }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                    Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DispatchLine(string json)
        {
            Message? msg;
            try { msg = JsonConvert.DeserializeObject<Message>(json); }
            catch { return; }

            if (msg == null) return;

            // Raise for all subscribers (IdeCallbackHandler, WorkspaceContextProvider, etc.)
            MessageReceived?.Invoke(this, msg);

            // Check if this resolves a pending request.
            if (!string.IsNullOrEmpty(msg.MessageId)
                && _pending.TryGetValue(msg.MessageId, out var req))
            {
                // Binary wraps responses: { done: bool, content: T, status: "success"|"error" }
                var done    = msg.Data?["done"]?.Value<bool>()    ?? true;
                var status  = msg.Data?["status"]?.Value<string>() ?? "success";
                var content = msg.Data?["content"];

                if (status == "error")
                {
                    var err = msg.Data?["error"]?.Value<string>() ?? "Unknown error";
                    req.Tcs.TrySetException(new InvalidOperationException(err));
                }
                else if (done)
                {
                    req.Tcs.TrySetResult(content);
                }
                else
                {
                    req.Chunks.Add(content);
                }
            }
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
            _sendLock.Dispose();
            _cts?.Dispose();
        }

        // -----------------------------------------------------------------
        // Helper types
        // -----------------------------------------------------------------

        private sealed class PendingRequest
        {
            public TaskCompletionSource<JToken?> Tcs { get; }
                = new TaskCompletionSource<JToken?>();
            public List<JToken?> Chunks { get; } = new List<JToken?>();
        }
    }
}


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
