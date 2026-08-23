using ContinueVS.ViewModels;

namespace ContinueVS.ViewModels.Models
{
    /// <summary>
    /// Represents a selectable chat mode option for the mode dropdown (gap27_1).
    /// </summary>
    public class ModeOption
    {
        /// <summary>Gets the display name shown in the ComboBox.</summary>
        public string Name { get; }

        /// <summary>Gets the underlying ChatMode enum value.</summary>
        public ChatMode Value { get; }

        /// <summary>Gets a brief description of the mode's behavior.</summary>
        public string Description { get; }

        /// <summary>Gets a short icon/emoji label for the mode.</summary>
        public string Icon { get; }

        /// <summary>
        /// Initializes a new instance of ModeOption.
        /// </summary>
        public ModeOption(string name, ChatMode value, string description, string icon)
        {
            Name = name;
            Value = value;
            Description = description;
            Icon = icon;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Icon} {Name} ({Value})";
    }
}
