using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ContinueVS.Services
{
    /// <summary>
    /// Implementation of markdown parsing and rendering service.
    /// Uses Markdig for robust markdown AST generation and language detection.
    /// </summary>
    public class MarkdownService : IMarkdownService
    {
        private readonly MarkdownPipeline _pipeline;

        public MarkdownService()
        {
            // Configure Markdig pipeline with standard markdown + extra features
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        /// <summary>
        /// Parses markdown content asynchronously into a MarkdownNode tree.
        /// Handles code blocks with language detection, bold, italic, links, and plain text.
        /// </summary>
        public async Task<MarkdownNode> ParseMarkdownAsync(string content)
        {
            return await Task.Run(() => ParseMarkdown(content));
        }

        /// <summary>
        /// Synchronous markdown parsing (runs on background thread via ParseMarkdownAsync).
        /// </summary>
        private MarkdownNode ParseMarkdown(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new MarkdownNode { NodeType = MarkdownNodeType.Container, Children = new List<MarkdownNode>() };
            }

            try
            {
                var markdownDocument = Markdown.Parse(content, _pipeline);
                return ParseDocument(markdownDocument);
            }
            catch (Exception ex)
            {
                throw new MarkdownParsingException($"Failed to parse markdown content: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts Markdig AST to MarkdownNode tree.
        /// </summary>
        private MarkdownNode ParseDocument(MarkdownDocument document)
        {
            var container = new MarkdownNode
            {
                NodeType = MarkdownNodeType.Container,
                Children = new List<MarkdownNode>()
            };

            foreach (var block in document)
            {
                var node = ParseBlock(block);
                if (node != null)
                {
                    container.Children.Add(node);
                }
            }

            return container;
        }

        /// <summary>
        /// Parses a Markdig block into a MarkdownNode.
        /// Handles code blocks, paragraphs, headings, lists, etc.
        /// </summary>
        private MarkdownNode? ParseBlock(Block block)
        {
            switch (block)
            {
                case FencedCodeBlock codeBlock:
                    return ParseCodeBlock(codeBlock);

                case ParagraphBlock para:
                    return ParseParagraph(para);

                case HeadingBlock heading:
                    return ParseHeading(heading);

                case ListBlock list:
                    return ParseList(list);

                case QuoteBlock quote:
                    return ParseQuote(quote);

                default:
                    // For unsupported block types, extract any inline content
                    if (block is ContainerBlock container)
                    {
                        var nodes = new List<MarkdownNode>();
                        foreach (var child in container)
                        {
                            var childNode = ParseBlock(child);
                            if (childNode != null)
                            {
                                nodes.Add(childNode);
                            }
                        }
                        return nodes.Count > 0 ? new MarkdownNode { NodeType = MarkdownNodeType.Container, Children = nodes } : null;
                    }
                    return null;
            }
        }

        /// <summary>
        /// Parses a fenced code block and extracts language.
        /// </summary>
        private MarkdownNode ParseCodeBlock(FencedCodeBlock codeBlock)
        {
            var language = ExtractLanguageFromFence(codeBlock.Info ?? string.Empty);

            // Markdig FencedCodeBlock stores content in trailing and the inner text
            // Simply use the block's ToString() representation
            var content = codeBlock.ToString() ?? string.Empty;

            return MarkdownNode.CodeBlock(content, language);
        }

        /// <summary>
        /// Parses a paragraph block with inline content (bold, italic, links, text).
        /// </summary>
        private MarkdownNode ParseParagraph(ParagraphBlock para)
        {
            var nodes = new List<MarkdownNode>();
            if (para?.Inline != null)
            {
                foreach (var inline in para.Inline)
                {
                    var node = ParseInline(inline);
                    if (node != null)
                    {
                        nodes.Add(node);
                    }
                }
            }

            return nodes.Count == 1 ? nodes[0] : new MarkdownNode { NodeType = MarkdownNodeType.Container, Children = nodes };
        }

        /// <summary>
        /// Parses a heading block.
        /// </summary>
        private MarkdownNode ParseHeading(HeadingBlock heading)
        {
            var nodes = new List<MarkdownNode>();
            if (heading?.Inline != null)
            {
                foreach (var inline in heading.Inline)
                {
                    var node = ParseInline(inline);
                    if (node != null)
                    {
                        nodes.Add(node);
                    }
                }
            }

            return new MarkdownNode
            {
                NodeType = MarkdownNodeType.Bold,
                Content = string.Join("", nodes.Select(n => n.Content)),
                Children = nodes
            };
        }

        /// <summary>
        /// Parses a list block (bullet or ordered).
        /// </summary>
        private MarkdownNode ParseList(ListBlock list)
        {
            var nodes = new List<MarkdownNode>();
            foreach (var item in list.AsEnumerable().OfType<ListItemBlock>())
            {
                var itemNodes = new List<MarkdownNode>();
                foreach (var block in item)
                {
                    var node = ParseBlock(block);
                    if (node != null)
                    {
                        itemNodes.Add(node);
                    }
                }
                nodes.AddRange(itemNodes);
            }

            return new MarkdownNode { NodeType = MarkdownNodeType.Container, Children = nodes };
        }

        /// <summary>
        /// Parses a block quote.
        /// </summary>
        private MarkdownNode ParseQuote(QuoteBlock quote)
        {
            var nodes = new List<MarkdownNode>();
            foreach (var block in quote)
            {
                var node = ParseBlock(block);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            return new MarkdownNode { NodeType = MarkdownNodeType.Container, Children = nodes };
        }

        /// <summary>
        /// Parses inline content (text, bold, italic, links, etc.).
        /// </summary>
        private MarkdownNode? ParseInline(Inline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    return MarkdownNode.Text(literal.Content.ToString());

                case EmphasisInline emph:
                    {
                        var text = string.Join("", emph.AsEnumerable().OfType<LiteralInline>().Select(l => l.Content.ToString()));
                        return emph.DelimiterCount == 2 ? MarkdownNode.Bold(text) : MarkdownNode.Italic(text);
                    }

                case LinkInline link:
                    {
                        var linkText = string.Join("", link.AsEnumerable().OfType<LiteralInline>().Select(l => l.Content.ToString()));
                        return MarkdownNode.Link(linkText, link.Url ?? string.Empty);
                    }

                case CodeInline code:
                    return MarkdownNode.CodeBlock(code.Content.ToString(), "inline");

                default:
                    // For unknown inline types, try to extract text
                    if (inline is ContainerInline container)
                    {
                        var text = string.Join("", container.AsEnumerable().OfType<LiteralInline>().Select(l => l.Content.ToString()));
                        return !string.IsNullOrEmpty(text) ? MarkdownNode.Text(text) : null;
                    }
                    return null;
            }
        }

        /// <summary>
        /// Extracts language identifier from a markdown code fence.
        /// E.g., "```csharp" -> "csharp", "``` python" -> "python"
        /// </summary>
        public string ExtractLanguageFromFence(string fence)
        {
            if (string.IsNullOrWhiteSpace(fence))
            {
                return string.Empty;
            }

            // Remove leading backticks and whitespace
            var trimmed = fence.TrimStart('`').Trim();

            // Extract first word (language identifier)
            var match = Regex.Match(trimmed, @"^(\w+)");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty;
        }
    }
}
