#nullable enable

using Xunit;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.Core.Types
{
    /// <summary>
    /// Tests for ContinuationPolicy enum type system (gap27_11).
    /// Verifies that all continuation policy states are defined correctly.
    /// </summary>
    public class PolicyTypeTests
    {
        /// <summary>
        /// Test 1: Verify ContinuationPolicy.Auto exists and equals 0.
        /// </summary>
        [Fact]
        public void Auto_EnumValue_EqualsZero()
        {
            // Arrange & Act
            var autoValue = ContinuationPolicy.Auto;

            // Assert
            Assert.Equal(0, (int)autoValue);
        }

        /// <summary>
        /// Test 2: Verify ContinuationPolicy.Interactive exists and equals 1.
        /// </summary>
        [Fact]
        public void Interactive_EnumValue_EqualsOne()
        {
            // Arrange & Act
            var interactiveValue = ContinuationPolicy.Interactive;

            // Assert
            Assert.Equal(1, (int)interactiveValue);
        }

        /// <summary>
        /// Test 3: Verify ContinuationPolicy.Bypass exists and equals 2.
        /// </summary>
        [Fact]
        public void Bypass_EnumValue_EqualsTwo()
        {
            // Arrange & Act
            var bypassValue = ContinuationPolicy.Bypass;

            // Assert
            Assert.Equal(2, (int)bypassValue);
        }
    }
}
