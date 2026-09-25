#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Fixtures;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tool-level tests for the retire_from_context built-in tool.
    ///
    /// Verifies: valid retire returns an EMPTY result (terminus), toggling on a second call,
    /// invalid message_id returns an error (non-terminal), required reason validation, and the
    /// tool is available in Agent/Debug modes only.
    /// </summary>
    public class ToolServiceRetireFromContextTests : IDisposable
    {
        private readonly Mock<IIdeService> _ideServiceMock;
        private readonly Mock<IConfigService> _configServiceMock;
        private readonly TempSessionServiceFactory _tempFactory;
        private readonly string _refDir;

        public ToolServiceRetireFromContextTests()
        {
            _ideServiceMock = new Mock<IIdeService>();
            _configServiceMock = new Mock<IConfigService>();
            _configServiceMock.Setup(s => s.GetEnabledTools())
                .Returns((IEnumerable<ToolDefinition>)BuiltInToolsRegistry.GetAllBuiltInTools());
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

        private (ToolService toolService, SessionService sessionService, IContextRetirementService retirement) CreateService()
        {
            var sessionService = _tempFactory.Create(new SimpleTokenCounterService());
            var retirement = new ContextRetirementService(sessionService, _refDir);
            var toolService = new ToolService(
                _ideServiceMock.Object,
                _configServiceMock.Object,
                sessionService: sessionService,
                contextRetirementService: retirement);
            return (toolService, sessionService, retirement);
        }

        private async Task<ChatMessage> AddMessageAsync(SessionService service, ChatMessageRole role, string content)
        {
            var msg = new ChatMessage { Id = Guid.NewGuid().ToString(), Role = role, Content = content };
            await service.AddMessageAsync(msg);
            return msg;
        }

        [Fact]
        public async Task InvokeRetireFromContext_ValidCall_RetiresAndReturnsEmptyResult()
        {
            // Arrange
            var (toolService, sessionService, retirement) = CreateService();
            await sessionService.CreateNewSessionAsync("Test Session");
            var msg = await AddMessageAsync(sessionService, ChatMessageRole.Assistant, "Earlier wrong answer");
            var sessionId = sessionService.GetCurrentSession().Id;

            var args = new Dictionary<string, object>
            {
                { "message_id", msg.Id! },
                { "reason", "factually wrong" },
                { "replaced_by_id", "new-response-id" }
            };

            // Act
            var result = await toolService.InvokeAsync("retire_from_context", args);

            // Assert: empty successful result (terminus — nothing to feed back into context)
            Assert.True(result.IsSuccess);
            Assert.Equal(string.Empty, result.Output);

            // Assert: message tombstoned via the shared IsDeleted (excluded from context)
            Assert.True(sessionService.GetCurrentSession().Messages.First(m => m.Id == msg.Id).IsDeleted);
            Assert.True(await retirement.IsRetiredAsync(sessionId, msg.Id!));

            // Assert: record carries reason + replaced_by_id
            var records = (await retirement.GetRecordsAsync(sessionId)).ToList();
            Assert.Single(records);
            Assert.Equal("factually wrong", records[0].Reason);
            Assert.Equal("new-response-id", records[0].ReplacedById);
        }

        [Fact]
        public async Task InvokeRetireFromContext_CalledAgain_TogglesBackToActive()
        {
            // Arrange
            var (toolService, sessionService, retirement) = CreateService();
            await sessionService.CreateNewSessionAsync("Test Session");
            var msg = await AddMessageAsync(sessionService, ChatMessageRole.Assistant, "Earlier answer");
            var sessionId = sessionService.GetCurrentSession().Id;

            var retireArgs = new Dictionary<string, object>
            {
                { "message_id", msg.Id! },
                { "reason", "superseded by new-id" }
            };

            // Act: first call retires
            await toolService.InvokeAsync("retire_from_context", retireArgs);
            Assert.True(await retirement.IsRetiredAsync(sessionId, msg.Id!));

            // Act: second call on the same message toggles back to active
            var unretireArgs = new Dictionary<string, object>
            {
                { "message_id", msg.Id! },
                { "reason", "reactivated" }
            };
            var secondResult = await toolService.InvokeAsync("retire_from_context", unretireArgs);

            // Assert
            Assert.True(secondResult.IsSuccess);
            Assert.False(await retirement.IsRetiredAsync(sessionId, msg.Id!));
            Assert.False(sessionService.GetCurrentSession().Messages.First(m => m.Id == msg.Id).IsDeleted);
        }

        [Fact]
        public async Task InvokeRetireFromContext_InvalidMessageId_ReturnsError()
        {
            // Arrange
            var (toolService, sessionService, _) = CreateService();
            await sessionService.CreateNewSessionAsync("Test Session");

            var args = new Dictionary<string, object>
            {
                { "message_id", "does-not-exist" },
                { "reason", "redundant" }
            };

            // Act
            var result = await toolService.InvokeAsync("retire_from_context", args);

            // Assert: non-terminal error result
            Assert.False(result.IsSuccess);
            Assert.Contains("not found", result.Output);
        }

        [Fact]
        public async Task InvokeRetireFromContext_MissingReason_ReturnsError()
        {
            // Arrange
            var (toolService, sessionService, _) = CreateService();
            await sessionService.CreateNewSessionAsync("Test Session");
            var msg = await AddMessageAsync(sessionService, ChatMessageRole.User, "Hello");

            var args = new Dictionary<string, object>
            {
                { "message_id", msg.Id! },
                { "reason", "  " }
            };

            // Act
            var result = await toolService.InvokeAsync("retire_from_context", args);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("reason", result.Output);
        }

        [Fact]
        public void GetAvailableTools_IncludesRetireFromContextInAgentAndDebugModes()
        {
            // Arrange
            var (toolService, _, _) = CreateService();

            // Act
            var agentTools = toolService.GetAvailableTools(ChatMode.Agent).ToList();
            var debugTools = toolService.GetAvailableTools(ChatMode.Debug).ToList();
            var planTools = toolService.GetAvailableTools(ChatMode.Plan).ToList();

            // Assert: terminus/loop tool — available in Agent and Debug, NOT read-only Plan.
            Assert.Contains(agentTools, t => t.Name == "retire_from_context");
            Assert.Contains(debugTools, t => t.Name == "retire_from_context");
            Assert.DoesNotContain(planTools, t => t.Name == "retire_from_context");
        }

        [Fact]
        public void GetAllBuiltInTools_IncludesRetireFromContext()
        {
            var all = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            Assert.Contains(all, t => t.Name == "retire_from_context");
        }
    }
}
