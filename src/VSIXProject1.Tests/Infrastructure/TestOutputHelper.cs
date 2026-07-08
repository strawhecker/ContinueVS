#nullable enable

using System;
using Xunit.Abstractions;

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Wrapper around xUnit's ITestOutputHelper providing structured diagnostic logging
    /// with timestamps, log levels, and metadata tags.
    /// 
    /// Integrates with xUnit test runner for test-scoped output capture and debugging.
    /// 
    /// Usage:
    ///   private readonly TestOutputHelper _output;
    ///   
    ///   public MyTest(ITestOutputHelper testOutput)
    ///   {
    ///       _output = new TestOutputHelper(testOutput);
    ///   }
    ///   
    ///   [Fact]
    ///   public async Task MyAsyncTest()
    ///   {
    ///       _output.LogDebug("Starting test");
    ///       _output.LogInfo("Transport initialized");
    ///       _output.LogWarning("Retrying operation");
    ///       _output.LogError("Operation failed", ex);
    ///   }
    /// </summary>
    public class TestOutputHelper
    {
        private readonly ITestOutputHelper? _xunitOutput;
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// Creates a new instance wrapping an xUnit ITestOutputHelper.
        /// If null, output is silently discarded.
        /// </summary>
        public TestOutputHelper(ITestOutputHelper? xunitOutput = null)
        {
            _xunitOutput = xunitOutput;
        }

        /// <summary>
        /// Logs a message at DEBUG level.
        /// </summary>
        public void LogDebug(string message)
        {
            WriteLine("DEBUG", message);
        }

        /// <summary>
        /// Logs a message at DEBUG level with a tag.
        /// </summary>
        public void LogDebug(string tag, string message)
        {
            WriteLineWithTag("DEBUG", tag, message);
        }

        /// <summary>
        /// Logs a message at INFO level.
        /// </summary>
        public void LogInfo(string message)
        {
            WriteLine("INFO", message);
        }

        /// <summary>
        /// Logs a message at INFO level with a tag.
        /// </summary>
        public void LogInfo(string tag, string message)
        {
            WriteLineWithTag("INFO", tag, message);
        }

        /// <summary>
        /// Logs a message at WARNING level.
        /// </summary>
        public void LogWarning(string message)
        {
            WriteLine("WARN", message);
        }

        /// <summary>
        /// Logs a message at WARNING level with a tag.
        /// </summary>
        public void LogWarning(string tag, string message)
        {
            WriteLineWithTag("WARN", tag, message);
        }

        /// <summary>
        /// Logs a message at ERROR level.
        /// </summary>
        public void LogError(string message)
        {
            WriteLine("ERROR", message);
        }

        /// <summary>
        /// Logs a message and exception at ERROR level.
        /// </summary>
        public void LogError(string message, Exception ex)
        {
            WriteLine("ERROR", message);
            if (ex != null)
            {
                WriteLine("ERROR", $"  Exception: {ex.GetType().Name}");
                WriteLine("ERROR", $"  Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    WriteLine("ERROR", $"  Inner: {ex.InnerException.Message}");
                }
                WriteLine("ERROR", $"  StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Logs a message at ERROR level with a tag.
        /// </summary>
        public void LogError(string tag, string message)
        {
            WriteLineWithTag("ERROR", tag, message);
        }

        /// <summary>
        /// Logs a message and exception at ERROR level with a tag.
        /// </summary>
        public void LogError(string tag, string message, Exception ex)
        {
            WriteLineWithTag("ERROR", tag, message);
            if (ex != null)
            {
                WriteLine("ERROR", $"  [{tag}] Exception: {ex.GetType().Name}");
                WriteLine("ERROR", $"  [{tag}] Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    WriteLine("ERROR", $"  [{tag}] Inner: {ex.InnerException.Message}");
                }
                WriteLine("ERROR", $"  [{tag}] StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Logs a critical error message.
        /// </summary>
        public void LogCritical(string message)
        {
            WriteLine("CRITICAL", message);
        }

        /// <summary>
        /// Logs a critical error message with exception details.
        /// </summary>
        public void LogCritical(string message, Exception ex)
        {
            WriteLine("CRITICAL", message);
            if (ex != null)
            {
                WriteLine("CRITICAL", $"  Exception: {ex.GetType().Name}");
                WriteLine("CRITICAL", $"  Message: {ex.Message}");
                WriteLine("CRITICAL", $"  StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Logs a divider/separator line for visual organization.
        /// </summary>
        public void LogDivider()
        {
            WriteLiteral("=".PadRight(80, '='));
        }

        /// <summary>
        /// Logs a section header.
        /// </summary>
        public void LogSection(string title)
        {
            LogDivider();
            LogInfo($">>> {title}");
            LogDivider();
        }

        /// <summary>
        /// Logs a raw message without timestamp or level prefix.
        /// </summary>
        public void LogRaw(string message)
        {
            WriteLiteral(message);
        }

        /// <summary>
        /// Logs an assertion event.
        /// </summary>
        public void LogAssertion(string conditionName, bool passed, string? details = null)
        {
            var status = passed ? "✓ PASS" : "✗ FAIL";
            var msg = $"{status} {conditionName}";
            if (details != null)
            {
                msg += $" ({details})";
            }
            WriteLine(passed ? "INFO" : "ERROR", msg);
        }

        /// <summary>
        /// Logs a performance measurement.
        /// </summary>
        public void LogPerformance(string operationName, long elapsedMs)
        {
            WriteLine("INFO", $"[PERF] {operationName}: {elapsedMs}ms");
        }

        /// <summary>
        /// Logs state information for debugging.
        /// </summary>
        public void LogState(string stateName, string stateValue)
        {
            WriteLineWithTag("DEBUG", "STATE", $"{stateName}={stateValue}");
        }

        /// <summary>
        /// Logs a transport message (for protocol debugging).
        /// </summary>
        public void LogMessage(string direction, string messageType, string? content = null)
        {
            var msg = $"[{direction}] {messageType}";
            if (content != null)
            {
                msg += $": {content}";
            }
            WriteLine("DEBUG", msg);
        }

        // Private helpers

        private void WriteLine(string level, string message)
        {
            var timestamp = DateTime.Now.ToString(DateTimeFormat);
            var formatted = $"[{timestamp}] [{level}] {message}";
            _xunitOutput?.WriteLine(formatted);
        }

        private void WriteLineWithTag(string level, string tag, string message)
        {
            var timestamp = DateTime.Now.ToString(DateTimeFormat);
            var formatted = $"[{timestamp}] [{level}] [{tag}] {message}";
            _xunitOutput?.WriteLine(formatted);
        }

        private void WriteLiteral(string text)
        {
            _xunitOutput?.WriteLine(text);
        }
    }
}
