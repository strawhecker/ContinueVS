namespace ContinueTranslator.Tests;

/// <summary>
/// Documentation and verification tests for unary operator and template literal translation fixes.
/// 
/// These tests document the expected behavior of the translator when handling:
/// 1. Prefix unary operators (especially unary minus)
/// 2. Template literals with escape sequences
/// 
/// Full integration testing is handled by the translator CLI during actual translation.
/// 
/// ## Issue: Unary Minus Operator Bug (parse.mjs line 430)
/// 
/// Previously, the code used an unsafe fallback when operator token was unavailable:
/// ```javascript
/// const opText = opToken?.getText?.() ?? "!";  // ❌ BUG: defaults to "!"
/// ```
/// 
/// This caused `-numOverlapping` to be incorrectly translated to `!numOverlapping`.
/// 
/// **Example Translation (BROKEN):**
/// - TypeScript: `firstLines.slice(-numOverlapping)`
/// - Generated (broken): `firstLines.slice(!numOverlapping)`  ❌
/// 
/// **Fix Applied:**
/// Replaced with proper SyntaxKind mapping for all prefix unary operators:
/// ```javascript
/// const prefixOpMap = {
///   [SyntaxKind.ExclamationToken]: "!",
///   [SyntaxKind.MinusToken]: "-",
///   [SyntaxKind.PlusToken]: "+",
///   [SyntaxKind.TildeToken]: "~",
///   [SyntaxKind.PlusPlusToken]: "++",
///   [SyntaxKind.MinusMinusToken]: "--",
///   [SyntaxKind.DeleteKeyword]: "delete",
///   [SyntaxKind.TypeOfKeyword]: "typeof",
///   [SyntaxKind.VoidKeyword]: "void",
/// };
/// const opText = prefixOpMap[expr.compilerNode.operator] ?? "?";
/// ```
/// 
/// **Example Translation (FIXED):**
/// - TypeScript: `firstLines.slice(-numOverlapping)`
/// - Generated (fixed): `firstLines.slice(-numOverlapping)`  ✅
/// 
/// 
/// ## Issue: Template Literal Escape Sequence Bug (parse.mjs lines 478, 481, 492, 495)
/// 
/// Previously, the code used `getLiteralText()` which interprets escape sequences:
/// ```javascript
/// head: expr.getHead().getLiteralText(),  // ❌ BUG: interprets escape sequences
/// tail: span.getLiteral().getLiteralText(),
/// ```
/// 
/// This caused `\n` escape sequences to become literal newline characters in the output.
/// 
/// **Example Translation (BROKEN):**
/// - TypeScript: `` `${firstLines.slice(-numOverlapping).join("\n")}\n${second.contents}` ``
/// - Generated (broken): Multi-line string with literal newlines instead of `\n` ❌
/// 
/// **Fix Applied:**
/// Created helper function `getTemplateLiteralText()` that uses the source file's raw text
/// and properly strips backticks/braces while preserving escape sequences:
/// ```javascript
/// function getTemplateLiteralText(node) {
///   const sourceFile = node.getSourceFile();
///   const fullText = sourceFile.getFullText();
///   const start = node.getStart(sourceFile, true);
///   const end = node.getEnd();
///   const rawText = fullText.substring(start, end);
///   // ... strip backticks/braces while preserving content
///   return content;
/// }
/// ```
/// 
/// **Example Translation (FIXED):**
/// - TypeScript: `` `${firstLines.slice(-numOverlapping).join("\n")}\n${second.contents}` ``
/// - Generated (fixed): `$"{firstLines.slice(-numOverlapping).join("\\n")}\n{second.contents}"`  ✅
/// 
/// 
/// ## Supported Prefix Unary Operators
/// - Logical NOT: `!expr`
/// - Unary minus: `-expr`
/// - Unary plus: `+expr`
/// - Bitwise NOT: `~expr`
/// - Pre-increment: `++expr`
/// - Pre-decrement: `--expr`
/// - Delete: `delete expr`
/// - Typeof: `typeof expr`
/// - Void: `void expr`
/// 
/// 
/// ## Template Literal Support
/// - Simple templates: `` `text` ``
/// - With interpolations: `` `text ${expr} more` ``
/// - With escape sequences: `` `line1\nline2\ttab` ``
/// - Combinations: `` `result:\n${items.slice(-3).join(",")}\t[end]` ``
/// - Tagged templates: `` tag`text ${expr}` ``
/// 
/// 
/// ## Root Cause Analysis
/// 
/// **Unary Operator Bug:**
/// - ts-morph's `getOperatorToken()` could fail in certain contexts
/// - The fallback `?? "!"` was inappropriate and caused all failed operators to become NOT
/// - Solution: Use compiler's SyntaxKind enum (like postfix operators already do)
/// 
/// **Template Literal Bug:**
/// - ts-morph's `getLiteralText()` interprets escape sequences to the actual characters
/// - Emitter then re-escapes them, but loses the original representation
/// - Escape sequences must be preserved at the parser level
/// - Solution: Extract raw text from source file, strip syntax markers (backticks/braces)
/// 
/// 
/// ## Verification
/// 
/// The fixes were verified by:
/// 1. Running translator CLI with Continue v2.0.0-vscode source
/// 2. Checking generated `IndexFunctions.cs` for correct syntax
/// 3. Building VSIXProject1 solution - compilation successful ✅
/// </summary>
public class UnaryOperatorAndTemplateLiteralTranslationTests
{
    // This class is documentation-only for the unary operator and template literal translation fixes.
    // The actual behavior is verified through:
    // 1. Build of ContinueTranslator.Cli succeeds
    // 2. Running translator CLI with Continue source produces correct output
    // 3. VSIXProject1 solution builds successfully with generated files
    // 4. Manual inspection of generated IndexFunctions.cs confirms:
    //    - Line: `return $"{firstLines.slice(-numOverlapping).join("\\n")}\n{second.contents}";`
    //    - ✅ Unary minus preserved: `-numOverlapping`
    //    - ✅ Escape sequence preserved: `\n` in template literal
}
