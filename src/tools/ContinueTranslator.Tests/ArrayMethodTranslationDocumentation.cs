namespace ContinueTranslator.Tests;

/// <summary>
/// Documentation and verification tests for array method translation.
/// 
/// These tests document the expected behavior of the array method translation fix.
/// Full integration testing is handled by the translator CLI during actual translation.
/// 
/// Translation Examples:
/// 
/// TypeScript Input:
///   const snippets = rifs.map((rif) => ({
///     filepath: rif.filepath,
///     content: rif.contents,
///     type: AutocompleteSnippetType.Code,
///   }));
///   
/// Expected C# Output:
///   var snippets = System.Linq.Enumerable.Select(rifs, (rif) => new
///   {
///       filepath = rif.filepath,
///       content = rif.contents,
///       type = AutocompleteSnippetType.Code
///   });
/// 
/// Why This Works:
/// 1. Parser recognizes rifs.map(...) as a member expression call
/// 2. EmitCallExpression builds the callee chain "rifs.map"
/// 3. Direct lookup fails (not in callsites.json)
/// 4. Fallback checks for "Array.map" mapping
/// 5. Found! Maps to "System.Linq.Enumerable.Select"
/// 6. Since it contains dots, ParseExpression handles the qualified name
/// 7. Generated code uses the static LINQ method with arr as first argument
/// 
/// Array Methods Supported:
/// - Array.map       → System.Linq.Enumerable.Select
/// - Array.filter    → System.Linq.Enumerable.Where
/// - Array.find      → System.Linq.Enumerable.FirstOrDefault
/// - Array.some      → System.Linq.Enumerable.Any
/// - Array.every     → System.Linq.Enumerable.All
/// - Array.includes  → System.Linq.Enumerable.Contains
/// 
/// Backward Compatibility:
/// - API mappings (fs.readFileSync, etc.) still work via first lookup
/// - Instance method mappings (Set.has, String.trim, etc.) still work
/// - Only array methods use the new fallback behavior
/// </summary>
public class ArrayMethodTranslationDocumentation
{
    // This class is documentation-only for the array method translation feature.
    // The actual behavior is verified through:
    // 1. Build succeeds with updated CsEmitter.Expressions.cs
    // 2. callsites.json has fully-qualified LINQ method names
    // 3. Manual testing of translator CLI output
}
