# TODO — Ordered Action List

One item per agent session. Commit after each item. Remove completed items.

## Fork workflow

Source is read from a **local clone of a personal fork** of continuedev/continue.
To update to a newer upstream version:
```
cd <fork-clone-path>
git fetch upstream
git merge upstream/vX.Y.Z
# resolve conflicts — @ct: cookies on stable lines survive automatically
git push origin
```
Translation annotations (`@ct:` cookies) are placed directly in the fork's TS source and
are version-controlled alongside the code they annotate.

Cookie syntax:
```
// @ct:map=System.Net.Http.HttpClient
// @ct:ignore
// @ct:rename=GetFileContents
// @ct:nuget=System.Net.Http
```

## Repo structure (current — TODO-033 complete, TODO-034 onward pending)

```
ContinueVS/
├── src/
│   ├── VSIXProject1/                ← VSIX (net472)
│   │   └── Generated/               ← translator .cs output — committed as source, not binary
│   ├── output/                      ← last translator run output (ContinueCore.csproj + stubs)
│   └── tools/
│       ├── ContinueTranslator.Cli/
│       ├── ContinueTranslator.Core/
│       └── ContinueTranslator.Tests/
├── docs/
│   ├── TODO.md
│   ├── translator-design.md
│   ├── architecture.md
│   ├── protocol.md
│   └── AGENTS.md
├── adr/
├── .gitignore                       ← bin/ obj/ out/ excluded; Generated/ must NOT be excluded
├── ContinueVS.slnx                  ← all four projects under /src/ folder
└── README.md
```

`ContinueVS.slnx` (current):
```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/VSIXProject1/VSIXProject1.csproj" />
    <Project Path="src/tools/ContinueTranslator.Cli/ContinueTranslator.Cli.csproj" />
    <Project Path="src/tools/ContinueTranslator.Core/ContinueTranslator.Core.csproj" />
    <Project Path="src/tools/ContinueTranslator.Tests/ContinueTranslator.Tests.csproj" />
  </Folder>
</Solution>
```

**Why bin/ and obj/ are excluded from the repo:** build outputs are reproducible and machine-specific (obj/ embeds absolute paths). The `.vsix` is a release artifact published to the Marketplace. Exception: translator-generated `.cs` files are source — commit them under `src/VSIXProject1/Generated/`.

---

TODO-048 [Trans] — Gap 2 (expression emitter): Implement CsEmitter.Expressions.cs as a new partial of CsEmitter. Translate each TsExpression IR node to a Roslyn ExpressionSyntax. For TsCallExpression nodes, consult the injected CallSiteMap first; if a .NET replacement is found emit it verbatim, otherwise emit the original identifier chain. For untranslatable expression kinds, emit a string literal comment placeholder that keeps the file compilable.

---

TODO-049 [Trans] — Gap 2 (statement emitter): Implement CsEmitter.Statements.cs as a new partial of CsEmitter. Translate each TsStatement IR node to a Roslyn StatementSyntax using CsEmitter.Expressions.cs for sub-expressions. Handle Return, If (with optional else), For, ForOf (foreach), While, Try/catch, Var (local variable declaration), ExpressionStatement, and Throw. For untranslatable statement kinds, emit a // TODO: untranslatable — <tsFilePath> comment statement.

---

TODO-050 [Trans] — Gap 2 (wiring): Update BuildClassMethods (CsEmitter.Classes.cs) and BuildFunctionStub (CsEmitter.Functions.cs) to call the statement emitter when Body is non-empty. When the body is empty or contains only untranslatable nodes, fall back to the // TODO: <tsFilePath> :: <Type>.<Method> comment stub introduced in TODO-043. Remove the old throw new NotImplementedException() fallback entirely.

---

TODO-051 [Trans] — Sync (core): Add SyncResult record and GeneratedFolderSync.cs in a new ContinueTranslator.Core/Sync/ folder. GeneratedFolderSync.Sync() takes the emitted file list and the Generated/ directory path. It loads .translator-manifest.json (SHA-256 keyed by relative path), skips files whose Generated/ copy differs from the translator's last-written hash (hand-edited), skips files whose content still contains // TODO stubs or raw TS type leaks, writes the rest, updates the manifest, and returns a SyncResult with counts for promoted, skipped-manual-edit, and skipped-has-stubs.

---

TODO-052 [Trans] — Sync (CLI): Add --generated <path> optional argument to Program.cs. Pass it through TranslationOptions. In PipelineRunner.Run(), add step 7: if GeneratedDirectory is set, call GeneratedFolderSync.Sync() and print the SyncResult counts to stdout. Document the argument in the usage message.

---

TODO-053 [Trans] — Tests: ContinueTranslator.Tests is currently empty. Add xUnit tests covering: (a) CallSiteMap.TryResolve for known and unknown entries, (b) GeneratedFolderSync promotion, skip-manual-edit, and skip-has-stubs paths using a temp directory, (c) at least one round-trip test for the statement/expression emitter — a minimal TsMethod with a known body IR should produce the expected C# source text. These tests have no VS SDK dependency and run on dotnet test.

---

TODO-054 [Trans] — Re-run the translator after TODO-038–053 are complete. Review output/ for any remaining // TODO stubs in FileSystemIde.cs. Implement those stubs using System.IO.* (file exists, read, write, list directory, walk directory, get file stats). Run the translator with --generated to auto-promote the completed file to src/VSIXProject1/Generated/.

---

TODO-055 [Trans] — Re-run the translator after TODO-038–053 are complete. Review output/ for remaining // TODO stubs in MessageIde.cs. The _request-pattern methods should now be body-translated automatically. Implement any remaining stubs that require VS SDK access (DTE, IVsRunningDocumentTable, IVsStatusbar) directly in Generated/MessageIde.cs. The manifest protects hand-written bodies from being overwritten on the next translator run. Depends on TODO-053 and TODO-037.

