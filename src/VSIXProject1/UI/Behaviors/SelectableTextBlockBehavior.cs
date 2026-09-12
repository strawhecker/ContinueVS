using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ContinueVS.Core.Types;

namespace ContinueVS.UI.Behaviors
{
    /// <summary>
    /// Attached behavior for enabling multi-TextBlock selection and copy functionality.
    /// Attached to TextBlock elements within the StreamingReasoningRenderer ItemsControl.
    /// Supports:
    /// - Click to select/deselect individual TextBlocks
    /// - Shift+Click to select a range
    /// - Ctrl+C to copy selected text to clipboard
    /// - Visual highlight for selected blocks
    /// </summary>
    public static class SelectableTextBlockBehavior
    {
        private static HashSet<TextBlock> _selectedBlocks = new HashSet<TextBlock>();
        private static TextBlock? _lastSelectedBlock;
        private static ItemsControl? _parentItemsControl;

        public static readonly DependencyProperty IsSelectableProperty =
            DependencyProperty.RegisterAttached(
                "IsSelectable",
                typeof(bool),
                typeof(SelectableTextBlockBehavior),
                new PropertyMetadata(false, IsSelectableChanged));

        public static bool GetIsSelectable(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsSelectableProperty);
        }

        public static void SetIsSelectable(DependencyObject obj, bool value)
        {
            obj.SetValue(IsSelectableProperty, value);
        }

        private static void IsSelectableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock && (bool)e.NewValue)
            {
                textBlock.MouseLeftButtonDown += TextBlock_MouseLeftButtonDown;
                textBlock.Cursor = Cursors.Hand;
            }
        }

        private static void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null)
            {
                return;
            }

            // Find the parent ItemsControl
            _parentItemsControl = FindParentItemsControl(textBlock);

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Ctrl+Click: toggle selection
                if (_selectedBlocks.Contains(textBlock))
                {
                    _selectedBlocks.Remove(textBlock);
                    UpdateTextBlockVisuals(textBlock, false);
                }
                else
                {
                    _selectedBlocks.Add(textBlock);
                    UpdateTextBlockVisuals(textBlock, true);
                }
                _lastSelectedBlock = textBlock;
            }
            else if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                // Shift+Click: select range
                if (_lastSelectedBlock != null && _parentItemsControl != null)
                {
                    SelectRange(_lastSelectedBlock, textBlock, _parentItemsControl);
                }
            }
            else
            {
                // Regular click: select only this block
                _selectedBlocks.Clear();
                _selectedBlocks.Add(textBlock);

                // Deselect all others
                if (_parentItemsControl != null)
                {
                    foreach (var item in _parentItemsControl.Items)
                    {
                        if (_parentItemsControl.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter container)
                        {
                            var tb = FindTextBlockInVisualTree(container);
                            if (tb != null && tb != textBlock)
                            {
                                UpdateTextBlockVisuals(tb, false);
                            }
                        }
                    }
                }

                UpdateTextBlockVisuals(textBlock, true);
                _lastSelectedBlock = textBlock;
            }

            e.Handled = true;
        }

        private static void SelectRange(TextBlock from, TextBlock to, ItemsControl itemsControl)
        {
            _selectedBlocks.Clear();

            var generator = itemsControl.ItemContainerGenerator;
            int fromIndex = -1, toIndex = -1;

            // Find indices
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var container = generator.ContainerFromIndex(i);
                var tb = FindTextBlockInVisualTree(container);
                if (tb == from) fromIndex = i;
                if (tb == to) toIndex = i;
            }

            if (fromIndex < 0 || toIndex < 0)
            {
                return;
            }

            int start = Math.Min(fromIndex, toIndex);
            int end = Math.Max(fromIndex, toIndex);

            // Select all blocks in range
            for (int i = start; i <= end; i++)
            {
                var container = generator.ContainerFromIndex(i);
                var tb = FindTextBlockInVisualTree(container);
                if (tb != null)
                {
                    _selectedBlocks.Add(tb);
                    UpdateTextBlockVisuals(tb, true);
                }
            }
        }

        private static void UpdateTextBlockVisuals(TextBlock textBlock, bool isSelected)
        {
            if (isSelected)
            {
                textBlock.Opacity = 0.8;
                textBlock.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(80, 80, 120));
            }
            else
            {
                textBlock.Opacity = 1.0;
                textBlock.Background = null;
            }
        }

        private static ItemsControl? FindParentItemsControl(DependencyObject obj)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            while (parent != null)
            {
                if (parent is ItemsControl itemsControl)
                {
                    return itemsControl;
                }
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static TextBlock? FindTextBlockInVisualTree(DependencyObject? obj)
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
        /// Copy selected TextBlock text to clipboard, preserving newlines between blocks.
        /// </summary>
        public static void CopySelectedText()
        {
            if (_selectedBlocks.Count == 0)
            {
                return;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var block in _selectedBlocks)
            {
                sb.AppendLine(block.Text);
            }

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SelectableTextBlockBehavior] Failed to copy to clipboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all selections.
        /// </summary>
        public static void ClearSelection()
        {
            _selectedBlocks.Clear();
            _lastSelectedBlock = null;
        }
    }
}
