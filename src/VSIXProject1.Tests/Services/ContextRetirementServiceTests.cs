#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Fixtures;
using Newtonsoft.Json;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Context pruning &amp; error supersession (retire_from_context) tests — the underlying
    /// <see cref="ContextRetirementService"/>.
    ///
    /// Verifies the agreed design: retirring REUSES the shared <see cref="ChatMessage.IsDeleted"/>
    /// tombstone (no new IsRetired flag, no new exclusion code), so a retired message is excluded
    /// from <see cref="ISessionService.PackageMessages"/> exactly like a user-pruned one; the
    /// auditable record (reason, replaced_by_id, verbatim content) lives in the append-only
    /// reference file and is never lost.
    /// </summary>
    public class ContextRetirementServiceTests : IDisposable
    {
        private readonly TempSessionServiceFactory _tempFactory;
        private readonly string _refDir;

        public ContextRetirementServiceTests()
        {
            _tempFactory = new TempSessionServiceFactory();
            _refDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ContinueVS-Test-Retire-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            _tempFactory.Dispose();
            try
            {
                if (System.IO.Directory.Exists(_refDir))
                    System.IO.Directory.Delete(_refDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; never throw from Dispose.
            }
            GC.SuppressFinalize(this);
        }

        private ContextRetirementService CreateRetirementService(ISessionService sessionService)
        {
            return new ContextRetirementService(sessionService, _refDir);
        }

        [Fact]
        public async Task RetireAsync_SetsIsDeletedAndExcludesFromContext()
        {
            // Arrange
            var sessionService = _tempFactory.Create(new SimpleTokenCounterService());
            await sessionService.CreateNewSessionAsync("Test Session");
            var msg = new ChatMessage { Id = Guid.NewGuid().ToString(), Role = ChatMessageRole.User, Content = "Hello" };
            await sessionService.AddMessageAsync(msg);
            var retirement = CreateRetirementService(sessionService);
            var sessionId = sessionService.GetCurrentSession().Id;

            // Act
            await retirement.RetireAsync(sessionId, msg.Id!, "redundant", null, JsonConvert.SerializeObject(msg));

            // Assert: message tombstoned (reuses IsDeleted — NOT a new flag)
            var stored = sessionService.GetCurrentSession().Messages.First(m => m.Id == msg.Id);
            Assert.True(stored.IsDeleted, "Reusing IsDeleted means retire sets IsDeleted=true.");

            // Assert: excluded from the LLM payload via the existing PackageMessages filter
            var package = sessionService.PackageMessages(null,
                new ChatMessage { Role = ChatMessageRole.System, Content = "sys" },
                "new user turn");
            Assert.DoesNotContain(package, m => m.Id == msg.Id);

            // Assert: retired state tracked in the reference file
            Assert.True(await retirement.IsRetiredAsync(sessionId, msg.Id!));
        }

        [Fact]
        public async Task UnretireAsync_ClearsIsDeletedAndRestoresToContext()
        {
            // Arrange
            var sessionService = _tempFactory.Create(new SimpleTokenCounterService());
            await sessionService.CreateNewSessionAsync("Test Session");
            var msg = new ChatMessage { Id = Guid.NewGuid().ToString(), Role = ChatMessageRole.User, Content = "Hello" };
            await sessionService.AddMessageAsync(msg);
            var retirement = CreateRetirementService(sessionService);
            var sessionId = sessionService.GetCurrentSession().Id;
            await retirement.RetireAsync(sessionId, msg.Id!, "superseded by x", "x", JsonConvert.SerializeObject(msg));

            // Act
            await retirement.UnretireAsync(sessionId, msg.Id!, "reactivated");

            // Assert: tombstone cleared
            var stored = sessionService.GetCurrentSession().Messages.First(m => m.Id == msg.Id);
            Assert.False(stored.IsDeleted, "Unretire clears IsDeleted=false.");

            // Assert: back in the LLM payload
            var package = sessionService.PackageMessages(null,
                new ChatMessage { Role = ChatMessageRole.System, Content = "sys" },
                "new user turn");
            Assert.Contains(package, m => m.Id == msg.Id);

            // Assert: no longer considered retired
            Assert.False(await retirement.IsRetiredAsync(sessionId, msg.Id!));
        }

        [Fact]
        public async Task Toggle_RoundTrips_RetireThenUnretire()
        {
            // Arrange
            var sessionService = _tempFactory.Create(new SimpleTokenCounterService());
            await sessionService.CreateNewSessionAsync("Test Session");
            var msg = new ChatMessage { Id = Guid.NewGuid().ToString(), Role = ChatMessageRole.Assistant, Content = "Earlier wrong answer" };
            await sessionService.AddMessageAsync(msg);
            var retirement = CreateRetirementService(sessionService);
            var sessionId = sessionService.GetCurrentSession().Id;

            // Act / Assert: active -> retired -> active
            await retirement.RetireAsync(sessionId, msg.Id!, "factually wrong", "new-id", JsonConvert.SerializeObject(msg));
            Assert.True(await retirement.IsRetiredAsync(sessionId, msg.Id!));
            Assert.True(sessionService.GetCurrentSession().Messages.First(m => m.Id == msg.Id).IsDeleted);

            await retirement.UnretireAsync(sessionId, msg.Id!, "pointer served its purpose");
            Assert.False(await retirement.IsRetiredAsync(sessionId, msg.Id!));
            Assert.False(sessionService.GetCurrentSession().Messages.First(m => m.Id == msg.Id).IsDeleted);
        }

        [Fact]
        public async Task ReferenceFile_IsAppendOnlyAndPreservesVerbatim()
        {
            // Arrange
            var sessionService = _tempFactory.Create(new SimpleTokenCounterService());
            await sessionService.CreateNewSessionAsync("Test Session");
            var msg = new ChatMessage { Id = Guid.NewGuid().ToString(), Role = ChatMessageRole.Tool, Content = "raw bytes" };
            await sessionService.AddMessageAsync(msg);
            var retirement = CreateRetirementService(sessionService);
            var sessionId = sessionService.GetCurrentSession().Id;
            var verbatim = JsonConvert.SerializeObject(msg);

            // Act
            await retirement.RetireAsync(sessionId, msg.Id!, "superseded", "s1", verbatim);
            await retirement.UnretireAsync(sessionId, msg.Id!, "reactivated");
            await retirement.RetireAsync(sessionId, msg.Id!, "superseded again", "s2", verbatim);

            // Assert: the reference file grew (never rewritten) and holds the full toggle history.
            var records = (await retirement.GetRecordsAsync(sessionId)).ToList();
            Assert.Equal(3, records.Count);
            Assert.Equal(new[] { "retire", "unretire", "retire" }, records.Select(r => r.Type).ToArray());

            // Assert: the first record's verbatim bytes are preserved exactly.
            Assert.Equal(verbatim, records[0].VerbatimJson);
            Assert.Equal("superseded", records[0].Reason);
            Assert.Equal("s1", records[0].ReplacedById);

            // Assert: file physically on disk (append-only), the trail is not lost.
            Assert.True(System.IO.File.Exists(retirement.ReferenceFilePath));
            var lines = System.IO.File.ReadAllLines(retirement.ReferenceFilePath);
            Assert.Equal(3, lines.Length);
        }

        [Fact]
        public async Task RetireAsync_NullMessageId_Throws()
        {
            var retirement = CreateRetirementService(_tempFactory.Create(new SimpleTokenCounterService()));
            await Assert.ThrowsAsync<ArgumentException>(() => retirement.RetireAsync("s", "  ", "reason", null, null));
        }
    }
}
