using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class ToolServiceTests
    {
        private Mock<IIdeService> CreateMockIdeService()
        {
            var mock = new Mock<IIdeService>();
            mock.Setup(s => s.ReadFileAsync(It.IsAny<string>()))
                .ReturnsAsync("file content");
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService()
        {
            var mock = new Mock<IConfigService>();
            mock.Setup(s => s.GetEnabledTools())
                .Returns(BuiltInToolsRegistry.GetAllBuiltInTools() as IEnumerable<ToolDefinition>);
            return mock;
        }

        [Fact]
        public void Constructor_WithValidDependencies_Succeeds()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();

            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithNullIdeService_ThrowsArgumentNullException()
        {
            var configServiceMock = CreateMockConfigService();

            Assert.Throws<ArgumentNullException>(() =>
                new ToolService(null, configServiceMock.Object));
        }

        [Fact]
        public void Constructor_WithNullConfigService_ThrowsArgumentNullException()
        {
            var ideServiceMock = CreateMockIdeService();

            Assert.Throws<ArgumentNullException>(() =>
                new ToolService(ideServiceMock.Object, null));
        }

        [Fact]
        public void GetAvailableTools_ReturnsAllBuiltInTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools().ToList();

            Assert.NotEmpty(tools);
            Assert.Equal(19, tools.Count);
        }

        [Fact]
        public void GetAvailableTools_ContainsExpectedToolNames()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools().ToList();
            var toolNames = tools.Select(t => t.Name).ToList();

            Assert.Contains("read_file", toolNames);
            Assert.Contains("create_new_file", toolNames);
            Assert.Contains("run_terminal_command", toolNames);
            Assert.Contains("file_glob_search", toolNames);
            Assert.Contains("view_diff", toolNames);
            Assert.Contains("read_currently_open_file", toolNames);
            Assert.Contains("ls", toolNames);
            Assert.Contains("edit_file", toolNames);
            Assert.Contains("search_codebase", toolNames);
        }

        [Fact]
        public void GetTool_WithValidName_ReturnsTool()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool("read_file");

            Assert.NotNull(tool);
            Assert.Equal("read_file", tool.Name);
        }

        [Fact]
        public void GetTool_WithInvalidName_ReturnsNull()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool("nonexistent_tool");

            Assert.Null(tool);
        }

        [Fact]
        public void GetTool_WithNullName_ReturnsNull()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool(null);

            Assert.Null(tool);
        }

        [Fact]
        public void GetTool_WithEmptyName_ReturnsNull()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool(string.Empty);

            Assert.Null(tool);
        }

        [Fact]
        public async Task InvokeAsync_WithInvalidToolName_ReturnsErrorResult()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var result = await service.InvokeAsync("nonexistent", new Dictionary<string, object>());

            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("not found", result.Output);
        }

        [Fact]
        public async Task InvokeAsync_WithNullToolName_ReturnsErrorResult()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var result = await service.InvokeAsync(null, new Dictionary<string, object>());

            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void GetAvailableTools_AllToolsHaveBuiltInCategory()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools();

            foreach (var tool in tools)
            {
                Assert.Equal("Built-In", tool.Category);
            }
        }

        [Fact]
        public void GetAvailableTools_AllToolsHaveBuiltInType()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools();

            foreach (var tool in tools)
            {
                Assert.Equal("builtin", tool.ToolType);
            }
        }
    }
}
