using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;

namespace ContinueVS.UI.Behaviors
{
    /// <summary>
    /// Behavior enabling multi-block selection across TextBlock collection.
    /// Supports click, Ctrl+click (toggle), Shift+click (range), and Ctrl+C (copy to clipboard).
    /// </summary>
    public class SelectableTextBlockBehavior : Behavior<ItemsControl>
    {
        private HashSet<int> _selectedIndices = new();
        private int _lastSelectedIndex = -1;

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.PreviewMouseDown += OnItemsControlMouseDown;
                AssociatedObject.KeyDown += OnItemsControlKeyDown;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.PreviewMouseDown -= OnItemsControlMouseDown;
                AssociatedObject.KeyDown -= OnItemsControlKeyDown;
            }
            base.OnDetaching();
        }

        private void OnItemsControlMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Find the TextBlock that was clicked
            var element = e.OriginalSource as FrameworkElement;
            if (element == null)
            {
                return;
            }

            var itemsControl = AssociatedObject;
            int clickedIndex = -1;

            // Walk up the visual tree to find the item container
            var current = element;
            while (current != null && current != itemsControl)
            {
                var index = itemsControl.Items.IndexOf(itemsControl.ItemContainerGenerator.ItemFromContainer(current));
                if (index >= 0)
                {
                    clickedIndex = index;
                    break;
                }
                current = VisualTreeHelper.GetParent(current) as FrameworkElement;
            }

            if (clickedIndex < 0)
            {
                return;
            }

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Ctrl+click: toggle selection
                if (_selectedIndices.Contains(clickedIndex))
                {
                    _selectedIndices.Remove(clickedIndex);
                }
                else
                {
                    _selectedIndices.Add(clickedIndex);
                }
            }
            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                // Shift+click: range selection
                if (_lastSelectedIndex >= 0)
                {
                    var start = Math.Min(_lastSelectedIndex, clickedIndex);
                    var end = Math.Max(_lastSelectedIndex, clickedIndex);
                    for (int i = start; i <= end; i++)
                    {
                        _selectedIndices.Add(i);
                    }
                }
                else
                {
                    _selectedIndices.Add(clickedIndex);
                }
            }
            else
            {
                // Regular click: single selection
                _selectedIndices.Clear();
                _selectedIndices.Add(clickedIndex);
            }

            _lastSelectedIndex = clickedIndex;
            UpdateVisualSelection();
            e.Handled = true;
        }

        private void OnItemsControlKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.C)
            {
                CopySelectedToClipboard();
                e.Handled = true;
            }
        }

        private void UpdateVisualSelection()
        {
            var itemsControl = AssociatedObject;
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
                if (container != null)
                {
                    var textBlock = FindTextBlock(container);
                    if (textBlock != null)
                    {
                        if (_selectedIndices.Contains(i))
                        {
                            textBlock.Opacity = 0.7;
                            textBlock.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromArgb(50, 100, 149, 237));
                        }
                        else
                        {
                            textBlock.Opacity = 1.0;
                            textBlock.Background = null;
                        }
                    }
                }
            }
        }

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

        private void CopySelectedToClipboard()
        {
            var itemsControl = AssociatedObject;
            var selectedTexts = new List<string>();

            foreach (var index in _selectedIndices.OrderBy(x => x))
            {
                if (itemsControl.Items[index] is ContinueVS.Core.Types.TextBlockModel model && model.Text != null)
                {
                    selectedTexts.Add(model.Text);
                }
            }

            if (selectedTexts.Count > 0)
            {
                var combinedText = string.Join("\n", selectedTexts);
                System.Windows.Forms.Clipboard.SetText(combinedText);
            }
        }
    }
}
