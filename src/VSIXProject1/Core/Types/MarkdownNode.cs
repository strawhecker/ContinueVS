using System;
using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Exception thrown when markdown parsing fails.
    /// </summary>
    public class MarkdownParsingException : Exception
    {
        public MarkdownParsingException(string message) : base(message) { }
        public MarkdownParsingException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Represents a node in the markdown AST.
    /// Used to structure parsed markdown for rendering in WPF.
    /// </summary>
    public class MarkdownNode
    {
        /// <summary>
        /// Type of markdown node (determines rendering behavior).
        /// </summary>
        public MarkdownNodeType NodeType { get; set; }

        /// <summary>
        /// Text content of this node (e.g., code block source, plain text, link URL).
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Language identifier for code blocks (e.g., "csharp", "javascript").
        /// Null or empty for non-code-block nodes.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Child nodes (for containers like lists, nested blocks).
        /// </summary>
        public List<MarkdownNode> Children { get; set; } = new List<MarkdownNode>();

        /// <summary>
        /// Creates a text node.
        /// </summary>
        public static MarkdownNode Text(string content) => new MarkdownNode
        {
            NodeType = MarkdownNodeType.Text,
            Content = content
        };

        /// <summary>
        /// Creates a code block node.
        /// </summary>
        public static MarkdownNode CodeBlock(string content, string? language = null) => new MarkdownNode
        {
            NodeType = MarkdownNodeType.CodeBlock,
            Content = content,
            Language = language
        };

        /// <summary>
        /// Creates a bold text node.
        /// </summary>
        public static MarkdownNode Bold(string content) => new MarkdownNode
        {
            NodeType = MarkdownNodeType.Bold,
            Content = content
        };

        /// <summary>
        /// Creates an italic text node.
        /// </summary>
        public static MarkdownNode Italic(string content) => new MarkdownNode
        {
            NodeType = MarkdownNodeType.Italic,
            Content = content
        };

        /// <summary>
        /// Creates a link node.
        /// </summary>
        public static MarkdownNode Link(string text, string url) => new MarkdownNode
        {
            NodeType = MarkdownNodeType.Link,
            Content = url,
            Children = new List<MarkdownNode> { Text(text) }
        };
    }

    /// <summary>
    /// Enumerates supported markdown node types.
    /// </summary>
    public enum MarkdownNodeType
    {
        /// <summary>Plain text content.</summary>
        Text,

        /// <summary>Code block (language-specific syntax highlighting eligible).</summary>
        CodeBlock,

        /// <summary>Bold/strong emphasis.</summary>
        Bold,

        /// <summary>Italic emphasis.</summary>
        Italic,

        /// <summary>Hyperlink.</summary>
        Link,

        /// <summary>Container (para, list, etc.).</summary>
        Container
    }
}
