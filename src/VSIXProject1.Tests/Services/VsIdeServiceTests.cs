using System;
using System.Collections.Generic;
using Xunit;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

#nullable enable

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for VsIdeService.GetActiveFilepath() wired through IDteProvider.
    /// DteProvider wraps COM DTE and cannot be tested directly; a stub satisfies DI.
    /// </summary>
    public class VsIdeServiceTests
    {
        private class StubDteProvider : IDteProvider
        {
            public string ActiveFilepath { get; set; } = string.Empty;
            public string SelectedText { get; set; } = string.Empty;
            public Selection? CursorSelection { get; set; }

            public string GetActiveFilepath() => ActiveFilepath;
            public string GetSelectedText() => SelectedText;
            public string GetActiveDocumentContent() => string.Empty;
            public List<string> GetRecentFiles(int maxCount) => new List<string>();
            public Selection? GetCursorSelection() => CursorSelection;
        }

        [Fact]
        public void GetActiveFilepath_ReturnsFilepath_WhenDteProviderReturnsPath()
        {
            // Arrange
            var stub = new StubDteProvider { ActiveFilepath = @"C:\Foo\Bar.cs" };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetActiveFilepath();

            // Assert
            Assert.Equal(@"C:\Foo\Bar.cs", result);
        }

        [Fact]
        public void GetActiveFilepath_ReturnsEmpty_WhenDteProviderReturnsEmpty()
        {
            // Arrange
            var stub = new StubDteProvider { ActiveFilepath = string.Empty };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetActiveFilepath();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenDteProviderIsNull()
        {
            // Arrange / Act / Assert
            Assert.Throws<ArgumentNullException>(() => new VsIdeService(null!));
        }

        [Fact]
        public void GetSelectedText_ReturnsDelegatedText_WhenDteProviderReturnsText()
        {
            // Arrange
            var stub = new StubDteProvider { SelectedText = "hello world" };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetSelectedText();

            // Assert
            Assert.Equal("hello world", result);
        }

        [Fact]
        public void GetSelectedText_ReturnsEmpty_WhenDteProviderReturnsEmpty()
        {
            // Arrange
            var stub = new StubDteProvider { SelectedText = string.Empty };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetSelectedText();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetCursorSelection_ReturnsSelection_WhenDteProviderReturnsSelection()
        {
            // Arrange
            var expected = new Selection
            {
                Start = new Location { FilePath = @"C:\Foo\Bar.cs", Line = 10, Column = 5 },
                End = new Location { FilePath = @"C:\Foo\Bar.cs", Line = 10, Column = 15 }
            };
            var stub = new StubDteProvider { CursorSelection = expected };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetCursorSelection();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result!.Start!.Line);
            Assert.Equal(5, result.Start.Column);
            Assert.Equal(10, result.End!.Line);
            Assert.Equal(15, result.End.Column);
        }

        [Fact]
        public void GetCursorSelection_ReturnsNull_WhenDteProviderReturnsNull()
        {
            // Arrange
            var stub = new StubDteProvider { CursorSelection = null };
            var sut = new VsIdeService(stub);

            // Act
            var result = sut.GetCursorSelection();

            // Assert
            Assert.Null(result);
        }
    }
}
