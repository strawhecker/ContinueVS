#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Services
{
    public class LocalStorageServiceTests : IDisposable
    {
        private readonly LocalStorageService _service;
        private readonly string _testCacheDir;
        private readonly string _testCachePath;

        public LocalStorageServiceTests()
        {
            // Use a temporary directory for test isolation
            _testCacheDir = Path.Combine(Path.GetTempPath(), $"continueVS_test_{Guid.NewGuid()}");
            _testCachePath = Path.Combine(_testCacheDir, "localStorageCache.json");

            // Create service without logger
            _service = new LocalStorageService();
        }

        public void Dispose()
        {
            // Clean up test directory
            try
            {
                if (Directory.Exists(_testCacheDir))
                {
                    Directory.Delete(_testCacheDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void SetItem_StoresValue_AndFiresEvent()
        {
            // Arrange
            var uniqueKey = $"uniqueTestKey_{Guid.NewGuid()}";
            var eventFired = false;
            LocalStorageChangedEventArgs? eventArgs = null;
            _service.LocalStorageChanged += (s, e) =>
            {
                eventFired = true;
                eventArgs = e;
            };

            // Act
            _service.SetItem(uniqueKey, "testValue");

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(eventArgs);
            Assert.Equal(uniqueKey, eventArgs.Key);
            Assert.Equal("testValue", eventArgs.NewValue);
            Assert.Null(eventArgs.OldValue);
        }

        [Fact]
        public void GetItem_ReturnsStoredValue_WhenKeyExists()
        {
            // Arrange
            _service.SetItem("uniquekey1", "value1");

            // Act
            var result = _service.GetItem<string>("uniquekey1");

            // Assert
            Assert.Equal("value1", result);
        }

        [Fact]
        public void GetItem_ReturnsNull_WhenKeyMissing()
        {
            // Act
            var result = _service.GetItem<string>("nonexistent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void RemoveItem_DeletesKey_AndFiresEvent()
        {
            // Arrange
            _service.SetItem("toRemove", "value");
            var eventFired = false;
            LocalStorageChangedEventArgs? eventArgs = null;
            _service.LocalStorageChanged += (s, e) =>
            {
                eventFired = true;
                eventArgs = e;
            };

            // Act
            _service.RemoveItem("toRemove");

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(eventArgs);
            Assert.Equal("toRemove", eventArgs.Key);
            Assert.Null(eventArgs.NewValue);
            Assert.Equal("value", eventArgs.OldValue);
            Assert.Null(_service.GetItem<string>("toRemove"));
        }

        [Fact]
        public void SetItem_WithSameValue_FiresEventAnyway()
        {
            // Arrange
            _service.SetItem("uniquekey456", "value");
            var eventCount = 0;
            _service.LocalStorageChanged += (s, e) => eventCount++;

            // Act
            _service.SetItem("uniquekey456", "value");

            // Assert
            Assert.Equal(1, eventCount); // Event fires even with identical value
        }

        [Fact]
        public void LocalStorageChanged_EventFires_WithCorrectEventArgs()
        {
            // Arrange
            var capturedArgs = new List<LocalStorageChangedEventArgs>();
            _service.LocalStorageChanged += (s, e) => capturedArgs.Add(e);

            // Act
            _service.SetItem("uniquekey789", 42);
            _service.SetItem("uniquekey789", 100);
            _service.RemoveItem("uniquekey789");

            // Assert
            Assert.Equal(3, capturedArgs.Count);
            Assert.Equal("uniquekey789", capturedArgs[0].Key);
            Assert.Equal(42, capturedArgs[0].NewValue);
            Assert.Equal("uniquekey789", capturedArgs[1].Key);
            Assert.Equal(42, capturedArgs[1].OldValue);
            Assert.Equal(100, capturedArgs[1].NewValue);
            Assert.Equal("uniquekey789", capturedArgs[2].Key);
            Assert.Equal(100, capturedArgs[2].OldValue);
            Assert.Null(capturedArgs[2].NewValue);
        }

        [Fact]
        public void SetItem_WithComplexObject_SerializesAndDeserializes()
        {
            // Arrange
            var complexObject = new { Name = "Test", Count = 5, Values = new[] { 1, 2, 3 } };

            // Act
            _service.SetItem("uniquecomplexkey", complexObject);
            var result = _service.GetItem<dynamic>("uniquecomplexkey");

            // Assert
            Assert.NotNull(result);
            if (result != null)
            {
                Assert.Equal("Test", (string)result.Name);
                Assert.Equal(5L, (long)result.Count); // JSON deserializes to long by default
            }
        }

        [Fact]
        public void FileNotExists_OnFirstLoad_CreatesEmptyCache()
        {
            // Arrange & Act
            var service = new LocalStorageService();

            // Assert
            // Service should initialize without error and have empty cache
            var value = service.GetItem<string>("anykey");
            Assert.Null(value);
        }

        [Fact]
        public async System.Threading.Tasks.Task Concurrent_SetItem_RespectsMutex()
        {
            // Arrange
            var tasks = new List<System.Threading.Tasks.Task>();

            // Act: Launch multiple concurrent SetItem operations
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    _service.SetItem($"key{index}", $"value{index}");
                });
                tasks.Add(task);
            }
            await System.Threading.Tasks.Task.WhenAll(tasks);

            // Assert: All keys should be stored without exceptions
            for (int i = 0; i < 10; i++)
            {
                var value = _service.GetItem<string>($"key{i}");
                Assert.Equal($"value{i}", value);
            }
        }

        [Fact]
        public void SetItem_WithNullString_StoresNullValue()
        {
            // Arrange & Act
            _service.SetItem("uniquenullkey", (string?)null);
            var result = _service.GetItem<string>("uniquenullkey");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SetItem_WithEmptyString_StoresEmptyString()
        {
            // Arrange & Act
            _service.SetItem("uniqueemptykey", "");
            var result = _service.GetItem<string>("uniqueemptykey");

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void SetItem_WithInteger_StoresAndRetrievesInteger()
        {
            // Arrange & Act
            _service.SetItem("uniqueintkey", 42);
            var result = _service.GetItem<int>("uniqueintkey");

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void SetItem_WithBoolean_StoresAndRetrievesBoolean()
        {
            // Arrange & Act
            _service.SetItem("uniqueboolkey", true);
            var result = _service.GetItem<bool>("uniqueboolkey");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void RemoveItem_OnNonexistentKey_DoesNotThrow()
        {
            // Act & Assert (should not throw)
            _service.RemoveItem("nonexistent");
        }

        [Fact]
        public void RemoveItem_OnNonexistentKey_DoesNotFireEvent()
        {
            // Arrange
            var eventFired = false;
            _service.LocalStorageChanged += (s, e) => eventFired = true;

            // Act
            _service.RemoveItem("nonexistent");

            // Assert
            Assert.False(eventFired);
        }

        [Fact]
        public void SetItem_WithNullKey_DoesNotThrow()
        {
            // Act & Assert (should not throw)
            _service.SetItem(null!, "value");
        }

        [Fact]
        public void GetItem_WithNullKey_ReturnsNull()
        {
            // Act
            var result = _service.GetItem<string>(null!);

            // Assert
            Assert.Null(result);
        }
    }
}
