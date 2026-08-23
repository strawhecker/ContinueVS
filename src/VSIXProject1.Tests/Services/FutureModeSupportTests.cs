using System;
using ContinueVS.Services.Utilities;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests for future mode graceful degradation (gap27_4).
    /// Ensures that unknown/future modes are handled safely without crashing.
    /// </summary>
    public class FutureModeSupportTests
    {
        /// <summary>
        /// Test gap27_4: Unknown mode integer is detected and flagged as unsupported.
        /// </summary>
        [Fact]
        public void UnknownMode_IsNotValid()
        {
            // Arrange: Use a mode value beyond the current enum (e.g., 99)
            int unknownMode = 99;

            // Act: Check if mode is valid
            bool isValid = ModeValidator.IsValidMode(unknownMode);

            // Assert: Unknown mode is not valid
            Assert.False(isValid);
        }

        /// <summary>
        /// Test gap27_4: Unknown mode is coerced to default (Ask = 0).
        /// </summary>
        [Fact]
        public void UnknownMode_CoercedToDefault()
        {
            // Arrange: Use an unknown mode value
            int unknownMode = 5;

            // Act: Coerce the mode
            int coercedMode = ModeValidator.CoerceToValidMode(unknownMode);

            // Assert: Coerced mode is Ask (0)
            Assert.Equal(0, coercedMode);
        }

        /// <summary>
        /// Test gap27_4: Known modes (Ask=0, Agent=1, Plan=2) remain valid.
        /// </summary>
        [Theory]
        [InlineData(0)]  // Ask
        [InlineData(1)]  // Agent
        [InlineData(2)]  // Plan
        public void KnownMode_IsValid(int mode)
        {
            // Act: Check if mode is valid
            bool isValid = ModeValidator.IsValidMode(mode);

            // Assert: Known modes are valid
            Assert.True(isValid);
        }

        /// <summary>
        /// Test gap27_4: Known modes are not coerced, returned as-is.
        /// </summary>
        [Theory]
        [InlineData(0)]  // Ask
        [InlineData(1)]  // Agent
        [InlineData(2)]  // Plan
        public void KnownMode_NotCoerced(int mode)
        {
            // Act: Coerce the mode
            int coercedMode = ModeValidator.CoerceToValidMode(mode);

            // Assert: Known mode is unchanged
            Assert.Equal(mode, coercedMode);
        }

        /// <summary>
        /// Test gap27_4: Negative mode values are treated as unknown.
        /// </summary>
        [Fact]
        public void NegativeMode_IsNotValid()
        {
            // Arrange: Use negative mode value
            int negativeMode = -1;

            // Act: Check if mode is valid
            bool isValid = ModeValidator.IsValidMode(negativeMode);

            // Assert: Negative mode is not valid
            Assert.False(isValid);
        }
    }
}
