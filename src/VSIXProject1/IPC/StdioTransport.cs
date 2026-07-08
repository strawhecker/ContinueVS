using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Implements IBridgeTransport for stdio-based communication with the Continue npm bridge.
    /// 
    /// Coordinates:
    /// - ProcessManager for process lifecycle
    /// - MessageBufferer for stdout buffering and deserialization
    /// - Sender loop for stdin serialization with ordered delivery
    /// - Event dispatch for subscribers
    /// 
    /// Threading model:
    /// - StartAsync/StopAsync must be called from a single thread (typically the UI/main thread)
    /// - SendMessageAsync can be called from multiple threads (serialized by _sendSemaphore)
    /// - ReceiveMessageAsync can be called from multiple threads (queued via MessageBufferer)
    /// - Events fire on background threads; subscribers must not block
    /// </summary>
    internal sealed class StdioTransport : IBridgeTransport
    {
        private readonly IBridgeConfiguration _configuration;
        private ProcessManager _processManager;
        private MessageBufferer _messageBufferer;
        private Task _receiveLoopTask;
        private volatile bool _isRunning;
        private bool _isDisposed;
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);

        // Events
        public event EventHandler<MessageReceivedEventArgs> OnMessageReceived;
        public event EventHandler<BridgeErrorEventArgs> OnError;
        public event EventHandler OnClosed;

        public bool IsRunning => _isRunning;

        public StdioTransport(IBridgeConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _isRunning = false;
        }

        /// <summary>
        /// Starts the Continue process and initializes communication channels.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return Task.CompletedTask; // Idempotent

            return Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _processManager = new ProcessManager(_configuration);
                    var process = _processManager.Start();

                    // Initialize message bufferer
                    _messageBufferer = new MessageBufferer(_processManager.StdoutReader);
                    _messageBufferer.StartBuffering();

                    _isRunning = true;

                    // Wait for the process to be ready (with timeout)
                    // The core-server.js (Step 13) will signal readiness
                    await Task.Delay((int)_configuration.ProcessStartupTimeoutMs, cancellationToken);

                    // Start the receive loop on a background thread
                    _receiveLoopTask = Task.Run(() => ReceiveLoop(cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _isRunning = false;
                    throw;
                }
                catch (Exception ex)
                {
                    _isRunning = false;
                    RaiseError(ex, isFatal: true);
                    throw new InvalidOperationException($"Failed to start bridge transport: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Gracefully shuts down the bridge transport and terminates the Continue process.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
                return; // Idempotent

            try
            {
                _isRunning = false;

                if (_processManager?.IsRunning == true)
                {
                    await _processManager.StopAsync(_configuration.ShutdownTimeoutMs);
                }

                // Wait for receive loop to finish (with timeout)
                if (_receiveLoopTask != null)
                {
                    try
                    {
                        await _receiveLoopTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected if CancellationToken was signaled
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error waiting for receive loop: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during StopAsync: {ex.Message}");
            }
            finally
            {
                _messageBufferer?.Dispose();
                _processManager?.Dispose();
                _messageBufferer = null;
                _processManager = null;
            }
        }

        /// <summary>
        /// Sends a message to the Continue process via stdin.
        /// </summary>
        public async Task SendMessageAsync(Message message, CancellationToken cancellationToken)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (!_isRunning)
                throw new InvalidOperationException("Transport is not running.");

            try
            {
                await _sendSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    if (!_isRunning)
                        throw new InvalidOperationException("Transport is not running.");

                    var json = JsonConvert.SerializeObject(message);
                    var writer = _processManager?.StdinWriter;

                    if (writer == null)
                        throw new InvalidOperationException("StdinWriter is not available.");

                    // Write JSON and line delimiter
                    await Task.Run(() =>
                    {
                        writer.WriteLine(json);
                        writer.Flush();
                    }, cancellationToken);

                    if (_configuration.IsDebugMode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[StdioTransport] Sent: {json}");
                    }
                }
                finally
                {
                    _sendSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RaiseError(ex, isFatal: false);
                throw new InvalidOperationException($"Failed to send message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Receives the next message from the Continue process via stdout.
        /// </summary>
        public async Task<Message> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Transport is not running.");

            try
            {
                // Dequeue with timeout from the bufferer
                Message message = await Task.Run(() =>
                    _messageBufferer.Dequeue((int)_configuration.RpcTimeoutMs), cancellationToken);

                if (message == null)
                {
                    // Stream closed
                    _isRunning = false;
                    RaiseClosed();
                    return null;
                }

                if (_configuration.IsDebugMode)
                {
                    System.Diagnostics.Debug.WriteLine($"[StdioTransport] Received: {JsonConvert.SerializeObject(message)}");
                }

                return message;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RaiseError(ex, isFatal: true);
                throw new InvalidOperationException($"Failed to receive message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Background loop that continuously receives messages and dispatches them.
        /// Runs until the process closes or an error occurs.
        /// </summary>
        private async void ReceiveLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (_isRunning && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var message = await ReceiveMessageAsync(cancellationToken);

                        if (message == null)
                        {
                            // Process closed
                            break;
                        }

                        // Fire OnMessageReceived event (fire-and-forget on background thread)
                        var handler = OnMessageReceived;
                        if (handler != null)
                        {
                            Task.Run(() =>
                            {
                                try
                                {
                                    handler(this, new MessageReceivedEventArgs(message));
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error in OnMessageReceived handler: {ex.Message}");
                                }
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (InvalidOperationException)
                    {
                        // Transport was stopped
                        break;
                    }
                    catch (Exception ex)
                    {
                        RaiseError(ex, isFatal: true);
                        break;
                    }
                }
            }
            finally
            {
                _isRunning = false;
                RaiseClosed();
            }
        }

        /// <summary>
        /// Raises the OnError event on a background thread.
        /// </summary>
        private void RaiseError(Exception exception, bool isFatal)
        {
            var handler = OnError;
            if (handler != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        handler(this, new BridgeErrorEventArgs(exception, isFatal));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in OnError handler: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Raises the OnClosed event on a background thread.
        /// </summary>
        private void RaiseClosed()
        {
            var handler = OnClosed;
            if (handler != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        handler(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in OnClosed handler: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Disposes the transport and its resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch { /* Best effort */ }

            try
            {
                _sendSemaphore?.Dispose();
            }
            catch { /* Best effort */ }
        }
    }
}
