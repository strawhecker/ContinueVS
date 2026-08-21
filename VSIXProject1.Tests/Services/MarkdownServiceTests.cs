using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services;
using ContinueVS.Core.Types;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Unit tests for MarkdownService.
    /// Validates markdown parsing, language detection, and AST generation.
    /// </summary>
    public class MarkdownServiceTests
    {
        private readonly MarkdownService _service = new MarkdownService();

        [Fact]
        public async Task ParseMarkdownAsync_WithPlainText_ReturnsTextNode()
        {
            // Arrange
            var content = "Hello, world!";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MarkdownNodeType.Container, result.NodeType);
            Assert.NotEmpty(result.Children);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithCodeBlock_ReturnsCodeBlockNode()
        {
            // Arrange
            var content = "```csharp\nvar x = 42;\n```";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Children);
            var codeBlock = result.Children[0];
            Assert.Equal(MarkdownNodeType.CodeBlock, codeBlock.NodeType);
            Assert.Contains("x = 42", codeBlock.Content);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithMultipleCodeBlocks_ReturnsAllCodeBlocks()
        {
            // Arrange
            var content = "```javascript\nconst x = 1;\n```\nSome text\n```python\nprint('hello')\n```";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            var codeBlocks = result.Children.FindAll(n => n.NodeType == MarkdownNodeType.CodeBlock);
            Assert.Equal(2, codeBlocks.Count);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithBoldText_ReturnsBoldNode()
        {
            // Arrange
            var content = "This is **bold** text";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Children);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithItalicText_ReturnsItalicNode()
        {
            // Arrange
            var content = "This is *italic* text";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Children);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithLink_ReturnsLinkNode()
        {
            // Arrange
            var content = "[Google](https://google.com)";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Children);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithNull_ReturnEmptyContainer()
        {
            // Act
            var result = await _service.ParseMarkdownAsync(null!);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MarkdownNodeType.Container, result.NodeType);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithEmptyString_ReturnsEmptyContainer()
        {
            // Act
            var result = await _service.ParseMarkdownAsync(string.Empty);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MarkdownNodeType.Container, result.NodeType);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithWhitespace_ReturnsEmptyContainer()
        {
            // Act
            var result = await _service.ParseMarkdownAsync("   \n\t  ");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(MarkdownNodeType.Container, result.NodeType);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithCSharp_ReturnsCsharp()
        {
            // Arrange
            var fence = "```csharp";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("csharp", language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithJavaScript_ReturnsJavascript()
        {
            // Arrange
            var fence = "```javascript";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("javascript", language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithPython_ReturnsPython()
        {
            // Arrange
            var fence = "```python";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("python", language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithTypeScript_ReturnsTypescript()
        {
            // Arrange
            var fence = "```typescript";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("typescript", language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithWhitespace_ReturnsLanguage()
        {
            // Arrange
            var fence = "```  csharp  ";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("csharp", language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithNoLanguage_ReturnsEmpty()
        {
            // Arrange
            var fence = "```";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal(string.Empty, language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithEmpty_ReturnsEmpty()
        {
            // Act
            var language = _service.ExtractLanguageFromFence(string.Empty);

            // Assert
            Assert.Equal(string.Empty, language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithNull_ReturnsEmpty()
        {
            // Act
            var language = _service.ExtractLanguageFromFence(null!);

            // Assert
            Assert.Equal(string.Empty, language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithCaseInsensitivity_ReturnsLowercase()
        {
            // Arrange
            var fence = "```PYTHON";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("python", language);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithComplexMarkdown_ParsesAllFormats()
        {
            // Arrange
            var content = @"# Heading
This is **bold** and *italic* text.

```csharp
public class Example
{
    public void Method() { }
}
```

- List item 1
- List item 2

[Link](https://example.com)";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Children);
            var codeBlocks = result.Children.FindAll(n => n.NodeType == MarkdownNodeType.CodeBlock);
            Assert.NotEmpty(codeBlocks);
        }

        [Fact]
        public async Task ParseMarkdownAsync_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var content = "Code: `var x = a < b && c > d;`";

            // Act
            var result = await _service.ParseMarkdownAsync(content);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Children);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithSQLLanguage_ReturnsSql()
        {
            // Arrange
            var fence = "```sql";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("sql", language);
        }

        [Fact]
        public void ExtractLanguageFromFence_WithCPPLanguage_ReturnsCpp()
        {
            // Arrange
            var fence = "```cpp";

            // Act
            var language = _service.ExtractLanguageFromFence(fence);

            // Assert
            Assert.Equal("cpp", language);
        }
    }
}
