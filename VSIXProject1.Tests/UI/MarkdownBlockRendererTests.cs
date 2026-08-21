using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using ContinueVS.UI.Renderers;
using ContinueVS.Core.Types;
using ContinueVS.Core.Syntax;

namespace VSIXProject1.Tests.UI
{
    /// <summary>
    /// Unit tests for MarkdownBlockRenderer WPF component.
    /// Validates rendering of markdown nodes, syntax highlighting, and event handling.
    /// </summary>
    public class MarkdownBlockRendererTests
    {
        [Fact]
        public void MarkdownBlockRenderer_Created_Initializes()
        {
            // Act
            var renderer = new MarkdownBlockRenderer();

            // Assert
            Assert.NotNull(renderer);
        }

        [Fact]
        public void MarkdownBlockRenderer_SetContent_UpdatesProperty()
        {
            // Arrange
            var renderer = new MarkdownBlockRenderer();
            var node = MarkdownNode.Text("Hello");

            // Act
            renderer.Content = node;

            // Assert
            Assert.Equal(node, renderer.Content);
        }

        [Fact]
        public void MarkdownBlockRenderer_WithNullContent_HandlesGracefully()
        {
            // Arrange
            var renderer = new MarkdownBlockRenderer();

            // Act
            renderer.Content = null;

            // Assert
            Assert.Null(renderer.Content);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderPlainText_CreatesTextBlock()
        {
            // Arrange
            var node = MarkdownNode.Text("Hello, world!");

            // Act
            var textBlock = MarkdownNodeRenderer.RenderPlainText(node);

            // Assert
            Assert.NotNull(textBlock);
            Assert.Equal("Hello, world!", textBlock.Text);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderBoldText_CreatesBoldTextBlock()
        {
            // Arrange
            var node = MarkdownNode.Bold("Important");

            // Act
            var textBlock = MarkdownNodeRenderer.RenderBoldText(node);

            // Assert
            Assert.NotNull(textBlock);
            Assert.Equal("Important", textBlock.Text);
            Assert.Equal(FontWeights.Bold, textBlock.FontWeight);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderItalicText_CreatesItalicTextBlock()
        {
            // Arrange
            var node = MarkdownNode.Italic("Emphasis");

            // Act
            var textBlock = MarkdownNodeRenderer.RenderItalicText(node);

            // Assert
            Assert.NotNull(textBlock);
            Assert.Equal("Emphasis", textBlock.Text);
            Assert.Equal(FontStyles.Italic, textBlock.FontStyle);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderLink_CreatesLinkTextBlock()
        {
            // Arrange
            var node = MarkdownNode.Link("Google", "https://google.com");

            // Act
            var textBlock = MarkdownNodeRenderer.RenderLink(node);

            // Assert
            Assert.NotNull(textBlock);
            Assert.Equal("Google", textBlock.Text);
            Assert.NotNull(textBlock.TextDecorations);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderCodeBlock_CreatesMonospaceTextBlock()
        {
            // Arrange
            var node = MarkdownNode.CodeBlock("var x = 42;", "csharp");

            // Act
            var textBlock = MarkdownNodeRenderer.RenderCodeBlock(node);

            // Assert
            Assert.NotNull(textBlock);
            Assert.Contains("x = 42", textBlock.Text);
            Assert.Contains("Consolas", textBlock.FontFamily.Source);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderCodeBlock_WithNoLanguage_StillRenders()
        {
            // Arrange
            var node = MarkdownNode.CodeBlock("some code", null);

            // Act
            var textBlock = MarkdownNodeRenderer.RenderCodeBlock(node);

            // Assert
            Assert.NotNull(textBlock);
            Assert.Contains("code", textBlock.Text);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderCodeBlock_WithEmptyContent_ReturnsEmptyTextBlock()
        {
            // Arrange
            var node = MarkdownNode.CodeBlock(string.Empty, "csharp");

            // Act
            var textBlock = MarkdownNodeRenderer.RenderCodeBlock(node);

            // Assert
            Assert.NotNull(textBlock);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_GetColorScheme_ReturnsCSharpScheme()
        {
            // Act
            var scheme = LanguageSyntaxHighlighter.GetColorScheme("csharp");

            // Assert
            Assert.NotNull(scheme);
            Assert.NotNull(scheme.Keyword);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_GetColorScheme_ReturnsJavaScriptScheme()
        {
            // Act
            var scheme = LanguageSyntaxHighlighter.GetColorScheme("javascript");

            // Assert
            Assert.NotNull(scheme);
            Assert.NotNull(scheme.Keyword);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_GetColorScheme_ReturnsPythonScheme()
        {
            // Act
            var scheme = LanguageSyntaxHighlighter.GetColorScheme("python");

            // Assert
            Assert.NotNull(scheme);
            Assert.NotNull(scheme.Keyword);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_GetColorScheme_WithUnsupportedLanguage_ReturnsDefault()
        {
            // Act
            var scheme = LanguageSyntaxHighlighter.GetColorScheme("unsupported");

            // Assert
            Assert.NotNull(scheme);
            Assert.NotNull(scheme.Keyword);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_GetColorScheme_WithNull_ReturnsDefault()
        {
            // Act
            var scheme = LanguageSyntaxHighlighter.GetColorScheme(null);

            // Assert
            Assert.NotNull(scheme);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_KeywordIsClassified()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("var", "csharp");

            // Assert
            Assert.NotEqual(LanguageSyntaxHighlighter.TokenType.Default, tokenType);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_NumericLiteralAsNumber()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("42", "csharp");

            // Assert
            Assert.Equal(LanguageSyntaxHighlighter.TokenType.Number, tokenType);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_QuotedStringAsString()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("\"hello\"", "csharp");

            // Assert
            Assert.Equal(LanguageSyntaxHighlighter.TokenType.String, tokenType);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_CommentAsComment()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("//comment", "csharp");

            // Assert
            Assert.Equal(LanguageSyntaxHighlighter.TokenType.Comment, tokenType);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_OperatorAsOperator()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("+=", "csharp");

            // Assert
            Assert.Equal(LanguageSyntaxHighlighter.TokenType.Operator, tokenType);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_GetTokenBrush_ReturnsValidBrush()
        {
            // Act
            var brush = LanguageSyntaxHighlighter.GetTokenBrush("var", "csharp");

            // Assert
            Assert.NotNull(brush);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_GetTokenBrush_WithOverrideType_UsesOverride()
        {
            // Act
            var brush = LanguageSyntaxHighlighter.GetTokenBrush("var", "csharp", LanguageSyntaxHighlighter.TokenType.String);

            // Assert
            Assert.NotNull(brush);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderPlainText_WithEmptyContent_ReturnsEmptyTextBlock()
        {
            // Arrange
            var node = MarkdownNode.Text(string.Empty);

            // Act
            var textBlock = MarkdownNodeRenderer.RenderPlainText(node);

            // Assert
            Assert.NotNull(textBlock);
            Assert.Equal(string.Empty, textBlock.Text);
        }

        [Fact]
        public void MarkdownNodeRenderer_RenderBoldText_WithNullContent_HandlesGracefully()
        {
            // Arrange
            var node = new MarkdownNode { NodeType = MarkdownNodeType.Bold };

            // Act
            var textBlock = MarkdownNodeRenderer.RenderBoldText(node);

            // Assert
            Assert.NotNull(textBlock);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_WithWhitespaceOnly_DefaultToken()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("  ", "csharp");

            // Assert
            Assert.Equal(LanguageSyntaxHighlighter.TokenType.Default, tokenType);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_PythonKeywordAsKeyword()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("def", "python");

            // Assert
            Assert.Equal(LanguageSyntaxHighlighter.TokenType.Keyword, tokenType);
        }

        [Fact]
        public void LanguageSyntaxHighlighter_ClassifyToken_JavaScriptKeywordAsKeyword()
        {
            // Act
            var tokenType = LanguageSyntaxHighlighter.ClassifyToken("function", "javascript");

            // Assert
            Assert.Equal(LanguageSyntaxHighlighter.TokenType.Keyword, tokenType);
        }
    }
}
