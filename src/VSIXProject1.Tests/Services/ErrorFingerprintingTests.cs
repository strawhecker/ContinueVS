using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Comprehensive xUnit test suite for ErrorFingerprintService (gap29_5).
    /// Tests coverage:
    /// - Fingerprint generation consistency
    /// - Duplicate error detection and occurrence counting
    /// - Manual error grouping (bidirectional)
    /// - Edge cases (fewer than 3 frames, null handling)
    /// </summary>
    public class ErrorFingerprintingTests
    {
        private readonly ErrorFingerprintService _service;

        public ErrorFingerprintingTests()
        {
            _service = new ErrorFingerprintService();
        }

        // ====================================================================
        // TEST 1: GenerateFingerprint_CreatesConsistentHash_For_Same_Exception
        // ====================================================================

        [Fact]
        public async Task GenerateFingerprint_CreatesConsistentHash_For_Same_Exception()
        {
            // Arrange
            var frame1 = new StackTraceFrame
            {
                FrameIndex = 0,
                MethodName = "Method1",
                FilePath = "/path/to/file1.cs",
                LineNumber = 10,
                ExceptionType = "System.NullReferenceException",
                ExceptionMessage = "Object reference not set"
            };

            var frame2 = new StackTraceFrame
            {
                FrameIndex = 1,
                MethodName = "Method2",
                FilePath = "/path/to/file2.cs",
                LineNumber = 20
            };

            var parseResult1 = new ParseResult
            {
                Frames = new[] { frame1, frame2 },
                SuccessfulParserName = "DotNetParser"
            };

            var parseResult2 = new ParseResult
            {
                Frames = new[] { frame1, frame2 },
                SuccessfulParserName = "DotNetParser"
            };

            // Act
            var fingerprint1 = await _service.GenerateFingerprintAsync(parseResult1);
            var fingerprint2 = await _service.GenerateFingerprintAsync(parseResult2);

            // Assert
            Assert.Equal(fingerprint1.Fingerprint, fingerprint2.Fingerprint);
            Assert.Equal("System.NullReferenceException", fingerprint1.ExceptionType);
            Assert.NotEmpty(fingerprint1.TopFrameSummaries);
            Assert.True(fingerprint1.Fingerprint.Length > 0);
        }

        // ====================================================================
        // TEST 2: RecordError_DetectsDuplicate_And_IncrementCount
        // ====================================================================

        [Fact]
        public async Task RecordError_DetectsDuplicate_And_IncrementCount()
        {
            // Arrange
            var frame = new StackTraceFrame
            {
                FrameIndex = 0,
                MethodName = "TestMethod",
                FilePath = "/test.cs",
                LineNumber = 42,
                ExceptionType = "System.InvalidOperationException"
            };

            var parseResult = new ParseResult { Frames = new[] { frame } };
            var fingerprint = await _service.GenerateFingerprintAsync(parseResult);

            // Act - Record the same error 3 times
            await _service.RecordErrorAsync(fingerprint);
            var countAfterFirst = await _service.GetOccurrenceCountAsync(fingerprint.Fingerprint);

            await _service.RecordErrorAsync(fingerprint);
            var countAfterSecond = await _service.GetOccurrenceCountAsync(fingerprint.Fingerprint);

            await _service.RecordErrorAsync(fingerprint);
            var countAfterThird = await _service.GetOccurrenceCountAsync(fingerprint.Fingerprint);

            var isKnown = await _service.GetIsKnownErrorAsync(fingerprint.Fingerprint);

            // Assert
            Assert.Equal(1, countAfterFirst);
            Assert.Equal(2, countAfterSecond);
            Assert.Equal(3, countAfterThird);
            Assert.True(isKnown);
        }

        // ====================================================================
        // TEST 3: GroupErrors_LinksManuallyRelatedErrors
        // ====================================================================

        [Fact]
        public async Task GroupErrors_LinksManuallyRelatedErrors()
        {
            // Arrange
            var fingerprint1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var fingerprint2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            // Act
            await _service.GroupErrorsAsync(fingerprint1, fingerprint2);

            var grouped1 = await _service.GetGroupedFingerprintsAsync(fingerprint1);
            var grouped2 = await _service.GetGroupedFingerprintsAsync(fingerprint2);

            // Assert
            Assert.Single(grouped1);
            Assert.Contains(fingerprint2, grouped1);

            Assert.Single(grouped2);
            Assert.Contains(fingerprint1, grouped2);
        }

        // ====================================================================
        // BONUS TEST: GenerateFingerprint_HandlesFewerThan3Frames
        // ====================================================================

        [Fact]
        public async Task GenerateFingerprint_HandlesFewerThan3Frames()
        {
            // Arrange - Only 1 frame
            var frame = new StackTraceFrame
            {
                FrameIndex = 0,
                MethodName = "SingleMethod",
                FilePath = "/single.cs",
                LineNumber = 1,
                ExceptionType = "System.ArgumentException"
            };

            var parseResult = new ParseResult { Frames = new[] { frame } };

            // Act
            var fingerprint = await _service.GenerateFingerprintAsync(parseResult);

            // Assert
            Assert.NotEmpty(fingerprint.Fingerprint);
            Assert.Equal("System.ArgumentException", fingerprint.ExceptionType);
            Assert.Single(fingerprint.TopFrameSummaries);
        }

        // ====================================================================
        // BONUS TEST: ManualGrouping_IsBidirectional
        // ====================================================================

        [Fact]
        public async Task ManualGrouping_IsBidirectional()
        {
            // Arrange
            var fp1 = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff11";
            var fp2 = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff22";
            var fp3 = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff33";

            // Act - Group fp1 with fp2, then fp1 with fp3
            await _service.GroupErrorsAsync(fp1, fp2);
            await _service.GroupErrorsAsync(fp1, fp3);

            var groupedWith1 = await _service.GetGroupedFingerprintsAsync(fp1);
            var groupedWith2 = await _service.GetGroupedFingerprintsAsync(fp2);
            var groupedWith3 = await _service.GetGroupedFingerprintsAsync(fp3);

            // Assert
            Assert.Equal(2, groupedWith1.Count);
            Assert.Single(groupedWith2);
            Assert.Single(groupedWith3);

            Assert.Contains(fp2, groupedWith1);
            Assert.Contains(fp3, groupedWith1);
            Assert.Contains(fp1, groupedWith2);
            Assert.Contains(fp1, groupedWith3);
        }

        // ====================================================================
        // BONUS TEST: GetIsKnownErrorAsync_ReturnsFalseForNewError
        // ====================================================================

        [Fact]
        public async Task GetIsKnownErrorAsync_ReturnsFalseForNewError()
        {
            // Arrange
            var unknownFingerprint = "0000000000000000000000000000000000000000000000000000000000000000";

            // Act
            var isKnown = await _service.GetIsKnownErrorAsync(unknownFingerprint);

            // Assert
            Assert.False(isKnown);
        }

        // ====================================================================
        // BONUS TEST: GetOccurrenceCountAsync_ReturnsZeroForUnknownFingerprint
        // ====================================================================

        [Fact]
        public async Task GetOccurrenceCountAsync_ReturnsZeroForUnknownFingerprint()
        {
            // Arrange
            var unknownFingerprint = "1111111111111111111111111111111111111111111111111111111111111111";

            // Act
            var count = await _service.GetOccurrenceCountAsync(unknownFingerprint);

            // Assert
            Assert.Equal(0, count);
        }
    }
}
