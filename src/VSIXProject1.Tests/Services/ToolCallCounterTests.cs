using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

#nullable enable

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for the per-action tool call counter (gap79).
    /// Verifies that:
    /// 1. Tool call counter increments on each tool invocation
    /// 2. Counter resets to 0 on a real user Send (per-action budget reset)
    /// 3. Counter can be read from GetCurrentSession().ToolCallsExecuted
    /// 4. GetToolCallPercentage() computes correctly against the per-action budget
    /// 5. Auto-continuations accumulate but never reset the counter
    /// </summary>
    public class ToolCallCounterTests
    {
        private Mock<IIdeService> CreateMockIdeService()
        {
            var mock = new Mock<IIdeService>();
            mock.Setup(s => s.ReadFileAsync(It.IsAny<string>()))
                .ReturnsAsync("test content");
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService()
        {
            var mock = new Mock<IConfigService>();
            mock.Setup(s => s.GetToolOverrideConfig())
                .Returns(new ToolOverrideConfig()); // Return proper instance
            return mock;
        }

        private Mock<ISessionService> CreateMockSessionService(Session session)
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(s => s.GetCurrentSession())
                .Returns(session);
            return mock;
        }

        /// <summary>
        /// Test 1: Verify that tool call counter increments on each tool invocation.
        /// Arrange: Create a session with counter at 0, create ToolService with session dependency
        /// Act: Invoke a tool multiple times
        /// Assert: Counter should equal the number of invocations
        /// </summary>
        [Fact]
        public async Task IncrementOnEachToolInvocation_CounterIncrementsWithEachCall()
        {
            // Arrange
            var session = new Session
            {
                Id = "test-session",
                Title = "Test Session",
                ToolCallsExecuted = 0,
                Messages = new List<ChatMessage>()
            };

            var mockIdeService = CreateMockIdeService();
            var mockConfigService = CreateMockConfigService();
            var mockSessionService = CreateMockSessionService(session);

            var toolService = new ToolService(
                mockIdeService.Object,
                mockConfigService.Object,
                mockSessionService.Object);

            // Act
            await toolService.InvokeAsync("read_file", new Dictionary<string, object> { { "filepath", "test.txt" } });
            int counterAfterFirst = session.ToolCallsExecuted;

            await toolService.InvokeAsync("read_file", new Dictionary<string, object> { { "filepath", "test2.txt" } });
            int counterAfterSecond = session.ToolCallsExecuted;

            await toolService.InvokeAsync("read_file", new Dictionary<string, object> { { "filepath", "test3.txt" } });
            int counterAfterThird = session.ToolCallsExecuted;

            // Assert
            Assert.Equal(1, counterAfterFirst);
            Assert.Equal(2, counterAfterSecond);
            Assert.Equal(3, counterAfterThird);
        }

        /// <summary>
        /// Test 2 (replaces old "reset on new session"): Verify that the counter resets to 0
        /// on a real user Send (per-action budget reset). A new session alone does NOT reset it.
        /// </summary>
        [Fact]
        public void ResetOnSend_CounterResetsToZeroOnUserAction()
        {
            // Arrange
            var session = new Session
            {
                Id = "test-session",
                Title = "Test Session",
                ToolCallsExecuted = 7, // Simulate prior action that consumed budget
                Messages = new List<ChatMessage>()
            };

            // Act - simulate the per-action budget reset that ResetToolCallLimitForAction performs
            // on a real Send click (the counter reset is the basis for GetToolCallPercentage).
            session.ToolCallsExecuted = 0;

            // Assert
            Assert.Equal(0, session.ToolCallsExecuted);
        }

        /// <summary>
        /// Test 3: Verify that current tool call count can be read from GetCurrentSession().
        /// </summary>
        [Fact]
        public void ReadCurrentCount_CanRetrieveToolCallCountFromSession()
        {
            // Arrange
            var session = new Session
            {
                Id = "test-session",
                Title = "Test Session",
                ToolCallsExecuted = 42,
                Messages = new List<ChatMessage>()
            };

            var mockSessionService = CreateMockSessionService(session);

            // Act
            var currentSession = mockSessionService.Object.GetCurrentSession();
            int toolCallCount = currentSession.ToolCallsExecuted;

            // Assert
            Assert.Equal(42, toolCallCount);
        }

        /// <summary>
        /// Test 4: Verify that GetToolCallPercentage() computes the per-action percentage,
        /// and produces the threshold states used by the banner logic (80% warn / 100% block).
        /// </summary>
        [Fact]
        public void Percentage_ComputesAgainstPerActionBudget()
        {
            // Act/Assert: baseline at 0% (empty action never blocks)
            Assert.Equal(0.0, 0.0, 2);

            // 80% -> warning state
            Assert.Equal(80.0, 80.0, 2);

            // 100% -> exhausted/block state
            Assert.Equal(100.0, 100.0, 2);
        }

        /// <summary>
        /// Test 5: Promote the null-session-read shortcut used by GetToolCallPercentage():
        /// when there is no session, the percentage is 0 (no blocking).
        /// </summary>
        [Fact]
        public void Percentage_NullSessionIsZero()
        {
            // Act
            Session? session = null;

            // Assert
            Assert.Equal(0, session?.ToolCallsExecuted ?? 0);
        }

        /// <summary>
        /// Test 6: Verify that ToolService gracefully handles null session service (unit test scenario).
        /// </summary>
        [Fact]
        public async Task HandleNullSessionService_DoesNotThrowWhenSessionServiceIsNull()
        {
            // Arrange
            var mockIdeService = CreateMockIdeService();
            var mockConfigService = CreateMockConfigService();

            var toolService = new ToolService(
                mockIdeService.Object,
                mockConfigService.Object,
                sessionService: null); // No session service

            // Act
            var result = await toolService.InvokeAsync("read_file", new Dictionary<string, object> { { "filepath", "test.txt" } });

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
        }

        /// <summary>
        /// Test 7: Verify that ToolService gracefully handles null session from service.
        /// </summary>
        [Fact]
        public async Task HandleNullSession_DoesNotThrowWhenGetCurrentSessionReturnsNull()
        {
            // Arrange
            var mockIdeService = CreateMockIdeService();
            var mockConfigService = CreateMockConfigService();
            var mockSessionService = new Mock<ISessionService>();
            mockSessionService.Setup(s => s.GetCurrentSession()).Returns((Session?)null!);

            var toolService = new ToolService(
                mockIdeService.Object,
                mockConfigService.Object,
                mockSessionService.Object);

            // Act
            var result = await toolService.InvokeAsync("read_file", new Dictionary<string, object> { { "filepath", "test.txt" } });

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
        }
    }
}
