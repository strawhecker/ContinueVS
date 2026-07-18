#nullable enable

using ContinueVS.IPC;
using ContinueVS.Services;
using Newtonsoft.Json.Linq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Test Suite for CodeLensService (Step 90)
    ///
    /// Tests cover basic functionality:
    /// - Service creation and disposal
    /// - Cache invalidation
    /// - Input validation
    ///
    /// Note: Full integration tests with bridge communication 
    /// are part of bridge integration tests (Step 30).
    /// </summary>
    public class CodeLensServiceTests : IDisposable
    {
        /// <summary>
        /// Mock transport that captures sent messages and simulates responses.
        /// </summary>
        private class TestBridgeTransport : IBridgeTransport
        {
            public bool IsRunning => true;

            // Captured state for verification
            public Message? LastSentMessage { get; private set; }
            public int SendCount { get; private set; }

            // Response simulation
            private Message? _responseToReturn;

#pragma warning disable CS0067 // Event is never used
            public event EventHandler<MessageReceivedEventArgs>? OnMessageReceived;
            public event EventHandler<BridgeErrorEventArgs>? OnError;
            public event EventHandler? OnClosed;
#pragma warning restore CS0067

            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync() => Task.CompletedTask;

            public Task SendMessageAsync(Message message, CancellationToken cancellationToken)
            {
                LastSentMessage = message;
                SendCount++;
                return Task.CompletedTask;
            }

            public Task<Message?> ReceiveMessageAsync(CancellationToken cancellationToken)
            {
                if (_responseToReturn == null)
                    return Task.FromResult<Message?>(null);

                // Automatically correlate MessageId with the last sent message
                // (so the service can match request/response)
                if (LastSentMessage != null)
                {
                    _responseToReturn.MessageId = LastSentMessage.MessageId;
                }

                return Task.FromResult<Message?>(_responseToReturn);
            }

            public async ValueTask DisposeAsync()
            {
                await Task.CompletedTask;
            }

            /// <summary>
            /// Configure the response to return on next ReceiveMessageAsync call.
            /// </summary>
            public void SetResponse(Message response)
            {
                _responseToReturn = response;
            }

            /// <summary>
            /// Reset captured state for next test.
            /// </summary>
            public void Reset()
            {
                LastSentMessage = null;
                SendCount = 0;
                _responseToReturn = null;
            }
        }

        [Fact]
        public void CodeLensService_Constructor_AcceptsValidTransport()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();

            // Act
            var service = new CodeLensService(mockTransport);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public void CodeLensService_Constructor_ThrowsOnNullTransport()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CodeLensService(null!));
        }

        [Fact]
        public void InvalidateCache_WithValidFilePath_DoesNotThrow()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            // Act & Assert (should not throw)
            service.InvalidateCache("src/Test.cs");
        }

        [Fact]
        public void InvalidateCache_WithNullFilePath_DoesNotThrow()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            // Act & Assert (should not throw)
            service.InvalidateCache(null!);
        }

        [Fact]
        public void ClearCache_DoesNotThrow()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            // Act & Assert (should not throw)
            service.ClearCache();
        }

        [Fact]
        public async Task GetCodeLensesAsync_WithNullFilePath_ReturnsEmptyList()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            // Act
            var result = await service.GetCodeLensesAsync(null!);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCodeLensesAsync_WithEmptyFilePath_ReturnsEmptyList()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            // Act
            var result = await service.GetCodeLensesAsync("");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCodeLensesAsync_WithValidFilePath_ReturnsListOfCodeLenses()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            // Act
            var result = await service.GetCodeLensesAsync("src/Test.cs");

            // Assert
            Assert.NotNull(result);
            Assert.IsType<List<CodeLensService.CodeLensData>>(result);
        }

        [Fact]
        public async Task GetCodeLensesAsync_WithSuccessfulResponse_MapsLensesCorrectly()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            // Create a successful bridge response with two lenses
            var lensesArray = new JArray
            {
                new JObject
                {
                    { "line", 10 },
                    { "command", "runTest" },
                    { "title", "Run Test" },
                    { "data", new JObject { { "testName", "TestMethod1" } } }
                },
                new JObject
                {
                    { "line", 25 },
                    { "command", "viewReferences" },
                    { "title", "View References" },
                    { "data", JObject.FromObject(new { }) }
                }
            };

            var response = new Message
            {
                MessageId = "", // Will be set by SendBridgeMessageAsync
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject { { "lenses", lensesArray } } }
                }
            };

            mockTransport.SetResponse(response);

            // Act
            var result = await service.GetCodeLensesAsync("src/Test.cs");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            // Verify first lens
            Assert.Equal(10, result[0].Line);
            Assert.Equal("runTest", result[0].Command);
            Assert.Equal("Run Test", result[0].Title);
            Assert.NotNull(result[0].Data);

            // Verify second lens
            Assert.Equal(25, result[1].Line);
            Assert.Equal("viewReferences", result[1].Command);
            Assert.Equal("View References", result[1].Title);
        }

        [Fact]
        public async Task GetCodeLensesAsync_WithErrorResponse_ReturnsEmptyList()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", false },
                    { "error", new JObject { { "code", "FILE_NOT_FOUND" } } }
                }
            };

            mockTransport.SetResponse(response);

            // Act
            var result = await service.GetCodeLensesAsync("src/NonExistent.cs");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCodeLensesAsync_WithoutLensesArray_ReturnsEmptyList()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject { } }  // Missing 'lenses' array
                }
            };

            mockTransport.SetResponse(response);

            // Act
            var result = await service.GetCodeLensesAsync("src/Test.cs");

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCodeLensesAsync_CachesResultsForSubsequentCalls()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject 
                    { 
                        { "lenses", new JArray 
                        { 
                            new JObject 
                            { 
                                { "line", 5 }, 
                                { "command", "test" }, 
                                { "title", "Test" } 
                            } 
                        } } 
                    } }
                }
            };

            mockTransport.SetResponse(response);

            // Act - First call
            var result1 = await service.GetCodeLensesAsync("src/Test.cs");
            var firstSendCount = mockTransport.SendCount;

            // Reconfigure response for subsequent calls
            mockTransport.SetResponse(new Message { Data = new JObject { { "success", true } } });

            // Act - Second call (should use cache)
            var result2 = await service.GetCodeLensesAsync("src/Test.cs");
            var secondSendCount = mockTransport.SendCount;

            // Assert - Bridge should only be called once (cache hit on second call)
            Assert.Equal(1, firstSendCount);
            Assert.Equal(1, secondSendCount); // Not incremented
            Assert.Single(result1);
            Assert.Equal(result1[0].Line, result2[0].Line);
        }

        [Fact]
        public async Task GetCodeLensesAsync_SendsCorrectPayload_IncludesRangeCharField()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject { { "success", true }, { "data", new JObject { { "lenses", new JArray() } } } }
            };

            mockTransport.SetResponse(response);

            // Act
            await service.GetCodeLensesAsync("src/Test.cs", range: (10, 20));

            // Assert - Verify the sent message
            Assert.NotNull(mockTransport.LastSentMessage);
            Assert.Equal("bridge:getCodeLenses", mockTransport.LastSentMessage.MessageType);

            var data = mockTransport.LastSentMessage.Data as JObject;
            Assert.NotNull(data);
            Assert.Equal("src/Test.cs", data["filePath"]?.Value<string>());

            // Verify range structure with 'char' field (not 'c')
            var range = data["range"] as JObject;
            Assert.NotNull(range);

            var start = range["start"] as JObject;
            Assert.NotNull(start);
            Assert.Equal(10, start["line"]?.Value<int>());
            Assert.Equal(0, start["@char"]?.Value<int>() ?? start["char"]?.Value<int>()); // Handle @ escape in JSON

            var end = range["end"] as JObject;
            Assert.NotNull(end);
            Assert.Equal(20, end["line"]?.Value<int>());
            Assert.Equal(0, end["@char"]?.Value<int>() ?? end["char"]?.Value<int>());
        }

        [Fact]
        public async Task GetCodeLensesAsync_GeneratesAndSendsUniqueMessageId()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject { { "success", true }, { "data", new JObject { { "lenses", new JArray() } } } }
            };

            mockTransport.SetResponse(response);

            // Act
            await service.GetCodeLensesAsync("src/Test.cs");

            // Assert - MessageId should be set and non-empty (UUID format)
            Assert.NotNull(mockTransport.LastSentMessage);
            Assert.NotEmpty(mockTransport.LastSentMessage.MessageId);
            Assert.True(Guid.TryParse(mockTransport.LastSentMessage.MessageId, out _),
                "MessageId should be a valid GUID");
        }

        [Fact]
        public async Task GetCodeLensesAsync_CacheTTLExpiry_FetchesNewResultsAfterExpiry()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var initialResponse = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject 
                    { 
                        { "lenses", new JArray 
                        { 
                            new JObject 
                            { 
                                { "line", 1 }, 
                                { "command", "test1" }, 
                                { "title", "First Version" } 
                            } 
                        } } 
                    } }
                }
            };

            mockTransport.SetResponse(initialResponse);

            // Act - First call
            var result1 = await service.GetCodeLensesAsync("src/TTLTest.cs");
            var firstSendCount = mockTransport.SendCount;
            Assert.Single(result1);
            Assert.Equal("First Version", result1[0].Title);

            // Wait for cache to expire (cache TTL is 5000ms in CodeLensService)
            // Use a shorter wait in tests by triggering cache invalidation behavior
            await Task.Delay(5100); // Wait 5.1 seconds for TTL to expire

            var expiredResponse = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject 
                    { 
                        { "lenses", new JArray 
                        { 
                            new JObject 
                            { 
                                { "line", 1 }, 
                                { "command", "test2" }, 
                                { "title", "Updated Version" } 
                            } 
                        } } 
                    } }
                }
            };

            mockTransport.SetResponse(expiredResponse);

            // Act - Second call after TTL expiry
            var result2 = await service.GetCodeLensesAsync("src/TTLTest.cs");
            var secondSendCount = mockTransport.SendCount;

            // Assert - Bridge should be called again after TTL expiry
            Assert.Equal(1, firstSendCount);
            Assert.Equal(2, secondSendCount); // Incremented after TTL expiry
            Assert.Single(result2);
            Assert.Equal("Updated Version", result2[0].Title); // New data should be returned
        }

        [Fact]
        public async Task GetCodeLensesAsync_InvalidateCacheBeforeTTL_FetchesNewResults()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var initialResponse = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject 
                    { 
                        { "lenses", new JArray 
                        { 
                            new JObject 
                            { 
                                { "line", 1 }, 
                                { "command", "test" }, 
                                { "title", "Initial" } 
                            } 
                        } } 
                    } }
                }
            };

            mockTransport.SetResponse(initialResponse);

            // Act - First call
            var result1 = await service.GetCodeLensesAsync("src/InvalidateTest.cs");
            var sendCountBefore = mockTransport.SendCount;

            // Manually invalidate cache (simulates document change)
            service.InvalidateCache("src/InvalidateTest.cs");

            var updatedResponse = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject 
                    { 
                        { "lenses", new JArray 
                        { 
                            new JObject 
                            { 
                                { "line", 2 }, 
                                { "command", "test" }, 
                                { "title", "Updated" } 
                            } 
                        } } 
                    } }
                }
            };

            mockTransport.SetResponse(updatedResponse);

            // Act - Second call after cache invalidation
            var result2 = await service.GetCodeLensesAsync("src/InvalidateTest.cs");
            var sendCountAfter = mockTransport.SendCount;

            // Assert - Bridge should be called again after cache invalidation
            Assert.Equal(1, sendCountBefore);
            Assert.Equal(2, sendCountAfter); // Called again after invalidation
            Assert.Equal(1, result1[0].Line); // Original data
            Assert.Equal(2, result2[0].Line); // Updated data
        }

        [Fact]
        public async Task GetCodeLensesAsync_BridgeTimeout_ReturnsEmptyList()
        {
            // Arrange - Create a transport that simulates timeout
            var timeoutTransport = new TestBridgeTransportWithDelay(delayMs: 4000); // Delay longer than timeout
            var service = new CodeLensService(timeoutTransport);

            // Act - Should handle timeout gracefully
            var result = await service.GetCodeLensesAsync("src/TimeoutTest.cs");

            // Assert - Should return empty list without throwing
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCodeLensesAsync_BridgeException_ReturnsEmptyListWithoutThrowing()
        {
            // Arrange - Create a transport that throws an exception
            var faultyTransport = new TestBridgeTransportWithFault("Simulated bridge failure");
            var service = new CodeLensService(faultyTransport);

            // Act - Should handle exception gracefully
            var result = await service.GetCodeLensesAsync("src/FaultyTest.cs");

            // Assert - Should return empty list without throwing
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCodeLensesAsync_MalformedLensObject_SkipsMalformedAndReturnsValidLenses()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject 
                    { 
                        { "lenses", new JArray 
                        { 
                            new JObject 
                            { 
                                { "line", 10 }, 
                                { "command", "validCommand" }, 
                                { "title", "Valid Lens" } 
                            },
                            new JObject 
                            { 
                                // Missing required fields
                                { "data", new JObject() } 
                            },
                            new JObject 
                            { 
                                { "line", 20 }, 
                                { "command", "anotherValid" }, 
                                { "title", "Another Valid Lens" } 
                            }
                        } } 
                    } }
                }
            };

            mockTransport.SetResponse(response);

            // Act
            var result = await service.GetCodeLensesAsync("src/MalformedTest.cs");

            // Assert - Should skip malformed object but still return valid lenses
            Assert.NotNull(result);
            Assert.Equal(2, result.Count); // Only valid lenses
            Assert.Equal(10, result[0].Line);
            Assert.Equal(20, result[1].Line);
        }

        [Fact]
        public async Task GetCodeLensesAsync_WithoutRangeParameter_SendsMinimalPayload()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject { { "success", true }, { "data", new JObject { { "lenses", new JArray() } } } }
            };

            mockTransport.SetResponse(response);

            // Act
            await service.GetCodeLensesAsync("src/MinimalPayload.cs");

            // Assert - Verify minimal payload (no range)
            Assert.NotNull(mockTransport.LastSentMessage);
            var data = mockTransport.LastSentMessage.Data as JObject;
            Assert.NotNull(data);
            Assert.Equal("src/MinimalPayload.cs", data["filePath"]?.Value<string>());
            Assert.Null(data["range"]); // No range field when not specified
        }

        [Fact]
        public async Task ClearCache_RemovesAllCachedEntries()
        {
            // Arrange
            var mockTransport = new TestBridgeTransport();
            var service = new CodeLensService(mockTransport);

            var response = new Message
            {
                MessageId = "",
                MessageType = "response",
                Data = new JObject
                {
                    { "success", true },
                    { "data", new JObject { { "lenses", new JArray 
                    { 
                        new JObject { { "line", 1 }, { "command", "test" }, { "title", "Test" } } 
                    } } } }
                }
            };

            mockTransport.SetResponse(response);

            // Act - Populate cache with multiple files
            var result1 = await service.GetCodeLensesAsync("file1.cs");
            var result2 = await service.GetCodeLensesAsync("file2.cs");
            Assert.NotEmpty(result1);
            Assert.NotEmpty(result2);

            var sendCountBeforeClear = mockTransport.SendCount;

            // Clear cache
            service.ClearCache();

            mockTransport.Reset();
            mockTransport.SetResponse(response);

            // Act - Request same files again
            var result3 = await service.GetCodeLensesAsync("file1.cs");
            var result4 = await service.GetCodeLensesAsync("file2.cs");
            var sendCountAfterClear = mockTransport.SendCount;

            // Assert - Should fetch from bridge again (cache was cleared)
            Assert.Equal(2, sendCountBeforeClear); // Initial calls
            Assert.Equal(2, sendCountAfterClear); // Calls after clear (cache was empty)
        }

        /// <summary>
        /// Test transport that simulates delayed responses (for timeout testing).
        /// </summary>
        private class TestBridgeTransportWithDelay : IBridgeTransport
        {
            private readonly int _delayMs;

            public bool IsRunning => true;

#pragma warning disable CS0067 // Event is never used
            public event EventHandler<MessageReceivedEventArgs>? OnMessageReceived;
            public event EventHandler<BridgeErrorEventArgs>? OnError;
            public event EventHandler? OnClosed;
#pragma warning restore CS0067

            public TestBridgeTransportWithDelay(int delayMs)
            {
                _delayMs = delayMs;
            }

            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync() => Task.CompletedTask;

            public async Task SendMessageAsync(Message message, CancellationToken cancellationToken)
            {
                await Task.Delay(_delayMs, cancellationToken);
            }

            public async Task<Message?> ReceiveMessageAsync(CancellationToken cancellationToken)
            {
                await Task.Delay(_delayMs, cancellationToken);
                return null; // Simulate timeout by returning nothing
            }

            public async ValueTask DisposeAsync()
            {
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// Test transport that simulates bridge faults.
        /// </summary>
        private class TestBridgeTransportWithFault : IBridgeTransport
        {
            private readonly string _faultMessage;

            public bool IsRunning => true;

#pragma warning disable CS0067 // Event is never used
            public event EventHandler<MessageReceivedEventArgs>? OnMessageReceived;
            public event EventHandler<BridgeErrorEventArgs>? OnError;
            public event EventHandler? OnClosed;
#pragma warning restore CS0067

            public TestBridgeTransportWithFault(string faultMessage)
            {
                _faultMessage = faultMessage;
            }

            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync() => Task.CompletedTask;

            public Task SendMessageAsync(Message message, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException(_faultMessage);
            }

            public Task<Message?> ReceiveMessageAsync(CancellationToken cancellationToken)
            {
                throw new InvalidOperationException(_faultMessage);
            }

            public async ValueTask DisposeAsync()
            {
                await Task.CompletedTask;
            }
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
