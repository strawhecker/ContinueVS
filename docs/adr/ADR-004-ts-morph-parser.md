# ADR-004 — Use ts-morph via Node.js Subprocess for TypeScript Parsing

## Status
Accepted

## Context
Two options were evaluated for parsing TypeScript source in ContinueTranslator:

| Option | Pros | Cons |
|---|---|---|
| ANTLR4 + TS grammar (pure C#) | No Node.js dependency | Grammar lags modern TS; no type resolution across files |
| ts-morph via Node.js subprocess | Official compiler; full type resolution; handles generics, conditional types, decorators | Requires Node.js on build machine |

The Continue source uses modern TypeScript features including generics, conditional types,
mapped types, and decorator syntax. ANTLR4's TypeScript grammar does not reliably handle all
of these. More critically, ANTLR4 cannot resolve types across file boundaries, which is
required to correctly translate imported interfaces and generic constraints.

## Decision
Use **ts-morph** (a wrapper over the official TypeScript Compiler API) via a Node.js subprocess.

A small `parse.mjs` script is embedded as a resource in `ContinueTranslator.Core`.
`TsParser.cs` extracts it, spawns `node.exe`, and reads the JSON AST from stdout.

## Consequences
- Node.js (any recent LTS version) must be installed on the build machine.
- `parse.mjs` has one npm dependency: `ts-morph` (installed via `npm install` in a temp dir,
  or bundled as a self-contained script using a pre-built bundle).
- The JSON AST emitted by `parse.mjs` is the contract between the Node.js and C# layers.
  Changes to the AST schema require updating both `parse.mjs` and `ContinueTranslator.Core/IR/`.
