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

            public string GetActiveFilepath() => ActiveFilepath;
            public string GetSelectedText() => string.Empty;
            public string GetActiveDocumentContent() => string.Empty;
            public List<string> GetRecentFiles(int maxCount) => new List<string>();
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
    }
}
