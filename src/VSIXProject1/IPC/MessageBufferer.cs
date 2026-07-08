using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Thread-safe buffer for line-delimited JSON messages from the Continue process stdout.
    /// 
    /// Responsibilities:
    /// - Read lines from stdout in a background loop
    /// - Parse each line as a JSON message
    /// - Maintain a thread-safe FIFO queue of deserialized messages
    /// - Signal when messages are available or when the stream closes
    /// 
    /// Protocol:
    /// - Messages are delimited by \r\n (CRLF)
    /// - Each message is valid JSON corresponding to the Message class
    /// - If JSON parsing fails, the message is logged and the line is skipped
    /// - When the stream closes (EOF), Dequeue returns null
    /// </summary>
    internal sealed class MessageBufferer : IDisposable
    {
        private readonly StreamReader _reader;
        private readonly Queue<Message> _messageQueue;
        private readonly object _queueLock = new object();
        private readonly ManualResetEvent _messageAvailable = new ManualResetEvent(false);
        private readonly ManualResetEvent _streamClosed = new ManualResetEvent(false);
        private bool _isDisposed;
        private bool _isClosed;

        public MessageBufferer(StreamReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _messageQueue = new Queue<Message>();
        }

        /// <summary>
        /// Starts the background buffering loop. Should be called once after construction.
        /// This method runs on a background thread via Task.Run in StdioTransport.
        /// </summary>
        public void StartBuffering()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_isDisposed)
                    {
                        string? line = null;

                        try
                        {
                            line = await Task.Run(() => _reader.ReadLine());
                        }
                        catch (ObjectDisposedException)
                        {
                            // Stream was disposed
                            break;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error reading from stdout: {ex.Message}");
                            break;
                        }

                        if (line == null)
                        {
                            // EOF: stream closed
                            _isClosed = true;
                            _streamClosed.Set();
                            break;
                        }

                        // Parse JSON message
                        try
                        {
                            var message = JsonConvert.DeserializeObject<Message>(line);
                            if (message != null)
                            {
                                lock (_queueLock)
                                {
                                    _messageQueue.Enqueue(message);
                                    _messageAvailable.Set();
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            // Log and skip malformed JSON
                            System.Diagnostics.Debug.WriteLine($"Failed to parse JSON message: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"  Line: {line}");
                        }
                    }
                }
                finally
                {
                    _isClosed = true;
                    _streamClosed.Set();
                }
            });
        }

        /// <summary>
        /// Dequeues the next message from the buffer, or null if the stream has closed.
        /// This method blocks until a message is available or the stream closes.
        /// </summary>
        /// <param name="timeoutMs">Maximum time to wait for a message. -1 = infinite.</param>
        /// <returns>The next Message, or null if stream closed or timeout elapsed.</returns>
        public Message? Dequeue(int timeoutMs = -1)
        {
            while (true)
            {
                lock (_queueLock)
                {
                    if (_messageQueue.Count > 0)
                    {
                        var message = _messageQueue.Dequeue();
                        if (_messageQueue.Count == 0)
                            _messageAvailable.Reset();
                        return message;
                    }
                }

                // Wait for either a message or the stream to close
                int waitTime = timeoutMs < 0 ? 100 : timeoutMs; // Small timeout for checking closed state
                int signaled = WaitHandle.WaitAny(new[] { _messageAvailable, _streamClosed }, waitTime);

                if (signaled == 0)
                {
                    // Message available; loop back to dequeue
                    continue;
                }
                else if (signaled == 1)
                {
                    // Stream closed
                    return null;
                }
                else if (timeoutMs >= 0)
                {
                    // Timeout elapsed
                    return null;
                }
                // If infinite timeout, continue waiting
            }
        }

        /// <summary>
        /// Gets a value indicating whether the stream has closed.
        /// </summary>
        public bool IsClosed => _isClosed;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _messageAvailable?.Dispose();
            _streamClosed?.Dispose();
        }
    }
}
