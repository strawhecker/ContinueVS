# ContinueTranslator — Project Design

## Purpose

Source-to-source translator: **Continue TypeScript source → C# source**.
Targets Continue's GitHub repository at a specific release tag.
Output is a valid C# project that can be referenced by VSIXProject1.

## Solution location

`E:\DevStudio\ContinueTranslator\`  (separate from VSIXProject1)

## Project structure

```
ContinueTranslator/
├── ContinueTranslator.slnx
│
├── src/
│   ├── ContinueTranslator.Cli/          .NET 8 console — entry point
│   │   └── Program.cs
│   │
│   ├── ContinueTranslator.Core/         .NET 8 class library
│   │   ├── Acquisition/
│   │   │   └── SourceFetcher.cs         downloads + extracts GitHub tag ZIP
│   │   ├── Parsing/
│   │   │   ├── TsParser.cs              spawns node.exe, runs parse.mjs, reads JSON
│   │   │   └── parse.mjs                embedded resource — ts-morph AST → JSON
│   │   ├── IR/                          Intermediate Representation
│   │   │   ├── TsFile.cs
│   │   │   ├── TsClass.cs
│   │   │   ├── TsInterface.cs
│   │   │   ├── TsEnum.cs
│   │   │   ├── TsFunction.cs
│   │   │   └── TsTypeRef.cs
│   │   ├── Mapping/
│   │   │   ├── MappingEngine.cs         orchestrates all maps
│   │   │   ├── NodeApiMap.cs            reads node-api.json, applies substitutions
│   │   │   ├── NpmPackageMap.cs         reads npm-packages.json
│   │   │   └── TypeMap.cs               reads types.json
│   │   └── Emission/
│   │       ├── CsEmitter.cs             Roslyn SyntaxFactory — emits .cs files
│   │       └── ProjectEmitter.cs        generates ContinueCore.csproj
│   │
│   └── ContinueTranslator.Tests/        xUnit
│
├── mappings/                            human-edited, version controlled
│   ├── node-api.json
│   ├── npm-packages.json
│   └── types.json
│
└── output/                              gitignored — regenerated each run
    └── ContinueCore/
        ├── ContinueCore.csproj
        ├── Protocol/
        └── ...
```

## Pipeline

```
1. SourceFetcher   → download ZIP from github.com/continuedev/continue/archive/refs/tags/v{tag}.zip
2. Filter          → include: core/**/*.ts   exclude: *.test.ts, extensions/vscode/**, gui/**
3. TsParser        → spawn node parse.mjs → JSON AST (ts-morph, full type resolution)
4. IR Builder      → JSON AST → TsFile / TsClass / TsInterface / TsEnum / TsFunction
5. MappingEngine   → apply NodeApiMap + NpmPackageMap + TypeMap; mark unknowns with TODO
6. CsEmitter       → Roslyn SyntaxFactory → .cs files (partial classes, < 400 lines each)
7. ProjectEmitter  → ContinueCore.csproj with NuGet refs discovered during mapping
```

## CLI usage

```
dotnet run -- --tag v2.0.0 --out E:\DevStudio\ContinueTranslator\output
dotnet run -- --tag v2.1.0 --out E:\DevStudio\ContinueTranslator\output
# then: git diff output/ to review delta
```

## Mapping tables (sample)

### mappings/node-api.json
```json
{
  "fs.readFile":   "System.IO.File.ReadAllTextAsync",
  "fs.writeFile":  "System.IO.File.WriteAllTextAsync",
  "fs.exists":     "System.IO.File.Exists",
  "path.join":     "System.IO.Path.Combine",
  "path.dirname":  "System.IO.Path.GetDirectoryName",
  "process.env":   "System.Environment.GetEnvironmentVariable",
  "console.log":   "System.Diagnostics.Debug.WriteLine"
}
```

### mappings/npm-packages.json
```json
{
  "axios":         "System.Net.Http.HttpClient",
  "node-fetch":    "System.Net.Http.HttpClient",
  "ws":            "System.Net.WebSockets.ClientWebSocket",
  "uuid":          "System.Guid",
  "events":        "System.EventHandler"
}
```

### mappings/types.json
```json
{
  "string":        "string",
  "number":        "double",
  "boolean":       "bool",
  "any":           "object",
  "unknown":       "object",
  "void":          "void",
  "Promise<T>":    "Task<T>",
  "Array<T>":      "List<T>",
  "Record<K,V>":   "Dictionary<K,V>"
}
```

## Parsing dependency

Requires **Node.js** on the build machine (for ts-morph).
See ADR-004 for the decision rationale.
