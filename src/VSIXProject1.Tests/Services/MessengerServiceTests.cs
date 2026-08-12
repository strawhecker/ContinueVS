using System;
using Xunit;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services.Tests
{
    public class MessengerServiceTests
    {
        [Fact]
        public void Constructor_InitializesService()
        {
            var service = new MessengerService(null, null);
            Assert.NotNull(service);
        }

        [Fact]
        public void Send_ThrowsArgumentNullException_WhenMessageTypeIsNull()
        {
            var service = new MessengerService(null, null);
            Assert.Throws<ArgumentNullException>(() => 
                service.Send<object>(null!, new object()));
        }

        [Fact]
        public void On_ThrowsArgumentNullException_WhenMessageTypeIsNull()
        {
            var service = new MessengerService(null, null);
            Assert.Throws<ArgumentNullException>(() => 
                service.On<object, object>(null!, async (data) => default!));
        }
    }
}
