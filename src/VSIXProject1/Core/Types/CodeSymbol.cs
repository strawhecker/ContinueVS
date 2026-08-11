using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration of code symbol kinds.
    /// </summary>
    public enum CodeSymbolKind
    {
        /// <summary>
        /// Class symbol.
        /// </summary>
        Class,

        /// <summary>
        /// Method or function symbol.
        /// </summary>
        Method,

        /// <summary>
        /// Property symbol.
        /// </summary>
        Property,

        /// <summary>
        /// Field or variable symbol.
        /// </summary>
        Variable,

        /// <summary>
        /// Namespace or module symbol.
        /// </summary>
        Namespace,

        /// <summary>
        /// Interface symbol.
        /// </summary>
        Interface,

        /// <summary>
        /// Enum symbol.
        /// </summary>
        Enum,

        /// <summary>
        /// Struct symbol.
        /// </summary>
        Struct,

        /// <summary>
        /// Event symbol.
        /// </summary>
        Event,

        /// <summary>
        /// Other or unknown symbol kind.
        /// </summary>
        Other
    }

    /// <summary>
    /// Represents a code symbol parsed from the codebase.
    /// </summary>
    public class CodeSymbol
    {
        /// <summary>
        /// Name of the symbol.
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Kind of symbol (class, method, property, etc.).
        /// </summary>
        [JsonProperty("kind")]
        public CodeSymbolKind Kind { get; set; }

        /// <summary>
        /// File path where the symbol is defined.
        /// </summary>
        [JsonProperty("filePath")]
        public string? FilePath { get; set; }

        /// <summary>
        /// Line number where the symbol appears (1-based).
        /// </summary>
        [JsonProperty("lineNumber")]
        public int LineNumber { get; set; }

        /// <summary>
        /// Ending line number of the symbol (1-based).
        /// </summary>
        [JsonProperty("lineEnd")]
        public int LineEnd { get; set; }

        /// <summary>
        /// Signature or declaration of the symbol.
        /// </summary>
        [JsonProperty("signature")]
        public string? Signature { get; set; }

        /// <summary>
        /// Documentation or comments associated with the symbol.
        /// </summary>
        [JsonProperty("documentation")]
        public string? Documentation { get; set; }

        /// <summary>
        /// Timestamp when this symbol was indexed.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
