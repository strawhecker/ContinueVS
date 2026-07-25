using ContinueVS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// SettingsCollector Tests (Step 95)
    /// Basic test suite for settings reading and caching functionality.
    /// </summary>
    public class SettingsCollectorTests
    {
        [Fact]
        public async Task ReadSettingsAsync_Returns_NotNull_Dictionary()
        {
            // Arrange
            SettingsCollector.ClearCache();

            // Act
            var result = await SettingsCollector.ReadSettingsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.IsType<Dictionary<string, object>>(result);
        }

        [Fact]
        public void ClearCache_Completes_Without_Error()
        {
            // Act
            SettingsCollector.ClearCache();

            // Assert - successful execution (no exception thrown)
            Assert.True(true);
        }
    }
}
