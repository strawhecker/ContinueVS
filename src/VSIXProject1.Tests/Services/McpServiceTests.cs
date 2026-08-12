using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Tests
{
    public class McpServiceTests
    {
        [Fact]
        public async Task InitializeServerAsync_ConnectsServer()
        {
            var service = new McpService(null);
            var config = new McpServerConfig
            {
                Id = "test-server",
                Name = "Test",
                Type = "stdio"
            };
            await service.InitializeServerAsync(config);
            var status = service.GetServerStatus("test-server");
            Assert.NotNull(status);
        }

        [Fact]
        public async Task ShutdownServerAsync_DisconnectsServer()
        {
            var service = new McpService(null);
            var config = new McpServerConfig
            {
                Id = "test-server",
                Name = "Test",
                Type = "stdio"
            };
            await service.InitializeServerAsync(config);
            await service.ShutdownServerAsync("test-server");
            var status = service.GetServerStatus("test-server");
            Assert.Null(status);
        }

        [Fact]
        public async Task GetServerStatus_ReturnsStatus()
        {
            var service = new McpService(null);
            var config = new McpServerConfig
            {
                Id = "test-server",
                Name = "Test",
                Type = "stdio"
            };
            await service.InitializeServerAsync(config);
            var status = service.GetServerStatus("test-server");
            Assert.NotNull(status);
            Assert.Equal("test-server", status.Id);
        }

        [Fact]
        public async Task GetAllServers_ReturnsServers()
        {
            var service = new McpService(null);
            var config = new McpServerConfig
            {
                Id = "test-server",
                Name = "Test",
                Type = "stdio"
            };
            await service.InitializeServerAsync(config);
            var servers = service.GetAllServers();
            Assert.Single(servers);
        }

        [Fact]
        public async Task GetResourceAsync_ReturnsPlaceholder()
        {
            var service = new McpService(null);
            var result = await service.GetResourceAsync("test", "resource://test");
            Assert.Contains("Placeholder", result);
        }

        [Fact]
        public async Task GetPromptAsync_ReturnsTemplate()
        {
            var service = new McpService(null);
            var template = await service.GetPromptAsync("test", "test-prompt");
            Assert.NotNull(template);
            Assert.Equal("test-prompt", template.Name);
        }

        [Fact]
        public void GetServerTools_ReturnsEmpty()
        {
            var service = new McpService(null);
            var tools = service.GetServerTools("test");
            Assert.Empty(tools);
        }
    }
}
