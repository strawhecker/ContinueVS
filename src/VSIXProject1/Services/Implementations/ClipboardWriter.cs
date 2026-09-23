using System;
using System.Windows;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// gap89: view-state-aware clipboard writer. Places both clipboard formats on a single
    /// <see cref="DataObject"/> in pretty mode and only <see cref="DataFormats.UnicodeText"/>
    /// in raw mode. The receiving application picks its preferred format — no destination
    /// detection is ever performed.
    ///
    ///   Raw    → UnicodeText only  (byte-faithful source for editors/VS).
    ///   Pretty → UnicodeText + RTF (rich render for Word/WordPad + plain for code editors).
    ///
    /// The model stays single-sourced (gap88): both formats are derived from the same raw
    /// <c>ChatMessage.Content</c>; nothing is copied between representations.
    /// </summary>
    public static class ClipboardWriter
    {
        /// <summary>
        /// Copies a message's raw content to the clipboard honoring the per-card view mode.
        /// </summary>
        /// <param name="message">The bound message whose <see cref="ChatMessage.Content"/> is the
        /// single source of truth and whose <see cref="ChatMessage.ViewMode"/> selects the format.</param>
        /// <returns>True when the write succeeded; false if the message content was null/empty.</returns>
        public static bool Copy(ChatMessage? message)
        {
            if (message == null)
                return false;

            return Copy(message.Content, message.ViewMode);
        }

        /// <summary>
        /// Copies raw content to the clipboard in the selected view mode.
        /// </summary>
        public static bool Copy(string? raw, MessageViewMode viewMode)
        {
            if (string.IsNullOrEmpty(raw))
                return false;

            DataObject data;
            try
            {
                data = BuildDataObject(raw!, viewMode);
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                // true = the data survives app shutdown (clipboard ownership).
                Clipboard.SetDataObject(data, true);
                return true;
            }
            catch (Exception)
            {
                // Clipboard access can fail (CLIPBRD_E_CANT_OPEN, STA/WPF hosting).
                return false;
            }
        }

        /// <summary>
        /// Builds the clipboard <see cref="DataObject"/> for the given raw content and view mode.
        /// Exposed for testability (format inspection) without touching the system clipboard.
        ///
        ///   Raw    → UnicodeText only.
        ///   Pretty → UnicodeText + RTF on the same object.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when raw is null/empty.</exception>
        public static DataObject BuildDataObject(string raw, MessageViewMode viewMode)
        {
            if (string.IsNullOrEmpty(raw))
                throw new ArgumentException("Cannot build a DataObject from empty content.", nameof(raw));

            var data = new DataObject();

            if (viewMode == MessageViewMode.Pretty)
            {
                // Pretty (reader) → rich RTF + co-shipped plain. Office/code-editor parity.
                data.SetData(DataFormats.Rtf, RtfExporter.ToRtf(raw));
                data.SetData(DataFormats.UnicodeText, raw);
            }
            else
            {
                // Raw (destination) → byte-faithful plain only.
                data.SetData(DataFormats.UnicodeText, raw);
            }

            return data;
        }
    }
}
