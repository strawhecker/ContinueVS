using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services.Tests
{
    public class VsIdeServiceTests
    {
        [Fact]
        public void Constructor_InitializesService()
        {
            var service = new VsIdeService(null, null, null);
            Assert.NotNull(service);
        }

        [Fact]
        public async Task ReadFileAsync_ThrowsArgumentNullException_WhenFilepathIsNull()
        {
            var service = new VsIdeService(null, null, null);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ReadFileAsync(null!));
        }

        [Fact]
        public async Task ReadFileAsync_ThrowsArgumentNullException_WhenFilepathIsEmpty()
        {
            var service = new VsIdeService(null, null, null);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ReadFileAsync(""));
        }
    }
}
