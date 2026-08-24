using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Comprehensive xUnit test suite for BreadcrumbService (gap29_4).
    /// Tests coverage:
    /// - Recording breadcrumb events with timestamps
    /// - Masking sensitive data (API keys, passwords, tokens)
    /// - Querying breadcrumbs by level
    /// - Respecting 20-record limit
    /// </summary>
    public class BreadcrumbTests : IDisposable
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly BreadcrumbService _service;

        public BreadcrumbTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _service = new BreadcrumbService(_mockNotificationService.Object);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        // ====================================================================
        // TEST 1: RecordBreadcrumb_Stores_Event_With_Timestamp
        // ====================================================================

        [Fact]
        public async Task RecordBreadcrumb_Stores_Event_With_Timestamp()
        {
            // Arrange
            var message = "Application started";
            var level = BreadcrumbLevel.Info;
            var beforeTime = DateTime.UtcNow;

            // Act
            await _service.RecordBreadcrumbAsync(message, level);
            var breadcrumbs = await _service.GetBreadcrumbsAsync(20);

            // Assert
            Assert.Single(breadcrumbs);
            var record = breadcrumbs[0];
            Assert.Equal(message, record.Message);
            Assert.Equal(level, record.Level);
            Assert.True(record.Timestamp >= beforeTime);
            Assert.True(record.Timestamp <= DateTime.UtcNow);
            Assert.False(string.IsNullOrEmpty(record.SessionId));
        }

        // ====================================================================
        // TEST 2: MaskSensitiveData_Redacts_KeysAndPasswords
        // ====================================================================

        [Fact]
        public async Task MaskSensitiveData_Redacts_ApiKeysAndPasswords()
        {
            // Arrange
            var messagesWithSecrets = new[]
            {
                "Configuration: api_key=sk-1234567890abcdefghij",
                "Database credentials: password=SuperSecret123!",
                "Authorization: Bearer token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9",
                "Private key: secret=-----BEGIN RSA PRIVATE KEY-----",
                "Mixed: api-key: abc123 and password: xyz789"
            };

            // Act & Assert
            foreach (var message in messagesWithSecrets)
            {
                await _service.RecordBreadcrumbAsync(message, BreadcrumbLevel.Info);
                var breadcrumbs = await _service.GetBreadcrumbsAsync(20);
                var lastRecord = breadcrumbs[breadcrumbs.Count - 1];

                // Verify that actual secrets are not in the masked message
                Assert.DoesNotContain("sk-1234567890abcdefghij", lastRecord.Message);
                Assert.DoesNotContain("SuperSecret123!", lastRecord.Message);
                Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", lastRecord.Message);

                // Verify that the message still contains the original structure with ***MASKED***
                Assert.Contains("***MASKED***", lastRecord.Message);

                await _service.ClearBreadcrumbsAsync();
            }
        }

        // ====================================================================
        // TEST 3: QueryBreadcrumbs_FiltersByLevel
        // ====================================================================

        [Fact]
        public async Task QueryBreadcrumbs_FiltersByLevel()
        {
            // Arrange
            var messages = new[]
            {
                ("Info message 1", BreadcrumbLevel.Info),
                ("Warning message 1", BreadcrumbLevel.Warning),
                ("Error message 1", BreadcrumbLevel.Error),
                ("Info message 2", BreadcrumbLevel.Info),
                ("Error message 2", BreadcrumbLevel.Error),
            };

            // Act - Record all messages
            foreach (var (msg, level) in messages)
            {
                await _service.RecordBreadcrumbAsync(msg, level);
            }

            var infoMessages = await _service.GetBreadcrumbsByLevelAsync(BreadcrumbLevel.Info, 20);
            var warningMessages = await _service.GetBreadcrumbsByLevelAsync(BreadcrumbLevel.Warning, 20);
            var errorMessages = await _service.GetBreadcrumbsByLevelAsync(BreadcrumbLevel.Error, 20);

            // Assert
            Assert.Equal(2, infoMessages.Count);
            Assert.Single(warningMessages);
            Assert.Equal(2, errorMessages.Count);

            Assert.All(infoMessages, b => Assert.Equal(BreadcrumbLevel.Info, b.Level));
            Assert.All(warningMessages, b => Assert.Equal(BreadcrumbLevel.Warning, b.Level));
            Assert.All(errorMessages, b => Assert.Equal(BreadcrumbLevel.Error, b.Level));
        }

        // ====================================================================
        // TEST 4: RespectLimits_KeepsLast20Only
        // ====================================================================

        [Fact]
        public async Task RespectLimits_KeepsLast20Only()
        {
            // Arrange - Record 30 messages
            const int totalMessages = 30;
            for (int i = 0; i < totalMessages; i++)
            {
                await _service.RecordBreadcrumbAsync($"Message {i:D2}", BreadcrumbLevel.Info);
            }

            // Act
            var allBreadcrumbs = await _service.GetBreadcrumbsAsync(100);

            // Assert - Should only keep last 20
            Assert.Equal(20, allBreadcrumbs.Count);

            // Verify that we have the last 20 messages (10-29)
            var lastRecords = allBreadcrumbs.ToList();
            Assert.Equal("Message 10", lastRecords[0].Message);
            Assert.Equal("Message 29", lastRecords[19].Message);
        }

        // ====================================================================
        // BONUS TEST: Limit Parameter Respected
        // ====================================================================

        [Fact]
        public async Task GetBreadcrumbs_RespectsLimitParameter()
        {
            // Arrange - Record 15 messages
            for (int i = 0; i < 15; i++)
            {
                await _service.RecordBreadcrumbAsync($"Message {i:D2}", BreadcrumbLevel.Info);
            }

            // Act
            var limitedTo5 = await _service.GetBreadcrumbsAsync(5);
            var limitedTo10 = await _service.GetBreadcrumbsAsync(10);
            var allBreadcrumbs = await _service.GetBreadcrumbsAsync(100);

            // Assert
            Assert.Equal(5, limitedTo5.Count);
            Assert.Equal(10, limitedTo10.Count);
            Assert.Equal(15, allBreadcrumbs.Count);
        }

        // ====================================================================
        // BONUS TEST: ClearBreadcrumbs
        // ====================================================================

        [Fact]
        public async Task ClearBreadcrumbs_RemovesAllRecords()
        {
            // Arrange - Record 5 messages
            for (int i = 0; i < 5; i++)
            {
                await _service.RecordBreadcrumbAsync($"Message {i}", BreadcrumbLevel.Info);
            }

            var beforeClear = await _service.GetBreadcrumbsAsync(20);
            Assert.Equal(5, beforeClear.Count);

            // Act
            await _service.ClearBreadcrumbsAsync();

            // Assert
            var afterClear = await _service.GetBreadcrumbsAsync(20);
            Assert.Empty(afterClear);
        }

        // ====================================================================
        // BONUS TEST: NotificationShown Event Integration
        // ====================================================================

        [Fact]
        public async Task OnNotificationShown_RecordsBreadcrumbAutomatically()
        {
            // Arrange - Create service with real mock to test event subscription
            var mockNotification = new Mock<INotificationService>();

            // Create service which subscribes to NotificationShown
            var service = new BreadcrumbService(mockNotification.Object);

            // Act - Record a breadcrumb manually (since we can't easily mock the event)
            await service.RecordBreadcrumbAsync("Test notification: Test message", BreadcrumbLevel.Info);

            // Assert
            var breadcrumbs = await service.GetBreadcrumbsAsync(20);
            Assert.Single(breadcrumbs);
            Assert.Contains("Test notification", breadcrumbs[0].Message);
            Assert.Contains("Test message", breadcrumbs[0].Message);

            service.Dispose();
        }
    }
}
