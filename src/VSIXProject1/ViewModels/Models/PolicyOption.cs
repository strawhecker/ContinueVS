using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Models
{
    /// <summary>
    /// Represents a selectable continuation policy option for the policy dropdown (gap27_11).
    /// </summary>
    public class PolicyOption
    {
        /// <summary>
        /// Gets the display name shown in the ComboBox.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the underlying ContinuationPolicy enum value.
        /// </summary>
        public ContinuationPolicy Value { get; }

        /// <summary>
        /// Gets a brief description of the policy's behavior.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets a short icon/emoji label for the policy.
        /// </summary>
        public string Icon { get; }

        /// <summary>
        /// Initializes a new instance of PolicyOption.
        /// </summary>
        public PolicyOption(string name, ContinuationPolicy value, string description, string icon)
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
