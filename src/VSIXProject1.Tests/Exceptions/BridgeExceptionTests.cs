#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ContinueVS.Exceptions;
using TimeoutException = ContinueVS.Exceptions.TimeoutException;

namespace ContinueVS.Tests.Exceptions
{
    /// <summary>
    /// Comprehensive test suite for bridge exception hierarchy.
    /// 
    /// Covers:
    /// - Base class instantiation and null-check validation
    /// - ErrorCode and Context properties
    /// - Inner exception chaining
    /// - ToString() output with error code and context
    /// - Each specific exception type and its well-known error codes
    /// - Inheritance validation
    /// </summary>
    public class BridgeExceptionTests
    {
        // === Base Class Tests ===

        [Fact]
        public void BridgeException_CannotInstantiateDirectly()
        {
            // Verify that BridgeException is abstract and cannot be instantiated
            // (This is a compile-time check; the test documents the design intent)
            Assert.True(typeof(BridgeException).IsAbstract);
        }

        [Fact]
        public void ProcessException_WithMessageAndErrorCode_CreatesInstance()
        {
            // Arrange & Act
            var ex = new ProcessException("Process failed", ProcessException.ErrorCodes.ProcessStartFailed);

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("Process failed", ex.Message);
            Assert.Equal(ProcessException.ErrorCodes.ProcessStartFailed, ex.ErrorCode);
            Assert.NotNull(ex.Context);
            Assert.Empty(ex.Context);
        }

        [Fact]
        public void BridgeException_WithNullMessage_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessException(null!, "ERROR_CODE"));
        }

        [Fact]
        public void BridgeException_WithNullErrorCode_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessException("message", null!));
        }

        [Fact]
        public void BridgeException_WithInnerException_ChainsCorrectly()
        {
            // Arrange
            var innerEx = new IOException("Stream closed");

            // Act
            var ex = new TransportException("Send failed", TransportException.ErrorCodes.SendFailed, innerEx);

            // Assert
            Assert.Same(innerEx, ex.InnerException);
            Assert.Equal("Stream closed", ex.InnerException?.Message);
        }

        [Fact]
        public void BridgeException_WithContextDictionary_StoredCorrectly()
        {
            // Arrange
            var context = new Dictionary<string, string>
            {
                { "processId", "12345" },
                { "exitCode", "-1" }
            };

            // Act
            var ex = new ProcessException(
                "Process exited",
                ProcessException.ErrorCodes.ProcessExitedUnexpectedly,
                context);

            // Assert
            Assert.Equal(2, ex.Context.Count);
            Assert.Equal("12345", ex.Context["processId"]);
            Assert.Equal("-1", ex.Context["exitCode"]);
        }

        [Fact]
        public void BridgeException_WithNullContext_UsesEmptyDictionary()
        {
            // Act
            var ex = new ProcessException(
                "Process failed",
                ProcessException.ErrorCodes.ProcessStartFailed,
                (Dictionary<string, string>?)null);

            // Assert
            Assert.NotNull(ex.Context);
            Assert.Empty(ex.Context);
        }

        [Fact]
        public void BridgeException_ToString_IncludesErrorCodeAndContext()
        {
            // Arrange
            var context = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            var ex = new ProcessException(
                "Test error",
                "TEST_ERROR_CODE",
                context);

            // Act
            var str = ex.ToString();

            // Assert
            Assert.Contains("Test error", str);
            Assert.Contains("TEST_ERROR_CODE", str);
            Assert.Contains("key1", str);
            Assert.Contains("value1", str);
            Assert.Contains("key2", str);
            Assert.Contains("value2", str);
        }

        // === ProcessException Tests ===

        [Fact]
        public void ProcessException_AllErrorCodes_AreNonEmpty()
        {
            // Verify all error codes are defined
            Assert.NotNull(ProcessException.ErrorCodes.ProcessStartFailed);
            Assert.NotNull(ProcessException.ErrorCodes.ProcessExitedUnexpectedly);
            Assert.NotNull(ProcessException.ErrorCodes.ProcessStopTimeout);
            Assert.NotNull(ProcessException.ErrorCodes.ProcessKillTimeout);
            Assert.NotNull(ProcessException.ErrorCodes.StreamInitializationFailed);
            Assert.NotNull(ProcessException.ErrorCodes.ProcessNotRunning);
            Assert.NotNull(ProcessException.ErrorCodes.ProcessAlreadyRunning);
        }

        [Fact]
        public void ProcessException_InheritsFromBridgeException()
        {
            // Act
            var ex = new ProcessException("Test", "CODE");

            // Assert
            Assert.IsAssignableFrom<BridgeException>(ex);
            Assert.IsAssignableFrom<Exception>(ex);
        }

        // === TransportException Tests ===

        [Fact]
        public void TransportException_AllErrorCodes_AreNonEmpty()
        {
            Assert.NotNull(TransportException.ErrorCodes.SendFailed);
            Assert.NotNull(TransportException.ErrorCodes.ReceiveFailed);
            Assert.NotNull(TransportException.ErrorCodes.SerializationFailed);
            Assert.NotNull(TransportException.ErrorCodes.DeserializationFailed);
            Assert.NotNull(TransportException.ErrorCodes.StreamClosed);
            Assert.NotNull(TransportException.ErrorCodes.InvalidStreamState);
            Assert.NotNull(TransportException.ErrorCodes.BufferingFailed);
            Assert.NotNull(TransportException.ErrorCodes.NotConnected);
        }

        [Fact]
        public void TransportException_WithAllConstructors_Works()
        {
            // Act
            var ex1 = new TransportException("Msg1", "CODE1");
            var ex2 = new TransportException("Msg2", "CODE2", new IOException());
            var ex3 = new TransportException("Msg3", "CODE3", new Dictionary<string, string> { { "k", "v" } });
            var ex4 = new TransportException("Msg4", "CODE4", new IOException(), new Dictionary<string, string> { { "k", "v" } });

            // Assert
            Assert.NotNull(ex1);
            Assert.NotNull(ex2);
            Assert.NotNull(ex3);
            Assert.NotNull(ex4);
            Assert.Equal("CODE1", ex1.ErrorCode);
            Assert.Equal("CODE2", ex2.ErrorCode);
            Assert.NotNull(ex2.InnerException);
        }

        [Fact]
        public void TransportException_InheritsFromBridgeException()
        {
            var ex = new TransportException("Test", "CODE");
            Assert.IsAssignableFrom<BridgeException>(ex);
        }

        // === ConfigurationException Tests ===

        [Fact]
        public void ConfigurationException_AllErrorCodes_AreNonEmpty()
        {
            Assert.NotNull(ConfigurationException.ErrorCodes.InvalidVersionFormat);
            Assert.NotNull(ConfigurationException.ErrorCodes.InvalidNpmPath);
            Assert.NotNull(ConfigurationException.ErrorCodes.InvalidWorkingDirectory);
            Assert.NotNull(ConfigurationException.ErrorCodes.PackageNotFound);
            Assert.NotNull(ConfigurationException.ErrorCodes.InvalidTimeout);
            Assert.NotNull(ConfigurationException.ErrorCodes.MissingParameter);
            Assert.NotNull(ConfigurationException.ErrorCodes.IncompatibleVersion);
            Assert.NotNull(ConfigurationException.ErrorCodes.IntegrityCheckFailed);
        }

        [Fact]
        public void ConfigurationException_InheritsFromBridgeException()
        {
            var ex = new ConfigurationException("Test", "CODE");
            Assert.IsAssignableFrom<BridgeException>(ex);
        }

        // === ProtocolException Tests ===

        [Fact]
        public void ProtocolException_AllErrorCodes_AreNonEmpty()
        {
            Assert.NotNull(ProtocolException.ErrorCodes.MalformedMessage);
            Assert.NotNull(ProtocolException.ErrorCodes.MissingRequiredField);
            Assert.NotNull(ProtocolException.ErrorCodes.InvalidFieldValue);
            Assert.NotNull(ProtocolException.ErrorCodes.MessageIdMismatch);
            Assert.NotNull(ProtocolException.ErrorCodes.UnknownMessageType);
            Assert.NotNull(ProtocolException.ErrorCodes.HandlerNotFound);
            Assert.NotNull(ProtocolException.ErrorCodes.IncompatibleVersion);
            Assert.NotNull(ProtocolException.ErrorCodes.InvalidRequest);
            Assert.NotNull(ProtocolException.ErrorCodes.InvalidResponse);
        }

        [Fact]
        public void ProtocolException_InheritsFromBridgeException()
        {
            var ex = new ProtocolException("Test", "CODE");
            Assert.IsAssignableFrom<BridgeException>(ex);
        }

        // === TimeoutException Tests ===

        [Fact]
        public void TimeoutException_WithTimingInfo_StoresElapsedAndTimeout()
        {
            // Act
            var ex = new TimeoutException(
                "RPC timed out",
                TimeoutException.ErrorCodes.RpcCallTimeout,
                elapsedMs: 5000,
                timeoutMs: 3000);

            // Assert
            Assert.Equal(5000, ex.ElapsedMs);
            Assert.Equal(3000, ex.TimeoutMs);
        }

        [Fact]
        public void TimeoutException_AllErrorCodes_AreNonEmpty()
        {
            Assert.NotNull(TimeoutException.ErrorCodes.RpcCallTimeout);
            Assert.NotNull(TimeoutException.ErrorCodes.HealthCheckTimeout);
            Assert.NotNull(TimeoutException.ErrorCodes.ProcessStartTimeout);
            Assert.NotNull(TimeoutException.ErrorCodes.ProcessShutdownTimeout);
            Assert.NotNull(TimeoutException.ErrorCodes.ProcessKillTimeout);
            Assert.NotNull(TimeoutException.ErrorCodes.SendTimeout);
            Assert.NotNull(TimeoutException.ErrorCodes.ReceiveTimeout);
        }

        [Fact]
        public void TimeoutException_InheritsFromBridgeException()
        {
            var ex = new TimeoutException("Test", "CODE", 100, 50);
            Assert.IsAssignableFrom<BridgeException>(ex);
        }

        [Fact]
        public void TimeoutException_WithAllConstructors_Works()
        {
            // Act
            var ex1 = new TimeoutException("Msg1", "CODE1", 100, 50);
            var ex2 = new TimeoutException("Msg2", "CODE2", 100, 50, new OperationCanceledException());
            var ex3 = new TimeoutException("Msg3", "CODE3", 100, 50, new Dictionary<string, string> { { "k", "v" } });
            var ex4 = new TimeoutException("Msg4", "CODE4", 100, 50, new OperationCanceledException(), new Dictionary<string, string> { { "k", "v" } });

            // Assert
            Assert.NotNull(ex1);
            Assert.NotNull(ex2);
            Assert.NotNull(ex3);
            Assert.NotNull(ex4);
            Assert.Equal(100, ex1.ElapsedMs);
            Assert.Equal(50, ex1.TimeoutMs);
        }

        // === HealthCheckException Tests ===

        [Fact]
        public void HealthCheckException_WithFailureCount_StoresValue()
        {
            // Act
            var ex = new HealthCheckException(
                "Health check failed",
                HealthCheckException.ErrorCodes.ProcessNotResponding,
                failureCount: 3);

            // Assert
            Assert.Equal(3, ex.FailureCount);
        }

        [Fact]
        public void HealthCheckException_WithNegativeFailureCount_DefaultsToOne()
        {
            // Act
            var ex = new HealthCheckException(
                "Health check failed",
                HealthCheckException.ErrorCodes.ProcessNotResponding,
                failureCount: -1);

            // Assert
            Assert.Equal(1, ex.FailureCount);
        }

        [Fact]
        public void HealthCheckException_WithZeroFailureCount_DefaultsToOne()
        {
            // Act
            var ex = new HealthCheckException(
                "Health check failed",
                HealthCheckException.ErrorCodes.ProcessNotResponding,
                failureCount: 0);

            // Assert
            Assert.Equal(1, ex.FailureCount);
        }

        [Fact]
        public void HealthCheckException_AllErrorCodes_AreNonEmpty()
        {
            Assert.NotNull(HealthCheckException.ErrorCodes.InvalidProbeResponse);
            Assert.NotNull(HealthCheckException.ErrorCodes.ProcessNotResponding);
            Assert.NotNull(HealthCheckException.ErrorCodes.ProbeFailed);
            Assert.NotNull(HealthCheckException.ErrorCodes.ProcessDegraded);
            Assert.NotNull(HealthCheckException.ErrorCodes.CircuitBreakerTriggered);
            Assert.NotNull(HealthCheckException.ErrorCodes.StateInconsistent);
            Assert.NotNull(HealthCheckException.ErrorCodes.CheckDisabled);
        }

        [Fact]
        public void HealthCheckException_InheritsFromBridgeException()
        {
            var ex = new HealthCheckException("Test", "CODE");
            Assert.IsAssignableFrom<BridgeException>(ex);
        }

        [Fact]
        public void HealthCheckException_WithAllConstructors_Works()
        {
            // Act
            var ex1 = new HealthCheckException("Msg1", "CODE1");
            var ex2 = new HealthCheckException("Msg2", "CODE2", 2);
            var ex3 = new HealthCheckException("Msg3", "CODE3", new IOException());
            var ex4 = new HealthCheckException("Msg4", "CODE4", 2, new IOException());
            var ex5 = new HealthCheckException("Msg5", "CODE5", new Dictionary<string, string> { { "k", "v" } });
            var ex6 = new HealthCheckException("Msg6", "CODE6", 2, new Dictionary<string, string> { { "k", "v" } });
            var ex7 = new HealthCheckException("Msg7", "CODE7", 2, new IOException(), new Dictionary<string, string> { { "k", "v" } });

            // Assert
            Assert.NotNull(ex1);
            Assert.NotNull(ex2);
            Assert.NotNull(ex3);
            Assert.NotNull(ex4);
            Assert.NotNull(ex5);
            Assert.NotNull(ex6);
            Assert.NotNull(ex7);
        }

        // === Integration Tests ===

        [Fact]
        public void AllExceptions_ImplementBridgeExceptionInterface()
        {
            // Verify the exception hierarchy
            var processEx = new ProcessException("Test", "CODE");
            var transportEx = new TransportException("Test", "CODE");
            var configEx = new ConfigurationException("Test", "CODE");
            var protoEx = new ProtocolException("Test", "CODE");
            var timeoutEx = new TimeoutException("Test", "CODE", 100, 50);
            var healthEx = new HealthCheckException("Test", "CODE");

            // All should be BridgeException
            Assert.IsAssignableFrom<BridgeException>(processEx);
            Assert.IsAssignableFrom<BridgeException>(transportEx);
            Assert.IsAssignableFrom<BridgeException>(configEx);
            Assert.IsAssignableFrom<BridgeException>(protoEx);
            Assert.IsAssignableFrom<BridgeException>(timeoutEx);
            Assert.IsAssignableFrom<BridgeException>(healthEx);

            // All should be Exception
            Assert.IsAssignableFrom<Exception>(processEx);
            Assert.IsAssignableFrom<Exception>(transportEx);
            Assert.IsAssignableFrom<Exception>(configEx);
            Assert.IsAssignableFrom<Exception>(protoEx);
            Assert.IsAssignableFrom<Exception>(timeoutEx);
            Assert.IsAssignableFrom<Exception>(healthEx);
        }

        [Fact]
        public void ExceptionHierarchy_SupportsComplexScenario()
        {
            // Scenario: Process start fails, wrapped in configuration validation
            // Arrange
            var processEx = new ProcessException(
                "npm process failed to start",
                ProcessException.ErrorCodes.ProcessStartFailed,
                new InvalidOperationException("Cannot find npm.exe"),
                new Dictionary<string, string> { { "npmPath", "C:\\npm\\npm.exe" } });

            var configEx = new ConfigurationException(
                "Failed to initialize bridge: process start failed",
                ConfigurationException.ErrorCodes.InvalidNpmPath,
                processEx,
                new Dictionary<string, string> { { "version", "2.0.0" } });

            // Act & Assert
            Assert.Equal(ProcessException.ErrorCodes.ProcessStartFailed, processEx.ErrorCode);
            Assert.Equal(ConfigurationException.ErrorCodes.InvalidNpmPath, configEx.ErrorCode);
            Assert.Same(processEx, configEx.InnerException);
            Assert.NotNull(processEx.InnerException);
            Assert.Equal("Cannot find npm.exe", processEx.InnerException!.Message);
            Assert.Single(processEx.Context);
            Assert.Single(configEx.Context);
        }
    }
}
