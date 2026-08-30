#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Xunit;

namespace ContinueVS.Services.Tests
{
    public class WorkspaceStatsServiceTests
    {
        // ---- Stubs ----

        private sealed class StubIdeService : IIdeService
        {
            public Func<Task<string>> OnGetActiveDocumentPath { get; set; } = () => Task.FromResult("none");
            public Func<Task<string>> OnGetBranchAsync { get; set; } = () => Task.FromResult(string.Empty);
            public Func<Task<string>> OnGetGitRootPathAsync { get; set; } = () => Task.FromResult(string.Empty);

            public Task<string> GetActiveDocumentPathAsync() => OnGetActiveDocumentPath();
            public Task<string> GetBranchAsync() => OnGetBranchAsync();
            public Task<string> GetGitRootPathAsync() => OnGetGitRootPathAsync();

            public event EventHandler<FileChangedEventArgs>? FileChanged { add { } remove { } }
            public event EventHandler<ActiveFileChangedEventArgs>? ActiveFileChanged { add { } remove { } }

            public Task<string> ReadFileAsync(string filepath) => Task.FromResult(string.Empty);
            public Task WriteFileAsync(string filepath, string contents) => Task.CompletedTask;
            public Task<string> ReadRangeInFileAsync(string filepath, int startLine, int endLine) => Task.FromResult(string.Empty);
            public Task SaveFileAsync(string filepath) => Task.CompletedTask;
            public Task DeleteFileAsync(string filepath) => Task.CompletedTask;
            public Task<string> GetRepoNameAsync() => Task.FromResult(string.Empty);
            public Task<IEnumerable<Location>> GotoDefinitionAsync(Location location) => Task.FromResult<IEnumerable<Location>>(Array.Empty<Location>());
            public Task<IEnumerable<Location>> GetReferencesAsync(Location location) => Task.FromResult<IEnumerable<Location>>(Array.Empty<Location>());
            public Task<IEnumerable<DocumentSymbol>> GetDocumentSymbolsAsync(string filepath) => Task.FromResult<IEnumerable<DocumentSymbol>>(Array.Empty<DocumentSymbol>());
            public Task<IEnumerable<Diagnostic>> GetProblemsAsync(string filepath) => Task.FromResult<IEnumerable<Diagnostic>>(Array.Empty<Diagnostic>());
            public Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd) => Task.FromResult((string.Empty, string.Empty));
            public string? GetActiveFilepath() => null;
            public string? GetSelectedText() => null;
            public Selection? GetCursorSelection() => null;
            public bool FileExists(string filepath) => File.Exists(filepath);
            public IEnumerable<string> GetWorkspaceFiles(string pattern = "*") => Array.Empty<string>();
            public Task OpenFileInEditorAsync(string filePath) => Task.CompletedTask;
            public Task<TestRunResult> RunTestAsync(string testPath, TestRunOptions options, CancellationToken ct = default) => Task.FromResult(new TestRunResult());
            public Task<RuntimeState?> InspectVariablesAsync(CancellationToken cancellationToken = default) => Task.FromResult<RuntimeState?>(null);
            public Task<BreakpointInfo?> SetBreakpointAsync(string filePath, int lineNumber, string? condition = null, CancellationToken cancellationToken = default) => Task.FromResult<BreakpointInfo?>(null);
            public Task<bool> ClearBreakpointAsync(string filePath, int lineNumber, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<RuntimeState?> StepAsync(DebugStepAction action, CancellationToken cancellationToken = default) => Task.FromResult<RuntimeState?>(null);
            public Task ResumeDebugAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class StubDebuggerService : IDebuggerService
        {
            public Func<Task<RuntimeState?>> OnGetCurrentState { get; set; } = () => Task.FromResult<RuntimeState?>(null);

            public Task<RuntimeState?> GetCurrentStateAsync(CancellationToken cancellationToken = default) => OnGetCurrentState();
            public Task<BreakpointInfo?> SetBreakpointAsync(string filePath, int lineNumber, string? condition = null, CancellationToken cancellationToken = default) => Task.FromResult<BreakpointInfo?>(null);
            public Task<bool> ClearBreakpointAsync(string filePath, int lineNumber, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<RuntimeState?> ExecuteStepAsync(DebugStepAction action, CancellationToken cancellationToken = default) => Task.FromResult<RuntimeState?>(null);
            public Task ResumeExecutionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<bool> IsDebuggerActiveAsync() => Task.FromResult(false);
        }

        private static string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ---- Tests ----

        [Fact]
        public void GetStats_ReturnsDefaults_WhenAllSourcesFail()
        {
            // Arrange
            var ideStub = new StubIdeService
            {
                OnGetActiveDocumentPath = () => throw new Exception("fail"),
                OnGetBranchAsync = () => throw new Exception("fail"),
                OnGetGitRootPathAsync = () => throw new Exception("fail")
            };
            var debugStub = new StubDebuggerService
            {
                OnGetCurrentState = () => throw new Exception("fail")
            };
            var svc = new WorkspaceStatsService(ideStub, debugStub, testGitRoot: CreateTempDir(), testGitBranch: "unknown");

            // Act
            var stats = svc.GetStats();

            // Assert - no exception; all fields fall back to defaults
            Assert.Equal("none", stats.ActiveFile);
            Assert.Equal("unknown", stats.GitBranch);
            Assert.Equal("none", stats.GitRemote);
            Assert.Equal("none", stats.SolutionPath);
            Assert.Equal("unknown", stats.TargetFrameworks);
            Assert.Equal("none", stats.DebugMode);
            Assert.Equal("none", stats.BreakLocation);
            Assert.Equal("none", stats.CompletedGaps);
        }

        [Fact]
        public void GetStats_GitBranch_PopulatedFromIdeService()
        {
            // Arrange — use testGitBranch seam; git process is not available in test environment
            var svc = new WorkspaceStatsService(
                new StubIdeService(), new StubDebuggerService(),
                testGitRoot: CreateTempDir(), testGitBranch: "main");

            // Act
            var stats = svc.GetStats();

            // Assert
            Assert.Equal("main", stats.GitBranch);
        }

        [Fact]
        public void GetStats_TargetFrameworks_ParsedFromCsproj()
        {
            // Arrange
            var tempDir = CreateTempDir();
            File.WriteAllText(
                Path.Combine(tempDir, "TestProject.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net472</TargetFramework></PropertyGroup></Project>");

            var svc = new WorkspaceStatsService(new StubIdeService(), new StubDebuggerService(), testGitRoot: tempDir);

            // Act
            var stats = svc.GetStats();

            // Assert
            Assert.Equal("net472", stats.TargetFrameworks);
        }

        [Fact]
        public void GetStats_CompletedGaps_ParsedFromSessionContext()
        {
            // Arrange - gap1 line contains checkmark, gap2 does not
            var tempDir = CreateTempDir();
            Directory.CreateDirectory(Path.Combine(tempDir, "docs"));
            File.WriteAllText(
                Path.Combine(tempDir, "docs", "session-context.md"),
                "### gap1: Some Feature \u2705\n### gap2: Another Feature \u23F3\n");

            var svc = new WorkspaceStatsService(new StubIdeService(), new StubDebuggerService(), testGitRoot: tempDir);

            // Act
            var stats = svc.GetStats();

            // Assert
            Assert.Contains("gap1", stats.CompletedGaps);
            Assert.DoesNotContain("gap2", stats.CompletedGaps);
        }
    }
}
