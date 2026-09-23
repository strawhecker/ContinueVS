#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// gap83 — append-only JSONL delta-log writer/reader coverage.
    /// Verifies: append-only growth, per-line tagged JSON, generic→specific
    /// dispatch on the "type" tag, crash-safe skip of malformed/truncated tails,
    /// and that delete/softDelete/undelete are expressed as appended lines
    /// (bytes are never erased from the log).
    /// </summary>
    public class SessionDeltaLogTests : IDisposable
    {
        private readonly string _dir;
        private readonly SessionDeltaLog _log;

        public SessionDeltaLogTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "gap83_delta_log_" + Guid.NewGuid().ToString("N"));
            _log = new SessionDeltaLog(_dir);
        }

        public void Dispose()
        {
            _log.Dispose();
            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
            }
        }

        private static string CreateSessionId() => "sess_" + Guid.NewGuid().ToString("N");

        private static ChatMessage Msg(string content, ChatMessageRole role = ChatMessageRole.User)
        {
            return new ChatMessage { Id = Guid.NewGuid().ToString("N"), Role = role, Content = content, Timestamp = DateTime.Now };
        }

        [Fact]
        public async Task Append_OnlyGrows_FileIsNeverRewritten()
        {
            // Arrange
            var sessionId = CreateSessionId();
            var path = SessionDeltaLog.GetPath(_dir, sessionId);

            // Act
            await _log.EnqueueAndFlushAsync(new SessionDeltaInit { SessionId = sessionId, Id = sessionId, Title = "T", Mode = 0 });
            long afterInit = new FileInfo(path).Length;

            await _log.EnqueueAndFlushAsync(new SessionDeltaAdd { SessionId = sessionId, Message = Msg("hello") });
            long afterAdd = new FileInfo(path).Length;

            // Assert — strictly monotonic growth, never a rewrite/shrink
            Assert.True(afterAdd > afterInit, "Append must strictly grow the file (no rewrite).");
            Assert.True(afterInit > 0, "Init line must have been written.");
        }

        [Fact]
        public async Task Replay_ReadsFromBeginning_ReconstructsSession()
        {
            // Arrange
            var sessionId = CreateSessionId();
            var m1 = Msg("first");
            var m2 = Msg("second");

            await _log.EnqueueAndFlushAsync(new SessionDeltaInit { SessionId = sessionId, Id = sessionId, Title = "Replay", Mode = 2 });
            await _log.EnqueueAndFlushAsync(new SessionDeltaAdd { SessionId = sessionId, Message = m1 });
            await _log.EnqueueAndFlushAsync(new SessionDeltaAdd { SessionId = sessionId, Message = m2 });

            var index = new Dictionary<string, ChatMessage>();
            var order = new List<ChatMessage>();

            // Act — replay (from beginning) on a fresh index
            var result = _log.ReplayInto(sessionId, index, order);

            // Assert
            Assert.Equal(sessionId, result.Id);
            Assert.Equal("Replay", result.Title);
            Assert.Equal(2, result.Mode);
            Assert.Collection(order,
                f => Assert.Equal("first", f.Content),
                s => Assert.Equal("second", s.Content));
            Assert.Equal("first", order[0].Content);
            Assert.Equal("second", order[1].Content);
            Assert.True(index.ContainsKey(m1.Id!));
            Assert.True(index.ContainsKey(m2.Id!));
        }

        [Fact]
        public async Task Update_MergesOntoSameReference_ContentReflectsLatest()
        {
            // Arrange
            var sessionId = CreateSessionId();
            var m = Msg("original");
            await _log.EnqueueAndFlushAsync(new SessionDeltaInit { SessionId = sessionId, Id = sessionId });
            await _log.EnqueueAndFlushAsync(new SessionDeltaAdd { SessionId = sessionId, Message = m });

            var index = new Dictionary<string, ChatMessage>();
            var order = new List<ChatMessage>();
            _log.ReplayInto(sessionId, index, order);

            // Act — update with a full payload carrying the same id
            var updated = new ChatMessage { Id = m.Id, Role = m.Role, Content = "revised" };
            await _log.EnqueueAndFlushAsync(new SessionDeltaUpdate { SessionId = sessionId, MessageId = m.Id, Message = updated });
            var index2 = new Dictionary<string, ChatMessage>();
            var order2 = new List<ChatMessage>();
            _log.ReplayInto(sessionId, index2, order2);

            // Assert — the dictionary references the very instance in the ordered list
            Assert.Same(order2[0], index2[m.Id!]);
            Assert.Equal("revised", order2[0].Content);
            Assert.Single(order2);
            Assert.Single(index2);
        }

        [Fact]
        public async Task Delete_AppendsLine_RemovesFromIndex_ButLeavesBytes()
        {
            // Arrange
            var sessionId = CreateSessionId();
            var m = Msg("to delete");
            await _log.EnqueueAndFlushAsync(new SessionDeltaInit { SessionId = sessionId, Id = sessionId });
            await _log.EnqueueAndFlushAsync(new SessionDeltaAdd { SessionId = sessionId, Message = m });
            long beforeDelete = new FileInfo(SessionDeltaLog.GetPath(_dir, sessionId)).Length;

            // Act
            await _log.EnqueueAndFlushAsync(new SessionDeltaDelete { SessionId = sessionId, MessageId = m.Id });
            long afterDelete = new FileInfo(SessionDeltaLog.GetPath(_dir, sessionId)).Length;

            var index = new Dictionary<string, ChatMessage>();
            var order = new List<ChatMessage>();
            var result = _log.ReplayInto(sessionId, index, order);

            // Assert — bytes retained (append-only), entry removed from live view
            Assert.True(afterDelete > beforeDelete, "Delete must append, never erase bytes.");
            Assert.Empty(order);
            Assert.Empty(index);
            Assert.Equal(sessionId, result.Id);
        }

        [Fact]
        public async Task SoftDelete_And_Undelete_ReplayAsTombstoneToggle()
        {
            // Arrange
            var sessionId = CreateSessionId();
            var m = Msg("tombstone");
            await _log.EnqueueAndFlushAsync(new SessionDeltaInit { SessionId = sessionId, Id = sessionId });
            await _log.EnqueueAndFlushAsync(new SessionDeltaAdd { SessionId = sessionId, Message = m });

            // Act — soft delete then undelete (gap80/gap81)
            await _log.EnqueueAndFlushAsync(new SessionDeltaSoftDelete { SessionId = sessionId, MessageId = m.Id });
            var idx1 = new Dictionary<string, ChatMessage>();
            var order1 = new List<ChatMessage>();
            _log.ReplayInto(sessionId, idx1, order1);
            Assert.Single(order1);
            Assert.True(order1[0].IsDeleted, "Soft delete must tombstone (IsDeleted=true).");

            await _log.EnqueueAndFlushAsync(new SessionDeltaUndelete { SessionId = sessionId, MessageId = m.Id });
            var idx2 = new Dictionary<string, ChatMessage>();
            var order2 = new List<ChatMessage>();
            _log.ReplayInto(sessionId, idx2, order2);
            Assert.Single(order2);
            Assert.False(order2[0].IsDeleted, "Undelete must clear the tombstone (IsDeleted=false).");
        }

        [Fact]
        public void TaggedLine_IsSingleObject_WithFirstClassType()
        {
            // Act — serialize a delta, then parse as generic JObject
            string line = SessionDeltaLog.Serialize(new SessionDeltaAdd { SessionId = "s", Message = Msg("x") });
            JObject obj = JObject.Parse(line);

            // Assert — a single JSON object (not an array) with a first-class tag
            Assert.Equal(JTokenType.Object, obj.Type);
            Assert.True(obj.ContainsKey("type"));
            Assert.Equal("add", obj["type"]?.Value<string>());
            Assert.Equal("s", obj["sessionId"]?.Value<string>());
        }

        [Fact]
        public async Task MalformedOrTruncatedTail_IsSkipped_CrashSafe()
        {
            // Arrange
            var sessionId = CreateSessionId();
            var path = SessionDeltaLog.GetPath(_dir, sessionId);
            await _log.EnqueueAndFlushAsync(new SessionDeltaInit { SessionId = sessionId, Id = sessionId });
            await _log.EnqueueAndFlushAsync(new SessionDeltaAdd { SessionId = sessionId, Message = Msg("good") });

            // Corrupt the tail with a truncated line (partial write on crash)
            File.AppendAllText(path, "{ \"type\": \"add\", \"sessionId\": \"" + sessionId + "\", \"message\": { \"id\" ");
            File.AppendAllText(path, Environment.NewLine);

            // Act
            var index = new Dictionary<string, ChatMessage>();
            var order = new List<ChatMessage>();
            var result = _log.ReplayInto(sessionId, index, order);

            // Assert — valid lines replayed, malformed tail skipped without throwing
            Assert.Single(order);
            Assert.Equal("good", order[0].Content);
            Assert.Equal(sessionId, result.Id);
        }
    }
}
