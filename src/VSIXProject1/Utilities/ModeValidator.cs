using System;

namespace ContinueVS.Services.Utilities
{
    /// <summary>
    /// Utility for validating, converting, and coercing chat modes (gap27_4, gap27_5).
    /// Centralizes mode enum ↔ int mapping and handles unknown/future modes gracefully.
    /// </summary>
    public static class ModeValidator
    {
        /// <summary>
        /// Gets the maximum valid mode value (Plan = 2).
        /// </summary>
        public const int MaxValidMode = 2;

        /// <summary>
        /// Gets the default/fallback mode (Ask = 0).
        /// </summary>
        public const int DefaultMode = 0;

        /// <summary>
        /// Determines if a mode value is known and supported.
        /// </summary>
        /// <param name="mode">The mode integer value (0=Ask, 1=Agent, 2=Plan).</param>
        /// <returns>True if mode is a known ChatMode; false for unknown/future modes.</returns>
        public static bool IsValidMode(int mode)
        {
            return mode >= 0 && mode <= MaxValidMode;
        }

        /// <summary>
        /// Coerces an unknown mode to the default (Ask).
        /// If mode is valid, returns it unchanged.
        /// </summary>
        /// <param name="mode">The mode to validate.</param>
        /// <returns>The mode if valid, or DefaultMode if unknown.</returns>
        public static int CoerceToValidMode(int mode)
        {
            return IsValidMode(mode) ? mode : DefaultMode;
        }
    }
}
