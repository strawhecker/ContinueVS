using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Events;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Comprehensive xUnit test suite for ErrorRepository (gap29_7).
    /// Tests coverage:
    /// - Store and retrieve error records
    /// - Query by type and fingerprint
    /// - Cleanup old errors (30+ days)
    /// - Export to JSON and CSV formats
    /// </summary>
    public class ErrorRepositoryTests : IDisposable
    {
        private readonly string _testDir;

        public ErrorRepositoryTests()
        {
            // Create a truly unique test directory to avoid cross-test contamination
            _testDir = Path.Combine(Path.GetTempPath(), "error-repo-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                try
                {
                    Directory.Delete(_testDir, true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        // ====================================================================
        // TEST 1: StoreError_And_Retrieve_By_Fingerprint_Success
        // ====================================================================

        [Fact]
        public async Task StoreError_And_Retrieve_By_Fingerprint_Success()
        {
            // Arrange
            var mockConfigService = new MockConfigService(_testDir);
            var errorsDir = Path.Combine(_testDir, "errors");
            var repo = new ErrorRepository(mockConfigService, null, errorsDir);
            await repo.InitializeAsync();

            var errorRecord = new ErrorRecord(
                fingerprint: "test-fingerprint-001",
                exceptionType: "System.NullReferenceException",
                exceptionMessage: "Object reference not set to an instance of an object",
                stackTraceJson: @"[{""method"":""TestMethod"",""file"":""test.cs""}]",
                userNotes: "Test error 1",
                sessionId: "session-123"
            );

            // Act
            await repo.StoreErrorAsync(errorRecord);
            var retrieved = await repo.GetErrorsByFingerprintAsync("test-fingerprint-001");

            // Assert
            Assert.NotEmpty(retrieved);
            var result = retrieved.First();
            Assert.Equal("test-fingerprint-001", result.Fingerprint);
            Assert.Equal("System.NullReferenceException", result.ExceptionType);
            Assert.Equal("Object reference not set to an instance of an object", result.ExceptionMessage);
            Assert.Equal("session-123", result.SessionId);
        }

        // ====================================================================
        // TEST 2: QueryByType_Returns_Matching_Errors_Only
        // ====================================================================

        [Fact]
        public async Task QueryByType_Returns_Matching_Errors_Only()
        {
            // Arrange
            var mockConfigService = new MockConfigService(_testDir);
            var errorsDir = Path.Combine(_testDir, "errors");
            var repo = new ErrorRepository(mockConfigService, null, errorsDir);
            await repo.InitializeAsync();

            var error1 = new ErrorRecord(
                fingerprint: "fp-001",
                exceptionType: "System.NullReferenceException",
                exceptionMessage: "Object reference",
                stackTraceJson: "[]",
                sessionId: "session-1"
            );

            var error2 = new ErrorRecord(
                fingerprint: "fp-002",
                exceptionType: "System.ArgumentException",
                exceptionMessage: "Argument invalid",
                stackTraceJson: "[]",
                sessionId: "session-1"
            );

            var error3 = new ErrorRecord(
                fingerprint: "fp-003",
                exceptionType: "System.NullReferenceException",
                exceptionMessage: "Another null ref",
                stackTraceJson: "[]",
                sessionId: "session-2"
            );

            // Act
            await repo.StoreErrorAsync(error1);
            await repo.StoreErrorAsync(error2);
            await repo.StoreErrorAsync(error3);

            var nullRefErrors = await repo.GetErrorsByTypeAsync("System.NullReferenceException");
            var argErrors = await repo.GetErrorsByTypeAsync("System.ArgumentException");

            // Assert
            Assert.Equal(2, nullRefErrors.Count());
            Assert.Single(argErrors);
            Assert.All(nullRefErrors, e => Assert.Equal("System.NullReferenceException", e.ExceptionType));
            Assert.All(argErrors, e => Assert.Equal("System.ArgumentException", e.ExceptionType));
        }

        // ====================================================================
        // TEST 3: Cleanup_Auto_Deletes_Errors_Older_Than_30_Days
        // ====================================================================

        [Fact]
        public async Task Cleanup_Auto_Deletes_Errors_Older_Than_30_Days()
        {
            // Arrange
            var mockConfigService = new MockConfigService(_testDir);
            var errorsDir = Path.Combine(_testDir, "errors");
            var repo = new ErrorRepository(mockConfigService, null, errorsDir);
            await repo.InitializeAsync();

            // Create an old error manually (bypass timestamp creation)
            var oldTimestamp = DateTime.UtcNow.AddDays(-35);
            var oldRecord = new ErrorRecord(
                timestamp: oldTimestamp,
                fingerprint: "fp-old",
                exceptionType: "System.Exception",
                exceptionMessage: "Old error",
                stackTraceJson: "[]",
                userNotes: "",
                sessionId: ""
            );

            var recentRecord = new ErrorRecord(
                fingerprint: "fp-recent",
                exceptionType: "System.Exception",
                exceptionMessage: "Recent error",
                stackTraceJson: "[]",
                sessionId: ""
            );

            // Act
            await repo.StoreErrorAsync(oldRecord);
            await repo.StoreErrorAsync(recentRecord);

            var countBefore = await repo.GetTotalErrorCountAsync();

            // Cleanup errors older than 30 days
            await repo.DeleteErrorsOlderThanAsync(30);

            var countAfter = await repo.GetTotalErrorCountAsync();
            var oldErrors = await repo.GetErrorsByFingerprintAsync("fp-old");
            var recentErrors = await repo.GetErrorsByFingerprintAsync("fp-recent");

            // Assert
            Assert.Equal(2, countBefore);
            Assert.Equal(1, countAfter);
            Assert.Empty(oldErrors);
            Assert.NotEmpty(recentErrors);
        }

        // ====================================================================
        // TEST 4: Export_As_JSON_And_CSV_Creates_Valid_Files
        // ====================================================================

        [Fact]
        public async Task Export_As_JSON_And_CSV_Creates_Valid_Files()
        {
            // Arrange
            var mockConfigService = new MockConfigService(_testDir);
            var errorsDir = Path.Combine(_testDir, "errors");
            var repo = new ErrorRepository(mockConfigService, null, errorsDir);
            await repo.InitializeAsync();

            var error1 = new ErrorRecord(
                fingerprint: "fp-001",
                exceptionType: "System.NullReferenceException",
                exceptionMessage: "Null ref exception",
                stackTraceJson: "[]",
                userNotes: "Test note 1",
                sessionId: "session-1"
            );

            var error2 = new ErrorRecord(
                fingerprint: "fp-002",
                exceptionType: "System.IOException",
                exceptionMessage: "IO error",
                stackTraceJson: "[]",
                userNotes: "Test note 2",
                sessionId: "session-2"
            );

            await repo.StoreErrorAsync(error1);
            await repo.StoreErrorAsync(error2);

            var jsonPath = Path.Combine(_testDir, "export.json");
            var csvPath = Path.Combine(_testDir, "export.csv");

            // Act
            await repo.ExportAsJsonAsync(jsonPath);
            await repo.ExportAsCsvAsync(csvPath);

            // Assert
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(csvPath));

            var jsonContent = File.ReadAllText(jsonPath);
            var csvContent = File.ReadAllText(csvPath);

            // Verify JSON contains expected data
            Assert.Contains("fp-001", jsonContent);
            Assert.Contains("System.NullReferenceException", jsonContent);
            Assert.Contains("Test note 1", jsonContent);

            // Verify CSV contains expected data
            Assert.Contains("fp-001", csvContent);
            Assert.Contains("System.NullReferenceException", csvContent);
            Assert.Contains("System.IOException", csvContent);
            Assert.Contains("Timestamp,Fingerprint,ExceptionType", csvContent);
        }

        // ====================================================================
        // Helper: Stub IConfigService for testing (provides only minimal interface)
        // ====================================================================

        private class MockConfigService : IConfigService
        {
            private readonly string _testDir;

            public MockConfigService(string testDir)
            {
                _testDir = testDir;
            }

            public event EventHandler<ConfigChangedEventArgs> ConfigChanged
            {
                add { }
                remove { }
            }

            public Task InitializeAsync() => Task.CompletedTask;

            public ContinueConfig GetCurrentConfig() => new ContinueConfig();

            public Task AddModelAsync(ModelInfo model) => Task.CompletedTask;

            public Task RemoveModelAsync(string modelId) => Task.CompletedTask;

            public Task SelectModelAsync(string modelId) => Task.CompletedTask;

            public ModelInfo GetSelectedModel() => null;

            public IEnumerable<ToolDefinition> GetEnabledTools() => Enumerable.Empty<ToolDefinition>();

            public Task SetToolEnabledAsync(string toolName, bool enabled) => Task.CompletedTask;

            public IEnumerable<ProfileInfo> GetProfiles() => Enumerable.Empty<ProfileInfo>();

            public Task SelectProfileAsync(string profileId) => Task.CompletedTask;

            public Task SaveConfigAsync() => Task.CompletedTask;

            public Task ReloadConfigAsync() => Task.CompletedTask;

            public ToolOverrideConfig GetToolOverrideConfig() => null;

            public Task<UIState> GetUIStateAsync() => Task.FromResult(new UIState());

            public Task SaveUIStateAsync(UIState state) => Task.CompletedTask;

            public Task SaveDefaultModeAsync(int mode) => Task.CompletedTask;

            public Task<int> GetDefaultModeAsync() => Task.FromResult(0);

            public Task SaveDefaultPolicyAsync(ContinuationPolicy policy) => Task.CompletedTask;

            public Task<ContinuationPolicy> GetDefaultPolicyAsync() => Task.FromResult(ContinuationPolicy.Interactive);
        }
    }
}
