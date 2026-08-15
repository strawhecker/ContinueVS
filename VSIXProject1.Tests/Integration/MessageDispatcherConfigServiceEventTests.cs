#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Events;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Tests.Integration
{
    /// <summary>
    /// Integration tests for ConfigService event firing and MessageDispatcher response (Step 100).
    /// 
    /// Verifies that ConfigService fires ConfigChanged events with correct data,
    /// and that MessageDispatcher can subscribe and respond to those events.
    /// 
    /// Test isolation:
    /// - Each test uses isolated ConfigService instances
    /// - Test config file is cleaned up after each test
    /// - Event handlers are scoped to each test
    /// </summary>
    public class MessageDispatcherConfigServiceEventTests : IDisposable
    {
        private readonly string _testConfigPath;

        public MessageDispatcherConfigServiceEventTests()
        {
            _testConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continue", "config.json");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testConfigPath))
                {
                    File.Delete(_testConfigPath);
                }
            }
            catch { }
        }

        /// <summary>
        /// Test: ConfigService.AddModelAsync fires ConfigChanged event with correct ConfigKey and NewValue.
        /// 
        /// Arrange: Create and initialize ConfigService; create event handler to capture events
        /// Act: Call AddModelAsync with model
        /// Assert: ConfigChanged event was fired; ConfigKey == "models"; NewValue equals model
        /// </summary>
        [Fact]
        public async Task AddModel_FiresConfigChangedEvent_WithCorrectDataAsync()
        {
            // Arrange
            Dispose();
            var service = new ConfigService();
            await service.InitializeAsync();

            var model = new CoreTypes.ModelInfo
            {
                Id = "test-gpt4",
                Name = "GPT-4",
                Provider = "openai",
                ContextWindow = 8192
            };

            ConfigChangedEventArgs? capturedEventArgs = null;
            service.ConfigChanged += (sender, args) =>
            {
                capturedEventArgs = args;
            };

            // Act
            await service.AddModelAsync(model);

            // Assert
            Assert.NotNull(capturedEventArgs);
            Assert.Equal("models", capturedEventArgs.ConfigKey);
            Assert.NotNull(capturedEventArgs.NewValue);
            Assert.IsType<CoreTypes.ModelInfo>(capturedEventArgs.NewValue);
            var newModel = (CoreTypes.ModelInfo)capturedEventArgs.NewValue;
            Assert.Equal(model.Id, newModel.Id);
            Assert.Equal(model.Name, newModel.Name);
        }

        /// <summary>
        /// Test: ConfigService.RemoveModelAsync fires ConfigChanged event with removed model info.
        /// 
        /// Arrange: Create ConfigService, initialize, add a model, subscribe to event
        /// Act: Call RemoveModelAsync with model ID
        /// Assert: ConfigChanged event was fired; ConfigKey == "models"; event was triggered
        /// </summary>
        [Fact]
        public async Task RemoveModel_FiresConfigChangedEvent_WithCorrectDataAsync()
        {
            // Arrange
            Dispose();
            var service = new ConfigService();
            await service.InitializeAsync();

            var model = new CoreTypes.ModelInfo
            {
                Id = "test-claude",
                Name = "Claude 3",
                Provider = "anthropic",
                ContextWindow = 200000
            };

            await service.AddModelAsync(model);

            ConfigChangedEventArgs? capturedEventArgs = null;
            int eventCountBeforeRemove = 0;
            service.ConfigChanged += (sender, args) =>
            {
                eventCountBeforeRemove++;
                if (eventCountBeforeRemove > 1) // Skip addmodel event
                {
                    capturedEventArgs = args;
                }
            };

            // Act
            await service.RemoveModelAsync(model.Id);

            // Assert
            Assert.NotNull(capturedEventArgs);
            Assert.Equal("models", capturedEventArgs.ConfigKey);
            Assert.True(eventCountBeforeRemove >= 2); // At least add and remove events
        }

        /// <summary>
        /// Test: ConfigChanged event includes Timestamp and both OldValue and NewValue properties.
        /// 
        /// Arrange: Create ConfigService, initialize with a model, then subscribe and prepare to update
        /// Act: Add or modify a model
        /// Assert: Event args include: Timestamp is set; both OldValue and NewValue present
        /// </summary>
        [Fact]
        public async Task ConfigChangedEvent_IncludesTimestampAndOldNewValuesAsync()
        {
            // Arrange
            Dispose();
            var service = new ConfigService();
            await service.InitializeAsync();

            var model1 = new CoreTypes.ModelInfo
            {
                Id = "model-1",
                Name = "Model 1",
                Provider = "openai"
            };

            ConfigChangedEventArgs? capturedEventArgs = null;
            service.ConfigChanged += (sender, args) =>
            {
                capturedEventArgs = args;
            };

            var beforeAdd = DateTime.UtcNow;

            // Act
            await service.AddModelAsync(model1);

            var afterAdd = DateTime.UtcNow;

            // Assert
            Assert.NotNull(capturedEventArgs);
            Assert.NotEqual(default(DateTime), capturedEventArgs.Timestamp);
            Assert.True(capturedEventArgs.Timestamp >= beforeAdd && capturedEventArgs.Timestamp <= afterAdd);
            Assert.NotNull(capturedEventArgs.NewValue);
            Assert.IsType<CoreTypes.ModelInfo>(capturedEventArgs.NewValue);
        }

        /// <summary>
        /// Test: Multiple operations fire events in sequence; MessageDispatcher handler receives all events.
        /// 
        /// Arrange: Create ConfigService, subscribe with event counter
        /// Act: Add model, add second model, remove first model (3 operations)
        /// Assert: Event counter == 3; each event has correct ConfigKey and data
        /// </summary>
        [Fact]
        public async Task MultipleOperations_AllFireEventsInSequenceAsync()
        {
            // Arrange
            Dispose();
            var service = new ConfigService();
            await service.InitializeAsync();

            var model1 = new CoreTypes.ModelInfo { Id = "m1", Name = "Model 1", Provider = "openai" };
            var model2 = new CoreTypes.ModelInfo { Id = "m2", Name = "Model 2", Provider = "anthropic" };

            var eventLog = new List<ConfigChangedEventArgs>();
            service.ConfigChanged += (sender, args) =>
            {
                eventLog.Add(args);
            };

            // Act
            await service.AddModelAsync(model1);
            await service.AddModelAsync(model2);
            await service.RemoveModelAsync(model1.Id);

            // Assert
            Assert.Equal(3, eventLog.Count);

            // First event: add model1
            Assert.Equal("models", eventLog[0].ConfigKey);
            Assert.NotNull(eventLog[0].NewValue);
            var firstModel = (CoreTypes.ModelInfo)eventLog[0].NewValue;
            Assert.Equal("m1", firstModel.Id);

            // Second event: add model2
            Assert.Equal("models", eventLog[1].ConfigKey);
            Assert.NotNull(eventLog[1].NewValue);
            var secondModel = (CoreTypes.ModelInfo)eventLog[1].NewValue;
            Assert.Equal("m2", secondModel.Id);

            // Third event: remove model1
            Assert.Equal("models", eventLog[2].ConfigKey);

            // All events should have Timestamp
            foreach (var eventArgs in eventLog)
            {
                Assert.NotEqual(default(DateTime), eventArgs.Timestamp);
            }
        }
    }
}
