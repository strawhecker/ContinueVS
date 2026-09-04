#nullable enable

using System;
using System.Diagnostics;
using System.Linq;

namespace ContinueVS.Services
{
    /// <summary>
    /// Debug utilities for conditional breakpoint behavior and exception handling diagnostics.
    /// Used only in DEBUG builds to assist with contextual debugging.
    /// </summary>
    /// <remarks>
    /// This class provides environment-variable driven conditional breakpoints for selective exception handling.
    /// It allows developers to break on specific exceptions in DEBUG builds without code pollution or performance
    /// impact in Release builds (all code is removed by compiler).
    /// 
    /// Usage in catch blocks:
    /// <code>
    /// catch (Exception ex)
    /// {
    /// #if DEBUG
    ///     if (DebuggerHelper.ShouldBreakOnException("ServiceName"))
    ///         Debugger.Break();
    /// #endif
    ///     _ = LoggerService.Current.WriteErrorAsync("[ServiceName] Operation failed", ex);
    /// }
    /// </code>
    /// </remarks>
    public static class DebuggerHelper
    {
        /// <summary>
        /// Determines whether the debugger should break on an exception for a given context.
        /// Respects the CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS environment variable.
        /// </summary>
        /// <param name="context">The service/method context (e.g., "ConfigService", "ToolService"). 
        /// Used for selective filtering. Case-insensitive comparison.</param>
        /// <returns>True if debugger should break; false otherwise.</returns>
        /// <remarks>
        /// Environment variable format:
        /// - "1" or "true" = break on ALL exceptions (any context)
        /// - "ServiceName,AnotherService,ThirdService" = break only on listed contexts (comma-separated, case-insensitive)
        /// - empty/unset/null = no breaks (default)
        /// 
        /// Examples:
        /// - $env:CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS = "1"                          # break on all
        /// - $env:CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS = "ConfigService"              # break only on ConfigService
        /// - $env:CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS = "ConfigService,ToolService"  # break on either
        /// - $env:CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS = ""                           # disable breaks
        /// </remarks>
        public static bool ShouldBreakOnException(string context)
        {
            if (string.IsNullOrWhiteSpace(context))
                return false;

            var breakConfig = Environment.GetEnvironmentVariable("CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS");

            if (string.IsNullOrEmpty(breakConfig))
                return false;

            // Break on all exceptions
            if (breakConfig == "1" || breakConfig.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if context is in allowed list (comma-separated)
            var allowedContexts = breakConfig.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return allowedContexts.Any(c => c.Trim().Equals(context, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Logs the current debug configuration for exception breakpoints to the logger.
        /// Call this during startup (DEBUG only) to confirm breakpoint configuration.
        /// Useful for verifying that environment variable is set as expected.
        /// </summary>
        /// <remarks>
        /// Output messages:
        /// - "Exception breakpoints DISABLED (CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS not set)" - when env var unset
        /// - "Exception breakpoints ENABLED for ALL contexts" - when env var is "1" or "true"
        /// - "Exception breakpoints ENABLED for selective contexts: X,Y,Z" - when env var lists specific contexts
        /// </remarks>
        public static void LogDebugConfiguration()
        {
            var breakConfig = Environment.GetEnvironmentVariable("CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS");

            if (string.IsNullOrEmpty(breakConfig))
            {
                _ = LoggerService.Current.WriteDebugAsync("[DebuggerHelper] Exception breakpoints DISABLED (CONTINUEEVS_DEBUG_BREAK_ON_EXCEPTIONS not set)");
            }
            else if (breakConfig == "1" || breakConfig.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                _ = LoggerService.Current.WriteDebugAsync("[DebuggerHelper] Exception breakpoints ENABLED for ALL contexts");
            }
            else
            {
                _ = LoggerService.Current.WriteDebugAsync($"[DebuggerHelper] Exception breakpoints ENABLED for selective contexts: {breakConfig}");
            }
        }
    }
}
