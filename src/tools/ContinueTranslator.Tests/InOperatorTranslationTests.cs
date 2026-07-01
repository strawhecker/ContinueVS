namespace ContinueTranslator.Tests;

/// <summary>
/// Documentation and verification tests for TypeScript `in` operator translation.
/// 
/// These tests document the expected behavior of the `in` operator translation to C# HasProperty().
/// 
/// Translation Examples:
/// 
/// TypeScript Input:
///   "role" in message.content
///   
/// Expected C# Output:
///   HasProperty(message.content, "role")
/// 
/// Complex Pattern:
///   "role" in message.content && typeof message.content.role === "string"
///     ? message.content.role
///     : "assistant"
///     
/// Expected C# Output:
///   HasProperty(message.content, "role") && message.content.role is string
///     ? message.content.role
///     : "assistant"
/// 
/// Why This Works:
/// 1. Parser recognizes "role" in obj as a binary expression with op="in"
/// 2. EmitBinaryExpression detects bin.Op == "in"
/// 3. Extracts the string literal property name ("role")
/// 4. Emits InvocationExpression calling HasProperty(obj, "role")
/// 5. Sets _needsHasPropertyHelper = true flag
/// 6. CollectResults triggers BuildHasPropertyHelper() to emit HasProperty.cs
/// 7. HasProperty.cs uses reflection to check for public instance properties
/// 8. Handles null objects gracefully (returns false)
/// 
/// Pattern Support:
/// - Simple: "prop" in obj
/// - Combined with typeof: "prop" in obj && typeof obj.prop === "string"
/// - Chained: "role" in obj._meta && "role" in obj.content
/// - Nested: ("prop" in obj || "prop" in fallback) ? obj.prop : fallback.prop
/// 
/// The HasProperty helper uses reflection with:
/// - BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
/// - Null-safe: returns false for null objects
/// - Compatible with both .NET Framework 4.7.2 and .NET 10
/// </summary>
public class InOperatorTranslationTests
{
    // This class is documentation-only for the 'in' operator translation feature.
    // Full integration testing is handled by the translator CLI during actual translation.
}
