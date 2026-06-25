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

TODO items for both projects are listed below in dependency order. Each session: one item, commit, remove.

---

TODO-034 [VSIX] — Fix ContinueVSPackage.cs Dispose method: the base.Dispose(disposing); line and the three closing braces that follow it are all indented at 20 spaces instead of the correct 8/4/0. Reformat to correct indentation so the file is structurally valid.

---

TODO-035 [VSIX] — Honor ContinueOptionsPage in GhostTextController. At the start of RequestCompletionAsync, read the options page via ContinueVSPackage.Instance?.GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage and return early if EnableInlineCompletions is false. Replace the hard-coded 150 in OnBufferChanged with options?.DebounceDelayMs ?? 150.

---

TODO-036 [VSIX] — Surface LLM errors to the user. In LlmCompleteHandler and LlmStreamChatHandler, catch HttpRequestException specifically and call _control.SendToGui("showToast", new { message = "Continue: LLM request failed — " + ex.Message, type = "error" }) before returning the empty reply, so the user sees actionable feedback in the GUI.

---

TODO-037 [VSIX] — Make AutocompleteCompleteHandler produce real completions. Extract the AutocompleteInput from message.Data, build a prompt string from filepath and pos, call ContinueConfigReader.FindModel("") then LlmHttpClient.CompleteAsync, and reply with a string[] containing the single result. This closes the ghost text round-trip. Depends on TODO-036.

---

TODO-038 [Trans] — Fix translator emitter: map Task<void> → Task everywhere in output (TypeScript Promise<void> → Task). Change is in CsEmitter.Helpers.cs ParseTypeSyntax and/or MappingEngine type resolution.

---

TODO-039 [Trans] — Fix translator emitter: map TypeScript union return types (T | null) → nullable C# reference types (T?) in method signatures. Handle in MappingEngine.ResolveTypeRef by detecting " | null" suffix and rewriting to "T?".

---

TODO-040 [Trans] — Fix translator emitter: drop TypeScript inline object/index-signature parameter types (e.g., { [key: string]: string }) and emit Dictionary<string, string> instead. Detect in BuildParameterList via a regex on the raw type text; substitute Dictionary<string, string> and annotate with a // @ct:todo comment.

---

TODO-041 [Trans] — Fix translator emitter: convert TypeScript arrow-function property declarations into proper C# Func<> or event members. In CsEmitter.Classes.cs BuildClassProperties, detect when a property type text contains "=>" and rewrite to the matching Func<> or Action<> delegate type.

---

TODO-042 [Trans] — Add @ct:ignore cookies to the 27 test/vendor TypeScript files in the fork (all files under Vendor/, and all *.vitest.ts / *.test.ts files that were not already excluded by RepoScanner). Re-run the translator to confirm those 27 files and their 114 stubs are eliminated from output/.

---

TODO-043 [Trans] — Replace throw new NotImplementedException() fallback in BuildFunctionStub (CsEmitter.Functions.cs) and BuildClassMethods (CsEmitter.Classes.cs) with a single-line comment stub: // TODO: <tsFilePath> :: <ClassName>.<MethodName>. This keeps every output file compilable and provides an exact source address for human implementers.

---

TODO-044 [Trans] — Gap 3: Create mappings/callsites.json mapping Node.js call expressions to .NET equivalents (e.g., fs.readFileSync → File.ReadAllText, path.join → Path.Combine, crypto.randomUUID → Guid.NewGuid().ToString, os.homedir → Environment.GetFolderPath). Add CallSiteMap.cs in ContinueTranslator.Core/Mapping/ to load this file and expose TryResolve(callee, out string dotNetCall). Wire into MappingEngine so call-site names are resolved at the same pass as type names.

---

TODO-045 [Trans] — Gap 1 (parse.mjs — statements): Extend walkMethod() and walkFunction() in parse.mjs to emit a body array of statement nodes. Each node carries: kind (one of Return, If, For, ForOf, While, Try, Var, ExpressionStatement, Throw) plus kind-specific fields (e.g., Return.expression, If.condition/thenStatements/elseStatements, Var.name/initializer, Try.tryStatements/catchStatements). Emit empty array when the body is absent or the method is abstract.

---

TODO-046 [Trans] — Gap 1 (parse.mjs — expressions): Extend parse.mjs to walk expression nodes referenced by the statement body. Each expression node carries: kind (one of Call, Member, Await, Binary, Literal, Identifier, ObjectLiteral, Conditional, Arrow) plus kind-specific fields (e.g., Call.callee/args, Member.object/property, Await.expression, Binary.op/left/right, ObjectLiteral.properties as name/value pairs). This completes the Gap 1 JSON IR that the C# emitter will consume.

---

TODO-047 [Trans] — Gap 2 (C# IR): Add TsStatement.cs and TsExpression.cs sealed record hierarchies in ContinueTranslator.Core/IR/ whose shapes match the JSON produced by the updated parse.mjs. Add a Body field (TsStatement[]) to TsMethod and TsFunction records. Update TsParser.cs deserialization (JsonSerializerOptions / source-gen attributes as needed) to populate Body from the new JSON fields.

---

TODO-048 [Trans] — Gap 2 (expression emitter): Implement CsEmitter.Expressions.cs as a new partial of CsEmitter. Translate each TsExpression IR node to a Roslyn ExpressionSyntax. For TsCallExpression nodes, consult CallSiteMap first; if a .NET replacement is found, emit it verbatim, otherwise emit the original identifier chain. For untranslatable expression kinds, emit a string literal comment placeholder that keeps the file compilable.

---

TODO-049 [Trans] — Gap 2 (statement emitter): Implement CsEmitter.Statements.cs as a new partial of CsEmitter. Translate each TsStatement IR node to a Roslyn StatementSyntax using CsEmitter.Expressions.cs for sub-expressions. Handle Return, If (with optional else), For, ForOf (foreach), While, Try/catch, Var (local variable declaration), ExpressionStatement, and Throw. For untranslatable statement kinds, emit a // TODO: untranslatable — <tsFilePath> comment statement.

---

TODO-050 [Trans] — Gap 2 (wiring): Update BuildClassMethods (CsEmitter.Classes.cs) and BuildFunctionStub (CsEmitter.Functions.cs) to call the statement emitter when Body is non-empty. When the body is empty or contains only untranslatable nodes, fall back to the // TODO: <tsFilePath> :: <Type>.<Method> comment stub introduced in TODO-043. Remove the old throw new NotImplementedException() fallback entirely.

---

TODO-051 [Trans] — Sync (core): Add SyncResult record and GeneratedFolderSync.cs in a new ContinueTranslator.Core/Sync/ folder. GeneratedFolderSync.Sync() takes the emitted file list and the Generated/ directory path. It loads .translator-manifest.json (SHA-256 keyed by relative path), skips files whose Generated/ copy differs from the translator's last-written hash (hand-edited), skips files whose content still contains // TODO stubs or raw TS type leaks, writes the rest, updates the manifest, and returns a SyncResult with counts for promoted, skipped-manual-edit, and skipped-has-stubs.

---

TODO-052 [Trans] — Sync (CLI): Add --generated <path> optional argument to Program.cs. Pass it through TranslationOptions. In PipelineRunner.Run(), add step 7: if GeneratedDirectory is set, call GeneratedFolderSync.Sync() and print the SyncResult counts to stdout. Document the argument in the usage message.

---

TODO-053 [Trans] — Re-run the translator after TODO-038–052 are complete. Review output/ for any remaining // TODO stubs in FileSystemIde.cs. Implement those stubs using System.IO.* (file exists, read, write, list directory, walk directory, get file stats). Run the translator with --generated to auto-promote the completed file to src/VSIXProject1/Generated/.

---

TODO-054 [Trans] — Re-run the translator after TODO-038–052 are complete. Review output/ for remaining // TODO stubs in MessageIde.cs. The _request-pattern methods should now be body-translated automatically. Implement any remaining stubs that require VS SDK access (DTE, IVsRunningDocumentTable, IVsStatusbar) directly in Generated/MessageIde.cs. The manifest protects hand-written bodies from being overwritten on the next translator run. Depends on TODO-052 and TODO-037.

