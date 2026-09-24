#nullable enable

using ContinueVS.Core.Types;
using Xunit;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelGap89Tests
    {
        // gap89: The raw/processed view toggle was moved to the message-card control
        // (ChatMessageControl.RawToggleButton) and removed from the tool card (and its
        // now-removed ToggleRawViewCommand). Raw is a persistent per-card view state that
        // is shown only for markdown-capable Assistant/Thinking cards.
    }
}
