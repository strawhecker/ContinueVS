using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.Core.Services;
using ContinueVS.Core.Types;

namespace ContinueVS.UI.Renderers
{
    /// <summary>
    /// UserControl for rendering streaming responses with minimal markdown support.
    /// Replaces MarkdownBlockRenderer for high-performance streaming scenarios.
    /// Maintains an ObservableCollection<TextBlockModel> for O(n) append-only performance.
    /// </summary>
    public partial class StreamingReasoningRenderer : UserControl
    {
        private StreamingTextBlockCollection? _streamingBlocks;

        public StreamingReasoningRenderer()
        {
            InitializeComponent();
            this.Loaded += StreamingReasoningRenderer_Loaded;
        }

        /// <summary>
        /// Content property: accepts a string and populates StreamingBlocks collection.
        /// Uses new keyword to hide inherited ContentControl.Content property.
        /// </summary>
        public new static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(
                "Content",
                typeof(string),
                typeof(StreamingReasoningRenderer),
                new PropertyMetadata(null, (d, e) => ((StreamingReasoningRenderer)d).OnContentChanged(e.NewValue as string)));

        public new string? Content
        {
            get => (string?)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        /// <summary>
        /// StreamingBlocks property: exposes the ObservableCollection<TextBlockModel> for binding.
        /// </summary>
        public static readonly DependencyProperty StreamingBlocksProperty =
            DependencyProperty.Register(
                "StreamingBlocks",
                typeof(ObservableCollection<TextBlockModel>),
                typeof(StreamingReasoningRenderer),
                new PropertyMetadata(null));

        public ObservableCollection<TextBlockModel>? StreamingBlocks
        {
            get => (ObservableCollection<TextBlockModel>?)GetValue(StreamingBlocksProperty);
            set => SetValue(StreamingBlocksProperty, value);
        }

        private void StreamingReasoningRenderer_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize the collection if not already set
            if (StreamingBlocks == null)
            {
                _streamingBlocks = new StreamingTextBlockCollection();
                StreamingBlocks = _streamingBlocks.Blocks;
            }

            // Populate inline runs in TextBlocks after they are created
            if (TextBlocksItemsControl?.Items != null)
            {
                foreach (TextBlockModel model in TextBlocksItemsControl.Items)
                {
                    UpdateTextBlockInlines(model);
                }
            }
        }

        private void OnContentChanged(string? content)
        {
            if (_streamingBlocks == null)
            {
                _streamingBlocks = new StreamingTextBlockCollection();
                StreamingBlocks = _streamingBlocks.Blocks;
            }

            if (string.IsNullOrEmpty(content))
            {
                _streamingBlocks.Clear();
                return;
            }

            // AppendChunk will split by newlines and create/extend TextBlockModels
            _streamingBlocks.AppendChunk(content ?? string.Empty);

            // Update inline runs for the affected TextBlock
            if (StreamingBlocks?.Count > 0)
            {
                var lastBlock = StreamingBlocks[StreamingBlocks.Count - 1];
                UpdateTextBlockInlines(lastBlock);
            }
        }

        /// <summary>
        /// Synchronously populate TextBlock.Inlines from TextBlockModel.InlineRuns.
        /// TextBlock.Inlines is not bindable, so we must do this in code-behind.
        /// </summary>
        private void UpdateTextBlockInlines(TextBlockModel model)
        {
            // Find the corresponding TextBlock in the visual tree
            if (TextBlocksItemsControl?.ItemContainerGenerator is ItemContainerGenerator generator)
            {
                int index = TextBlocksItemsControl.Items.IndexOf(model);
                if (index >= 0)
                {
                    var container = generator.ContainerFromIndex(index);
                    if (container is ContentPresenter presenter)
                    {
                        // The TextBlock is inside the DataTemplate
                        // We need to walk the visual tree to find it
                        var textBlock = FindTextBlockInVisualTree(presenter);
                        if (textBlock != null)
                        {
                            textBlock.Inlines.Clear();
                            foreach (var run in model.InlineRuns)
                            {
                                textBlock.Inlines.Add(run);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively search the visual tree for a TextBlock.
        /// </summary>
        private TextBlock? FindTextBlockInVisualTree(DependencyObject? obj)
        {
            if (obj == null)
            {
                return null;
            }

            if (obj is TextBlock textBlock)
            {
                return textBlock;
            }

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                var result = FindTextBlockInVisualTree(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Append a chunk of streaming content to this renderer.
        /// Called incrementally as LLM chunks arrive.
        /// </summary>
        public void AppendChunk(string chunk)
        {
            if (_streamingBlocks == null)
            {
                _streamingBlocks = new StreamingTextBlockCollection();
                StreamingBlocks = _streamingBlocks.Blocks;
            }

            _streamingBlocks.AppendChunk(chunk);
        }

        /// <summary>
        /// Clear all blocks and prepare for a new streaming response.
        /// </summary>
        public void Reset()
        {
            _streamingBlocks?.Clear();
        }
    }
}
