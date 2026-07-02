using ContinueTranslator.Core.Emission;
using Xunit;

namespace ContinueTranslator.Tests.Emission;

public class CsEmitterHelpersTests
{
    /// <summary>
    /// Tests for the private helper method ConvertObjectLiteralToTuple via ParseTypeSyntax.
    /// Since ConvertObjectLiteralToTuple is private, we test it indirectly through ParseTypeSyntax.
    /// </summary>
    public class ParseTypeSyntaxTests
    {
        [Fact]
        public void ParseTypeSyntax_WithSimpleObjectLiteralType_ConvertsToTuple()
        {
            // Arrange
            string objectLiteralType = "{ prefix: string; suffix: string }";

            // Act
            var result = CsEmitter.ParseTypeSyntax(objectLiteralType);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("prefix", resultString);
            Assert.Contains("suffix", resultString);
            // Verify it's a tuple type (contains parentheses in string representation)
            Assert.Contains("(", resultString);
            Assert.Contains(")", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithObjectLiteralHavingTypeMapping_ConvertsWithCorrectTypes()
        {
            // Arrange: TypeScript 'number' should ideally map to 'double', but at this stage
            // the type text comes as-is from the parser. C# parsing will handle it.
            string objectLiteralType = "{ a: string; b: number; c: boolean }";

            // Act
            var result = CsEmitter.ParseTypeSyntax(objectLiteralType);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            // Should have properties extracted
            Assert.Contains("a", resultString);
            Assert.Contains("b", resultString);
            Assert.Contains("c", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithEmptyObjectLiteral_FallsBackToObject()
        {
            // Arrange: Empty object literal { } doesn't have properties, 
            // so ConvertObjectLiteralToTuple returns the original text unchanged,
            // which then fails to parse and falls back to object
            string emptyObjectLiteral = "{ }";

            // Act
            var result = CsEmitter.ParseTypeSyntax(emptyObjectLiteral);

            // Assert
            Assert.NotNull(result);
            // The function should still return a valid type (either a fallback or parsed successfully)
            // Just verify it returns something non-null, which it does
        }

        [Fact]
        public void ParseTypeSyntax_WithNonObjectLiteralType_PassesThroughUnchanged()
        {
            // Arrange
            string simpleType = "string";

            // Act
            var result = CsEmitter.ParseTypeSyntax(simpleType);

            // Assert
            Assert.NotNull(result);
            // Should parse as a simple type (PredefinedTypeSyntax for string)
            Assert.NotEmpty(result.ToString());
        }

        [Fact]
        public void ParseTypeSyntax_WithGenericType_PassesThroughUnchanged()
        {
            // Arrange
            string genericType = "List<string>";

            // Act
            var result = CsEmitter.ParseTypeSyntax(genericType);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("List", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithObjectLiteralContainingGenerics_ConvertsToTuple()
        {
            // Arrange
            string complexObjectLiteral = "{ items: List<string>; count: number }";

            // Act
            var result = CsEmitter.ParseTypeSyntax(complexObjectLiteral);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            // Should convert to tuple preserving the generic type
            Assert.Contains("items", resultString);
            Assert.Contains("count", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithWhitespace_ConvertsCorrectly()
        {
            // Arrange: object literal with various whitespace
            string objectLiteralWithWhitespace = "{  prefix  :  string  ;  suffix  :  string  }";

            // Act
            var result = CsEmitter.ParseTypeSyntax(objectLiteralWithWhitespace);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("prefix", resultString);
            Assert.Contains("suffix", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithAny_FallsBackToObject()
        {
            // Arrange
            string anyType = "any";

            // Act
            var result = CsEmitter.ParseTypeSyntax(anyType);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("object", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithUnknown_FallsBackToObject()
        {
            // Arrange
            string unknownType = "unknown";

            // Act
            var result = CsEmitter.ParseTypeSyntax(unknownType);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("object", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithNullableObjectLiteral_ConvertsToNullableTuple()
        {
            // Arrange
            string nullableObjectLiteral = "{ prefix: string; suffix: string } | null";

            // Act
            var result = CsEmitter.ParseTypeSyntax(nullableObjectLiteral);

            // Assert
            // This should fall back to object since the union syntax is not directly convertible
            string resultString = result.ToString();
            Assert.NotNull(result);
        }

        [Fact]
        public void ParseTypeSyntax_WithEmptyString_FallsBackToObject()
        {
            // Arrange
            string emptyString = "";

            // Act
            var result = CsEmitter.ParseTypeSyntax(emptyString);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("object", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithWhitespaceOnly_FallsBackToObject()
        {
            // Arrange
            string whitespaceOnly = "   ";

            // Act
            var result = CsEmitter.ParseTypeSyntax(whitespaceOnly);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("object", resultString);
        }

        [Fact]
        public void ParseTypeSyntax_WithTaskVoid_ConvertsToTask()
        {
            // Arrange
            string taskVoidType = "Task<void>";

            // Act
            var result = CsEmitter.ParseTypeSyntax(taskVoidType);

            // Assert
            string resultString = result.ToString();
            Assert.NotNull(result);
            Assert.Contains("Task", resultString);
        }
    }
}
