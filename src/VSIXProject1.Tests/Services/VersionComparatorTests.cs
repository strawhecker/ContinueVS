using ContinueVS.Services;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for VersionComparator.
    /// Tests semantic version comparison, pre-release handling, and edge cases.
    /// </summary>
    public class VersionComparatorTests
    {
        private readonly VersionComparator _comparator = new VersionComparator();

        [Theory]
        [InlineData("2.1.0", "2.0.0", 1)]  // v1 > v2
        [InlineData("2.0.0", "2.1.0", -1)] // v1 < v2
        [InlineData("2.0.0", "2.0.0", 0)]  // v1 == v2
        [InlineData("3.0.0", "2.9.9", 1)]  // Major version difference
        [InlineData("2.5.0", "2.4.9", 1)]  // Minor version difference
        [InlineData("2.0.5", "2.0.4", 1)]  // Patch version difference
        public void CompareVersions_StandardVersions_ReturnsCorrectComparison(string v1, string v2, int expected)
        {
            // Arrange & Act
            var result = _comparator.CompareVersions(v1, v2);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("2.1.0-beta", "2.0.0", 1)]      // Pre-release newer > stable older
        [InlineData("2.0.0-alpha", "2.0.0", 0)]     // Pre-release of same version treated as equal
        [InlineData("2.0.0-rc", "2.0.0-beta", 0)]   // Different pre-releases treated as equal
        public void CompareVersions_PreReleaseVersions_StripsSuffixBeforeComparison(string v1, string v2, int expected)
        {
            // Arrange & Act
            var result = _comparator.CompareVersions(v1, v2);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, "2.0.0")]
        [InlineData("2.0.0", null)]
        [InlineData("", "2.0.0")]
        [InlineData("2.0.0", "")]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void CompareVersions_NullOrEmptyInput_ReturnsZero(string v1, string v2)
        {
            // Arrange & Act
            var result = _comparator.CompareVersions(v1, v2);

            // Assert
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData("invalid", "2.0.0")]
        [InlineData("2.0.0", "invalid")]
        [InlineData("not.a.version", "2.0.0")]
        [InlineData("2.0", "2.0")]  // Incomplete version treated as equal if both invalid
        public void CompareVersions_InvalidFormat_ReturnsZero(string v1, string v2)
        {
            // Arrange & Act
            var result = _comparator.CompareVersions(v1, v2);

            // Assert
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData("2.1.0", "2.0.0", true)]   // Downgrade
        [InlineData("2.0.0", "2.1.0", false)]  // Upgrade
        [InlineData("2.0.0", "2.0.0", false)]  // Same version
        [InlineData("3.0.0", "2.5.0", true)]   // Major downgrade
        public void IsDowngrade_CorrectlyDetectsDowngrade(string current, string target, bool expected)
        {
            // Arrange & Act
            var result = _comparator.IsDowngrade(current, target);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsDowngrade_WithNullVersions_ReturnsFalse()
        {
            // Arrange & Act
            var result = _comparator.IsDowngrade(null, "2.0.0");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsDowngrade_WithInvalidVersions_ReturnsFalse()
        {
            // Arrange & Act
            var result = _comparator.IsDowngrade("invalid", "also-invalid");

            // Assert
            Assert.False(result);
        }
    }
}
