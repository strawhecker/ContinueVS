using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ContinueVS.Core.Types;
using ContinueVS.Core.Parsers;

namespace ContinueVS.UI.Renderers
{
    /// <summary>
    /// Code-behind for StreamingReasoningRenderer.xaml.
    /// Populates TextBlock.Inlines from TextBlockModel.InlineRuns synchronously.
    /// </summary>
    public partial class StreamingReasoningRenderer : UserControl
    {
        public StreamingReasoningRenderer()
        {
            InitializeComponent();
            this.DataContextChanged += StreamingReasoningRenderer_DataContextChanged;
            // Force width constraint to propagate for text wrapping
            this.MinWidth = 0;
        }

        /// <summary>
        /// Dependency property for MaxMessageWidth, bound from parent ViewModel.
        /// </summary>
        public static readonly DependencyProperty MaxMessageWidthProperty =
            DependencyProperty.Register(
                "MaxMessageWidth",
                typeof(double),
                typeof(StreamingReasoningRenderer),
                new PropertyMetadata(600.0, (d, e) => ((StreamingReasoningRenderer)d).OnMaxMessageWidthChanged((double)e.NewValue)));

        public double MaxMessageWidth
        {
            get => (double)GetValue(MaxMessageWidthProperty);
            set => SetValue(MaxMessageWidthProperty, value);
        }

        private void OnMaxMessageWidthChanged(double newWidth)
        {
            // Apply the width constraint to TextBlocksPanel so text can wrap
            if (TextBlocksPanel != null && newWidth > 0)
            {
                TextBlocksPanel.MaxWidth = newWidth;
                TextBlocksPanel.Width = newWidth;
            }
        }

        private void StreamingReasoningRenderer_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // When DataContext changes (ChatMessage), try to get width from ancestor ViewModel
            UpdateWidthFromDataContext();
        }

        private void UpdateWidthFromDataContext()
        {
            // Try to find the ViewModel in the parent hierarchy
            var parent = this.Parent as FrameworkElement;
            while (parent != null)
            {
                if (parent.DataContext is ViewModels.ChatPageViewModel vm)
                {
                    MaxMessageWidth = vm.AvailableMessageWidth;
                    break;
                }
                parent = parent.Parent as FrameworkElement;
            }
        }

        /// <summary>
        /// Content dependency property — accepts string and auto-converts to StreamingTextBlockCollection.
        /// </summary>
        public new static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(
                "RendererContent",
                typeof(string),
                typeof(StreamingReasoningRenderer),
                new PropertyMetadata(null, (d, e) => ((StreamingReasoningRenderer)d).OnContentChanged(e.NewValue as string)));

        public new string? Content
        {
            get => (string?)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        private void OnContentChanged(string? content)
        {
            TextBlocksPanel.Children.Clear();

            if (string.IsNullOrEmpty(content))
                return;

            var lines = content!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var model = new TextBlockModel(line);
                TextBlocksPanel.Children.Add(MakeWrappingRichTextBox(model));
            }
        }

        private static RichTextBox MakeWrappingRichTextBox(TextBlockModel model)
        {
            var para = new Paragraph { Margin = new Thickness(0) };

            if (model.InlineRuns != null && model.InlineRuns.Count > 0)
            {
                foreach (var run in model.InlineRuns)
                {
                    var inlineRun = new Run { Text = run.Text };
                    switch (run.Style)
                    {
                        case InlineStyle.Bold:
                            inlineRun.FontWeight = FontWeights.Bold;
                            break;
                        case InlineStyle.Italic:
                            inlineRun.FontStyle = FontStyles.Italic;
                            break;
                        case InlineStyle.Code:
                            inlineRun.FontFamily = new FontFamily("Consolas, monospace");
                            inlineRun.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                            inlineRun.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 100));
                            break;
                    }
                    para.Inlines.Add(inlineRun);
                }
            }
            else if (!string.IsNullOrEmpty(model.Text))   // fallback if no parsed runs
            {
                para.Inlines.Add(new Run(model.Text));
            }

            var doc = new FlowDocument(para)
            {
                PagePadding = new Thickness(0),
                TextAlignment = TextAlignment.Left,
                PageWidth = 9999   // defeats FlowDocument's default 200px column layout
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
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = System.Windows.Input.Cursors.IBeam
            };
            rtb.SetResourceReference(RichTextBox.ForegroundProperty, "VsBrush.WindowText");

            // Keep PageWidth in sync with actual width so text wraps (markdown renderer's trick)
            rtb.SizeChanged += (s, e) =>
            {
                if (e.NewSize.Width > 0)
                    rtb.Document.PageWidth = e.NewSize.Width;
            };

            return rtb;
        }
    }
}
