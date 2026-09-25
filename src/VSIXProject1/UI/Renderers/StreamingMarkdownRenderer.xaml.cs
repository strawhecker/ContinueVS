using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ContinueVS.Core.Parsers;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigBlock = Markdig.Syntax.Block;

namespace ContinueVS.UI.Renderers
{
    /// <summary>
    /// gap88: Unified chat card renderer based on content kind and phase.
    ///
    /// One control owns a card from its first token to its final render, with a
    /// single hosting RichTextBox/FlowDocument reused across all modes. Rendering
    /// behavior is selected per card:
    ///
    ///   - <see cref="MarkdownMode"/> = content kind (guaranteed markdown vs verbatim)
    ///   - streaming vs finalized (driven by <see cref="ChatMessage.IsFinalized"/>)
    ///
    /// Modes:
    ///   - Verbatim (user | tool): plain, whitespace-preserving, no markdown ever
    ///     invokes the Markdig pipeline, so md-shaped source/paste cannot inject
    ///     fake code blocks/headings.
    ///   - Streaming (reasoning | response while !IsFinalized): incremental,
    ///     append-only, O(n); no full reparse.
    ///   - Full Markdig (reasoning | response at IsFinalized): the SINGLE clear-and-
    ///     rebuild once with the full pipeline + gap53 code-block chrome.
    ///
    /// Completion is driven by <see cref="ChatMessage.IsFinalized"/> (a separate
    /// event from Content), so the full render happens exactly once, at finalize.
    /// The model stays single-sourced; no content is ever copied between views.
    /// </summary>
    public partial class StreamingMarkdownRenderer : UserControl
    {
        private static readonly MarkdownPipeline _pipeline =
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()  // GFM: Tables, Grid tables, Task lists, Strikethrough
                .UseEmojiAndSmiley()      // :smile: → emoji
                .UseMathematics()         // $$LaTeX$$ math
                .DisableHtml()            // security: block LLM HTML injection
                .Build();

        /// <summary>
        /// Monospace font fallback chain using ONLY real, installed font names.
        /// (WPF cannot resolve the CSS generic "monospace", which throws
        /// ArgumentException; keep this list to actual fonts.)
        /// </summary>
        private static readonly FontFamily MonospaceFont =
            new FontFamily("Consolas, Courier New, Lucida Console");

        private static readonly FontFamily NormalFont =
            new FontFamily("Segoe UI, Calibri, Verdana");

        private readonly FlowDocument _document;
        private readonly RichTextBox _richTextBox;

        /// <summary>
        /// Cached last Content value received via the DP full-replace path.
        /// </summary>
        private string? _receivedContent;

        /// <summary>
        /// Reentrancy guard so a PropertyChanged storm cannot run two renders at once.
        /// </summary>
        private bool _isRendering;

        /// <summary>
        /// When set, the next content/scroll affinity update may not touch the controls.
        /// </summary>
        private bool _isUnloaded;

        private ChatMessage? _boundMessage;

        public StreamingMarkdownRenderer()
        {
            InitializeComponent();

            MinWidth = 0;

            // Reuse the XAML-hosted RichTextBox (HostText) and its FlowDocument
            // for every mode — one control owns the card, no renderer swap.
            _document = HostText.Document ?? new FlowDocument();
            HostText.Document = _document;

            _richTextBox = HostText;
            _richTextBox.SizeChanged += HostText_SizeChanged;
            _richTextBox.PreviewMouseWheel += HostText_PreviewMouseWheel;

            // When a long response is taller than the conversation viewport, clicking
            // or dragging to select text raises a RequestBringIntoView routed event.
            // The outer ScrollViewer handles that by scrolling to reveal the focused
            // caret — but for a tall card it can only align the card's TOP to the top
            // of the viewport, yanking the content being selected out of view
            // ("snaps to the top of the response"). The card is read-only and we own
            // scrolling, so suppress that repositioning while the user drag-selects.
            // Keyboard caret navigation still brings text into view (left button not
            // pressed, nothing captured → we let it through).
            _richTextBox.AddHandler(
                Control.RequestBringIntoViewEvent,
                new RequestBringIntoViewEventHandler(HostText_RequestBringIntoView),
                handledEventsToo: true);

            // Honor an explicitly-set Foreground (e.g. tool cards set #CCCCCC);
            // otherwise fall back to the VS theme text brush. The RichTextBox
            // sets its own Foreground, so we must re-apply here for it to cascade.
            var fgDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                TextElement.ForegroundProperty, typeof(StreamingMarkdownRenderer));
            fgDescriptor?.AddValueChanged(this, (s, e) => ApplyEffectiveForeground());
            ApplyEffectiveForeground();

            Loaded += (s, e) => { _isUnloaded = false; };
            Unloaded += (s, e) => { _isUnloaded = true; };
            DataContextChanged += StreamingMarkdownRenderer_DataContextChanged;
        }

        private void ApplyEffectiveForeground()
        {
            var local = ReadLocalValue(ForegroundProperty);
            if (local != DependencyProperty.UnsetValue && local is Brush brush)
            {
                _richTextBox.Foreground = brush;
            }
            else
            {
                _richTextBox.Foreground = TryGetBrush("VsBrush.WindowText")
                                          ?? new SolidColorBrush(Color.FromRgb(220, 220, 220));
            }
        }

        private void HostText_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                _document.PageWidth = e.NewSize.Width;
            }
        }

        /// <summary>
        /// Prevents the conversation <see cref="ScrollViewer"/> from snapping a tall card
        /// to the top of the viewport while the user is dragging to select/copy text.
        /// </summary>
        private void HostText_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Only suppress the scroll-to-reveal during an active mouse drag selection.
            // If the left button is not pressed (no capture), allow normal behavior
            // (e.g. keyboard caret navigation scrolling text into view).
            if (Mouse.LeftButton == MouseButtonState.Pressed && _richTextBox != null)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// gap90c: Restore a working scroll wheel for the unified renderer.
        ///
        /// A RichTextBox natively swallows PreviewMouseWheel to scroll its own
        /// FlowDocument, but this control hosts with VerticalScrollBarVisibility=Disabled,
        /// so the wheel used to be absorbed with nothing visible and never reached the
        /// conversation <see cref="ScrollViewer"/> — hover any card and scrolling stopped.
        ///
        /// Fix: when the inner document does not overflow, OR the wheel would push past
        /// the current internal scroll edge, forward the delta to the nearest ancestor
        /// ScrollViewer (the conversation MessagesScrollViewer) and mark handled. The card
        /// only consumes the wheel for its own internal scroll when it actually overflows
        /// and there is room to move in the delta direction. This restores
        /// "hover a card → wheel scrolls the whole conversation" (matching how the legacy
        /// tool card's plain TextBlock behaved) while keeping oversized cards individually
        /// scrollable.
        /// </summary>
        private void HostText_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // If the inner document overflows the rich text box, let it scroll internally
            // and only bubble to the conversation when it hits a scroll edge.
            bool hasOverflow = _document.PageHeight > _richTextBox.ActualHeight + 0.5;
            if (hasOverflow)
            {
                bool atTop = _richTextBox.VerticalOffset <= 0;
                bool atBottom = _richTextBox.VerticalOffset + _richTextBox.ViewportHeight >= _richTextBox.ExtentHeight - 0.5;
                bool wheelUp = e.Delta > 0;
                bool wheelDown = e.Delta < 0;

                // There is still internal room in the wheel direction → consume it here.
                if ((wheelUp && !atTop) || (wheelDown && !atBottom))
                {
                    return; // let the RichTextBox handle it internally
                }
            }

            // Forward to the nearest scrollable ancestor (the conversation list).
            if (VisualTreeHelper.GetParent(this) is Visual parent)
            {
                var scrollViewer = FindAncestor<ScrollViewer>(parent);
                if (scrollViewer != null)
                {
                    double newOffset = scrollViewer.VerticalOffset - e.Delta;
                    newOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, newOffset));
                    scrollViewer.ScrollToVerticalOffset(newOffset);
                    e.Handled = true;
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                    return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // ===================================================================
        // Dependency properties
        // ===================================================================

        /// <summary>
        /// Content kind: Verbatim (user/tool) or Markdown (reasoning/response).
        /// The DP uses the control's own <see cref="MarkdownMode"/> type.
        /// </summary>
        public static readonly DependencyProperty ContentKindProperty =
            DependencyProperty.Register(
                nameof(ContentKind),
                typeof(MarkdownMode),
                typeof(StreamingMarkdownRenderer),
                new PropertyMetadata(MarkdownMode.Verbatim, OnModeChanged));

        public MarkdownMode ContentKind
        {
            get => (MarkdownMode)GetValue(ContentKindProperty);
            set => SetValue(ContentKindProperty, value);
        }

        /// <summary>
        /// True for tool cards (monospaced verbatim); false for user cards
        /// (variable-width verbatim). Only consulted in <see cref="MarkdownMode.Verbatim"/>.
        /// </summary>
        public static readonly DependencyProperty IsMonospaceProperty =
            DependencyProperty.Register(
                nameof(IsMonospace),
                typeof(bool),
                typeof(StreamingMarkdownRenderer),
                new PropertyMetadata(false, OnModeChanged));

        public bool IsMonospace
        {
            get => (bool)GetValue(IsMonospaceProperty);
            set => SetValue(IsMonospaceProperty, value);
        }

        /// <summary>
        /// The raw content to render (single source of truth; never copied).
        /// </summary>
        public new static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(
                "Content",
                typeof(string),
                typeof(StreamingMarkdownRenderer),
                new PropertyMetadata(null, OnContentChanged));

        public new string? Content
        {
            get => (string?)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        /// <summary>
        /// Optional bound message. When set, the renderer subscribes to
        /// TokenAppended (incremental) and PropertyChanged(IsFinalized | Content).
        /// </summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                "Message",
                typeof(ChatMessage),
                typeof(StreamingMarkdownRenderer),
                new PropertyMetadata(null, (d, e) => ((StreamingMarkdownRenderer)d).OnMessageChanged(e.NewValue as ChatMessage)));

        public ChatMessage? Message
        {
            get => (ChatMessage?)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>
        /// gap89: Per-card raw/processed view selector. <see cref="MessageViewMode.Pretty"/> is the
        /// resting default (full Markdig render); <see cref="MessageViewMode.Raw"/> shows the uniform
        /// verbatim source (flat, monospaced, no rendering, code blocks included — the markdown
        /// pipeline is NEVER invoked on raw). Re-renders the card when the view toggles.
        /// </summary>
        public static readonly DependencyProperty ViewModeProperty =
            DependencyProperty.Register(
                nameof(ViewMode),
                typeof(MessageViewMode),
                typeof(StreamingMarkdownRenderer),
                new PropertyMetadata(MessageViewMode.Pretty, (d, e) => ((StreamingMarkdownRenderer)d).OnViewModeChanged()));

        public MessageViewMode ViewMode
        {
            get => (MessageViewMode)GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        /// <summary>
        /// gap89: View-mode toggle handler. In Raw mode the card is a flat verbatim source render
        /// ("destination is the audience"); in Pretty mode it is the processed Markdig render ("the
        /// reader is the audience"). This is an intent choice, not a rendering choice.
        /// </summary>
        private void OnViewModeChanged()
        {
            var content = _receivedContent ?? _boundMessage?.Content ?? Content ?? string.Empty;
            if (ViewMode == MessageViewMode.Raw)
                RenderRawVerbatim(content);
            else
                FullRenderIfNeeded();
        }

        // ===================================================================
        // DP change handlers
        // ===================================================================

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((StreamingMarkdownRenderer)d).FullRenderIfNeeded();

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var renderer = (StreamingMarkdownRenderer)d;
            renderer.OnContentReceived(e.NewValue as string);
        }

        private void OnContentReceived(string? content)
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyContent(content);
            }
            else
            {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                Dispatcher.Invoke(() => ApplyContent(content));
#pragma warning restore VSTHRD001
            }
        }

        // ===================================================================
        // Message binding (incremental + finalize signal)
        // ===================================================================

        private void StreamingMarkdownRenderer_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Fallback: if no explicit Message DP is set, bind to the DataContext.
            if (GetValue(MessageProperty) == null && DataContext is ChatMessage msg)
            {
                Message = msg;
            }
        }

        /// <summary>
        /// Entry point for gap75-style incremental token delivery from a bound
        /// <see cref="ChatMessage"/> (TokenAppended). Only meaningful in
        /// markdown-streaming mode, but harmless if called in verbatim mode
        /// (it is ignored unless streaming is active and not finalized).
        /// </summary>
        public void AppendToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            if (ContentKind != MarkdownMode.Markdown) return;

            if (Dispatcher.CheckAccess())
            {
                AppendIncremental(token);
            }
            else
            {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                Dispatcher.Invoke(() => AppendIncremental(token));
#pragma warning restore VSTHRD001
            }
        }

        private void OnMessageChanged(ChatMessage? message)
        {
            if (_boundMessage != null)
            {
                _boundMessage.TokenAppended -= OnTokenAppended;
                _boundMessage.PropertyChanged -= OnMessagePropertyChanged;
            }

            _boundMessage = message;

            if (message != null)
            {
                message.TokenAppended += OnTokenAppended;
                message.PropertyChanged += OnMessagePropertyChanged;

                // gap89: keep the per-card view in sync when the message owns it.
                ViewMode = message.ViewMode;

                // The DataContext may be set before the visual tree is loaded;
                // run the full render for the finalized/bound content immediately.
                if (message.IsFinalized)
                {
                    FullRenderIfNeeded();
                }
            }
        }

        private void OnTokenAppended(string token) => AppendToken(token);

        private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // gap88: IsFinalized drives the single full Markdig pass.
            if (e.PropertyName == nameof(ChatMessage.IsFinalized) && _boundMessage?.IsFinalized == true)
            {
                FullRenderIfNeeded();
            }
            else if (e.PropertyName == nameof(ChatMessage.Content))
            {
                OnContentReceived(_boundMessage?.Content);
            }
            // gap89: re-render when the per-card view toggle changes on the message.
            else if (e.PropertyName == nameof(ChatMessage.ViewMode) && _boundMessage != null)
            {
                ViewMode = _boundMessage.ViewMode;
            }
        }

        // ===================================================================
        // Rendering dispatch
        // ===================================================================

        private void ApplyContent(string? content)
        {
            if (_isUnloaded) return;
            content ??= string.Empty;

            // Verbatim content is always rendered as-is (zero markdown, ever),
            // regardless of the view toggle — user/tool cards have no pretty/raw split.
            if (ContentKind == MarkdownMode.Verbatim)
            {
                RenderVerbatim(content);
                return;
            }

            _receivedContent = content;

            // gap89: Raw view overrides the processed render for markdown cards — the
            // destination is the audience, so show the uniform verbatim source flat.
            if (ViewMode == MessageViewMode.Raw)
            {
                RenderRawVerbatim(content);
                return;
            }

            // Markdown content: if the message is finalized, do the single full
            // markdig pass; otherwise keep whatever streaming has produced so far.
            if (_boundMessage?.IsFinalized == true)
            {
                FullRenderIfNeeded();
            }
            else
            {
                RenderStreaming(content);
            }
        }

        private void FullRenderIfNeeded()
        {
            if (_isUnloaded) return;
            if (_isRendering) return;
            _isRendering = true;

            try
            {
                if (ContentKind == MarkdownMode.Verbatim)
                {
                    var text = _receivedContent ?? Content ?? _boundMessage?.Content ?? string.Empty;
                    RenderVerbatim(text);
                    return;
                }

                var markdown = _receivedContent ?? _boundMessage?.Content ?? Content ?? string.Empty;

                // gap89: raw view short-circuits before the Markdig pipeline (state-guaranteed).
                if (ViewMode == MessageViewMode.Raw)
                {
                    RenderRawVerbatim(markdown);
                    return;
                }

                RenderFullMarkdig(markdown);
            }
            finally
            {
                _isRendering = false;
            }
        }

        // ===================================================================
        // Verbatim mode (user | tool): plain, whitespace-preserving, NO markdown.
        // ===================================================================

        private void RenderVerbatim(string text)
        {
            _document.Blocks.Clear();

            if (string.IsNullOrEmpty(text))
                return;

            foreach (var para in SplitLines(text))
            {
                var p = new Paragraph
                {
                    Margin = new Thickness(0),
                    FontFamily = IsMonospace ? MonospaceFont : NormalFont
                };
                p.Inlines.Add(new Run(para));
                _document.Blocks.Add(p);
            }
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            // Preserve line structure: split on \n so user/source line breaks are
            // honored (no markdown normalization, no spurious block parsing).
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.Split('\n');
        }

        // ===================================================================
        // gap89: Raw (verbatim) view for markdown cards.
        // "Pretty is for the user; raw is for the destination."
        // Uniform flat source — code blocks included, NO rendering, NO emphasis,
        // NO chrome. The Markdig pipeline is never invoked on raw (state-guaranteed).
        // ===================================================================

        private void RenderRawVerbatim(string text)
        {
            _document.Blocks.Clear();

            if (string.IsNullOrEmpty(text))
                return;

            foreach (var para in SplitLines(text))
            {
                var p = new Paragraph
                {
                    Margin = new Thickness(0),
                    FontFamily = MonospaceFont
                };
                p.Inlines.Add(new Run(para));
                _document.Blocks.Add(p);
            }
        }

        // ===================================================================
        // Streaming mode (markdown, !IsFinalized): O(n) append-only.
        // ===================================================================

        private void RenderStreaming(string content)
        {
            // gap88: streaming is append-only on the shared document. We support
            // both the incremental TokenAppended path (AppendIncremental) and a
            // full-replacement snapshot (used when a Content PropertyChanged fires
            // mid-stream). AppendIncremental is called per fresh delta; here we
            // only refresh the live paragraph's partial state and rely on the
            // incremental path for new tokens.
            var delta = content;
            if (!string.IsNullOrEmpty(_receivedPrefix) && content.StartsWith(_receivedPrefix, StringComparison.Ordinal))
            {
                delta = content.Substring(_receivedPrefix.Length);
            }
            _receivedPrefix = content;

            AppendIncremental(delta);
        }

        private string _receivedPrefix = string.Empty;

        private void AppendIncremental(string delta)
        {
            if (string.IsNullOrEmpty(delta)) return;

            delta = delta.Replace("\r\n", "\n").Replace("\r", "\n");

            int newlineIndex;
            while ((newlineIndex = delta.IndexOf('\n')) >= 0)
            {
                string segment = delta.Substring(0, newlineIndex);
                delta = delta.Substring(newlineIndex + 1);

                AppendMarkdownRuns(segment);
                _pendingParagraph = null;
            }

            AppendMarkdownRuns(delta);
        }

        // ===================================================================
        // Full Markdig mode (reasoning | response at IsFinalized): ONE render.
        // ===================================================================

        private void RenderFullMarkdig(string markdown)
        {
            _document.Blocks.Clear();

            if (string.IsNullOrEmpty(markdown))
                return;

            try
            {
                var doc = Markdown.Parse(markdown, _pipeline);
                foreach (var block in doc)
                    RenderBlock(block);
            }
            catch
            {
                // Fallback: plain selectable paragraph.
                var fallback = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                fallback.Inlines.Add(new Run(markdown));
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
                    _document.Blocks.Add(MakeTextParagraph(para.Inline));
                    break;

                case HeadingBlock heading:
                    _document.Blocks.Add(MakeTextParagraph(
                        heading.Inline,
                        fontSize: heading.Level <= 3 ? 20 - heading.Level * 2 : 14,
                        weight: FontWeights.Bold,
                        margin: new Thickness(0, 4, 0, 2)));
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

                case Markdig.Extensions.Tables.Table table:
                    _document.Blocks.Add(RenderTable(table));
                    break;

                case CodeBlock indentedCode:
                    _document.Blocks.Add(new BlockUIContainer(
                        BuildPlainCodeControl(ExtractCodeLines(indentedCode))));
                    break;

                default:
                    // Silently skip unknown block types.
                    break;
            }
        }

        // Reused from gap21/gap53 markdig chrome.
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

        private BlockUIContainer RenderTable(Markdig.Extensions.Tables.Table table)
        {
            // Render the table as a WPF Grid wrapped in a BlockUIContainer.
            //
            // Why not a WPF Table / GridLength.Auto alone: a wrapping TextBlock
            // measures its desired width against the full available width, so
            // Auto columns all report roughly the same large size -> the table
            // stretches edge-to-edge and every column comes out equal. That's the
            // "card-wide, equal-width columns" symptom.
            //
            // Fix: measure each column's NATURAL width (wrapping disabled) first,
            // then apply explicit pixel column widths capped to the card width. If
            // the natural table is wider than the card, columns scale down
            // proportionally (HTML-table behavior) instead of being equalized.
            var grid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            int colCount = table.ColumnDefinitions.Count;

            // Pass 1: build every cell (so we can measure before it's in the tree)
            // and record each column's natural (no-wrap) width.
            var cells = new List<(Border Border, TextBlock Text, int Row, int Col)>();
            var naturalWidths = new double[Math.Max(colCount, 1)];

            // Populate the cell's text from its markdown blocks (reusing the
            // inline -> run mapping so bold/italic/code in cells still work).
            void Populate(TextBlock t, Markdig.Extensions.Tables.TableCell cell)
            {
                foreach (var subBlock in cell)
                {
                    if (subBlock is ParagraphBlock para)
                    {
                        if (para.Inline != null)
                        {
                            foreach (var inline in para.Inline)
                                AppendInline(t.Inlines, inline);
                        }
                    }
                    else if (subBlock is Markdig.Extensions.Tables.Table nestedTable)
                    {
                        // Nested table: render its grid and host it as a UIElement
                        // inside this cell's inline flow.
                        var nested = RenderTable(nestedTable);
                        if (nested.Child is UIElement nestedElement)
                            t.Inlines.Add(new InlineUIContainer(nestedElement));
                    }
                }
            }

            int rowIndex = 0;
            foreach (var row in table)
            {
                if (row is not Markdig.Extensions.Tables.TableRow tableRow)
                    continue;

                grid.RowDefinitions.Add(new RowDefinition());

                int colIndex = 0;
                foreach (var cell in tableRow)
                {
                    if (cell is not Markdig.Extensions.Tables.TableCell tableCell)
                    { colIndex++; continue; }

                    // Build content first so we can measure its natural width.
                    var cellText = new TextBlock
                    {
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Populate(cellText, tableCell);

                    // Natural width with wrapping disabled (one visual line).
                    cellText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    if (colIndex < colCount)
                        naturalWidths[colIndex] = Math.Max(naturalWidths[colIndex], cellText.DesiredSize.Width);

                    var border = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(90, 90, 90)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(6, 2, 6, 2),
                        Background = tableRow.IsHeader
                            ? new SolidColorBrush(Color.FromRgb(55, 55, 55))
                            : Brushes.Transparent
                    };

                    cells.Add((border, cellText, rowIndex, colIndex));
                    colIndex++;
                }
                rowIndex++;
            }

            // Pass 2: convert natural widths into capped pixel column widths.
            double available = double.IsNaN(_document.PageWidth)
                ? 700
                : Math.Max(_document.PageWidth - 16, 160);
            // Each cell has horizontal padding (6+6) plus its 1px border on each
            // side (2). This frame is over-and-above the text area, so it must be
            // reserved separately — otherwise every column is ~14px too narrow and
            // narrow content (e.g. one/two digit numbers) wraps onto two lines.
            double perColFrame = 6 + 6 + 2; // Padding + border.
            double paddingAndBorder = colCount * perColFrame;
            double total = paddingAndBorder;
            foreach (var w in naturalWidths) total += w;

            // Scale the whole table down proportionally only if it overflows.
            double scale = total > available ? available / Math.Max(total, 1) : 1.0;
            var columnWidths = new double[Math.Max(colCount, 1)];
            for (int c = 0; c < columnWidths.Length; c++)
            {
                // Content (text) width: natural width scaled down on overflow,
                // capped to a fair share of the card, but never below a readable
                // floor. The frame is subtracted from the per-column cap so the
                // text area is what you'd expect from the natural width.
                double content = Math.Max(
                    Math.Min(naturalWidths[c] * scale,
                        (available / Math.Max(colCount, 1)) - perColFrame),
                    24);
                // Total grid column = text area + the per-column frame.
                double w = content + perColFrame;
                columnWidths[c] = w;
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(w, GridUnitType.Pixel)
                });
            }

            // Pass 3: lay cells into the grid with wrapping ON + per-cell cap.
            foreach (var (border, text, r, c) in cells)
            {
                text.TextWrapping = TextWrapping.Wrap;
                if (c < columnWidths.Length)
                    // Reserve the cell's padding + border; cap only the text area
                    // so the content gets its full computed width before wrapping.
                    text.MaxWidth = Math.Max(columnWidths[c] - perColFrame, 1);

                // Column alignment (left/center/right) from the parsed table.
                if (c < colCount)
                {
                    switch (table.ColumnDefinitions[c].Alignment)
                    {
                        case Markdig.Extensions.Tables.TableColumnAlign.Center:
                            text.TextAlignment = TextAlignment.Center;
                            break;
                        case Markdig.Extensions.Tables.TableColumnAlign.Right:
                            text.TextAlignment = TextAlignment.Right;
                            break;
                        default:
                            text.TextAlignment = TextAlignment.Left;
                            break;
                    }
                }

                border.Child = text;
                Grid.SetColumn(border, c);
                Grid.SetRow(border, r);
                grid.Children.Add(border);
            }

            grid.MaxWidth = available;
            return new BlockUIContainer(grid);
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

            // Header bar: language label + Copy/Apply dropdown (gap53).
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

            // Header bar actions: explicit Copy/Apply buttons (gap53). Replaced the
            // ComboBox whose default selection was Copy, so clicking Copy did not
            // change selection -> SelectionChanged never fired -> copy did nothing.
            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 4, 2),
                VerticalAlignment = VerticalAlignment.Center
            };

            var copyButton = new Button
            {
                Content = "📋 Copy",
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(8, 0, 8, 0)
            };
            copyButton.Click += (s, e) => CopyCodeBlock(blockId, language, lines);

            var applyButton = new Button
            {
                Content = "✔ Apply",
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(8, 0, 8, 0)
            };
            applyButton.Click += (s, e) => ApplyCodeBlock(blockId, language, lines);

            actionsPanel.Children.Add(copyButton);
            actionsPanel.Children.Add(applyButton);

            DockPanel.SetDock(actionsPanel, Dock.Right);

            header.Children.Add(langLabel);
            header.Children.Add(actionsPanel);
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

        private void CopyCodeBlock(string blockId, string language, string content)
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

        private void ApplyCodeBlock(string blockId, string language, string content)
        {
            // Apply is intentionally a no-op for now; log the intent only.
            LoggerService.Current.WriteDebug($"[gap53-block-action] Apply selected for block (lang={language}, id={blockId})");
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

        // ===================================================================
        // Streaming markdown inline runs (gap75 minimal parser + gap75 TextBlockModel)
        // ===================================================================

        private Paragraph? _pendingParagraph;

        private void AppendMarkdownRuns(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            EnsurePendingParagraph();

            foreach (System.Windows.Documents.Inline inline in ParseMarkdownInline(text))
            {
                _pendingParagraph?.Inlines.Add(inline);
            }
        }

        private void EnsurePendingParagraph()
        {
            if (_pendingParagraph != null) return;

            _pendingParagraph = new Paragraph
            {
                Margin = new Thickness(0)
            };
            _document.Blocks.Add(_pendingParagraph);
        }

        private IEnumerable<System.Windows.Documents.Inline> ParseMarkdownInline(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            var model = new TextBlockModel(text);

            if (model.InlineRuns == null || model.InlineRuns.Count == 0)
            {
                yield return new Run(text);
                yield break;
            }

            foreach (var parsedRun in model.InlineRuns)
            {
                if (parsedRun == null) continue;

                var inline = new Run
                {
                    Text = parsedRun.Text ?? string.Empty
                };

                switch (parsedRun.Style)
                {
                    case InlineStyle.Bold:
                        inline.FontWeight = FontWeights.Bold;
                        break;
                    case InlineStyle.Italic:
                        inline.FontStyle = FontStyles.Italic;
                        break;
                    case InlineStyle.Code:
                        inline.FontFamily = new FontFamily("Consolas");
                        inline.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                        break;
                }

                yield return inline;
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

    /// <summary>
    /// gap88: Content-kind discriminator for the unified renderer.
    /// </summary>
    public enum MarkdownMode
    {
        /// <summary>
        /// Plain, whitespace-preserving, variable-width, no markdown (user cards...).
        /// </summary>
        Verbatim,

        /// <summary>
        /// Markdown-guaranteed content (reasoning/response). Streaming simple-md →
        /// one full Markdig render at finalize.
        /// </summary>
        Markdown
    }
}
