#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Services.Tests
{
    /// <summary>
    /// Tests for DebugStrategyGeneratorService strategy parsing.
    ///
    /// Root-cause regression: the previous brittle regex-based parser failed whenever the
    /// LLM emitted a snippet <c>code</c> containing brackets/quotes/interpolation — e.g.
    /// <c>Console.WriteLine($"[Messages_CollectionChanged] ...")</c>. The non-greedy
    /// <c>snippets</c> array regex stopped at the literal <c>]</c> inside the string, so zero
    /// snippets parsed and the whole strategy was discarded (0 changes applied).
    /// </summary>
    public class DebugStrategyGeneratorServiceTests
    {
        private static DebugStrategyGeneratorService CreateService(string streamedJson)
        {
            var chunk = new CoreTypes.CompletionChunk
            {
                Type = CoreTypes.ChunkType.Text,
                Content = streamedJson
            };

            var mockLlm = new Mock<ILlmService>();
            mockLlm
                .Setup(m => m.StreamAsync(
                    It.IsAny<IEnumerable<CoreTypes.ChatMessage>>(),
                    It.IsAny<StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new AsyncEnumerableWrapper<CoreTypes.CompletionChunk>(new[] { chunk }));

            return new DebugStrategyGeneratorService(mockLlm.Object);
        }

        [Fact]
        public async Task GenerateStrategyAsync_SnippetCodeWithBracketsQuotesInterpolation_ParsesBothSnippets()
        {
            // Regression for the reported bug: 'code' contains ']' (inside "[Messages_...]"),
            // escaped quotes, and interpolated braces. The old parser returned 0 snippets here.
            var json = @"{
  ""description"": ""Add temporary Console.WriteLine logging"",
  ""instrumentationType"": ""LoggingStatement"",
  ""targetFile"": ""ViewModels/MessagesViewModel.cs"",
  ""rationale"": ""Verify runtime scroll behavior."",
  ""snippets"": [
    {
      ""lineNumber"": 120,
      ""code"": ""Console.WriteLine($\""[Messages_CollectionChanged] ScrollableHeight={ScrollableHeight}, VerticalOffset={VerticalOffset}, AboutToCallScrollToEnd=true\"");"",
      ""reason"": ""Log current scroll metrics""
    },
    {
      ""lineNumber"": 125,
      ""code"": ""// ScrollToEnd is called unconditionally here; logging after the call for confirmation"",
      ""reason"": ""Placeholder comment""
    }
  ]
}";

            var service = CreateService(json);

            var strategy = await service.GenerateStrategyAsync("verify ScrollToEnd behavior");

            Assert.NotNull(strategy);
            Assert.Equal(2, strategy!.CodeSnippets.Count);
            Assert.Equal("ViewModels/MessagesViewModel.cs", strategy.TargetFile);
            Assert.Equal(CoreTypes.InstrumentationType.LoggingStatement, strategy.InstrumentationType);

            var first = strategy.CodeSnippets[0];
            Assert.Equal(120, first.LineNumber);
            Assert.Contains("[Messages_CollectionChanged]", first.Code);
            Assert.Contains("ScrollableHeight={ScrollableHeight}", first.Code);
            Assert.Equal("Log current scroll metrics", first.Reason);

            var second = strategy.CodeSnippets[1];
            Assert.Equal(125, second.LineNumber);
            Assert.Contains("// ScrollToEnd is called unconditionally", second.Code);
        }

        [Fact]
        public async Task GenerateStrategyAsync_MultipleSnippets_ParsesAll()
        {
            var json = @"{
  ""description"": ""Add null guards"",
  ""instrumentationType"": ""NullCheck"",
  ""targetFile"": ""Services/Foo.cs"",
  ""rationale"": ""Prevent null dereference"",
  ""snippets"": [
    { ""lineNumber"": 10, ""code"": ""if (x == null) throw new ArgumentNullException(nameof(x));"", ""reason"": ""Null guard"" },
    { ""lineNumber"": 20, ""code"": ""if (y == null) return;"", ""reason"": ""Early return"" },
    { ""lineNumber"": 30, ""code"": ""if (z == null) throw new InvalidOperationException();"", ""reason"": ""Guard invariant"" }
  ]
}";

            var service = CreateService(json);

            var strategy = await service.GenerateStrategyAsync("guard against nulls");

            Assert.NotNull(strategy);
            Assert.Equal(3, strategy!.CodeSnippets.Count);
            Assert.Equal(10, strategy.CodeSnippets[0].LineNumber);
            Assert.Equal("if (x == null) throw new ArgumentNullException(nameof(x));", strategy.CodeSnippets[0].Code);
            Assert.Equal(30, strategy.CodeSnippets[2].LineNumber);
        }

        [Fact]
        public async Task GenerateStrategyAsync_ResponseWrappedInMarkdownFence_ParsesStrategy()
        {
            var json = @"
```json
{
  ""description"": ""Try-catch wrapper"",
  ""instrumentationType"": ""TryCatchWrapper"",
  ""targetFile"": ""Services/Baz.cs"",
  ""rationale"": ""Log exceptions"",
  ""snippets"": [
    { ""lineNumber"": 5, ""code"": ""try { } catch (Exception ex) { log(ex); }"", ""reason"": ""Wrap body"" }
  ]
}
```";

            var service = CreateService(json);

            var strategy = await service.GenerateStrategyAsync("wrap in try catch");

            Assert.NotNull(strategy);
            Assert.Single(strategy!.CodeSnippets);
            Assert.Equal("Services/Baz.cs", strategy.TargetFile);
            Assert.Equal(CoreTypes.InstrumentationType.TryCatchWrapper, strategy.InstrumentationType);
        }

        [Fact]
        public async Task GenerateStrategyAsync_InvalidJsonNoSnippets_ReturnsNull()
        {
            // Empty snippets array => IsValid() false => null
            var json = @"{
  ""description"": ""No instrumentation needed"",
  ""instrumentationType"": ""ConsoleLog"",
  ""targetFile"": ""Services/Bar.cs"",
  ""rationale"": ""Nothing to do"",
  ""snippets"": []
}";

            var service = CreateService(json);

            var strategy = await service.GenerateStrategyAsync("do nothing");

            Assert.Null(strategy);
        }

        [Fact]
        public async Task GenerateStrategyAsync_GarbageResponse_ReturnsNull()
        {
            var service = CreateService("This is not JSON at all. There is no strategy here.");

            var strategy = await service.GenerateStrategyAsync("anything");

            Assert.Null(strategy);
        }
    }

    /// <summary>
    /// Minimal IAsyncEnumerable wrapper so StreamAsync can be mocked (matches the pattern used
    /// in MessageDispatcherLlmServiceStreamingTests).
    /// </summary>
    internal class AsyncEnumerableWrapper<T> : IAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> _items;
        private readonly CancellationToken _ct;

        public AsyncEnumerableWrapper(IEnumerable<T> items, CancellationToken ct = default)
        {
            _items = items;
            _ct = ct;
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                _ct.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }
}
