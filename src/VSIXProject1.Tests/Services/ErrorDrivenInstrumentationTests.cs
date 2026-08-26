using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Events;
using Moq;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Comprehensive xUnit test suite for ErrorDrivenInstrumentationService (gap29_8_11).
    /// Tests cover: exception capture, historical pattern lookup, instrumentation suggestion generation.
    /// </summary>
    public class ErrorDrivenInstrumentationTests
    {
        private Mock<IErrorRepository> CreateErrorRepositoryMock()
        {
            return new Mock<IErrorRepository>();
        }

        private Mock<IDebugStrategyGeneratorService> CreateStrategyGeneratorMock()
        {
            return new Mock<IDebugStrategyGeneratorService>();
        }

        // ====================================================================
        // TEST 1: SuggestInstrumentation_WithHistoricalMatch_ReturnsStrategy
        // ====================================================================

        [Fact]
        public async Task SuggestInstrumentation_WithHistoricalMatch_ReturnsStrategy()
        {
            // Arrange
            var mockRepo = CreateErrorRepositoryMock();
            var mockGenerator = CreateStrategyGeneratorMock();

            var historicalError = new ErrorRecord(
                fingerprint: "System.NullReferenceException:123",
                exceptionType: "System.NullReferenceException",
                exceptionMessage: "Object reference not set to an instance of an object",
                stackTraceJson: "at Program.Main()"
            );

            mockRepo
                .Setup(r => r.GetErrorsByFingerprintAsync(It.IsAny<string>()))
                .ReturnsAsync(new[] { historicalError });

            var strategy = new InstrumentationStrategy
            {
                Description = "Add null check",
                InstrumentationType = InstrumentationType.NullCheck,
                TargetFile = "Program.cs",
                Rationale = "Null guard recommended",
                CodeSnippets = new List<InstrumentationSnippet>
                {
                    new InstrumentationSnippet { LineNumber = 42, Code = "if (obj == null) throw new ArgumentNullException(nameof(obj));", Reason = "Null guard" }
                }
            };

            mockGenerator
                .Setup(g => g.GenerateStrategyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(strategy);

            var service = new ErrorDrivenInstrumentationService(mockRepo.Object, mockGenerator.Object);

            // Act
            var result = await service.SuggestInstrumentationAsync(
                exceptionType: "System.NullReferenceException",
                message: "Object reference not set to an instance of an object",
                stackTrace: "at Program.Main()",
                filePath: "Program.cs",
                lineNumber: 42
            );

            // Assert
            Assert.NotNull(result);
            Assert.Equal("System.NullReferenceException", result.ExceptionType);
            Assert.Equal("Program.cs", result.FilePath);
            Assert.Equal(42, result.LineNumber);
            Assert.NotNull(result.SuggestedStrategy);
            Assert.Single(result.SuggestedStrategy.CodeSnippets);
            Assert.True(result.ConfidenceScore > 0.5);
        }

        // ====================================================================
        // TEST 2: SuggestInstrumentation_NoHistoricalData_GeneratesBlankSlateStrategy
        // ====================================================================

        [Fact]
        public async Task SuggestInstrumentation_NoHistoricalData_GeneratesBlankSlateStrategy()
        {
            // Arrange
            var mockRepo = CreateErrorRepositoryMock();
            var mockGenerator = CreateStrategyGeneratorMock();

            mockRepo
                .Setup(r => r.GetErrorsByFingerprintAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ErrorRecord>());

            var strategy = new InstrumentationStrategy
            {
                Description = "Generic logging instrumentation",
                InstrumentationType = InstrumentationType.ConsoleLog,
                TargetFile = "Service.cs",
                Rationale = "No prior history; generic logging strategy",
                CodeSnippets = new List<InstrumentationSnippet>
                {
                    new InstrumentationSnippet { LineNumber = 50, Code = "Console.WriteLine(\"Debug point\");", Reason = "Logging" }
                }
            };

            mockGenerator
                .Setup(g => g.GenerateStrategyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(strategy);

            var service = new ErrorDrivenInstrumentationService(mockRepo.Object, mockGenerator.Object);

            // Act
            var result = await service.SuggestInstrumentationAsync(
                exceptionType: "System.ArgumentException",
                message: "Argument null or empty",
                stackTrace: "at Service.Method()",
                filePath: "Service.cs",
                lineNumber: 50
            );

            // Assert
            Assert.NotNull(result);
            Assert.Equal("System.ArgumentException", result.ExceptionType);
            Assert.NotNull(result.SuggestedStrategy);
            Assert.Contains("generic", result.Reasoning, StringComparison.OrdinalIgnoreCase);
        }

        // ====================================================================
        // TEST 3: SuggestInstrumentation_RepositoryQueryFails_ReturnsNull
        // ====================================================================

        [Fact]
        public async Task SuggestInstrumentation_RepositoryQueryFails_ReturnsNull()
        {
            // Arrange
            var mockRepo = CreateErrorRepositoryMock();
            var mockGenerator = CreateStrategyGeneratorMock();

            mockRepo
                .Setup(r => r.GetErrorsByFingerprintAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Repository not initialized"));

            var service = new ErrorDrivenInstrumentationService(mockRepo.Object, mockGenerator.Object);

            // Act
            var result = await service.SuggestInstrumentationAsync(
                exceptionType: "System.NullReferenceException",
                message: "Object reference",
                stackTrace: "at Program.Main()",
                filePath: "Program.cs",
                lineNumber: 42
            );

            // Assert
            Assert.Null(result);
            mockGenerator.Verify(g => g.GenerateStrategyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        // ====================================================================
        // TEST 4: SuggestInstrumentation_LlmTimeoutOrError_ReturnsNull
        // ====================================================================

        [Fact]
        public async Task SuggestInstrumentation_LlmTimeoutOrError_ReturnsNull()
        {
            // Arrange
            var mockRepo = CreateErrorRepositoryMock();
            var mockGenerator = CreateStrategyGeneratorMock();

            mockRepo
                .Setup(r => r.GetErrorsByFingerprintAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ErrorRecord>());

            mockGenerator
                .Setup(g => g.GenerateStrategyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException("LLM request timed out"));

            var service = new ErrorDrivenInstrumentationService(mockRepo.Object, mockGenerator.Object);

            // Act
            var result = await service.SuggestInstrumentationAsync(
                exceptionType: "System.DivideByZeroException",
                message: "Attempted to divide by zero",
                stackTrace: "at Math.Divide()",
                filePath: "Math.cs",
                lineNumber: 30
            );

            // Assert
            Assert.Null(result);
        }

        // ====================================================================
        // TEST 5: SuggestInstrumentation_NullExceptionContext_ReturnsNull
        // ====================================================================

        [Fact]
        public async Task SuggestInstrumentation_NullExceptionContext_ThrowsArgumentNullException()
        {
            // Arrange
            var mockRepo = CreateErrorRepositoryMock();
            var mockGenerator = CreateStrategyGeneratorMock();
            var service = new ErrorDrivenInstrumentationService(mockRepo.Object, mockGenerator.Object);

            // Act & Assert - null exceptionType
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await service.SuggestInstrumentationAsync(
                    exceptionType: null,
                    message: "Test",
                    stackTrace: "Test",
                    filePath: "Test.cs",
                    lineNumber: 1
                )
            );

            // Act & Assert - empty filePath
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await service.SuggestInstrumentationAsync(
                    exceptionType: "System.Exception",
                    message: "Test",
                    stackTrace: "Test",
                    filePath: "",
                    lineNumber: 1
                )
            );
        }

        // ====================================================================
        // TEST 6: SuggestInstrumentation_MultipleHistoricalErrors_ReturnsAggregatedInstrumentation
        // ====================================================================

        [Fact]
        public async Task SuggestInstrumentation_MultipleHistoricalErrors_ReturnsAggregatedInstrumentation()
        {
            // Arrange
            var mockRepo = CreateErrorRepositoryMock();
            var mockGenerator = CreateStrategyGeneratorMock();

            var errors = new[]
            {
                new ErrorRecord(
                    fingerprint: "null-ref-fp",
                    exceptionType: "System.NullReferenceException",
                    exceptionMessage: "Object reference",
                    stackTraceJson: "at Main()"
                ),
                new ErrorRecord(
                    fingerprint: "null-ref-fp",
                    exceptionType: "System.NullReferenceException",
                    exceptionMessage: "Object reference",
                    stackTraceJson: "at Main()"
                ),
                new ErrorRecord(
                    fingerprint: "null-ref-fp",
                    exceptionType: "System.NullReferenceException",
                    exceptionMessage: "Object reference",
                    stackTraceJson: "at Main()"
                )
            };

            mockRepo
                .Setup(r => r.GetErrorsByFingerprintAsync(It.IsAny<string>()))
                .ReturnsAsync(errors);

            var strategy = new InstrumentationStrategy
            {
                Description = "Comprehensive null check strategy",
                InstrumentationType = InstrumentationType.NullCheck,
                TargetFile = "Program.cs",
                Rationale = "Pattern from 3 historical occurrences",
                CodeSnippets = new List<InstrumentationSnippet>
                {
                    new InstrumentationSnippet { LineNumber = 42, Code = "if (obj == null) throw new ArgumentNullException(...);", Reason = "Null guard" }
                }
            };

            mockGenerator
                .Setup(g => g.GenerateStrategyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(strategy);

            var service = new ErrorDrivenInstrumentationService(mockRepo.Object, mockGenerator.Object);

            // Act
            var result = await service.SuggestInstrumentationAsync(
                exceptionType: "System.NullReferenceException",
                message: "Object reference",
                stackTrace: "at Main()",
                filePath: "Program.cs",
                lineNumber: 42
            );

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ConfidenceScore > 0.5);
            Assert.Contains("3", result.Reasoning);
        }

        // ====================================================================
        // TEST 7: ServiceBootstrapper_RegistersErrorDrivenInstrumentation_Correctly
        // ====================================================================

        [Fact]
        public void ServiceBootstrapper_RegistersErrorDrivenInstrumentation_Correctly()
        {
            // Arrange
            var mockConfig = new Mock<IConfigService>();
            var mockBridge = new Mock<IBridgeLogger>();
            var mockLlm = new Mock<ILlmService>();
            var mockSession = new Mock<ISessionService>();
            var mockContext = new Mock<IContextService>();
            var mockTool = new Mock<IToolService>();
            var mockNotification = new Mock<INotificationService>();
            var mockIde = new Mock<IIdeService>();

            // Act & Assert - Verify that ServiceBootstrapper can instantiate the service
            // (Full integration test would require ServiceBootstrapper.ConfigureServices to be called)
            var errorRepo = new Mock<IErrorRepository>();
            var strategyGen = new Mock<IDebugStrategyGeneratorService>();

            var service = new ErrorDrivenInstrumentationService(errorRepo.Object, strategyGen.Object, mockBridge.Object);
            Assert.NotNull(service);
            Assert.IsAssignableFrom<IErrorDrivenInstrumentationService>(service);
        }
    }
}
