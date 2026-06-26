using System.Text.Json.Serialization;

namespace ContinueTranslator.Core.IR;

internal sealed record TsObjectProperty(string Name, TsExpression? Value);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TsCallExpression), "Call")]
[JsonDerivedType(typeof(TsMemberExpression), "Member")]
[JsonDerivedType(typeof(TsAwaitExpression), "Await")]
[JsonDerivedType(typeof(TsBinaryExpression), "Binary")]
[JsonDerivedType(typeof(TsLiteralExpression), "Literal")]
[JsonDerivedType(typeof(TsIdentifierExpression), "Identifier")]
[JsonDerivedType(typeof(TsObjectLiteralExpression), "ObjectLiteral")]
[JsonDerivedType(typeof(TsConditionalExpression), "Conditional")]
[JsonDerivedType(typeof(TsArrowExpression), "Arrow")]
[JsonDerivedType(typeof(TsUnaryExpression), "Unary")]
[JsonDerivedType(typeof(TsUnknownExpression), "Unknown")]
internal abstract record TsExpression;

internal sealed record TsCallExpression(TsExpression Callee, TsExpression[] Args) : TsExpression;

internal sealed record TsMemberExpression(
    [property: JsonPropertyName("object")] TsExpression Obj,
    string Property) : TsExpression;

internal sealed record TsAwaitExpression(TsExpression Expression) : TsExpression;

internal sealed record TsBinaryExpression(string Op, TsExpression Left, TsExpression Right) : TsExpression;

internal sealed record TsLiteralExpression(string Value) : TsExpression;

internal sealed record TsIdentifierExpression(string Name) : TsExpression;

internal sealed record TsObjectLiteralExpression(TsObjectProperty[] Properties) : TsExpression;

internal sealed record TsConditionalExpression(TsExpression Condition, TsExpression WhenTrue, TsExpression WhenFalse) : TsExpression;

internal sealed record TsArrowExpression(TsParameter[] Parameters, TsStatement[] Body) : TsExpression;

internal sealed record TsUnaryExpression(string Op, TsExpression Operand) : TsExpression;

internal sealed record TsUnknownExpression(string Text) : TsExpression;
