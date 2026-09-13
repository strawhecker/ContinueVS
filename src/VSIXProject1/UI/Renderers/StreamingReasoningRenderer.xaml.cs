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
            this.Loaded += StreamingReasoningRenderer_Loaded;
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

        private StreamingTextBlockCollection? _textBlockCollection;

        private void OnContentChanged(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                _textBlockCollection = new StreamingTextBlockCollection();
            }
            else
            {
                _textBlockCollection = new StreamingTextBlockCollection();
                // Split content into lines and create TextBlockModels
                var lines = content!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    var model = new TextBlockModel(line);
                    _textBlockCollection.Add(model);
                }
            }

            TextBlocksItemsControl.ItemsSource = _textBlockCollection;
            if (TextBlocksItemsControl.ItemContainerGenerator != null)
            {
                TextBlocksItemsControl.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
            }
        }

        private void StreamingReasoningRenderer_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure ItemsSource is set
            if (TextBlocksItemsControl.ItemsSource == null && _textBlockCollection != null)
            {
                TextBlocksItemsControl.ItemsSource = _textBlockCollection;
                if (TextBlocksItemsControl.ItemContainerGenerator != null)
                {
                    TextBlocksItemsControl.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
                }
            }
        }

        private void ItemContainerGenerator_StatusChanged(object? sender, EventArgs e)
        {
            if (TextBlocksItemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                // Populate Inlines for all visible TextBlock items
                for (int i = 0; i < TextBlocksItemsControl.Items.Count; i++)
                {
                    var container = TextBlocksItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                    if (container != null)
                    {
                        var textBlock = FindTextBlock(container);
                        if (textBlock != null && TextBlocksItemsControl.Items[i] is TextBlockModel model)
                        {
                            PopulateInlines(textBlock, model);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively find TextBlock inside visual tree.
        /// </summary>
        private TextBlock? FindTextBlock(FrameworkElement element)
        {
            if (element is TextBlock tb)
            {
                return tb;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;
                if (child != null)
                {
                    var found = FindTextBlock(child);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Populate TextBlock.Inlines from TextBlockModel.InlineRuns.
        /// </summary>
        private void PopulateInlines(TextBlock textBlock, TextBlockModel model)
        {
            textBlock.Inlines.Clear();

            foreach (var run in model.InlineRuns)
            {
                Run inlineRun = new Run { Text = run.Text };

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
                    default:
                        // Plain style: no special formatting
                        break;
                }

                textBlock.Inlines.Add(inlineRun);
            }
        }
    }
}
