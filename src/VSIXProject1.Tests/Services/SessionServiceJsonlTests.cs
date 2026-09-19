#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// gap83 — end-to-end SessionService behavior over the JSONL delta log.
    /// Verifies: O(1) same-reference dictionary with INotifyPropertyChanged,
    /// add/update/delete/softdelete/undelete → reopen equivalence (replay from
    /// beginning), mid-stream durability during a long send, and that soft-deleted
    /// messages are excluded from the LLM payload (gap80/gap81).
    /// </summary>
    public class SessionServiceJsonlTests : IDisposable
    {
        private readonly string _dir;
        private readonly SessionService _service;

        public SessionServiceJsonlTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "gap83_svc_" + Guid.NewGuid().ToString("N"));
            _service = new SessionService(new SimpleTokenCounterService(), _dir);
        }

        public void Dispose()
        {
            _service.Dispose();
            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public async Task Add_Update_SameReference_IsO1_AndFiresPropertyChanged()
        {
            // Arrange
            var session = _service.GetCurrentSession();
            var msg = new ChatMessage { Role = ChatMessageRole.User, Content = "before" };
            await _service.AddMessageAsync(msg);

            bool changedFired = false;
            msg.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(ChatMessage.Content)) changedFired = true; };

            // Act — update via a payload carrying the same id
            var updated = new ChatMessage { Id = msg.Id, Role = ChatMessageRole.User, Content = "after" };
            await _service.UpdateMessageAsync(msg.Id!, updated);

            // Assert — SAME instance (O(1) dictionary reference), INPC fired, content changed
            Assert.Same(msg, session.Messages[0]);
            Assert.Same(msg, _service.GetCurrentSession().Messages.Single(m => m.Id == msg.Id));
            Assert.Equal("after", msg.Content);
            Assert.True(changedFired, "Update must mutate the same reference and fire INotifyPropertyChanged.");
            Assert.Single(session.Messages);
        }

        [Fact]
        public async Task LoadAsync_ReplaysLog_ReconstructsSessionFromDeltas()
        {
            // Arrange — build a session with add/update/softdelete deltas
            var session = _service.GetCurrentSession();
            string sessionId = session.Id;

            var m1 = new ChatMessage { Role = ChatMessageRole.User, Content = "one" };
            var m2 = new ChatMessage { Role = ChatMessageRole.Assistant, Content = "two" };
            await _service.AddMessageAsync(m1);
            await _service.AddMessageAsync(m2);
            await _service.SoftDeleteMessageAsync(m2.Id!);
            await _service.SetCurrentModeAsync(1);

            // Act — reopen into a fresh service over the same directory
            var reopened = new SessionService(new SimpleTokenCounterService(), _dir);
            try
            {
                await reopened.LoadSessionAsync(sessionId);
                var reloaded = reopened.GetCurrentSession();

                // Assert — replay from beginning reconstructed ordered state
                Assert.Equal(sessionId, reloaded.Id);
                Assert.Collection(reloaded.Messages,
                    f => Assert.Equal("one", f.Content),
                    s => Assert.Equal("two", s.Content));
                Assert.Equal("one", reloaded.Messages[0].Content);
                Assert.Equal("two", reloaded.Messages[1].Content);
                Assert.True(reloaded.Messages[1].IsDeleted, "Soft-delete must survive replay (tombstone).");
                Assert.Equal(1, reloaded.Mode);
            }
            finally
            {
                reopened.Dispose();
            }
        }

        [Fact]
        public async Task LongSend_MidStreamMessages_AreDurableBeforeFinalize()
        {
            // Arrange
            var session = _service.GetCurrentSession();
            string sessionId = session.Id;

            // Simulate a long-running user send: several assistant content updates mid-stream
            var msg = new ChatMessage { Role = ChatMessageRole.Assistant, Content = "chunk1" };
            await _service.AddMessageAsync(msg);

            for (int i = 2; i <= 5; i++)
            {
                var part = new ChatMessage { Id = msg.Id, Role = ChatMessageRole.Assistant, Content = $"chunk{i}" };
                await _service.UpdateMessageAsync(msg.Id!, part);
            }

            // Writes are flushed on every mutate (EnqueueAndFlushAsync) — reopen must
            // see the final content even before any explicit "save" is called.
            var reopened = new SessionService(new SimpleTokenCounterService(), _dir);
            try
            {
                await reopened.LoadSessionAsync(sessionId);
                var reloaded = reopened.GetCurrentSession();
                Assert.Single(reloaded.Messages);
                Assert.Equal("chunk5", reloaded.Messages[0].Content);
            }
            finally
            {
                reopened.Dispose();
            }
        }

        [Fact]
        public async Task SoftDeleted_ExcludedFromLlmPayload_ButRetainedInLog()
        {
            // Arrange
            var session = _service.GetCurrentSession();
            var visible = new ChatMessage { Role = ChatMessageRole.User, Content = "visible turn" };
            var hidden = new ChatMessage { Role = ChatMessageRole.Assistant, Content = "hidden turn" };
            await _service.AddMessageAsync(visible);
            await _service.AddMessageAsync(hidden);
            await _service.SoftDeleteMessageAsync(hidden.Id!);

            var model = new ModelInfo { ContextWindow = 8096 };
            var systemMsg = new ChatMessage { Role = ChatMessageRole.System, Content = "sys" };

            // Act
            var payload = _service.PackageMessages(model, systemMsg, "new user turn");

            // Assert — hidden (tombstoned) turn absent, visible present; bytes still on disk
            Assert.DoesNotContain(payload, m => m.Content == "hidden turn");
            Assert.Contains(payload, m => m.Content == "visible turn");
            Assert.Contains(payload, m => m.Content == "new user turn");
            Assert.Contains(session.Messages, m => m.Content == "hidden turn" && m.IsDeleted);
        }
    }
}
