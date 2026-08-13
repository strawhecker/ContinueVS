using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using System.Windows.Media;
using System.Collections.Generic;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for ThemeService.
    /// Tests theme loading, switching, brush resolution, and theme enumeration.
    /// </summary>
    public class ThemeServiceTests
    {
        [Fact]
        public void GetCurrentThemeName_ReturnsCurrentThemeName()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act
            var themeName = themeService.GetCurrentThemeName();

            // Assert
            Assert.Equal("dark", themeName);
        }

        [Fact]
        public void GetBrush_WithValidKey_ReturnsBrush()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act
            var brush = themeService.GetBrush("PrimaryTextBrush");

            // Assert
            Assert.NotNull(brush);
            Assert.IsType<SolidColorBrush>(brush);
        }

        [Fact]
        public void GetBrush_WithInvalidKey_ReturnsDefaultBrush()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act
            var brush = themeService.GetBrush("NonExistentKey");

            // Assert
            Assert.NotNull(brush);
            Assert.Equal(Colors.Gray, brush.Color);
        }

        [Fact]
        public void GetBrush_WithNullKey_ThrowsArgumentNullException()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => themeService.GetBrush(null));
        }

        [Fact]
        public void GetColor_WithValidKey_ReturnsColor()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act
            var color = themeService.GetColor("EditorBackground");

            // Assert
            Assert.NotEqual(Colors.Transparent, color);
        }

        [Fact]
        public void GetColor_WithInvalidKey_ReturnsDefaultColor()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act
            var color = themeService.GetColor("NonExistentKey");

            // Assert
            Assert.Equal(Colors.Gray, color);
        }

        [Fact]
        public void GetColor_WithNullKey_ThrowsArgumentNullException()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => themeService.GetColor(null));
        }

        [Fact]
        public void SetCurrentTheme_WithNullThemeName_ThrowsArgumentNullException()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => themeService.SetCurrentTheme(null));
        }

        [Fact]
        public void SetCurrentTheme_WithUnloadedTheme_ThrowsKeyNotFoundException()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => themeService.SetCurrentTheme("nonexistent"));
        }

        [Fact]
        public async Task LoadThemeAsync_WithNullThemeName_ThrowsArgumentNullException()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => themeService.LoadThemeAsync(null));
        }

        [Fact]
        public async Task LoadThemeAsync_WithEmptyThemeName_ThrowsArgumentNullException()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => themeService.LoadThemeAsync(""));
        }

        [Fact]
        public async Task LoadThemeAsync_WithInvalidThemeName_ThrowsFileNotFoundException()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert
            // This test expects FileNotFoundException when theme file doesn't exist
            await Assert.ThrowsAsync<FileNotFoundException>(() => themeService.LoadThemeAsync("nonexistent"));
        }

        [Fact]
        public void ThemeChanged_EventCanBeSubscribedTo()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act & Assert (no exception thrown)
            themeService.ThemeChanged += (sender, args) => { };
            Assert.True(true);
        }

        [Fact]
        public void GetAvailableThemes_WithNoThemesLoaded_ReturnsEmptyEnumerable()
        {
            // Arrange
            var themeService = new ThemeService();

            // Act
            var availableThemes = themeService.GetAvailableThemes();

            // Assert
            Assert.NotNull(availableThemes);
            Assert.Empty(availableThemes);
        }

        [Fact]
        public void ServiceImplementsInterface()
        {
            // Arrange & Act
            var themeService = new ThemeService();

            // Assert
            Assert.IsAssignableFrom<IThemeService>(themeService);
        }
    }
}
