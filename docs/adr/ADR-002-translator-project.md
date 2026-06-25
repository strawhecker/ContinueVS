# ADR-002 — ContinueTranslator: TypeScript-to-C# Source Translator

## Status
Accepted

## Context
The Continue message protocol and backend logic is written in TypeScript.
Manually translating and maintaining it across versions is not sustainable.

The VS Code VSIX bundle (`extension/out/extension.js`) is minified and unreadable.
However, Continue's full TypeScript source is available on GitHub under an open source license.

Source-to-source translation (TypeScript → C#) is proven technology (Haxe, Bridge.NET, etc.).
The TypeScript Compiler API (via ts-morph) provides full typed AST with cross-file resolution.
Roslyn SyntaxFactory can emit valid, formatted C# source.

## Decision
Build a **separate .NET 10 console tool** (`ContinueTranslator`) that:
1. Downloads Continue source from GitHub at a specific release tag.
2. Parses TypeScript using ts-morph (via Node.js subprocess).
3. Builds an intermediate representation (IR).
4. Applies human-maintained mapping tables (Node API, npm packages, types).
5. Emits C# source files and a .csproj via Roslyn.

Re-run per Continue release. Use `git diff output/` to review only the delta.

## Consequences
- Node.js must be available on the build machine (for ts-morph parsing).
- Mapping tables (`mappings/*.json`) are human-maintained and version-controlled.
- Unknown npm dependencies are emitted with `// TODO` markers for human resolution.
- The output `ContinueCore/` folder is gitignored and regenerated; reviewed before copy.
- See `docs/translator-design.md` for full structure.
