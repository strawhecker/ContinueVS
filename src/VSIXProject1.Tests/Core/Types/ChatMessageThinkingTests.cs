using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using Xunit;
using Newtonsoft.Json;

namespace ContinueVS.Tests.Core.Types
{
    /// <summary>
    /// gap68: Tests for ChatMessage IsThinking and IsExpanded properties.
    /// Verifies persistence, serialization, and UI state behavior.
    /// </summary>
    public class ChatMessageThinkingTests
    {
        [Fact]
        public void ChatMessage_IsThinking_DefaultsFalse()
        {
            // Act
            var msg = new ChatMessage();

            // Assert
            Assert.False(msg.IsThinking);
        }

        [Fact]
        public void ChatMessage_IsExpanded_DefaultsFalse()
        {
            // Act
            var msg = new ChatMessage();

            // Assert
            Assert.False(msg.IsExpanded);
        }

        [Fact]
        public void ChatMessage_IsThinking_CanBeSet()
        {
            // Arrange
            var msg = new ChatMessage();

            // Act
            msg.IsThinking = true;

            // Assert
            Assert.True(msg.IsThinking);
        }

        [Fact]
        public void ChatMessage_IsExpanded_CanBeSet()
        {
            // Arrange
            var msg = new ChatMessage();

            // Act
            msg.IsExpanded = true;

            // Assert
            Assert.True(msg.IsExpanded);
        }

        [Fact]
        public void ChatMessage_SerializesIsThinkingFlag_ToJson()
        {
            // Arrange
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = ChatMessageRole.Thinking,
                Content = "Analyzing the problem...",
                IsThinking = true
            };

            // Act
            var json = JsonConvert.SerializeObject(msg);
            var deserialized = JsonConvert.DeserializeObject<ChatMessage>(json);

            // Assert
            Assert.NotNull(json);
            Assert.Contains("\"isThinking\"", json);
            Assert.NotNull(deserialized);
            Assert.True(deserialized.IsThinking);
        }

        [Fact]
        public void ChatMessage_DoesNotSerializeIsExpanded_ToJson()
        {
            // Arrange
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = ChatMessageRole.Thinking,
                Content = "Thinking",
                IsExpanded = true
            };

            // Act
            var json = JsonConvert.SerializeObject(msg);

            // Assert
            Assert.NotNull(json);
            Assert.DoesNotContain("\"isExpanded\"", json);
        }

        [Fact]
        public void ChatMessage_PropertyChanged_FiredOnIsThinkingChange()
        {
            // Arrange
            var msg = new ChatMessage();
            var changedProperties = new List<string>();
            msg.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName ?? "");

            // Act
            msg.IsThinking = true;

            // Assert
            Assert.Contains("IsThinking", changedProperties);
        }

        [Fact]
        public void ChatMessage_PropertyChanged_FiredOnIsExpandedChange()
        {
            // Arrange
            var msg = new ChatMessage();
            var changedProperties = new List<string>();
            msg.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName ?? "");

            // Act
            msg.IsExpanded = true;

            // Assert
            Assert.Contains("IsExpanded", changedProperties);
        }

        [Fact]
        public void ChatMessage_IsExpanded_FiredMultipleTimes()
        {
            // Arrange
            var msg = new ChatMessage();
            int changeCount = 0;
            msg.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "IsExpanded")
                    changeCount++;
            };

            // Act
            msg.IsExpanded = true;
            msg.IsExpanded = false;
            msg.IsExpanded = true;

            // Assert
            Assert.Equal(3, changeCount);
        }

        [Fact]
        public void ChatMessage_IsExpanded_NoChangeIfSameValue()
        {
            // Arrange
            var msg = new ChatMessage();
            int changeCount = 0;
            msg.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "IsExpanded")
                    changeCount++;
            };

            // Act
            msg.IsExpanded = true;
            msg.IsExpanded = true;  // Same value, should not fire

            // Assert
            Assert.Equal(1, changeCount);
        }

        [Fact]
        public void ChatMessage_Thinking_WithThinkingRole()
        {
            // Arrange & Act
            var msg = new ChatMessage
            {
                Role = ChatMessageRole.Thinking,
                Content = "Model reasoning",
                IsThinking = true,
                IsExpanded = false
            };

            // Assert
            Assert.Equal(ChatMessageRole.Thinking, msg.Role);
            Assert.True(msg.IsThinking);
            Assert.False(msg.IsExpanded);
        }

        [Fact]
        public void ChatMessage_RoundTrip_PreservesIsThinking()
        {
            // Arrange
            var original = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = ChatMessageRole.Thinking,
                Content = "Deep thought process",
                IsThinking = true,
                Timestamp = DateTime.Now
            };

            // Act
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<ChatMessage>(json);

            // Assert
            Assert.NotNull(restored);
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.Role, restored.Role);
            Assert.Equal(original.Content, restored.Content);
            Assert.True(restored.IsThinking);
        }

        [Fact]
        public void ChatMessage_RoundTrip_IsExpanded_NotPreserved()
        {
            // Arrange
            var original = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = ChatMessageRole.Thinking,
                Content = "Thinking",
                IsExpanded = true
            };

            // Act
            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<ChatMessage>(json);

            // Assert
            Assert.NotNull(restored);
            // IsExpanded is not serialized, so restored should be false (default)
            Assert.False(restored.IsExpanded);
        }
    }
}
