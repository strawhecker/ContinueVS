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

            // VSTHRD003: Intentionally awaiting a TaskCompletionSource task; it is
            // completed by the receive-loop on a background thread and the result is
            // needed here.  ConfigureAwait(false) opts out of the sync-context capture.
#pragma warning disable VSTHRD003
            try     { return await req.Tcs.Task.ConfigureAwait(false); }
#pragma warning restore VSTHRD003
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
                await Task.Run(() => _process.StandardInput.Flush()); // VSTHRD003: avoid FlushAsync on foreign StreamWriter
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


