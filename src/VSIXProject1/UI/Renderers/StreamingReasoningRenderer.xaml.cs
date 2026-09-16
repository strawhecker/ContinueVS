using ContinueVS.Core.Parsers;
using ContinueVS.Core.Types;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ContinueVS.UI.Renderers
{
    public partial class StreamingReasoningRenderer : UserControl
    {
        private readonly FlowDocument _document;
        private readonly RichTextBox _richTextBox;

        // The complete cumulative value most recently received (DP full-replace path).
        private string _receivedContent = string.Empty;

        // The paragraph that is currently live (being appended to).
        private Paragraph? _pendingParagraph;

        public StreamingReasoningRenderer()
        {
            InitializeComponent();

            MinWidth = 0;

            _document = new FlowDocument
            {
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left,

                // This is changed after the RichTextBox receives its width.
                PageWidth = 1
            };

            _richTextBox = new RichTextBox
            {
                Document = _document,

                IsReadOnly = true,
                IsTabStop = false,
                IsDocumentEnabled = true,

                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 4),

                Cursor = Cursors.IBeam,

                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,

                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Disabled
            };

            _richTextBox.SetResourceReference(
                RichTextBox.ForegroundProperty,
                "VsBrush.WindowText");

            _richTextBox.SizeChanged += RichTextBox_SizeChanged;

            TextBlocksPanel.Children.Add(_richTextBox);

            _pendingParagraph = null;
        }

        private void RichTextBox_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0)
            {
                return;
            }

            _document.PageWidth = e.NewSize.Width;
        }

        /*
         * Use RendererContent rather than Content because UserControl already
         * has a built-in Content property.
         *
         * The value is expected to be cumulative (full replacement):
         *
         *   update 1: "Hello"
         *   update 2: "Hello world"
         *   update 3: "Hello world\nNext"
         */
        public static readonly DependencyProperty RendererContentProperty =
            DependencyProperty.Register(
                nameof(RendererContent),
                typeof(string),
                typeof(StreamingReasoningRenderer),
                new PropertyMetadata(
                    string.Empty,
                    OnRendererContentChanged));

        public string RendererContent
        {
            get
            {
                return (string)GetValue(RendererContentProperty);
            }
            set
            {
                SetValue(RendererContentProperty, value);
            }
        }

        private static void OnRendererContentChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            var renderer =
                (StreamingReasoningRenderer)dependencyObject;

            var content = e.NewValue as string ?? string.Empty;

            renderer.ApplyContent(content);
        }

        // ===================================================================
        // Incremental path (token pass-through). Nothing is accumulated into
        // a cumulative string by the caller; each token arrives as-is from
        // ChatMessage.TokenAppended and is appended to the live paragraph.
        // ===================================================================
        public void AppendToken(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                Dispatcher.Invoke(
                    new Action(() => AppendToken(token)));
#pragma warning restore VSTHRD001

                return;
            }

            AppendIncremental(token!);
        }

        private void AppendIncremental(string delta)
        {
            if (string.IsNullOrEmpty(delta))
            {
                return;
            }

            // Normalize line endings so they never create extra paragraphs.
            delta = delta.Replace("\r\n", "\n");
            delta = delta.Replace("\r", "\n");

            int newlineIndex;

            while ((newlineIndex = delta.IndexOf('\n')) >= 0)
            {
                string segment = delta.Substring(0, newlineIndex);
                delta = delta.Substring(newlineIndex + 1);

                // Complete the current live paragraph with this segment,
                // then start a fresh one for the next line.
                AppendRuns(segment);
                _pendingParagraph = null;
            }

            // Trailing text without a newline: append directly to the live
            // paragraph (token-incremental cadence). We ADD runs; we never
            // clear and rebuild the paragraph, so the FlowDocument stays
            // measured and selectable.
            AppendRuns(delta);
        }

        private void AppendRuns(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            EnsurePendingParagraph();

            foreach (Inline inline in ParseMarkdown(text))
            {
                _pendingParagraph?.Inlines.Add(inline);
            }
        }

        private void EnsurePendingParagraph()
        {
            if (_pendingParagraph != null)
            {
                return;
            }

            _pendingParagraph = new Paragraph
            {
                Margin = new Thickness(0)
            };

            _document.Blocks.Add(_pendingParagraph);
        }

        // ===================================================================
        // Full-replacement path (DP). Used ONLY for initial creation (new
        // user message) and reopening a session where Content arrives as a
        // complete value at bind time. Kept distinct from the incremental path.
        // ===================================================================
        private void ApplyContent(string newContent)
        {
            if (!Dispatcher.CheckAccess())
            {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                Dispatcher.Invoke(
                    new Action(() => ApplyContent(newContent)));
#pragma warning restore VSTHRD001

                return;
            }

            /*
             * A fresh renderer always renders whatever it first receives.
             * This covers the full-replacement cases (new user, reopen).
             */
            if (newContent.Length == 0)
            {
                return;
            }

            if (!newContent.StartsWith(
                    _receivedContent,
                    StringComparison.Ordinal))
            {
                ResetDocument();
            }

            if (newContent.Length <= _receivedContent.Length)
            {
                return;
            }

            string delta = newContent.Substring(
                _receivedContent.Length);

            _receivedContent = newContent;

            AppendIncremental(delta);
        }

        private IEnumerable<Inline> ParseMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            var model = new TextBlockModel(text);

            if (model.InlineRuns == null ||
                model.InlineRuns.Count == 0)
            {
                yield return new Run(text);
                yield break;
            }

            foreach (var parsedRun in model.InlineRuns)
            {
                if (parsedRun == null)
                {
                    continue;
                }

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
                        inline.FontFamily =
                            new FontFamily("Consolas");

                        inline.Background =
                            new SolidColorBrush(
                                Color.FromArgb(30, 0, 0, 0));

                        //inline.Foreground =
                        //    new SolidColorBrush(
                        //        Color.FromRgb(200, 100, 100));
                        break;
                }

                yield return inline;
            }
        }

        private void ResetDocument()
        {
            _receivedContent = string.Empty;
            _pendingParagraph = new Paragraph();

            _document.Blocks.Clear();
        }
    }
}

