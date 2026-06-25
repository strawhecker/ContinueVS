using ContinueVS.IPC;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ContinueVS.Editor
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class GhostTextControllerFactory : IWpfTextViewCreationListener
    {
        #pragma warning disable CS0649   // field assigned by MEF, never by code
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("ContinueGhostText")]
        [Order(After = PredefinedAdornmentLayers.Text)]
        public AdornmentLayerDefinition? AdornmentLayer;
#pragma warning restore CS0649

        public void TextViewCreated(IWpfTextView view)
        {
            view.Properties.GetOrCreateSingletonProperty(
                typeof(GhostTextController),
                () => new GhostTextController(view));
        }
    }

    internal sealed class GhostTextController
    {
        private readonly IWpfTextView    _view;
        private readonly IAdornmentLayer _layer;
        private CancellationTokenSource? _debounceCts;
        private string?  _pendingText;
        private string?  _pendingId;
        private bool     _subscribedKeys;
        private TextBlock? _adornmentBlock;

        public GhostTextController(IWpfTextView view)
        {
            _view  = view;
            _layer = view.GetAdornmentLayer("ContinueGhostText");
            view.TextBuffer.Changed    += OnBufferChanged;
            view.Caret.PositionChanged += OnCaretMoved;
            view.Closed                += OnViewClosed;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (e.EditTag is GhostTextAcceptTag) return;
            Dismiss();
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            _ = System.Threading.Tasks.Task.Delay(150, token)
                .ContinueWith(_ => RequestCompletionAsync(token),
                    token,
                    System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion,
                    System.Threading.Tasks.TaskScheduler.Default);
        }

        private async System.Threading.Tasks.Task RequestCompletionAsync(CancellationToken token)
        {
            var pkg = ContinueVSPackage.Instance;

            int lineNum = 0, colNum = 0;
            string filePath = "";

            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
            var snapshot = _view.TextBuffer.CurrentSnapshot;
            var caretPos = _view.Caret.Position.BufferPosition.Position;
            var snLine   = snapshot.GetLineFromPosition(caretPos);
            lineNum  = snLine.LineNumber;
            colNum   = caretPos - snLine.Start.Position;
            if (_view.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument doc))
                filePath = doc.FilePath;

            if (token.IsCancellationRequested) return;

            var completionId = Guid.NewGuid().ToString();
            var input = new AutocompleteInput
            {
                CompletionId   = completionId,
                IsUntitledFile = string.IsNullOrEmpty(filePath),
                Filepath       = filePath,
                Pos            = new Position { Line = lineNum, Character = colNum },
            };

            try
            {
                token.ThrowIfCancellationRequested();

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                var pane = pkg?.FindToolWindow(typeof(ContinueVS.UI.ContinueToolWindowPane), 0, false)
                    as ContinueVS.UI.ContinueToolWindowPane;
                if (pane == null) return;

                var reply = await pane.SendToGuiAndAwaitReplyAsync("autocomplete/complete", input, token)
                    .ConfigureAwait(false);

                var completions = reply?.ToObject<string[]>();
                var text = completions != null && completions.Length > 0 ? completions[0] : null;
                if (string.IsNullOrEmpty(text)) return;

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                _pendingText = text;
                _pendingId = completionId;
                RenderGhostText();
            }
            catch (OperationCanceledException) { }
        }

        private void RenderGhostText()
        {
            if (string.IsNullOrEmpty(_pendingText)) return;
            _layer.RemoveAllAdornments();

            var caretBuf = _view.Caret.Position.BufferPosition;
            var line     = _view.GetTextViewLineContainingBufferPosition(caretBuf);
            if (line == null) return;

            var bounds = line.GetCharacterBounds(caretBuf);
            var ff     = _view.FormattedLineSource.DefaultTextProperties.Typeface.FontFamily;
            var fs     = _view.FormattedLineSource.DefaultTextProperties.FontRenderingEmSize;

            _adornmentBlock = new TextBlock
            {
                Text             = _pendingText,
                FontFamily       = ff,
                FontSize         = fs,
                Foreground       = new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(_adornmentBlock, bounds.Right);
            Canvas.SetTop(_adornmentBlock,  bounds.TextTop);
            _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative,
                new SnapshotSpan(caretBuf, 0), null, _adornmentBlock, null);
            SubscribeKeys();
        }

        private void SubscribeKeys()
        {
            if (_subscribedKeys) return;
            _subscribedKeys = true;
            _view.VisualElement.PreviewKeyDown += OnKeyDown;
        }

        private void UnsubscribeKeys()
        {
            if (!_subscribedKeys) return;
            _subscribedKeys = false;
            _view.VisualElement.PreviewKeyDown -= OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_pendingText == null) return;
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                AcceptCompletion();
            }
            else if (e.Key == Key.Escape)
            {
                Dismiss(); NotifyOutcome(false);
            }
            else if (!IsModifier(e.Key))
            {
                Dismiss(); NotifyOutcome(false);
            }
        }

        private static bool IsModifier(Key k) =>
            k == Key.LeftShift || k == Key.RightShift ||
            k == Key.LeftCtrl  || k == Key.RightCtrl  ||
            k == Key.LeftAlt   || k == Key.RightAlt;

        private void AcceptCompletion()
        {
            if (string.IsNullOrEmpty(_pendingText)) return;
            var text = _pendingText; var id = _pendingId;
            Dismiss();
            using (var edit = _view.TextBuffer.CreateEdit(EditOptions.None, null, new GhostTextAcceptTag()))
            {
                edit.Insert(_view.Caret.Position.BufferPosition.Position, text);
                edit.Apply();
            }
            NotifyOutcome(true, id);
        }

        private void Dismiss()
        {
            _pendingText = null; _pendingId = null;
            _layer.RemoveAllAdornments();
            _adornmentBlock = null;
            UnsubscribeKeys();
        }

        private void OnCaretMoved(object sender, CaretPositionChangedEventArgs e)
        {
            if (_pendingText != null) Dismiss();
        }

        private void NotifyOutcome(bool accepted, string? id = null)
        {
            var pkg = ContinueVSPackage.Instance;

            var resolvedId = id ?? _pendingId ?? "";
            Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var pane = pkg?.FindToolWindow(typeof(ContinueVS.UI.ContinueToolWindowPane), 0, false)
                    as ContinueVS.UI.ContinueToolWindowPane;
                if (pane == null) return;
                if (accepted)
                    pane.SendToGui("autocomplete/accept", new { completionId = resolvedId });
                else
                    pane.SendToGui("autocomplete/cancel", new object());
            }).FileAndForget("vs/continuevs/autocomplete/outcome");
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            _view.TextBuffer.Changed    -= OnBufferChanged;
            _view.Caret.PositionChanged -= OnCaretMoved;
            _view.Closed                -= OnViewClosed;
            _debounceCts?.Cancel();
            UnsubscribeKeys();
        }
    }

    internal sealed class GhostTextAcceptTag { }
}
