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
    /// Unit tests for tool call counter tracking in session state (gap23_4_2).
    /// Verifies that:
    /// 1. Tool call counter increments on each tool invocation
    /// 2. Counter resets to 0 when a new session is created
    /// 3. Counter can be read from GetCurrentSession().ToolCallsExecuted
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
        /// Test 2: Verify that counter resets to 0 when a new session is created.
        /// Arrange: Create SessionService, create initial session with some tool calls
        /// Act: Create a new session
        /// Assert: New session should have counter = 0
        /// </summary>
        [Fact]
        public async Task ResetOnNewSession_CounterResetsToZeroWhenNewSessionCreated()
        {
            // Arrange
            var mockTokenCountingService = new Mock<ITokenCountingService>();
            var sessionService = new SessionService(mockTokenCountingService.Object);

            // Create first session
            await sessionService.CreateNewSessionAsync("First Session");
            var firstSession = sessionService.GetCurrentSession();
            firstSession.ToolCallsExecuted = 5; // Simulate tool calls

            // Act
            await sessionService.CreateNewSessionAsync("Second Session");
            var secondSession = sessionService.GetCurrentSession();

            // Assert
            Assert.NotEqual(firstSession.Id, secondSession.Id);
            Assert.Equal(0, secondSession.ToolCallsExecuted);
        }

        /// <summary>
        /// Test 3: Verify that current tool call count can be read from GetCurrentSession().
        /// Arrange: Create a session with known tool call count
        /// Act: Retrieve session and read ToolCallsExecuted
        /// Assert: Value should match expected count
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
        /// Test 4: Verify that ToolService gracefully handles null session service (unit test scenario).
        /// Arrange: Create ToolService without ISessionService dependency
        /// Act: Invoke a tool
        /// Assert: Should not throw and should return a result
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
        /// Test 5: Verify that ToolService gracefully handles null session from service.
        /// Arrange: Create ToolService with mock session service that returns null
        /// Act: Invoke a tool
        /// Assert: Should not throw and should return a result
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
