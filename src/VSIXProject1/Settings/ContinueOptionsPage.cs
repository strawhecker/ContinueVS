using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace ContinueVS.Settings
{
    /// <summary>
    /// Options page surfaced under Tools → Options → Continue.
    /// Registered via <c>[ProvideOptionPage]</c> on <see cref="ContinueVSPackage"/>.
    /// </summary>
    internal sealed class ContinueOptionsPage : DialogPage
    {
        [Category("Autocomplete")]
        [DisplayName("Enable inline completions")]
        [Description("Show grey ghost-text inline completions as you type.")]
        public bool EnableInlineCompletions { get; set; } = true;

        [Category("Autocomplete")]
        [DisplayName("Debounce delay (ms)")]
        [Description("Milliseconds to wait after the last keystroke before requesting a completion.")]
        public int DebounceDelayMs { get; set; } = 150;

        [Category("Privacy")]
        [DisplayName("Disable anonymous telemetry")]
        [Description("Opt out of anonymous usage analytics sent to Continue Dev, Inc.")]
        public bool DisableTelemetry { get; set; } = false;

        [Category("Bridge")]
        [DisplayName("Continue Bridge Version")]
        [Description("Currently active npm-based Continue bridge version. Version changes require extension restart.")]
        [ReadOnly(true)]
        public string ActiveBridgeVersion { get; set; } = "2.0.0";
    }
}
