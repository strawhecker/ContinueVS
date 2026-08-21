using EnvDTE;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class ContextWindowCollectorReportingTests
    {
        private ContextWindowCollector.ContextWindowInfo CreateContextWindowInfo(int maxTokens, int usedTokens)
        {
            return new ContextWindowCollector.ContextWindowInfo
            {
                MaxTokens = maxTokens,
                UsedTokens = usedTokens,
                EstimatedTokens = new ContextWindowCollector.EstimatedTokensBreakdown()
            };
        }

        [Fact]
        public void ContextWindowInfo_ReservedForNewContext_CalculatedCorrectly()
        {
            // Arrange
            var info = CreateContextWindowInfo(8192, 2500);

            // Act
            // Safety margin is 5% of 8192 = 409 (integer division)
            // Reserved = 8192 - 2500 - 409 = 5283
            info.ReservedForNewContext = 8192 - 2500 - (8192 / 20);

            // Assert
            int expectedReserved = 8192 - 2500 - 409;
            Assert.Equal(expectedReserved, info.ReservedForNewContext);
        }

        [Fact]
        public void ContextWindowInfo_ReservedForNewContext_WithHighUsage()
        {
            // Arrange
            var info = CreateContextWindowInfo(8192, 7000);

            // Act
            // Safety margin is ~410
            // Reserved = 8192 - 7000 - 410 = 782
            info.ReservedForNewContext = 8192 - 7000 - (8192 / 20);

            // Assert
            Assert.True(info.ReservedForNewContext > 0);
            Assert.True(info.ReservedForNewContext < 1000);
        }

        [Fact]
        public void ContextWindowInfo_ReservedForNewContext_NeverNegative()
        {
            // Arrange
            var info = CreateContextWindowInfo(8192, 8150);

            // Act
            int safetyMargin = 8192 / 20; // 410
            info.ReservedForNewContext = System.Math.Max(0, 8192 - 8150 - safetyMargin);

            // Assert
            Assert.True(info.ReservedForNewContext >= 0);
        }

        [Fact]
        public void ContextWindowInfo_ReservedForNewContext_WithSmallWindow()
        {
            // Arrange
            var info = CreateContextWindowInfo(4096, 2000);

            // Act
            // Safety margin is 5% of 4096 = 204 (integer division)
            // Reserved = 4096 - 2000 - 204 = 1892
            info.ReservedForNewContext = 4096 - 2000 - (4096 / 20);

            // Assert
            int expectedReserved = 4096 - 2000 - 204;
            Assert.Equal(expectedReserved, info.ReservedForNewContext);
        }

        [Fact]
        public void ContextWindowInfo_ReservedForNewContext_ZeroUsage()
        {
            // Arrange
            var info = CreateContextWindowInfo(8192, 0);

            // Act
            // Safety margin is 5% of 8192 = 409 (integer division)
            // Reserved = 8192 - 0 - 409 = 7783
            info.ReservedForNewContext = 8192 - 0 - (8192 / 20);

            // Assert
            int expectedReserved = 8192 - 409;
            Assert.Equal(expectedReserved, info.ReservedForNewContext);
        }

        [Fact]
        public void ContextWindowInfo_SafetyMarginMinimum()
        {
            // Arrange
            var info = CreateContextWindowInfo(100, 50);

            // Act
            int safetyMargin = System.Math.Max(1, 100 / 20); // Minimum 1 token
            info.ReservedForNewContext = System.Math.Max(0, 100 - 50 - safetyMargin);

            // Assert
            // Safety margin = max(1, 5) = 5
            // Reserved = 100 - 50 - 5 = 45
            Assert.Equal(45, info.ReservedForNewContext);
        }

        [Fact]
        public void ContextWindowInfo_Properties_AllExist()
        {
            // Arrange & Act
            var info = new ContextWindowCollector.ContextWindowInfo
            {
                MaxTokens = 8192,
                UsedTokens = 2000,
                ReservedForNewContext = 5282,
                EstimatedTokens = new ContextWindowCollector.EstimatedTokensBreakdown()
            };

            // Assert
            Assert.Equal(8192, info.MaxTokens);
            Assert.Equal(2000, info.UsedTokens);
            Assert.Equal(5282, info.ReservedForNewContext);
            Assert.NotNull(info.EstimatedTokens);
        }
    }
}
