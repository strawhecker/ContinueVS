#nullable enable

using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests for mode change event propagation (gap27_3).
    /// Verifies that IModeService/ModeService correctly propagates mode changes
    /// through ISessionService.SetCurrentModeAsync() and fires SessionChanged events.
    /// </summary>
    public class ModeChangePropagationTests
    {
        private static ModeService CreateModeService(out Mock<ISessionService> sessionMock)
        {
            sessionMock = new Mock<ISessionService>();
            sessionMock.Setup(x => x.SetCurrentModeAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);

            return new ModeService(sessionMock.Object);
        }

        [Fact]
        public async Task SetModeAsync_WhenSetToAsk_CallsSessionServiceWithAskMode()
        {
            // Arrange
            var service = CreateModeService(out var sessionMock);

            // Act
            await service.SetModeAsync(0); // Ask = 0

            // Assert
            sessionMock.Verify(x => x.SetCurrentModeAsync(0), Times.Once);
        }

        [Fact]
        public async Task SetModeAsync_WhenSetToAgent_CallsSessionServiceWithAgentMode()
        {
            // Arrange
            var service = CreateModeService(out var sessionMock);

            // Act
            await service.SetModeAsync(1); // Agent = 1

            // Assert
            sessionMock.Verify(x => x.SetCurrentModeAsync(1), Times.Once);
        }

        [Fact]
        public async Task SetModeAsync_WhenSetToPlan_CallsSessionServiceWithPlanMode()
        {
            // Arrange
            var service = CreateModeService(out var sessionMock);

            // Act
            await service.SetModeAsync(2); // Plan = 2

            // Assert
            sessionMock.Verify(x => x.SetCurrentModeAsync(2), Times.Once);
        }

        [Fact]
        public async Task SetCurrentModeAsync_FiresSessionChangedEventWithCurrentModeSet()
        {
            // Arrange
            var tokenCountingServiceMock = new Mock<ITokenCountingService>();
            var sessionService = new SessionService(tokenCountingServiceMock.Object);

            SessionChangedEventArgs? eventArgs = null;
            sessionService.SessionChanged += (s, e) => eventArgs = e;

            // Act
            await sessionService.SetCurrentModeAsync(1); // Agent = 1

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(1, eventArgs.CurrentMode);
            Assert.Equal(SessionChangeType.Updated, eventArgs.ChangeType);
        }
    }
}
