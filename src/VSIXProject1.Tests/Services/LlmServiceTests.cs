using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services.Tests
{
    public class LlmServiceTests
    {
        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMessengerServiceIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LlmService(null!, null));
        }

        [Fact]
        public void SupportsStreaming_ReturnsFalse()
        {
            var messengerService = new Moq.Mock<Services.Interfaces.IMessengerService>().Object;
            var service = new LlmService(messengerService);
            Assert.False(service.SupportsStreaming("test-model"));
        }

        [Fact]
        public void SupportsFunctionCalling_ReturnsFalse()
        {
            var messengerService = new Moq.Mock<Services.Interfaces.IMessengerService>().Object;
            var service = new LlmService(messengerService);
            Assert.False(service.SupportsFunctionCalling("test-model"));
        }

        [Fact]
        public void GetContextWindowSize_ReturnsDefaultValue()
        {
            var messengerService = new Moq.Mock<Services.Interfaces.IMessengerService>().Object;
            var service = new LlmService(messengerService);
            Assert.Equal(4096, service.GetContextWindowSize("test-model"));
        }

        [Fact]
        public async Task CountTokensAsync_ReturnsCount()
        {
            var messengerService = new Moq.Mock<Services.Interfaces.IMessengerService>().Object;
            var service = new LlmService(messengerService);
            var result = await service.CountTokensAsync("test text", "test-model");
            Assert.True(result > 0);
        }

        [Fact]
        public async Task LogInteractionAsync_Completes()
        {
            var messengerService = new Moq.Mock<Services.Interfaces.IMessengerService>().Object;
            var service = new LlmService(messengerService);
            var log = new Services.Interfaces.LlmInteractionLog { ModelId = "test" };
            await service.LogInteractionAsync(log);
        }
    }
}
