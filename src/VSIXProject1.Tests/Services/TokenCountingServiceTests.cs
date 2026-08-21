using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class TokenCountingServiceTests
    {
        [Fact]
        public void CountMessageTokens_EmptyMessage_ReturnsMinimumTokens()
        {
            // Arrange
            var service = new SimpleTokenCounterService();
            var message = new ChatMessage { Role = ChatMessageRole.User, Content = "" };

            // Act
            int tokens = service.CountMessageTokens(message);

            // Assert
            // Empty content: 0 / 4 = 0, max(1, 0) = 1, + 50 wrapper = 51, max(5, 51) = 51
            Assert.Equal(51, tokens);
        }

        [Fact]
        public void CountMessageTokens_ShortMessage_IncludesWrapperOverhead()
        {
            // Arrange
            var service = new SimpleTokenCounterService();
            var message = new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" };

            // Act
            int tokens = service.CountMessageTokens(message);

            // Assert
            // 2 chars / 4 chars-per-token = 0 (rounded down) + 1 (min) + 50 (wrapper) = 51
            // But MinTokensPerMessage is 5, so max(5, 51) = 51
            Assert.Equal(51, tokens);
        }

        [Fact]
        public void CountMessageTokens_LargeMessage_CalculatesCorrectly()
        {
            // Arrange
            var service = new SimpleTokenCounterService();
            var message = new ChatMessage { Role = ChatMessageRole.User, Content = new string('a', 400) };

            // Act
            int tokens = service.CountMessageTokens(message);

            // Assert
            // 400 chars / 4 = 100 tokens + 50 wrapper = 150 tokens
            Assert.Equal(150, tokens);
        }

        [Fact]
        public void CountMessageTokens_NullMessage_ReturnsZero()
        {
            // Arrange
            var service = new SimpleTokenCounterService();

            // Act
            int tokens = service.CountMessageTokens(null);

            // Assert
            Assert.Equal(0, tokens);
        }

        [Fact]
        public void CountMessagesTokens_EmptyList_ReturnsZero()
        {
            // Arrange
            var service = new SimpleTokenCounterService();
            var messages = new List<ChatMessage>();

            // Act
            int tokens = service.CountMessagesTokens(messages);

            // Assert
            Assert.Equal(0, tokens);
        }

        [Fact]
        public void CountMessagesTokens_MultipleMessages_SumsTotals()
        {
            // Arrange
            var service = new SimpleTokenCounterService();
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" },
                new ChatMessage { Role = ChatMessageRole.Assistant, Content = new string('a', 400) }
            };

            // Act
            int tokens = service.CountMessagesTokens(messages);

            // Assert
            // Message 1: 51 tokens (2 chars -> min 51)
            // Message 2: 150 tokens (400 chars -> 100 + 50 wrapper)
            // Total: 201
            Assert.Equal(201, tokens);
        }

        [Fact]
        public void EstimateFutureMessageTokens_EmptyContent_ReturnsMinimumTokens()
        {
            // Arrange
            var service = new SimpleTokenCounterService();

            // Act
            int tokens = service.EstimateFutureMessageTokens("");

            // Assert
            Assert.Equal(5, tokens); // MinTokensPerMessage
        }

        [Fact]
        public void EstimateFutureMessageTokens_ShortContent_IncludesWrapper()
        {
            // Arrange
            var service = new SimpleTokenCounterService();

            // Act
            int tokens = service.EstimateFutureMessageTokens("Hello");

            // Assert
            // 5 chars / 4 = 1 token + 50 wrapper = 51 (but min is 5, so max(5, 51) = 51)
            Assert.Equal(51, tokens);
        }

        [Fact]
        public void EstimateFutureMessageTokens_LargeContent_CalculatesCorrectly()
        {
            // Arrange
            var service = new SimpleTokenCounterService();

            // Act
            int tokens = service.EstimateFutureMessageTokens(new string('x', 800));

            // Assert
            // 800 chars / 4 = 200 tokens + 50 wrapper = 250
            Assert.Equal(250, tokens);
        }

        [Fact]
        public void CharactersPerToken_DefaultValue_IsFour()
        {
            // Arrange
            var service = new SimpleTokenCounterService();

            // Act & Assert
            Assert.Equal(4, service.CharactersPerToken);
        }

        [Fact]
        public void CharactersPerToken_CanBeCustomized()
        {
            // Arrange
            var service = new SimpleTokenCounterService { CharactersPerToken = 3 };

            // Act
            int tokens = service.EstimateFutureMessageTokens(new string('a', 300));

            // Assert
            // 300 chars / 3 = 100 tokens + 50 wrapper = 150
            Assert.Equal(150, tokens);
        }
    }
}
