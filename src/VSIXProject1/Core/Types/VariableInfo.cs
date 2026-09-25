using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// A variable (local, argument, or "this" member) in a debug frame (gap92_2 inspection surface).
    /// Read-only snapshot; children are lazily-expanded enumerations for object-shaped variables.
    /// </summary>
    public sealed class VariableInfo
    {
        /// <summary>
        /// Variable name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Variable type string (best effort).
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Stringified value (best effort).
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// True when this variable cannot be written (e.g. "this", read-only locals).
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// True when the variable has nested members (object/array).
        /// </summary>
        public bool HasChildren { get; set; }

        /// <summary>
        /// Nested members, when expanded (capped by the interop).
        /// </summary>
        public List<VariableInfo> Children { get; set; } = new List<VariableInfo>();
    }
}
