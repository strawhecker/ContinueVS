using System.Text.Json.Serialization;

namespace ContinueTranslator.Core.IR;

internal sealed record TsObjectProperty(string Name, TsExpression? Value);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TsCallExpression), "Call")]
[JsonDerivedType(typeof(TsNewExpression), "New")]
[JsonDerivedType(typeof(TsMemberExpression), "Member")]
[JsonDerivedType(typeof(TsAwaitExpression), "Await")]
[JsonDerivedType(typeof(TsBinaryExpression), "Binary")]
[JsonDerivedType(typeof(TsLiteralExpression), "Literal")]
[JsonDerivedType(typeof(TsIdentifierExpression), "Identifier")]
[JsonDerivedType(typeof(TsObjectLiteralExpression), "ObjectLiteral")]
[JsonDerivedType(typeof(TsArrayLiteralExpression), "ArrayLiteral")]
[JsonDerivedType(typeof(TsConditionalExpression), "Conditional")]
[JsonDerivedType(typeof(TsArrowExpression), "Arrow")]
[JsonDerivedType(typeof(TsUnaryExpression), "Unary")]
[JsonDerivedType(typeof(TsTypeOfExpression), "TypeOf")]
[JsonDerivedType(typeof(TsTemplateExpression), "Template")]
[JsonDerivedType(typeof(TsUnknownExpression), "Unknown")]
[JsonDerivedType(typeof(TsElementAccessExpression), "ElementAccess")]
[JsonDerivedType(typeof(TsAsExpression), "As")]
[JsonDerivedType(typeof(TsRegexExpression), "Regex")]
[JsonDerivedType(typeof(TsSpreadElement), "Spread")]
[JsonDerivedType(typeof(TsYieldExpression), "Yield")]
internal abstract record TsExpression;

/// <summary>One interpolated span: the expression inside <c>${…}</c> and the literal tail that follows it.</summary>
internal sealed record TsTemplateSpan(TsExpression Expression, string Tail);

internal sealed record TsCallExpression(TsExpression Callee, TsExpression[] Args) : TsExpression;

internal sealed record TsNewExpression(TsExpression Type, TsExpression[] Args) : TsExpression;

internal sealed record TsMemberExpression(
    [property: JsonPropertyName("object")] TsExpression Obj,
    string Property) : TsExpression;

internal sealed record TsAwaitExpression(TsExpression Expression) : TsExpression;

internal sealed record TsBinaryExpression(string Op, TsExpression Left, TsExpression Right) : TsExpression;

internal sealed record TsLiteralExpression(string Value) : TsExpression;

internal sealed record TsRegexExpression(string Pattern, string Flags) : TsExpression;

internal sealed record TsIdentifierExpression(string Name) : TsExpression;

internal sealed record TsObjectLiteralExpression(TsObjectProperty[] Properties) : TsExpression;

internal sealed record TsArrayLiteralExpression(TsExpression[] Elements) : TsExpression;

internal sealed record TsConditionalExpression(TsExpression Condition, TsExpression WhenTrue, TsExpression WhenFalse) : TsExpression;

internal sealed record TsArrowExpression(TsParameter[] Parameters, TsStatement[] Body, bool IsAsync = false, bool IsGenerator = false) : TsExpression;

internal sealed record TsUnaryExpression(string Op, TsExpression Operand) : TsExpression;

internal sealed record TsTypeOfExpression(TsExpression Expression) : TsExpression;

/// <summary>
/// A TypeScript template literal with one or more interpolated expressions,
/// e.g. <c>`Hello ${name}, you are ${age} years old`</c>.
/// <see cref="Head"/> is the literal text before the first <c>${</c>.
/// Each <see cref="TsTemplateSpan"/> carries the interpolated expression and the literal tail after its closing <c>}</c>.
/// </summary>
internal sealed record TsTemplateExpression(string Head, TsTemplateSpan[] Spans) : TsExpression;

internal sealed record TsElementAccessExpression(
    [property: JsonPropertyName("object")] TsExpression Obj,
    TsExpression Index) : TsExpression;

internal sealed record TsAsExpression(TsExpression Expression, string Type) : TsExpression;

internal sealed record TsUnknownExpression(string Text) : TsExpression;

/// <summary>Represents a spread element in function arguments or array literals: <c>...expr</c>.</summary>
internal sealed record TsSpreadElement(TsExpression Expression) : TsExpression;

/// <summary>Represents a yield expression: <c>yield expr</c> or <c>yield* expr</c>.</summary>
internal sealed record TsYieldExpression(TsExpression? Expression, bool Delegate = false) : TsExpression;
