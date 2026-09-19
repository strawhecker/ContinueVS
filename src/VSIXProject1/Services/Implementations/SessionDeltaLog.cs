using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Append-only JSONL delta-log writer/reader for sessions (gap83).
    ///
    /// Write path: mutating operations call <see cref="Enqueue"/> (or
    /// <see cref="EnqueueAndFlushAsync"/>) which serialize ONE JSON object per line
    /// and hand it to a dedicated background writer thread. That thread is the ONLY
    /// component that touches the file, so disk I/O never runs on the UI/streaming
    /// caller path. Each line carries its own sessionId, so a single writer can
    /// route concurrent mutations across different session files.
    ///
    /// Read path: <see cref="ReplayInto"/> is synchronous and performs real file
    /// I/O. Callers (SessionService) wrap it in Task.Run so the FILE READ runs on a
    /// pooled worker thread; the awaiting async continuation only wires results.
    ///
    /// The container is standard JSONL (one JSON object per line). A first-class
    /// "type" property per line discriminates the concrete delta. Each line parses
    /// as a generic JObject, then dispatches to a typed POCO via ToObject&lt;T&gt;().
    /// Malformed or truncated tail lines (partial write on crash) are skipped.
    /// </summary>
    public sealed class SessionDeltaLog : IDisposable
    {
        private readonly string _storageDir;
        private readonly BlockingCollection<WriteItem> _queue = new BlockingCollection<WriteItem>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _writerTask;
        private readonly object _sync = new object();
        private int _enqueuedSeq;
        private int _flushedSeq;
        private readonly AutoResetEvent _flushedSignal = new AutoResetEvent(false);

        public SessionDeltaLog(string storageDir)
        {
            _storageDir = storageDir ?? throw new ArgumentNullException(nameof(storageDir));
            _writerTask = Task.Run(() => WriterLoop());
        }

        /// <summary>
        /// The storage directory this log writes into.
        /// </summary>
        public string StorageDirectory => _storageDir;

        /// <summary>
        /// Absolute path to a session's JSONL log.
        /// </summary>
        public static string GetPath(string storageDir, string sessionId)
        {
            return Path.Combine(storageDir, $"{sessionId}.jsonl");
        }

        /// <summary>
        /// Serializes a delta to a single JSON object (no trailing newline).
        /// </summary>
        public static string Serialize(SessionDelta delta)
        {
            return JsonConvert.SerializeObject(delta, Formatting.None);
        }

        /// <summary>
        /// Serializes a delta to JSONL and enqueues it for the writer thread.
        /// Returns immediately; no file I/O on the calling thread.
        /// </summary>
        public void Enqueue(SessionDelta delta)
        {
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            bool signal;
            lock (_sync)
            {
                _enqueuedSeq++;
                _queue.Add(new WriteItem(Serialize(delta), _enqueuedSeq));
                signal = true;
            }
            if (signal) _flushedSignal.Set();
        }

        /// <summary>
        /// Enqueues a delta and waits until the writer thread has flushed it to disk.
        /// Guarantees durability + ordered log for save/reopen/tests.
        /// </summary>
        public async Task EnqueueAndFlushAsync(SessionDelta delta)
        {
            int seq;
            lock (_sync)
            {
                _enqueuedSeq++;
                seq = _enqueuedSeq;
                _queue.Add(new WriteItem(Serialize(delta), seq));
                _flushedSignal.Set();
            }
            while (Volatile.Read(ref _flushedSeq) < seq)
            {
                await Task.Delay(5);
                if (_cts.IsCancellationRequested) return;
            }
        }

        /// <summary>
        /// Blocks until the writer has flushed every line enqueued so far.
        /// </summary>
        public void FlushSync()
        {
            int target;
            lock (_sync) { target = _enqueuedSeq; }
            if (Volatile.Read(ref _flushedSeq) >= target) return;
            while (Volatile.Read(ref _flushedSeq) < target)
            {
                _flushedSignal.WaitOne(250);
            }
        }

        /// <summary>
        /// Total number of lines flushed to disk so far.
        /// </summary>
        public int FlushedCount => Volatile.Read(ref _flushedSeq);

        private void WriterLoop()
        {
            Directory.CreateDirectory(_storageDir);
            foreach (var item in _queue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    string sessionId = ExtractSessionId(item.Line);
                    string path = GetPath(_storageDir, sessionId);
                    using (var writer = new StreamWriter(path, append: true, Encoding.UTF8))
                    {
                        writer.WriteLine(item.Line);
                    }
                    Interlocked.Exchange(ref _flushedSeq, item.Seq);
                    _flushedSignal.Set();
                }
                catch
                {
                    // Swallow writer errors so a malformed/transient failure never
                    // stops the loop; subsequent lines retry or are skipped.
                }
            }
        }

        /// <summary>
        /// Reads the sessionId routing field from a serialized delta line.
        /// </summary>
        private static string ExtractSessionId(string line)
        {
            try
            {
                var obj = JObject.Parse(line);
                return obj["sessionId"]?.Value<string>() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Replays a session's log from the first line, applying deltas into the
        /// provided index and ordered list. The index references the SAME
        /// ChatMessage instances that form the ordered list (the live LLM surface).
        /// Malformed/truncated tail lines are skipped (crash-safe).
        /// Performs real file I/O — run on a worker thread via Task.Run.
        /// </summary>
        public LogReplayResult ReplayInto(string sessionId, Dictionary<string, ChatMessage> index, List<ChatMessage> order)
        {
            string path = GetPath(_storageDir, sessionId);
            if (!File.Exists(path)) return new LogReplayResult();

            var result = new LogReplayResult();
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    ApplyLine(line, index, order, result);
                }
            }
            return result;
        }

        private static void ApplyLine(string line, Dictionary<string, ChatMessage> index, List<ChatMessage> order, LogReplayResult result)
        {
            JObject obj;
            try
            {
                obj = JObject.Parse(line);
            }
            catch
            {
                return; // malformed / truncated tail — skip, crash-safe
            }

            string type = obj["type"]?.Value<string>() ?? string.Empty;
            switch (type)
            {
                case "init":
                    var init = obj.ToObject<SessionDeltaInit>();
                    if (init != null)
                    {
                        result.Id = init.Id ?? result.Id;
                        result.Title = init.Title ?? result.Title;
                        result.CreatedAt = init.CreatedAt ?? result.CreatedAt;
                        result.Mode = init.Mode;
                    }
                    break;

                case "add":
                    var add = obj.ToObject<SessionDeltaAdd>();
                    if (add?.Message != null && !string.IsNullOrEmpty(add.Message.Id))
                    {
                        if (index.TryGetValue(add.Message.Id!, out var prior))
                        {
                            order.Remove(prior);
                        }
                        index[add.Message.Id!] = add.Message;
                        order.Add(add.Message);
                    }
                    break;

                case "update":
                    var upd = obj.ToObject<SessionDeltaUpdate>();
                    if (upd?.Message != null && !string.IsNullOrEmpty(upd.Message.Id)
                        && index.TryGetValue(upd.Message.Id!, out var existing))
                    {
                        MergeInto(existing, upd.Message);
                    }
                    break;

                case "delete":
                    var del = obj.ToObject<SessionDeltaDelete>();
                    if (del?.MessageId != null && index.TryGetValue(del.MessageId, out var toDelete))
                    {
                        order.Remove(toDelete);
                        index.Remove(del.MessageId);
                    }
                    break;

                case "softDelete":
                    var soft = obj.ToObject<SessionDeltaSoftDelete>();
                    if (soft?.MessageId != null && index.TryGetValue(soft.MessageId, out var toSoft))
                    {
                        toSoft.IsDeleted = true;
                    }
                    break;

                case "undelete":
                    var un = obj.ToObject<SessionDeltaUndelete>();
                    if (un?.MessageId != null && index.TryGetValue(un.MessageId, out var toUn))
                    {
                        toUn.IsDeleted = false;
                    }
                    break;
            }
        }

        /// <summary>
        /// Copies mutable message fields from a deserialized payload onto the live
        /// instance so the O(1) dictionary reference (and PropertyChanged events)
        /// remain intact. Content is set last so INPC fires with final text.
        /// </summary>
        public static void MergeInto(ChatMessage target, ChatMessage source)
        {
            if (source.Role != target.Role) target.Role = source.Role;
            if (source.ToolCalls != null) target.ToolCalls = source.ToolCalls;
            if (source.ToolCallId != null) target.ToolCallId = source.ToolCallId;
            if (source.Timestamp != null) target.Timestamp = source.Timestamp;
            if (source.IsThinking != target.IsThinking) target.IsThinking = source.IsThinking;
            target.IsDeleted = source.IsDeleted;
            if (source.Content != null) target.Content = source.Content;
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { /* ignore */ }
            _queue.CompleteAdding();
#pragma warning disable VSTHRD002 // blocking wait is intentional on teardown
            try { _writerTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
#pragma warning restore VSTHRD002
            _cts.Dispose();
            _flushedSignal.Dispose();
        }

        private sealed class WriteItem
        {
            public WriteItem(string line, int seq) { Line = line; Seq = seq; }
            public string Line { get; }
            public int Seq { get; }
        }
    }

    /// <summary>
    /// Aggregated session header state recovered during a replay.
    /// </summary>
    public sealed class LogReplayResult
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public System.DateTime? CreatedAt { get; set; }
        public int Mode { get; set; }
    }

    /// <summary>
    /// Minimal synchronous producer/consumer collection targeting net472 without
    /// the System.Threading.Channels package (not referenced by this project).
    /// </summary>
    internal sealed class BlockingCollection<T>
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly object _sync = new object();
        private bool _adding = true;

        public void Add(T item)
        {
            lock (_sync) { _queue.Enqueue(item); Monitor.Pulse(_sync); }
        }

        public void CompleteAdding()
        {
            lock (_sync) { _adding = false; Monitor.PulseAll(_sync); }
        }

        public int Count
        {
            get { lock (_sync) { return _queue.Count; } }
        }

        public IEnumerable<T> GetConsumingEnumerable(CancellationToken token)
        {
            while (true)
            {
                T item = default!;
                bool got = false;
                lock (_sync)
                {
                    while (_queue.Count == 0 && _adding && !token.IsCancellationRequested)
                    {
                        Monitor.Wait(_sync);
                    }
                    if (_queue.Count > 0)
                    {
                        item = _queue.Dequeue();
                        got = true;
                    }
                    else
                    {
                        got = false;
                    }
                }
                if (!got) yield break;
                yield return item;
            }
        }
    }
}
