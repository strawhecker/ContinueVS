using System.Text.Json;
using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Regression tests for ternary conditional expression translation.
///
/// Bug: TypeScript `condition ? a : b` was generating C# `condition ? a = b`
/// (the false-branch colon ':' was being emitted as assignment '=').
/// </summary>
public class ConditionalExpressionTranslationTests
{
    private CsEmitter CreateEmitter()
    {
        var callSitesPath = Path.Combine(Path.GetTempPath(), $"test_callsites_{Guid.NewGuid():N}.json");
        var usingsPath = Path.Combine(Path.GetTempPath(), $"test_usings_{Guid.NewGuid():N}.json");

        File.WriteAllText(callSitesPath, "{}");
        File.WriteAllText(usingsPath, """{"Task":["System.Threading.Tasks"]}""");

        try
        {
            return new CsEmitter(new CallSiteMap(callSitesPath), new UsingsMap(usingsPath));
        }
        finally
        {
            File.Delete(callSitesPath);
            File.Delete(usingsPath);
        }
    }

    /// <summary>
    /// Verifies EmitExpression produces correct ternary syntax from a manually-built
    /// TsConditionalExpression IR object — this isolates the emitter from the parser.
    ///
    /// TypeScript: x === 2 ? a : b
    /// Expected C#: x == 2L ? a : b
    /// </summary>
    [Fact]
    public void EmitExpression_WithConditionalIR_ProducesColonNotEquals()
    {
        // Arrange — build IR directly, bypassing the parser entirely
        var ir = new TsConditionalExpression(
            Condition: new TsBinaryExpression("===",
                new TsIdentifierExpression("x"),
                new TsLiteralExpression("2")),
            WhenTrue:  new TsIdentifierExpression("a"),
            WhenFalse: new TsIdentifierExpression("b"));

        // Act
        var result = CreateEmitter().EmitExpression(ir).ToString();

        // Assert — must have ? and : in the right positions, no rogue =
        Assert.Contains("?", result);
        Assert.Contains(":", result);

        var qPos = result.IndexOf('?');
        var cPos = result.LastIndexOf(':');
        Assert.True(cPos > qPos, $"Colon must follow question mark. Got: {result}");

        // The false branch should NOT be an assignment expression
        Assert.DoesNotContain(" = b", result);
    }

    /// <summary>
    /// Verifies the round-trip: JSON deserialised from a correct "Conditional" payload
    /// is correctly reconstructed as TsConditionalExpression by System.Text.Json.
    ///
    /// This test isolates the deserialisation layer.
    /// </summary>
    [Fact]
    public void Deserialize_ConditionalJson_ProducesTsConditionalExpression()
    {
        // Arrange — the exact shape parse.mjs produces
        const string json = """
            {
              "kind": "Conditional",
              "condition": { "kind": "Identifier", "name": "x" },
              "whenTrue":  { "kind": "Identifier", "name": "a" },
              "whenFalse": { "kind": "Identifier", "name": "b" }
            }
            """;

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
        };

        // Act
        var expr = JsonSerializer.Deserialize<TsExpression>(json, opts);

        // Assert
        var cond = Assert.IsType<TsConditionalExpression>(expr);
        Assert.IsType<TsIdentifierExpression>(cond.Condition);
        Assert.IsType<TsIdentifierExpression>(cond.WhenTrue);
        Assert.IsType<TsIdentifierExpression>(cond.WhenFalse);
    }

    /// <summary>
    /// Full round-trip: deserialise → emit — ensures both layers work together.
    /// </summary>
    [Fact]
    public void RoundTrip_DeserialiseAndEmit_ProducesCorrectTernary()
    {
        // Arrange
        const string json = """
            {
              "kind": "Conditional",
              "condition": { "kind": "Identifier", "name": "x" },
              "whenTrue":  { "kind": "Identifier", "name": "a" },
              "whenFalse": { "kind": "Identifier", "name": "b" }
            }
            """;

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
        };

        // Act
        var expr = JsonSerializer.Deserialize<TsExpression>(json, opts)!;
        var result = CreateEmitter().EmitExpression(expr).ToString();

        // Assert
        Assert.Contains("?", result);
        Assert.Contains(":", result);
        Assert.DoesNotContain(" = b", result);

        var qPos = result.IndexOf('?');
        var cPos = result.LastIndexOf(':');
        Assert.True(cPos > qPos, $"Got: {result}");
    }
}
