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
            public Task<string> ReadCurrentlyOpenFileAsync() => Task.FromResult(string.Empty);
            public Task<bool?> IsOpenInViewerAsync(string path) => Task.FromResult<bool?>(false);
            public Task<string> GetBranchAsync() => OnGetBranchAsync();
            public Task<string> GetGitRootPathAsync() => OnGetGitRootPathAsync();

            public event EventHandler<FileChangedEventArgs>? FileChanged { add { } remove { } }
            public event EventHandler<ActiveFileChangedEventArgs>? ActiveFileChanged { add { } remove { } }

            public Task<string> ReadFileAsync(string filepath) => Task.FromResult(string.Empty);
            public Task WriteFileAsync(string filepath, string contents) => Task.CompletedTask;
            public Task CreateFileAsync(string filepath, string contents) => Task.CompletedTask;
            public Task CreateFolderAsync(string folderpath) => Task.CompletedTask;
            public Task<IEnumerable<string>> ListDirectoryAsync(string dirPath, bool recursive = false) => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
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
            public string? GetSolutionDirectory() => null;
            public string? GetSelectedText() => null;
            public Selection? GetCursorSelection() => null;
            public bool FileExists(string filepath) => File.Exists(filepath);
            public IEnumerable<string> GetWorkspaceFiles(string pattern = "*") => Array.Empty<string>();
            public Task OpenFileInEditorAsync(string filePath) => Task.CompletedTask;
            public Task<TestRunResult> RunTestAsync(string testPath, TestRunOptions options, CancellationToken ct = default) => Task.FromResult(new TestRunResult());
            public Task<string> RunCommandAsync(string command) => Task.FromResult(string.Empty);
            public Task<string> GetDiffAsync() => Task.FromResult(string.Empty);
            public Task<string> GetProblemsAsync() => Task.FromResult(string.Empty);
            public Task<string> GetGitStatusAsync() => Task.FromResult(string.Empty);
            public Task<string> GetGitDiffAsync(string filePath) => Task.FromResult(string.Empty);
            public Task<string> GetGitLogAsync(int maxCommits) => Task.FromResult(string.Empty);
            public Task<string> CreateGitCommitAsync(string message) => Task.FromResult(string.Empty);
            public Task OpenFileAsync(string filepath) => Task.CompletedTask;
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

            // gap92_2 inspection surface — benign stubs (not exercised by WorkspaceStatsService)
            public Task<DebugInspectionResult<List<CallStackFrame>>> GetCallStackAsync(DebugSessionState state, int threadId, int maxFrames, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<List<CallStackFrame>>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> SelectFrameAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<StatementInfo>> GetStatementAsync(DebugSessionState state, int threadId, int frameIndex, int contextLines, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<StatementInfo>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<List<VariableInfo>>> GetLocalsAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<List<VariableInfo>>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<List<VariableInfo>>> GetArgumentsAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<List<VariableInfo>>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<VariableInfo>> GetThisAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<VariableInfo>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<List<ThreadInfo>>> GetThreadsAsync(DebugSessionState state, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<List<ThreadInfo>>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<List<ModuleInfo>>> GetModulesAsync(DebugSessionState state, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<List<ModuleInfo>>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<ProcessInfo>> GetProcessInfoAsync(DebugSessionState state, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<ProcessInfo>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<List<ExceptionSettingInfo>>> ListExceptionSettingsAsync(DebugSessionState state, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<List<ExceptionSettingInfo>>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<ExceptionInfo>> GetCurrentExceptionAsync(DebugSessionState state, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<ExceptionInfo>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<string>> GetOutputAsync(DebugSessionState state, int maxChars, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<string>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> EnableBreakpointAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> DisableBreakpointAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> ConditionBreakpointAsync(DebugSessionState state, string breakpointId, string condition, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> ClearBreakpointByIdAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));

            // gap92_3 evaluate/mutate surface — benign stubs (not exercised by WorkspaceStatsService)
            public Task<DebugInspectionResult<VariableInfo>> EvaluateAsync(DebugSessionState state, int threadId, int frameIndex, string expression, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<VariableInfo>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> SetValueAsync(DebugSessionState state, int threadId, int frameIndex, string name, string value, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<string>> MemoryReadAsync(DebugSessionState state, string address, int length, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<string>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> MemoryWriteAsync(DebugSessionState state, string address, string bytes, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> FreezeThreadAsync(DebugSessionState state, int threadId, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> ThawThreadAsync(DebugSessionState state, int threadId, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
            public Task<DebugInspectionResult<bool>> RunToCursorAsync(DebugSessionState state, CancellationToken cancellationToken = default)
                => Task.FromResult(DebugInspectionResult<bool>.Rejected(state, "stub"));
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
