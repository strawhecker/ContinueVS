using Xunit;
using System.Collections.Generic;
using ContinueVS.Services.Interfaces;

#nullable enable

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for IDteProvider contract via stub.
    /// DteProvider wraps a COM DTE object and cannot be unit-tested directly;
    /// these tests verify the contract and stub behaviour used by consumers.
    /// </summary>
    public class DteProviderTests
    {
        private class StubDteProvider : IDteProvider
        {
            public string SelectedText { get; set; } = string.Empty;
            public string ActiveDocumentContent { get; set; } = string.Empty;
            public List<string> RecentFilesData { get; set; } = new();
            public string ActiveFilepath { get; set; } = string.Empty;

            public string GetSelectedText() => SelectedText;
            public string GetActiveDocumentContent() => ActiveDocumentContent;
            public List<string> GetRecentFiles(int maxCount) => new List<string>(RecentFilesData.GetRange(0, System.Math.Min(maxCount, RecentFilesData.Count)));
            public string GetActiveFilepath() => ActiveFilepath;
            public string GetSolutionDirectory() => string.Empty;
            public List<string> GetOpenDocumentPaths() => new List<string>();
            public EnvDTE.Debugger? GetDebugger() => null;
            public string? GetStartupProjectName() => null;
            public Selection? GetCursorSelection() => null;
            public string? OpenFileInIde(string filePath) => filePath;
            public bool NavigateTo(string filePath, int line) => true;
            public bool BuildSolution(string? projectName) => true;
            public ContinueVS.Core.Types.ActiveDocumentInfo? GetActiveDocumentInfo() => null;
            public ContinueVS.Core.Types.ActiveDocumentInfo? GotoDefinition() => null;
            public ContinueVS.Core.Types.BuildConfigInfo? GetActiveBuildConfiguration() => null;
            public ContinueVS.Core.Types.LaunchProfileInfo? GetLaunchProfile() => null;
            public ContinueVS.Core.Types.OutputPaneInfo? GetOutputPane(string paneName) => null;
        }

        [Fact]
        public void GetActiveFilepath_ReturnsFilepath_WhenSet()
        {
            var stub = new StubDteProvider { ActiveFilepath = @"C:\Projects\MyFile.cs" };
            var result = stub.GetActiveFilepath();
            Assert.Equal(@"C:\Projects\MyFile.cs", result);
        }

        [Fact]
        public void GetActiveFilepath_ReturnsEmpty_WhenNotSet()
        {
            var stub = new StubDteProvider();
            var result = stub.GetActiveFilepath();
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetActiveFilepath_ReturnsEmpty_WhenNull()
        {
            var stub = new StubDteProvider { ActiveFilepath = null! };
            // Simulate defensive consumer logic: treat null as empty
            var result = stub.GetActiveFilepath() ?? string.Empty;
            Assert.Equal(string.Empty, result);
        }
    }
}
