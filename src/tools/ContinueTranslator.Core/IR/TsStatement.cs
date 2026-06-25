using System.Text.Json.Serialization;

namespace ContinueTranslator.Core.IR;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TsReturnStatement), "Return")]
[JsonDerivedType(typeof(TsIfStatement), "If")]
[JsonDerivedType(typeof(TsForStatement), "For")]
[JsonDerivedType(typeof(TsForOfStatement), "ForOf")]
[JsonDerivedType(typeof(TsWhileStatement), "While")]
[JsonDerivedType(typeof(TsTryStatement), "Try")]
[JsonDerivedType(typeof(TsVarStatement), "Var")]
[JsonDerivedType(typeof(TsExpressionStatement), "ExpressionStatement")]
[JsonDerivedType(typeof(TsThrowStatement), "Throw")]
[JsonDerivedType(typeof(TsUnknownStatement), "Unknown")]
internal abstract record TsStatement;

internal sealed record TsReturnStatement(TsExpression? Expression) : TsStatement;

internal sealed record TsIfStatement(
    TsExpression? Condition,
    TsStatement[] ThenStatements,
    TsStatement[] ElseStatements) : TsStatement;

internal sealed record TsForStatement(
    string? Initializer,
    TsExpression? Condition,
    TsExpression? Incrementor,
    TsStatement[] Statements) : TsStatement;

internal sealed record TsForOfStatement(
    string? Variable,
    TsExpression? Expression,
    TsStatement[] Statements) : TsStatement;

internal sealed record TsWhileStatement(TsExpression? Condition, TsStatement[] Statements) : TsStatement;

internal sealed record TsTryStatement(TsStatement[] TryStatements, TsStatement[] CatchStatements) : TsStatement;

internal sealed record TsVarStatement(string Name, TsExpression? Initializer) : TsStatement;

internal sealed record TsExpressionStatement(TsExpression? Expression) : TsStatement;

internal sealed record TsThrowStatement(TsExpression? Expression) : TsStatement;

internal sealed record TsUnknownStatement(string Text) : TsStatement;
