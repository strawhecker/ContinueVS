#nullable enable

using System;
using System.Collections.Generic;
using Moq;
using ContinueVS.IPC;
using ContinueVS.Services;

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Factory for creating consistent, configured mock objects for bridge components.
    /// 
    /// Provides static factory methods that return fully-configured Moq Mock instances
    /// with sensible defaults, reducing duplication across test suites.
    /// 
    /// Usage:
    ///   var mockConfig = MockFactory.CreateMockBridgeConfiguration("2.0.0");
    ///   var mockTransport = MockFactory.CreateMockBridgeTransport();
    /// </summary>
    internal static class MockFactory
    {
        /// <summary>
        /// Creates a mock IBridgeConfiguration with default settings.
        /// </summary>
        /// <param name="version">The bridge version (default: "2.0.0")</param>
        /// <returns>Loose Mock&lt;IBridgeConfiguration&gt; configured with defaults</returns>
        internal static Mock<IBridgeConfiguration> CreateMockBridgeConfiguration(
            string version = TestConstants.DefaultTestVersion)
        {
            var mock = new Mock<IBridgeConfiguration>(MockBehavior.Default);

            mock.Setup(x => x.Version)
                .Returns(version);

            mock.Setup(x => x.IsDebugMode)
                .Returns(false);

            mock.Setup(x => x.EnableTelemetry)
                .Returns(true);

            mock.Setup(x => x.LogLevel)
                .Returns("info");

            return mock;
        }

        /// <summary>
        /// Creates a mock IBridgeTransport with default behavior.
        /// </summary>
        /// <returns>Loose Mock&lt;IBridgeTransport&gt; configured as a no-op</returns>
        internal static Mock<IBridgeTransport> CreateMockBridgeTransport()
        {
            var mock = new Mock<IBridgeTransport>(MockBehavior.Default);

            // Default behavior: no-op for most operations
            mock.Setup(x => x.IsRunning)
                .Returns(true);

            mock.Setup(x => x.StartAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            mock.Setup(x => x.StopAsync())
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            return mock;
        }

        /// <summary>
        /// Creates a strict mock IBridgeConfiguration that requires all calls to be explicitly set up.
        /// Useful for tests that need to verify specific configuration interactions.
        /// </summary>
        /// <param name="version">The bridge version</param>
        /// <returns>Strict Mock&lt;IBridgeConfiguration&gt;</returns>
        internal static Mock<IBridgeConfiguration> CreateStrictMockBridgeConfiguration(
            string version = TestConstants.DefaultTestVersion)
        {
            var mock = new Mock<IBridgeConfiguration>(MockBehavior.Strict);

            mock.Setup(x => x.Version)
                .Returns(version);

            mock.Setup(x => x.IsDebugMode)
                .Returns(false);

            mock.Setup(x => x.EnableTelemetry)
                .Returns(true);

            mock.Setup(x => x.LogLevel)
                .Returns("info");

            return mock;
        }

        /// <summary>
        /// Creates a strict mock IBridgeTransport with all calls explicitly defined.
        /// Useful for protocol-level testing.
        /// </summary>
        /// <returns>Strict Mock&lt;IBridgeTransport&gt;</returns>
        internal static Mock<IBridgeTransport> CreateStrictMockBridgeTransport()
        {
            var mock = new Mock<IBridgeTransport>(MockBehavior.Strict);

            // Strict mocks require explicit setup; add defaults as needed by tests
            return mock;
        }

        /// <summary>
        /// Creates a concrete VersionSelectorService mock for testing.
        /// Accepts all version strings without filesystem dependencies.
        /// 
        /// Used primarily for BridgeConfiguration tests where a concrete service is required.
        /// </summary>
        /// <returns>A MockVersionSelectorService instance</returns>
        internal static MockVersionSelectorService CreateMockVersionSelectorServiceConcrete()
        {
            return new MockVersionSelectorService();
        }
    }

    /// <summary>
    /// Mock implementation of <see cref="VersionSelectorService"/> for testing.
    /// Accepts all version strings for testing without filesystem dependencies.
    /// </summary>
    internal class MockVersionSelectorService : VersionSelectorService
    {
        public override List<string> GetAvailableVersions()
        {
            return new List<string> { TestConstants.DefaultTestVersion };
        }

        public override bool IsVersionAvailable(string version)
        {
            // For testing, accept any version string
            return !string.IsNullOrWhiteSpace(version);
        }
    }
}
