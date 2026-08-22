using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for ThemeCacheService.
    /// Tests theme color caching, retrieval, and cache clearing functionality.
    /// </summary>
    public class ThemeCacheServiceTests
    {
        [Fact]
        public void Constructor_WithNullLocalStorageService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ThemeCacheService(null));
        }

        [Fact]
        public void CacheThemeColors_WithValidColors_StoresInLocalStorage()
        {
            // Arrange
            var colors = new Dictionary<string, string>
            {
                { "--vscode-editor-background", "#1e1e1e" },
                { "--vscode-editor-foreground", "#d4d4d4" },
                { "--vscode-button-background", "#007acc" }
            };

            var mockStorageService = new Mock<ILocalStorageService>();
            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            service.CacheThemeColors(colors);

            // Assert
            mockStorageService.Verify(
                s => s.SetItem(
                    "theme_colors",
                    It.Is<ThemeCache>(tc => tc.Colors.Count == 3)),
                Times.Once);
        }

        [Fact]
        public void CacheThemeColors_WithNullColors_SkipsCaching()
        {
            // Arrange
            var mockStorageService = new Mock<ILocalStorageService>();
            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            service.CacheThemeColors(null);

            // Assert
            mockStorageService.Verify(s => s.SetItem(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public void GetCachedTheme_WithExistingCache_ReturnsColors()
        {
            // Arrange
            var cachedColors = new Dictionary<string, string>
            {
                { "--vscode-editor-background", "#1e1e1e" },
                { "--vscode-editor-foreground", "#d4d4d4" }
            };

            var themeCache = new ThemeCache
            {
                Colors = cachedColors,
                CachedAt = DateTime.UtcNow
            };

            var mockStorageService = new Mock<ILocalStorageService>();
            mockStorageService
                .Setup(s => s.GetItem<ThemeCache>("theme_colors"))
                .Returns(themeCache);

            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            var result = service.GetCachedTheme();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("#1e1e1e", result["--vscode-editor-background"]);
            Assert.Equal("#d4d4d4", result["--vscode-editor-foreground"]);
        }

        [Fact]
        public void GetCachedTheme_WithNoCache_ReturnsNull()
        {
            // Arrange
            var mockStorageService = new Mock<ILocalStorageService>();
            mockStorageService
                .Setup(s => s.GetItem<ThemeCache>("theme_colors"))
                .Returns((ThemeCache)null);

            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            var result = service.GetCachedTheme();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetCachedTheme_WithEmptyColors_ReturnsNull()
        {
            // Arrange
            var themeCache = new ThemeCache
            {
                Colors = new Dictionary<string, string>(),
                CachedAt = DateTime.UtcNow
            };

            var mockStorageService = new Mock<ILocalStorageService>();
            mockStorageService
                .Setup(s => s.GetItem<ThemeCache>("theme_colors"))
                .Returns(themeCache);

            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            var result = service.GetCachedTheme();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetCachedTheme_WithStorageException_ReturnsNull()
        {
            // Arrange
            var mockStorageService = new Mock<ILocalStorageService>();
            mockStorageService
                .Setup(s => s.GetItem<ThemeCache>("theme_colors"))
                .Throws(new InvalidOperationException("Storage error"));

            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            var result = service.GetCachedTheme();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ClearThemeCache_RemovesFromStorage()
        {
            // Arrange
            var mockStorageService = new Mock<ILocalStorageService>();
            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            service.ClearThemeCache();

            // Assert
            mockStorageService.Verify(s => s.RemoveItem("theme_colors"), Times.Once);
        }

        [Fact]
        public void ClearThemeCache_WithStorageException_HandlesGracefully()
        {
            // Arrange
            var mockStorageService = new Mock<ILocalStorageService>();
            mockStorageService
                .Setup(s => s.RemoveItem("theme_colors"))
                .Throws(new InvalidOperationException("Storage error"));

            var service = new ThemeCacheService(mockStorageService.Object);

            // Act & Assert (should not throw)
            service.ClearThemeCache();
        }

        [Fact]
        public void CacheThemeColors_PreservesCachedAtTimestamp()
        {
            // Arrange
            var colors = new Dictionary<string, string>
            {
                { "--vscode-editor-background", "#1e1e1e" }
            };

            var mockStorageService = new Mock<ILocalStorageService>();
            ThemeCache capturedCache = null;
            mockStorageService
                .Setup(s => s.SetItem("theme_colors", It.IsAny<ThemeCache>()))
                .Callback<string, object>((key, value) => capturedCache = value as ThemeCache);

            var service = new ThemeCacheService(mockStorageService.Object);

            // Act
            var beforeCache = DateTime.UtcNow;
            service.CacheThemeColors(colors);
            var afterCache = DateTime.UtcNow;

            // Assert
            Assert.NotNull(capturedCache);
            Assert.InRange(capturedCache.CachedAt, beforeCache, afterCache.AddSeconds(1));
        }
    }
}
