using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

#nullable enable

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for VsIdeService.GetActiveFilepath() wired through IDteProvider.
    /// DteProvider wraps COM DTE and cannot be tested directly; a stub satisfies DI.
    /// </summary>
    public class VsIdeServiceTests
    {
        private class StubDteProvider : IDteProvider
        {
            public string ActiveFilepath { get; set; } = string.Empty;
            public List<string> OpenDocumentPaths { get; set; } = new();
            public string SelectedText { get; set; } = string.Empty;
            public Selection? CursorSelection { get; set; }

            public string GetActiveFilepath() => ActiveFilepath;
            public string GetSolutionDirectory() => string.Empty;
            public string GetSelectedText() => SelectedText;
            public string GetActiveDocumentContent() => string.Empty;
            public List<string> GetOpenDocumentPaths() => OpenDocumentPaths;
            public List<string> GetRecentFiles(int maxCount) => new List<string>();
            public EnvDTE.Debugger? GetDebugger() => null;
            public string? GetStartupProjectName() => null;
            public Selection? GetCursorSelection() => CursorSelection;
            public string? OpenFileInIde(string filePath) => OpenFileInIdeResult ?? filePath;
            public bool NavigateTo(string filePath, int line) => NavigateToResult;
            public bool BuildSolution(string? projectName) => BuildSolutionResult;
            public ContinueVS.Core.Types.ActiveDocumentInfo? ActiveDocumentInfo { get; set; }
            public ContinueVS.Core.Types.ActiveDocumentInfo? GotoDefinitionResult { get; set; }
            public ContinueVS.Core.Types.BuildConfigInfo? BuildConfiguration { get; set; }
            public ContinueVS.Core.Types.LaunchProfileInfo? LaunchProfile { get; set; }
            public ContinueVS.Core.Types.OutputPaneInfo? OutputPane { get; set; }
            public string? OpenFileInIdeResult { get; set; } = null;
            public bool NavigateToResult { get; set; } = true;
            public bool BuildSolutionResult { get; set; } = true;
            public ContinueVS.Core.Types.ActiveDocumentInfo? GetActiveDocumentInfo() =>
                ActiveDocumentInfo ?? (string.IsNullOrEmpty(ActiveFilepath) ? null : new ContinueVS.Core.Types.ActiveDocumentInfo { FilePath = ActiveFilepath, Selection = CursorSelection });
            public ContinueVS.Core.Types.ActiveDocumentInfo? GotoDefinition() => GotoDefinitionResult;
            public ContinueVS.Core.Types.BuildConfigInfo? GetActiveBuildConfiguration() => BuildConfiguration;
            public ContinueVS.Core.Types.LaunchProfileInfo? GetLaunchProfile() => LaunchProfile;
            public ContinueVS.Core.Types.OutputPaneInfo? GetOutputPane(string paneName) => OutputPane;
        }

        [Fact]
        public void GetActiveFilepath_ReturnsFilepath_WhenDteProviderReturnsPath()
        {
            // Arrange
            var stub = new StubDteProvider { ActiveFilepath = @"C:\Foo\Bar.cs" };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetActiveFilepath();

            // Assert
            Assert.Equal(@"C:\Foo\Bar.cs", result);
        }

        [Fact]
        public void GetActiveFilepath_ReturnsEmpty_WhenDteProviderReturnsEmpty()
        {
            // Arrange
            var stub = new StubDteProvider { ActiveFilepath = string.Empty };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetActiveFilepath();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenDteProviderIsNull()
        {
            // Arrange / Act / Assert
            Assert.Throws<ArgumentNullException>(() => new VsIdeService(null!));
        }

        [Fact]
        public void GetSelectedText_ReturnsDelegatedText_WhenDteProviderReturnsText()
        {
            // Arrange
            var stub = new StubDteProvider { SelectedText = "hello world" };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetSelectedText();

            // Assert
            Assert.Equal("hello world", result);
        }

        [Fact]
        public void GetSelectedText_ReturnsEmpty_WhenDteProviderReturnsEmpty()
        {
            // Arrange
            var stub = new StubDteProvider { SelectedText = string.Empty };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetSelectedText();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetCursorSelection_ReturnsSelection_WhenDteProviderReturnsSelection()
        {
            // Arrange
            var expected = new Selection
            {
                Start = new Location { FilePath = @"C:\Foo\Bar.cs", Line = 10, Column = 5 },
                End = new Location { FilePath = @"C:\Foo\Bar.cs", Line = 10, Column = 15 }
            };
            var stub = new StubDteProvider { CursorSelection = expected };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetCursorSelection();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result!.Start!.Line);
            Assert.Equal(5, result.Start.Column);
            Assert.Equal(10, result.End!.Line);
            Assert.Equal(15, result.End.Column);
        }

        [Fact]
        public void GetCursorSelection_ReturnsNull_WhenDteProviderReturnsNull()
        {
            // Arrange
            var stub = new StubDteProvider { CursorSelection = null };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetCursorSelection();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task IsOpenInViewerAsync_ReturnsTrue_WhenPathIsOpen()
        {
            // Arrange
            var openPath = @"C:\Users\test\.continueVS\plans\refactor_20240101_000000.md";
            var stub = new StubDteProvider { OpenDocumentPaths = new List<string> { openPath } };
            var sut = new VsIdeService(stub);

            // Act / Assert
            Assert.True(await sut.IsOpenInViewerAsync(openPath));
        }

        [Fact]
        public async Task IsOpenInViewerAsync_ReturnsFalse_WhenPathNotOpen()
        {
            // Arrange
            var stub = new StubDteProvider { OpenDocumentPaths = new List<string> { @"C:\Other\file.cs" } };
            var sut = new VsIdeService(stub);

            // Act / Assert
            Assert.Equal(false, await sut.IsOpenInViewerAsync(@"C:\Users\test\.continueVS\plans\nope.md"));
        }

        [Fact]
        public async Task IsOpenInViewerAsync_ReturnsNull_WhenNoDocumentsReported()
        {
            // Arrange
            var stub = new StubDteProvider { OpenDocumentPaths = new List<string>() };
            var sut = new VsIdeService(stub);

            // Act / Assert
            Assert.Null(await sut.IsOpenInViewerAsync(@"C:\Users\test\.continueVS\plans\a.md"));
        }

        [Fact]
        public async Task IsOpenInViewerAsync_ReturnsFalse_ForEmptyPath()
        {
            // Arrange
            var stub = new StubDteProvider { OpenDocumentPaths = new List<string> { @"C:\a.cs" } };
            var sut = new VsIdeService(stub);

            // Act / Assert
            Assert.Equal(false, await sut.IsOpenInViewerAsync(string.Empty));
        }

        // ---------------- gap95 seam tests ----------------

        [Fact]
        public async Task GetActiveDocumentInfoAsync_ReturnsInfo_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider
            {
                ActiveDocumentInfo = new ContinueVS.Core.Types.ActiveDocumentInfo
                {
                    FilePath = @"C:\Foo\Bar.cs",
                    Selection = new Selection { Start = new Location { Line = 3, Column = 5 } }
                }
            };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.GetActiveDocumentInfoAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(@"C:\Foo\Bar.cs", result!.FilePath);
            Assert.Equal(3, result.Selection!.Start!.Line);
        }

        [Fact]
        public async Task OpenFileInIdeAsync_ReturnsPath_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider { OpenFileInIdeResult = @"C:\Foo\Bar.cs" };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.OpenFileInIdeAsync(@"C:\Foo\Bar.cs");

            // Assert
            Assert.Equal(@"C:\Foo\Bar.cs", result);
        }

        [Fact]
        public async Task NavigateToAsync_ReturnsTrue_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider { NavigateToResult = true };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.NavigateToAsync(@"C:\Foo\Bar.cs", 10);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GotoDefinitionAsync_ReturnsInfo_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider
            {
                GotoDefinitionResult = new ContinueVS.Core.Types.ActiveDocumentInfo { FilePath = @"C:\Def.cs" }
            };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.GotoDefinitionAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(@"C:\Def.cs", result!.FilePath);
        }

        [Fact]
        public async Task BuildSolutionAsync_ReturnsTrue_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider { BuildSolutionResult = true };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.BuildSolutionAsync("MyProj");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetActiveBuildConfigurationAsync_ReturnsConfig_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider
            {
                BuildConfiguration = new ContinueVS.Core.Types.BuildConfigInfo { Name = "Release", Platform = "x64" }
            };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.GetActiveBuildConfigurationAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Release", result!.Name);
            Assert.Equal("x64", result.Platform);
        }

        [Fact]
        public async Task GetLaunchProfileAsync_ReturnsProfile_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider
            {
                LaunchProfile = new ContinueVS.Core.Types.LaunchProfileInfo { StartupProject = "Proj", LaunchProfile = "local" }
            };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.GetLaunchProfileAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Proj", result!.StartupProject);
            Assert.Equal("local", result.LaunchProfile);
        }

        [Fact]
        public async Task GetOutputPaneAsync_ReturnsContent_FromDteProvider()
        {
            // Arrange
            var stub = new StubDteProvider
            {
                OutputPane = new ContinueVS.Core.Types.OutputPaneInfo { Name = "Build", Content = "Build succeeded" }
            };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.GetOutputPaneAsync("Build");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Build succeeded", result!.Content);
        }

        [Fact]
        public async Task GetActiveDocumentInfoAsync_ReturnsNull_OnProviderFailure()
        {
            // Arrange: provider returns null (no active document)
            var stub = new StubDteProvider { ActiveFilepath = string.Empty };
            var sut = new VsIdeService(stub);

            // Act
            var result = await sut.GetActiveDocumentInfoAsync();

            // Assert
            Assert.Null(result);
        }
    }
}
