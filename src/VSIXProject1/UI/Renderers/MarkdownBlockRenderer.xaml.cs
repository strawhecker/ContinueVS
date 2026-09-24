using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
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
    /// and renders all prose into ONE shared FlowDocument hosted inside a
    /// read-only FlowDocumentScrollViewer so that multi-line selection + copy
    /// works continuously across the whole response, and the scroll wheel
    /// bubbles correctly to the parent conversation ScrollViewer. Code blocks
    /// (with their gap53 Copy/Apply dropdown) are embedded via
    /// BlockUIContainer to preserve visual ordering.
    /// Implements debounced rendering during streaming to handle partial/incomplete markdown gracefully.
    /// </summary>
    public partial class MarkdownBlockRenderer : UserControl
    {
        private static readonly MarkdownPipeline _pipeline =
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        /// <summary>
        /// Monospace font fallback chain using ONLY real, installed font names.
        /// (WPF cannot resolve the CSS generic "monospace", which throws
        /// ArgumentException; keep this list to actual fonts.)
        /// </summary>
        private static readonly FontFamily MonospaceFont =
            new FontFamily("Consolas, Courier New, Lucida Console");

        /// <summary>
        /// Timer for debouncing markdown rendering during streaming.
        /// Delays parsing to allow more content to arrive, preventing failures on incomplete markdown.
        /// </summary>
        private DispatcherTimer? _renderDebounceTimer;

        /// <summary>
        /// Pending content to render after debounce delay.
        /// </summary>
        private string? _pendingRenderContent;

        /// <summary>
        /// Debounce delay in milliseconds. Allows incomplete markdown (e.g., unclosed code fences) to complete.
        /// </summary>
        private const int RenderDebounceMs = 100;

        /// <summary>
        /// The single shared document that hosts ALL prose blocks so that
        /// selection/copy spans multiple lines and paragraphs continuously.
        /// </summary>
        private readonly FlowDocument _document;

        public MarkdownBlockRenderer()
        {
            InitializeComponent();
            // Force width constraint to propagate for text wrapping
            this.MinWidth = 0;

            _document = new FlowDocument
            {
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left,
                // FlowDocumentScrollViewer auto-fits column width to its host,
                // so no PageWidth size-sync hack is needed here.
                PageWidth = double.NaN
            };

            RootDocumentViewer.Document = _document;
            RootDocumentViewer.SetResourceReference(
                FlowDocumentScrollViewer.ForegroundProperty, "VsBrush.WindowText");
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

        /// <summary>
        /// Dependency property for MaxMessageWidth, bound from parent ViewModel.
        /// </summary>
        public static readonly DependencyProperty MaxMessageWidthProperty =
            DependencyProperty.Register(
                "MaxMessageWidth",
                typeof(double),
                typeof(MarkdownBlockRenderer),
                new PropertyMetadata(600.0, (d, e) => ((MarkdownBlockRenderer)d).OnMaxMessageWidthChanged((double)e.NewValue)));

        public double MaxMessageWidth
        {
            get => (double)GetValue(MaxMessageWidthProperty);
            set => SetValue(MaxMessageWidthProperty, value);
        }

        private void OnMaxMessageWidthChanged(double newWidth)
        {
            // Apply the width constraint to the document viewer so text can wrap
            if (RootDocumentViewer != null && newWidth > 0)
            {
                RootDocumentViewer.MaxWidth = newWidth;
            }
        }

        private void OnContentChanged(string? text)
        {
            _pendingRenderContent = text;

            // If timer already running, it will render the latest content when it fires
            if (_renderDebounceTimer != null && _renderDebounceTimer.IsEnabled)
            {
                // Timer will use the newly updated _pendingRenderContent
                return;
            }

            // Only debounce if we have incomplete markdown (unclosed code fences, etc.)
            // Complete markdown renders immediately for responsiveness
            if (!string.IsNullOrEmpty(text) && IsIncompleteMarkdown(text))
            {
                // Start or restart the debounce timer
                _renderDebounceTimer ??= new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(RenderDebounceMs)
                };

                _renderDebounceTimer.Tick -= RenderDebounceTimer_Tick;
                _renderDebounceTimer.Tick += RenderDebounceTimer_Tick;
                _renderDebounceTimer.Start();
            }
            else
            {
                // No incomplete markdown, render immediately
                _renderDebounceTimer?.Stop();
                RenderDebounceTimer_Tick(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Detects if markdown is incomplete (e.g., unclosed code fences).
        /// This allows us to debounce only when necessary, rendering complete content immediately.
        /// </summary>
        private static bool IsIncompleteMarkdown(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // Count fence markers (```)
            string nonNullText = text!;
            int fenceCount = (nonNullText.Length - nonNullText.Replace("```", "").Length) / 3;

            // Odd number of fences means unclosed fence
            if (fenceCount % 2 != 0)
                return true;

            return false;
        }

        /// <summary>
        /// Debounce timer tick handler: performs the actual markdown rendering.
        /// Called after a delay to allow streaming content to stabilize.
        /// Rebuilds only the shared document; the host FlowDocumentScrollViewer stays put.
        /// </summary>
        private void RenderDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _renderDebounceTimer?.Stop();

            _document.Blocks.Clear();

            if (string.IsNullOrEmpty(_pendingRenderContent))
                return;

            string nonNullText = _pendingRenderContent!;

            try
            {
                var doc = Markdown.Parse(nonNullText, _pipeline);
                foreach (var block in doc)
                    RenderBlock(block);
            }
            catch
            {
                // Fallback: plain selectable paragraph
                var fallback = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                fallback.Inlines.Add(new Run(nonNullText));
                _document.Blocks.Add(fallback);
            }
        }

        private void RenderBlock(MarkdigBlock block)
        {
            switch (block)
            {
                case FencedCodeBlock code:
                    _document.Blocks.Add(new BlockUIContainer(
                        RenderCodeBlock(code.Info ?? string.Empty, ExtractCodeLines(code))));
                    break;

                case ParagraphBlock para:
                    var p = MakeTextParagraph(para.Inline);
                    _document.Blocks.Add(p);
                    break;

                case HeadingBlock heading:
                    var h = MakeTextParagraph(
                        heading.Inline,
                        fontSize: heading.Level <= 3 ? 20 - heading.Level * 2 : 14,
                        weight: FontWeights.Bold,
                        margin: new Thickness(0, 4, 0, 2));
                    _document.Blocks.Add(h);
                    break;

                case ListBlock list:
                    RenderList(list, 0);
                    break;

                case QuoteBlock quote:
                    _document.Blocks.Add(RenderQuote(quote));
                    break;

                case ThematicBreakBlock _:
                    _document.Blocks.Add(MakeHorizontalRule());
                    break;

                case CodeBlock indentedCode:
                    _document.Blocks.Add(new BlockUIContainer(
                        BuildPlainCodeControl(ExtractCodeLines(indentedCode))));
                    break;

                default:
                    // Silently skip unknown block types (don't render class name)
                    break;
            }
        }

        private void RenderList(ListBlock list, int depth)
        {
            int orderedIndex = 1;
            double left = 14.0 + depth * 16;

            foreach (var item in list)
            {
                if (item is not ListItemBlock listItem)
                    continue;

                string prefix = list.IsOrdered ? $"{orderedIndex++}. " : "• ";
                bool markerAdded = false;

                foreach (var subBlock in listItem)
                {
                    if (subBlock is ParagraphBlock paraBlock)
                    {
                        var para = MakeTextParagraph(null, margin: new Thickness(left, 1, 0, 1));
                        if (!markerAdded)
                        {
                            para.Inlines.Add(new Run(prefix));
                            markerAdded = true;
                        }
                        if (paraBlock.Inline != null)
                        {
                            foreach (var inline in paraBlock.Inline)
                                AppendInline(para.Inlines, inline);
                        }
                        _document.Blocks.Add(para);
                    }
                    else if (subBlock is ListBlock nestedList)
                    {
                        RenderList(nestedList, depth + 1);
                    }
                    else if (subBlock is QuoteBlock nestedQuote)
                    {
                        _document.Blocks.Add(RenderQuote(nestedQuote));
                    }
                }
            }
        }

        private Section RenderQuote(QuoteBlock quote)
        {
            var section = new Section
            {
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(10, 2, 0, 2),
                BorderThickness = new Thickness(3, 0, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };

            foreach (var subBlock in quote)
            {
                if (subBlock is ParagraphBlock para)
                {
                    var p = MakeTextParagraph(para.Inline);
                    p.FontStyle = FontStyles.Italic;
                    section.Blocks.Add(p);
                }
                else if (subBlock is ListBlock list)
                {
                    RenderList(list, 0);
                }
            }

            return section;
        }

        private Paragraph MakeHorizontalRule()
        {
            var p = new Paragraph
            {
                Margin = new Thickness(0, 4, 0, 4),
                FontSize = 1
            };
            p.BorderThickness = new Thickness(0, 0, 0, 1);
            p.BorderBrush = TryGetBrush("VsBrush.ToolWindowBorder")
                            ?? new SolidColorBrush(Color.FromRgb(80, 80, 80));
            return p;
        }

        private Paragraph MakeTextParagraph(
            ContainerInline? inlines,
            double? fontSize = null,
            FontWeight? weight = null,
            Thickness? margin = null)
        {
            var para = new Paragraph
            {
                Margin = margin ?? new Thickness(0, 2, 0, 2)
            };

            if (fontSize.HasValue) para.FontSize = fontSize.Value;
            if (weight.HasValue) para.FontWeight = weight.Value;

            if (inlines != null)
            {
                foreach (var inline in inlines)
                    AppendInline(para.Inlines, inline);
            }

            return para;
        }

        private static string ExtractCodeLines(LeafBlock? code)
        {
            var lines = new List<string>();
            if (code?.Lines.Lines != null)
            {
                foreach (var line in code.Lines.Lines)
                {
                    if (line.Slice.Text != null)
                    {
                        lines.Add(line.Slice.ToString());
                    }
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        private Border RenderCodeBlock(string language, string lines)
        {
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

            // Store block metadata in tag (blockId is used to identify which code block this dropdown belongs to)
            // Do NOT set Name property - WPF Name validation rejects GUIDs with dashes
            actionDropdown.Tag = blockId;

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
                FontFamily = MonospaceFont,
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
            return outerBorder;
        }

        private TextBox BuildPlainCodeControl(string lines)
        {
            return new TextBox
            {
                Text = lines,
                FontFamily = MonospaceFont,
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
            };
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
                        LoggerService.Current.WriteDebug($"[gap53-block-action] Code block copied (lang={language}, id={blockId})");
                    }
                    catch (Exception ex)
                    {
                        LoggerService.Current.WriteError($"[gap53-block-action-error] Failed to copy block: {ex.Message}", ex);
                    }
                }
                else if (selectedAction.Contains("Apply"))
                {
                    LoggerService.Current.WriteDebug($"[gap53-block-action] Apply selected for block (lang={language}, id={blockId})");
                    // ApplyCodeBlock will be wired via command through parent ChatMessageControl
                }

                // Reset dropdown to Copy
                comboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap53-block-action-handler-error] Exception in handler: {ex.Message}", ex);
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
                        FontFamily = MonospaceFont,
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

        private static Brush? TryGetBrush(string resourceKey)
        {
            try
            {
                if (Application.Current != null &&
                    Application.Current.TryFindResource(resourceKey) is Brush brush)
                {
                    return brush;
                }
            }
            catch
            {
                // ignore resource lookup failures
            }
            return null;
        }
    }
}
