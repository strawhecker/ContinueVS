using System;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// gap89: Converts a single-source raw markdown string into an RTF document for the
    /// pretty (rich) clipboard format. Runs the Markdig pipeline then walks the AST to a
    /// minimal RTF writer.
    ///
    /// The model stays single-sourced (gap88): the exact same <c>ChatMessage.Content</c>
    /// that backs the processed render also feeds this exporter. Formatting is a rendering
    /// concern only — no content is copied or duplicated between representations.
    ///
    /// Implementation notes (net472, no third-party RTF library):
    ///   - Only the run-level formatting the renderer itself honors (bold, italic, code,
    ///     headings) is emitted into RTF. Tables/lists degrade to their text content so the
    ///     pasted document is still complete, just not table-structured.
    ///   - Documents are UTF-16 (\uN) escaped so non-ASCII (e.g. emoji, code symbols) round-trips.
    ///   - A header/footer preamble wraps the body so RichTextBox/Word/WordPad can parse it.
    /// </summary>
    public static class RtfExporter
    {
        private static readonly MarkdownPipeline _pipeline =
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()  // GFM: Tables, Grid tables, Task lists, Strikethrough
                .UseEmojiAndSmiley()      // :smile: → emoji
                .UseMathematics()         // $$LaTeX$$ math
                .DisableHtml()            // security: block LLM HTML injection
                .Build();

        /// <summary>
        /// Converts raw markdown to a standalone RTF document.
        /// </summary>
        /// <param name="markdown">The raw single-source markdown content. Null/empty returns an empty RTF document.</param>
        /// <returns>A complete <c>\rtf1</c> document string.</returns>
        public static string ToRtf(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return BuildDocument(string.Empty);

            var body = new StringBuilder();
            try
            {
                var doc = Markdown.Parse(markdown!, _pipeline);
                foreach (var block in doc)
                    AppendBlock(body, block);
            }
            catch
            {
                // Fallback: emit the raw text as a single paragraph so copy never fails.
                body.Append("\\par ").Append(EscapeRtf(markdown!)).Append("\\par ");
            }

            return BuildDocument(body.ToString());
        }

        private static string BuildDocument(string body)
        {
            // Minimal RTF header: \rtf1 (format), Unicode-encoded codepage via \uc1,
            // a default font size (20 half-points = 10pt? keep readable at 24 = 12pt).
            return "{\\rtf1\\ansi\\deff0{\\fonttbl{\\f0 Consolas;}{\\f1 Calibri;}}" +
                   "{\\colortbl;\\red0\\green0\\blue0;\\red200\\green200\\blue200;}" +
                   "\\uc1\\f1\\fs24 " + body + "}";
        }

        private static void AppendBlock(StringBuilder sb, Markdig.Syntax.Block block)
        {
            switch (block)
            {
                case FencedCodeBlock code:
                    // Code block: monospaced, no further interpretation. Emit each line.
                    sb.Append("\\qc\\f0\\fs22\\cf2 ");
                    foreach (var line in code.Lines.Lines)
                    {
                        var text = line.Slice.Text;
                        if (text != null)
                            sb.Append(EscapeRtf(line.Slice.ToString())).Append("\\line ");
                    }
                    sb.Append("\\cf1\\f1\\par ");
                    break;

                case ParagraphBlock para:
                    sb.Append("\\par ");
                    AppendInline(sb, para.Inline);
                    sb.Append("\\par ");
                    break;

                case HeadingBlock heading:
                    sb.Append("\\par \\b\\fs").Append(heading.Level <= 2 ? 32 : 28).Append(' ');
                    AppendInline(sb, heading.Inline);
                    sb.Append("\\b0\\fs24\\par ");
                    break;

                case QuoteBlock quote:
                    sb.Append("\\par\\i ");
                    foreach (var sub in quote)
                        AppendBlock(sb, sub);
                    sb.Append("\\i0\\par ");
                    break;

                case ListBlock list:
                    AppendListItems(sb, list, 0);
                    break;

                default:
                    // Thematic breaks, code blocks (indented) and unknown blocks:
                    // degrade gracefully to their literal text.
                    if (block is LeafBlock leaf && leaf.Inline != null)
                    {
                        sb.Append("\\par ");
                        AppendInline(sb, leaf.Inline);
                        sb.Append("\\par ");
                    }
                    break;
            }
        }

        private static void AppendListItems(StringBuilder sb, ListBlock list, int depth)
        {
            int ordered = 1;
            foreach (var item in list)
            {
                if (item is not ListItemBlock listItem)
                    continue;

                string prefix = list.IsOrdered ? (ordered++ + ". ") : "• ";
                sb.Append("\\par \\li").Append(depth * 240).Append(' ');
                sb.Append(EscapeRtf(prefix));

                foreach (var sub in listItem)
                    AppendBlock(sb, sub);
            }
        }

        private static void AppendInline(StringBuilder sb, ContainerInline? inlines)
        {
            if (inlines == null)
                return;

            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case LiteralInline lit:
                        sb.Append(EscapeRtf(lit.Content.ToString()));
                        break;
                    case CodeInline code:
                        // Inline code: monospaced backslash/brace-free run.
                        sb.Append("\\f0\\fs22 ").Append(EscapeRtf(code.Content)).Append("\\f1\\fs24 ");
                        break;
                    case EmphasisInline em:
                        bool bold = em.DelimiterCount >= 2;
                        sb.Append(bold ? "\\b " : "\\i ");
                        AppendInline(sb, em);
                        sb.Append(bold ? "\\b0 " : "\\i0 ");
                        break;
                    case LineBreakInline:
                        sb.Append("\\line ");
                        break;
                    default:
                        var text = inline.ToString();
                        if (!string.IsNullOrEmpty(text))
                            sb.Append(EscapeRtf(text));
                        break;
                }
            }
        }

        /// <summary>
        /// Escapes RTF control characters and non-ASCII (high) chars as \uN. Backslashes,
        /// braces, and the control chars must be escaped to keep the document valid.
        /// </summary>
        private static string EscapeRtf(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '{': sb.Append("\\{"); break;
                    case '}': sb.Append("\\}"); break;
                    default:
                        if (ch < 32)
                        {
                            // control segment: emit a \'XX hex escape for 7-bit ASCII controls.
                            if (ch == '\n' || ch == '\r')
                                sb.Append("\\line ");
                            else
                                sb.Append("\\'").Append(((int)ch).ToString("X2"));
                        }
                        else if (ch > 127)
                        {
                            // Unicode RTF escape. \uc1 means 1 fallback char per escape.
                            sb.Append("\\u").Append((short)ch).Append('?');
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
