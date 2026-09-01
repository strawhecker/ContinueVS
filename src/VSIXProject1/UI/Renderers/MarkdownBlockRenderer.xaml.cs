using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ContinueVS.Services;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigBlock = Markdig.Syntax.Block;

namespace ContinueVS.UI.Renderers
{
    /// <summary>
    /// WPF UserControl for rendering markdown text.
    /// Accepts a plain string Content, parses it with Markdig synchronously,
    /// and builds the visual tree imperatively inside RootPanel.
    /// </summary>
    public partial class MarkdownBlockRenderer : UserControl
    {
        private static readonly MarkdownPipeline _pipeline =
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public MarkdownBlockRenderer()
        {
            InitializeComponent();
        }

        /// <summary>
        /// String content to parse and render as markdown.
        /// </summary>
        public new static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(
                "Content",
                typeof(string),
                typeof(MarkdownBlockRenderer),
                new PropertyMetadata(null, (d, e) => ((MarkdownBlockRenderer)d).OnContentChanged(e.NewValue as string)));

        public new string? Content
        {
            get => (string?)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        private void OnContentChanged(string? text)
        {
            RootPanel.Children.Clear();

            if (string.IsNullOrEmpty(text))
                return;

            // Non-nullable after the IsNullOrEmpty guard above
            string nonNullText = text!;

            try
            {
                var doc = Markdown.Parse(nonNullText, _pipeline);
                foreach (var block in doc)
                    RenderBlock(block);
            }
            catch
            {
                // Fallback: plain selectable text
                RootPanel.Children.Add(MakeSelectableTextBox(nonNullText));
            }
        }

        private void RenderBlock(MarkdigBlock block)
        {
            switch (block)
            {
                case FencedCodeBlock code:
                    RenderCodeBlock(code);
                    break;

                case ParagraphBlock para:
                    var rtb = MakeSelectableRichTextBox(para.Inline);
                    rtb.Margin = new Thickness(0, 2, 0, 2);
                    RootPanel.Children.Add(rtb);
                    break;

                case HeadingBlock heading:
                    var hrtb = MakeSelectableRichTextBox(heading.Inline);
                    hrtb.FontWeight = FontWeights.Bold;
                    hrtb.FontSize = heading.Level <= 3 ? 20 - heading.Level * 2 : 14;
                    hrtb.Margin = new Thickness(0, 4, 0, 2);
                    RootPanel.Children.Add(hrtb);
                    break;

                case ListBlock list:
                    RenderList(list, 0);
                    break;

                case QuoteBlock quote:
                    RenderQuote(quote);
                    break;

                case ThematicBreakBlock _:
                    var separator = new Separator { Margin = new Thickness(0, 4, 0, 4) };
                    separator.SetResourceReference(Separator.BackgroundProperty, "VsBrush.ToolWindowBorder");
                    RootPanel.Children.Add(separator);
                    break;

                case CodeBlock indentedCode:
                    // Indented (non-fenced) code block
                    RootPanel.Children.Add(new TextBox
                    {
                        Text = indentedCode.Lines.ToString(),
                        FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                        FontSize = 12,
                        TextWrapping = TextWrapping.NoWrap,
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 4, 0, 4),
                        Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                        BorderThickness = new Thickness(0),
                        IsReadOnly = true,
                        IsTabStop = false,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Cursor = System.Windows.Input.Cursors.IBeam
                    });
                    break;

                default:
                    // Silently skip unknown block types (don't render class name)
                    break;
            }
        }

        private void RenderList(ListBlock list, int depth)
        {
            int orderedIndex = 1;
            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    var itemGrid = new Grid
                    {
                        Margin = new Thickness(depth * 16, 1, 0, 1)
                    };
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var bullet = new TextBlock
                    {
                        Text = list.IsOrdered ? $"{orderedIndex++}." : "•",
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    bullet.SetResourceReference(TextBlock.ForegroundProperty, "VsBrush.WindowText");
                    Grid.SetColumn(bullet, 0);
                    itemGrid.Children.Add(bullet);

                    var contentPanel = new StackPanel { Orientation = Orientation.Vertical };
                    Grid.SetColumn(contentPanel, 1);
                    foreach (var subBlock in listItem)
                    {
                        if (subBlock is ParagraphBlock para)
                        {
                            contentPanel.Children.Add(MakeSelectableRichTextBox(para.Inline));
                        }
                        else if (subBlock is ListBlock nestedList)
                        {
                            var nestedPanel = new StackPanel();
                            RenderListInto(nestedList, depth + 1, nestedPanel);
                            contentPanel.Children.Add(nestedPanel);
                        }
                    }
                    itemGrid.Children.Add(contentPanel);
                    RootPanel.Children.Add(itemGrid);
                }
            }
        }

        private static void RenderListInto(ListBlock list, int depth, StackPanel target)
        {
            int orderedIndex = 1;
            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    var itemGrid = new Grid
                    {
                        Margin = new Thickness(depth * 16, 1, 0, 1)
                    };
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var bullet = new TextBlock
                    {
                        Text = list.IsOrdered ? $"{orderedIndex++}." : "•",
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    bullet.SetResourceReference(TextBlock.ForegroundProperty, "VsBrush.WindowText");
                    Grid.SetColumn(bullet, 0);
                    itemGrid.Children.Add(bullet);

                    var contentPanel = new StackPanel { Orientation = Orientation.Vertical };
                    Grid.SetColumn(contentPanel, 1);
                    foreach (var subBlock in listItem)
                    {
                        if (subBlock is ParagraphBlock para)
                        {
                            contentPanel.Children.Add(MakeSelectableRichTextBox(para.Inline));
                        }
                    }
                    itemGrid.Children.Add(contentPanel);
                    target.Children.Add(itemGrid);
                }
            }
        }

        private void RenderQuote(QuoteBlock quote)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(8, 2, 0, 2)
            };
            var inner = new StackPanel();
            foreach (var subBlock in quote)
                if (subBlock is ParagraphBlock para)
                {
                    var qrtb = MakeSelectableRichTextBox(para.Inline);
                    qrtb.FontStyle = FontStyles.Italic;
                    inner.Children.Add(qrtb);
                }
            border.Child = inner;
            RootPanel.Children.Add(border);
        }

        private void RenderCodeBlock(FencedCodeBlock code)
        {
            var language = code.Info ?? string.Empty;
            var lines = code.Lines.ToString();
            var blockId = Guid.NewGuid().ToString();

            var outerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 4, 0, 4)
            };

            var innerPanel = new StackPanel();

            // Header bar: language label + Copy/Apply dropdown (gap53)
            var header = new DockPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                LastChildFill = false
            };

            var langLabel = new TextBlock
            {
                Text = string.IsNullOrEmpty(language) ? "code" : language,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize = 11,
                Margin = new Thickness(8, 4, 0, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(langLabel, Dock.Left);

            // Gap53: Replace single Copy button with Copy/Apply dropdown
            var actionDropdown = new ComboBox
            {
                Height = 24,
                Width = 100,
                Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                SelectedIndex = 0,
                Margin = new Thickness(0, 2, 4, 2),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Store block metadata in tag as simple string-keyed dictionary-like format
            actionDropdown.Tag = blockId;
            // Store additional data as separate attributes to avoid dynamic issues
            actionDropdown.Name = $"CodeActionDropdown_{blockId}";

            var copyItem = new ComboBoxItem { Content = "📋 Copy", IsSelected = true };
            var applyItem = new ComboBoxItem { Content = "✔ Apply" };
            actionDropdown.Items.Add(copyItem);
            actionDropdown.Items.Add(applyItem);

            actionDropdown.SelectionChanged += (s, e) =>
            {
                if (s is ComboBox dropdown && dropdown.Tag is string bid)
                {
                    CodeBlockActionDropdown_SelectionChanged(dropdown, bid, language, lines);
                }
            };

            DockPanel.SetDock(actionDropdown, Dock.Right);

            header.Children.Add(langLabel);
            header.Children.Add(actionDropdown);
            innerPanel.Children.Add(header);

            var codeText = new TextBox
            {
                Text = lines,
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                TextWrapping = TextWrapping.NoWrap,
                Padding = new Thickness(8),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                IsTabStop = false,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Cursor = System.Windows.Input.Cursors.IBeam
            };
            innerPanel.Children.Add(codeText);

            outerBorder.Child = innerPanel;
            RootPanel.Children.Add(outerBorder);
        }

        /// <summary>
        /// Handles Copy/Apply dropdown selection change per code block (gap53).
        /// </summary>
        private void CodeBlockActionDropdown_SelectionChanged(ComboBox comboBox, string blockId, string language, string content)
        {
            if (comboBox == null)
                return;

            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
                return;

            try
            {
                string selectedAction = selectedItem.Content?.ToString() ?? "Copy";

                if (selectedAction.Contains("Copy"))
                {
                    try
                    {
                        Clipboard.SetText(content);
                        _ = LoggerService.Current.WriteDebugAsync($"[gap53-block-action] Code block copied (lang={language}, id={blockId})");
                    }
                    catch (Exception ex)
                    {
                        _ = LoggerService.Current.WriteErrorAsync($"[gap53-block-action-error] Failed to copy block: {ex.Message}", ex);
                    }
                }
                else if (selectedAction.Contains("Apply"))
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[gap53-block-action] Apply selected for block (lang={language}, id={blockId})");
                    // ApplyCodeBlock will be wired via command through parent ChatMessageControl
                }

                // Reset dropdown to Copy
                comboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[gap53-block-action-handler-error] Exception in handler: {ex.Message}", ex);
            }
        }

        private static void AppendInline(InlineCollection inlines, Markdig.Syntax.Inlines.Inline inline)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    inlines.Add(new Run(lit.Content.ToString()));
                    break;

                case EmphasisInline em:
                    Span span = em.DelimiterCount >= 2 ? (Span)new Bold() : new Italic();
                    foreach (var child in em)
                        AppendInline(span.Inlines, child);
                    inlines.Add(span);
                    break;

                case CodeInline codeInline:
                    inlines.Add(new Run(codeInline.Content)
                    {
                        FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                        Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                    });
                    break;

                case LineBreakInline:
                    inlines.Add(new LineBreak());
                    break;

                default:
                    var text = inline.ToString();
                    if (!string.IsNullOrEmpty(text))
                        inlines.Add(new Run(text));
                    break;
            }
        }

        /// <summary>
        /// Creates a selectable, read-only RichTextBox pre-populated with inline content.
        /// Supports bold, italic, code spans, and plain text.
        /// </summary>
        private static RichTextBox MakeSelectableRichTextBox(ContainerInline? inlines)
        {
            var para = new Paragraph { Margin = new Thickness(0) };
            if (inlines != null)
                foreach (var inline in inlines)
                    AppendInline(para.Inlines, inline);

            var doc = new FlowDocument(para)
            {
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left,
                // Prevent FlowDocument's default 200px column layout which collapses
                // text to one character per line. We track actual width to allow wrapping.
                PageWidth = 9999
            };

            var rtb = new RichTextBox(doc)
            {
                IsReadOnly = true,
                IsTabStop = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                IsDocumentEnabled = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Cursor = System.Windows.Input.Cursors.IBeam
            };
            rtb.SetResourceReference(RichTextBox.ForegroundProperty, "VsBrush.WindowText");
            // Keep PageWidth in sync with actual width so text wraps correctly
            rtb.SizeChanged += (s, e) =>
            {
                if (e.NewSize.Width > 0)
                    rtb.Document.PageWidth = e.NewSize.Width;
            };
            return rtb;
        }

        private static TextBox MakeSelectableTextBox(string text)
        {
            var tb = new TextBox
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2),
                IsReadOnly = true,
                IsTabStop = false,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.IBeam
            };
            tb.SetResourceReference(TextBox.ForegroundProperty, "VsBrush.WindowText");
            return tb;
        }
    }
}
