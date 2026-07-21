# Manual Testing Guide for ContinueVS Bridge v2.0.0

**Purpose**: Comprehensive QA reference for manual validation of all 20 handlers before release.  
**Last Updated**: 2024-01-15  
**Version**: 1.0  
**Target**: Release Gate Step 115 (Part III completion)

---

## Table of Contents

1. [Setup & Prerequisites](#setup--prerequisites)
2. [Handler Test Matrix](#handler-test-matrix)
3. [Integration Workflows](#integration-workflows)
4. [Performance & Regression Validation](#performance--regression-validation)
5. [Release Readiness Checklist Reference](#release-readiness-checklist-reference)
6. [Quick Reference Appendix](#quick-reference-appendix)

---

## Setup & Prerequisites

### Environment Requirements

- **Node.js**: v18+ (LTS)
- **.NET SDK**: 4.7.2+ (for IDE bridge)
- **Visual Studio**: 2026 or later (with ContinueVS extension enabled)
- **Continue npm package**: v2.0.0 (downloaded and verified at Step 35)

### Bridge Startup Verification

Before testing handlers, verify the bridge is operational:

```bash
# Terminal 1: Start the bridge
npm run bridge:start

# Expected output:
# Bridge listening on stdio
# Health check interval: 5000ms
# Ready to accept requests
```

```bash
# Terminal 2: Verify health (curl)
curl -X POST http://localhost:5173/health \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"bridge:health","params":{},"id":1}'

# Expected response:
# {"jsonrpc":"2.0","result":{"status":"ready","uptime":1234},"id":1}
```

### Test Message Format

All messages conform to **JSON-RPC 2.0** specification:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:handlerName",
  "params": { "param1": "value1" },
  "id": 1
}
```

**Success Response**:
```json
{
  "jsonrpc": "2.0",
  "result": { "data": "..." },
  "id": 1
}
```

**Error Response**:
```json
{
  "jsonrpc": "2.0",
  "error": { "code": -32602, "message": "Invalid params" },
  "id": 1
}
```

### Performance Measurement Setup

Use `curl` + `jq` for latency validation:

```bash
# Measure p99 latency (100 requests)
for i in {1..100}; do
  time curl -s -X POST http://localhost:5173/rpc \
    -H "Content-Type: application/json" \
    -d '{"jsonrpc":"2.0","method":"bridge:getEditorState","params":{},"id":'$i'}' \
    | jq '.result' > /dev/null
done
```

### Expected Performance Gates (from Step 112 Regression)

- **Factory handlers** (refactor, fix, apply, format): p99 <100ms
- **Subscription handlers** (editor state, terminal): p99 <2s (first event)
- **Bidirectional handlers** (search, go-to-def, find-refs): p99 <500ms
- **Analysis handlers** (completion, hover, test): p99 <200ms
- **Error rate**: <1% across all handlers
- **Memory**: <50MB baseline, +0 growth over 30s sustained load

---

## Handler Test Matrix

### Category 1: Factory Handlers (6 handlers)

Factory handlers apply transformations to code and return results synchronously.

---

#### 1.1 bridge:refactor (Step 76)

**Description**: Apply automated refactoring to selected code.  
**Typical Use**: Extract method, rename symbol, move statement.

**Success Scenario**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:refactor",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "range": { "start": 42, "end": 65 },
    "refactoringType": "extractMethod",
    "newName": "ValidateInput"
  },
  "id": 1
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "success": true,
    "newCode": "private void ValidateInput() { ... }",
    "affectedLines": [42, 65],
    "previewDiff": "- old code\n+ new code"
  },
  "id": 1
}
```

**Error Scenario** (invalid range):

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:refactor",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "range": { "start": 9999, "end": 10000 },
    "refactoringType": "extractMethod"
  },
  "id": 2
}
```

**Expected Error**:

```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32602,
    "message": "Range out of bounds"
  },
  "id": 2
}
```

**Performance Gate**: p99 <100ms  
**Edge Cases**:
- Large method (500+ lines): p99 <150ms
- Concurrent refactor requests: Non-blocking, queued
- Missing file: Error code -32603 (parse error)

**Integration Note**: Calls Step 78 (apply-edit) after user confirmation.

---

#### 1.2 bridge:fixSuggestion (Step 77)

**Description**: Generate fix suggestion for diagnostic.  
**Typical Use**: Auto-fix warnings, error corrections.

**Success Scenario**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:fixSuggestion",
  "params": {
    "filePath": "C:/project/src/app.cs",
    "line": 15,
    "diagnosticCode": "CS0168",
    "diagnosticMessage": "Variable assigned but never used"
  },
  "id": 3
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "fix": "Remove variable declaration",
    "newCode": "// Variable removed",
    "severity": "warning",
    "applicableRanges": [15]
  },
  "id": 3
}
```

**Performance Gate**: p99 <100ms  
**Edge Cases**:
- Unknown diagnostic code: Error -32602
- File modified since parse: Cache miss, re-parse

---

#### 1.3 bridge:applyEdit (Step 78)

**Description**: Apply code edit to document.  
**Typical Use**: User acceptance of refactor/fix.

**Success Scenario**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:applyEdit",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "range": { "start": 42, "end": 65 },
    "newText": "private void ValidateInput() { /* new impl */ }"
  },
  "id": 4
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "success": true,
    "newLineCount": 10,
    "savedToFile": true
  },
  "id": 4
}
```

**Performance Gate**: p99 <100ms  
**Edge Cases**:
- File locked: Error -32603
- Out-of-order edits: Reject with conflict message

---

#### 1.4 bridge:formatDocument (Step 79)

**Description**: Format entire document.  
**Typical Use**: Apply code style.

**Success Scenario**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:formatDocument",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "style": "microsoft"
  },
  "id": 5
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "success": true,
    "linesFormatted": 150,
    "indentationFixed": true
  },
  "id": 5
}
```

**Performance Gate**: p99 <100ms (even for large files)  
**Edge Cases**:
- Unsupported language: Error -32602
- No changes needed: Return `{ "linesFormatted": 0 }`

---

#### 1.5 bridge:snippet (Step 91)

**Description**: Insert code snippet at cursor.  
**Typical Use**: Template expansion.

**Success Scenario**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:snippet",
  "params": {
    "filePath": "C:/project/src/app.cs",
    "line": 20,
    "snippetName": "tryForEach",
    "language": "csharp"
  },
  "id": 6
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "snippet": "try { foreach(var item in collection) { } } catch { }",
    "placeholders": [{"name": "collection", "line": 21}]
  },
  "id": 6
}
```

**Performance Gate**: p99 <100ms

---

#### 1.6 bridge:diffViewer (Step 92)

**Description**: Generate diff preview.  
**Typical Use**: Show before/after changes.

**Success Scenario**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:diffViewer",
  "params": {
    "originalCode": "var x = 5;",
    "newCode": "const x = 5;"
  },
  "id": 7
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "diff": "- var x = 5;\n+ const x = 5;",
    "linesDiff": 1,
    "linesAdded": 1,
    "linesRemoved": 1
  },
  "id": 7
}
```

**Performance Gate**: p99 <100ms

---

### Category 2: Subscription Handlers (4 handlers)

Subscription handlers stream events. Test first event, multi-event sequence, and cleanup.

---

#### 2.1 bridge:onEditorStateChange (Step 51)

**Description**: Subscribe to editor state changes.  
**Typical Use**: Keep Continue sidebar in sync with cursor position.

**Subscription Setup**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:onEditorStateChange",
  "params": {},
  "id": 100
}
```

**Expected First Event**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:onEditorStateChange",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "selection": { "start": 42, "end": 42 },
    "line": 42,
    "column": 15,
    "language": "csharp",
    "symbolAtCursor": "ValidateInput"
  }
}
```

**Performance Gate**: p99 <2s for first event  
**Edge Cases**:
- Multiple files open: Emit per active file
- Selection change: Immediate event (no debounce)
- Unsubscribe: Send `{ "method": "bridge:unsubscribe", "params": { "id": 100 } }`

---

#### 2.2 bridge:onTerminalOutput (Step 82)

**Description**: Subscribe to terminal output.  
**Typical Use**: Stream build/test output to Continue.

**Subscription Setup**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:onTerminalOutput",
  "params": { "terminalId": "build" },
  "id": 101
}
```

**Expected Events**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:onTerminalOutput",
  "params": {
    "terminalId": "build",
    "output": "Building project...\n",
    "timestamp": 1705328400
  }
}
```

**Performance Gate**: p99 <500ms per event  
**Edge Cases**:
- Terminal closed: Send termination event
- High output rate: Buffer, batch events

---

#### 2.3 bridge:gitStatus (Step 81, subscription variant)

**Description**: Subscribe to Git status changes.  
**Typical Use**: Track file modifications, staged changes.

**Subscription Setup**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:gitStatus",
  "params": { "repoPath": "C:/project" },
  "id": 102
}
```

**Expected Events**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:gitStatus",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "status": "modified",
    "staged": false
  }
}
```

**Performance Gate**: p99 <1s per status change

---

#### 2.4 bridge:debugSession (Step 61, subscription variant)

**Description**: Subscribe to debug session events.  
**Typical Use**: Breakpoint hits, variable inspection.

**Subscription Setup**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:debugSession",
  "params": {},
  "id": 103
}
```

**Expected Events**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:debugSession",
  "params": {
    "event": "breakpoint",
    "filePath": "C:/project/src/Service.cs",
    "line": 42,
    "variables": {
      "x": { "value": "5", "type": "int" }
    }
  }
}
```

**Performance Gate**: p99 <1s per event

---

### Category 3: Bidirectional Handlers (3 handlers)

Request-response with multi-step correlation and state consistency.

---

#### 3.1 bridge:search (Step 55)

**Description**: Search code.  
**Typical Use**: Find symbol references.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:search",
  "params": {
    "query": "ValidateInput",
    "scope": "workspace",
    "matchCase": false
  },
  "id": 200
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "results": [
      {
        "filePath": "C:/project/src/Service.cs",
        "line": 42,
        "column": 15,
        "context": "private void ValidateInput() {"
      }
    ],
    "totalResults": 1
  },
  "id": 200
}
```

**Performance Gate**: p99 <500ms  
**Edge Cases**:
- Empty results: Return `{ "results": [] }`
- Large workspace: Timeout after 30s, return partial results

---

#### 3.2 bridge:goToDefinition (Step 56)

**Description**: Navigate to symbol definition.  
**Typical Use**: Jump to function/class definition.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:goToDefinition",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "line": 42,
    "column": 15,
    "symbol": "ValidateInput"
  },
  "id": 201
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "filePath": "C:/project/src/Service.cs",
    "line": 10,
    "column": 15,
    "snippet": "private void ValidateInput() { ... }"
  },
  "id": 201
}
```

**Performance Gate**: p99 <500ms  
**Edge Cases**:
- Cross-file navigation: Verify file opened
- External library: Return null if unavailable

---

#### 3.3 bridge:findReferences (Step 57)

**Description**: Find all references to symbol.  
**Typical Use**: Refactor awareness (how many usages?).

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:findReferences",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "line": 10,
    "column": 15,
    "symbol": "ValidateInput"
  },
  "id": 202
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "references": [
      { "filePath": "C:/project/src/app.cs", "line": 99 },
      { "filePath": "C:/project/src/Service.cs", "line": 42 }
    ],
    "totalReferences": 2
  },
  "id": 202
}
```

**Performance Gate**: p99 <500ms

---

### Category 4: Analysis & UI Handlers (4 handlers)

Data accuracy, context validation, performance under load.

---

#### 4.1 bridge:codeCompletion (Step 58)

**Description**: Generate code completions.  
**Typical Use**: Autocomplete suggestions as user types.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:codeCompletion",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "line": 50,
    "column": 10,
    "prefix": "Valid"
  },
  "id": 300
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "completions": [
      {
        "label": "ValidateInput",
        "detail": "method",
        "documentation": "Validates input parameters",
        "sortText": "ValidateInput"
      }
    ]
  },
  "id": 300
}
```

**Performance Gate**: p99 <200ms  
**Edge Cases**:
- Large file: Latency may reach 250ms
- No matches: Return `{ "completions": [] }`

---

#### 4.2 bridge:hoverInfo (Step 59)

**Description**: Generate hover information.  
**Typical Use**: Type hints, docstrings.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:hoverInfo",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "line": 42,
    "column": 15
  },
  "id": 301
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "contents": "private void ValidateInput()",
    "documentation": "Validates input parameters against business rules",
    "signature": "ValidateInput(): void"
  },
  "id": 301
}
```

**Performance Gate**: p99 <200ms

---

#### 4.3 bridge:testExplorer (Step 60)

**Description**: List tests in workspace.  
**Typical Use**: Run/debug specific test.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:testExplorer",
  "params": {
    "filePath": "C:/project/tests/ServiceTests.cs"
  },
  "id": 302
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "tests": [
      {
        "name": "ValidateInput_WithValidData_Passes",
        "line": 10,
        "testFramework": "xUnit"
      }
    ]
  },
  "id": 302
}
```

**Performance Gate**: p99 <200ms

---

#### 4.4 bridge:inlineMessage (Step 85)

**Description**: Display inline message at line.  
**Typical Use**: AI suggestions, warnings.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:inlineMessage",
  "params": {
    "filePath": "C:/project/src/Service.cs",
    "line": 50,
    "message": "Consider renaming this variable for clarity",
    "severity": "info"
  },
  "id": 303
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "messageId": "msg_12345",
    "displayed": true
  },
  "id": 303
}
```

**Performance Gate**: p99 <200ms

---

### Category 5: Metadata & Config Handlers (3 handlers)

State mutation, persistence, concurrent operation safety.

---

#### 5.1 bridge:loadSettings (Step 95)

**Description**: Load bridge configuration.  
**Typical Use**: Initialize bridge, load user preferences.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:loadSettings",
  "params": {},
  "id": 400
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "model": "gpt-4",
    "apiKey": "***",
    "enableTelemetry": true,
    "logLevel": "info"
  },
  "id": 400
}
```

**Performance Gate**: p99 <100ms

---

#### 5.2 bridge:applySettings (Step 95)

**Description**: Apply new settings.  
**Typical Use**: User changes config.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:applySettings",
  "params": {
    "model": "gpt-4-turbo",
    "enableTelemetry": false
  },
  "id": 401
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "applied": true,
    "requiresRestart": false
  },
  "id": 401
}
```

**Performance Gate**: p99 <100ms  
**Edge Cases**:
- Invalid setting value: Error -32602
- Requires restart: Return `{ "requiresRestart": true }`

---

#### 5.3 bridge:workspaceReload (Step 94)

**Description**: Reload workspace.  
**Typical Use**: After project structure change.

**Request**:

```json
{
  "jsonrpc": "2.0",
  "method": "bridge:workspaceReload",
  "params": {},
  "id": 402
}
```

**Expected Response**:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "reloaded": true,
    "filesScanned": 150
  },
  "id": 402
}
```

**Performance Gate**: p99 <2s  
**Edge Cases**:
- Large workspace: May take 3–5s (acceptable)
- Error during reload: Return error, rollback to previous state

---

## Integration Workflows

Test real-world multi-handler scenarios.

### Workflow 1: Context → Completion → Accept

**Scenario**: User opens file, types partial symbol, selects completion, refactor happens.

**Steps**:

1. **Emit editor context**:
   ```json
   {
     "method": "bridge:onEditorStateChange",
     "params": { "filePath": "C:/project/src/app.cs", "line": 50 }
   }
   ```

2. **Request completion**:
   ```json
   { "method": "bridge:codeCompletion", "params": { "prefix": "Valid" }, "id": 1 }
   ```

3. **User selects completion** → Handler completion accepted.

4. **Hover to confirm**:
   ```json
   { "method": "bridge:hoverInfo", "params": { "line": 50 }, "id": 2 }
   ```

**Validation**: All events fire in <2s total. State consistent across handlers.

---

### Workflow 2: Search → Go-To-Definition → Find References → Edit

**Scenario**: Find symbol, navigate, understand usage, refactor.

**Steps**:

1. **Search**:
   ```json
   { "method": "bridge:search", "params": { "query": "ValidateInput" }, "id": 10 }
   ```

2. **Go to definition** (from search result):
   ```json
   { "method": "bridge:goToDefinition", "params": { "symbol": "ValidateInput" }, "id": 11 }
   ```

3. **Find references**:
   ```json
   { "method": "bridge:findReferences", "params": { "symbol": "ValidateInput" }, "id": 12 }
   ```

4. **Refactor**:
   ```json
   { "method": "bridge:refactor", "params": { "refactoringType": "rename", "newName": "CheckInput" }, "id": 13 }
   ```

**Validation**: Multi-file navigation consistent. No cross-handler conflicts.

---

### Workflow 3: Refactor → Format → Diff → Apply

**Scenario**: Suggest refactor, format, preview, user accepts.

**Steps**:

1. **Refactor**:
   ```json
   { "method": "bridge:refactor", "params": { "refactoringType": "extractMethod" }, "id": 20 }
   ```

2. **Format result**:
   ```json
   { "method": "bridge:formatDocument", "params": { "style": "microsoft" }, "id": 21 }
   ```

3. **Generate diff**:
   ```json
   { "method": "bridge:diffViewer", "params": { "originalCode": "...", "newCode": "..." }, "id": 22 }
   ```

4. **Apply**:
   ```json
   { "method": "bridge:applyEdit", "params": { "newText": "..." }, "id": 23 }
   ```

**Validation**: State preserved across handlers. No data loss.

---

### Workflow 4: Load Settings → Change Model → Workspace Reload → Verify

**Scenario**: User changes AI model config.

**Steps**:

1. **Load current settings**:
   ```json
   { "method": "bridge:loadSettings", "params": {}, "id": 30 }
   ```

2. **Apply new settings**:
   ```json
   { "method": "bridge:applySettings", "params": { "model": "gpt-4-turbo" }, "id": 31 }
   ```

3. **Reload workspace**:
   ```json
   { "method": "bridge:workspaceReload", "params": {}, "id": 32 }
   ```

4. **Verify settings persisted**:
   ```json
   { "method": "bridge:loadSettings", "params": {}, "id": 33 }
   ```

**Validation**: Settings persisted. Reload completes successfully. New model active.

---

## Performance & Regression Validation

### Regression Gates (from Step 112)

**Baseline Performance**:

| Handler Type | p99 Latency | Error Rate | Memory |
|---|---|---|---|
| Factory (refactor, fix, apply, format) | <100ms | <1% | <10MB |
| Subscription (first event) | <2s | <1% | <15MB |
| Bidirectional (search, nav) | <500ms | <1% | <20MB |
| Analysis (completion, hover) | <200ms | <1% | <10MB |
| Metadata (settings, reload) | <100ms (≤2s for reload) | <1% | <10MB |

### Validation Procedure

1. **Measure p99 latency** (100 requests per handler):
   ```bash
   for i in {1..100}; do
     time curl -s -X POST http://localhost:5173/rpc \
       -H "Content-Type: application/json" \
       -d '{"jsonrpc":"2.0","method":"bridge:codeCompletion","params":{},"id":'$i'}' \
   done 2>&1 | grep real | sort -t'm' -k2 -n | tail -1
   ```

2. **Calculate error rate** (failed / total requests):
   ```bash
   passed=0; failed=0
   for i in {1..100}; do
     response=$(curl -s -X POST http://localhost:5173/rpc \
       -H "Content-Type: application/json" \
       -d '{"jsonrpc":"2.0","method":"bridge:codeCompletion","params":{},"id":'$i'}')
     if echo "$response" | jq -e '.result' > /dev/null 2>&1; then
       ((passed++))
     else
       ((failed++))
     fi
   done
   echo "Error rate: $(echo "scale=2; $failed * 100 / ($passed + $failed)" | bc)%"
   ```

3. **Monitor memory** (30s sustained load):
   ```bash
   # Start bridge in separate terminal with top
   top -p <bridge_pid> -d 1 | grep RES | head -30 | tail -1
   ```

4. **Isolation test** (one handler error doesn't cascade):
   - Send invalid request to one handler
   - Send valid request to different handler
   - Verify second handler responds normally

### Acceptance Criteria

✅ **Pass**: All handlers meet p99 gate + error rate <1% + memory stable  
❌ **Fail**: Any handler exceeds p99 gate OR error rate ≥1%

---

## Release Readiness Checklist Reference

See [RELEASE-READINESS-CHECKLIST.md](./RELEASE-READINESS-CHECKLIST.md) for formal sign-off procedure.

**Quick Checklist**:

- [ ] All 20 handlers pass manual testing (green in handler matrix)
- [ ] Performance gates met (p99 <gate, error <1%)
- [ ] Regression report PASS (Step 112, zero CRITICAL)
- [ ] E2E scenarios complete (Step 110, all workflows ✅)
- [ ] Compliance tests pass (Step 97, 10 dimensions × 20 handlers)
- [ ] Stress tests pass (Step 99, all 4 scenarios)
- [ ] Troubleshooting guide available (Step 114)

**Decision**: GO / GO-with-conditions / NO-GO (sign-off required)

---

## Quick Reference Appendix

### JSON-RPC Error Codes

| Code | Meaning | Action |
|------|---------|--------|
| -32600 | Invalid Request | Check JSON syntax, method name |
| -32601 | Method not found | Verify handler name (bridge:xxx) |
| -32602 | Invalid params | Check parameter types and ranges |
| -32603 | Internal error | Check bridge logs, restart if needed |
| -32700 | Parse error | Check JSON encoding |

### Common Failures & Remediation

| Failure | Cause | Fix |
|---------|-------|-----|
| Connection refused | Bridge not running | `npm run bridge:start` |
| Timeout (>5s) | Slow handler or stuck process | Restart bridge, check resource usage |
| Null response | Missing file or context | Verify file path, ensure context loaded |
| State inconsistency | Race condition | Add 100ms delay between multi-handler requests |

### Terminal Testing One-Liners

**Test bridge health**:
```bash
curl -X POST http://localhost:5173/rpc -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"bridge:health","params":{},"id":1}' | jq '.result'
```

**Test code completion**:
```bash
curl -X POST http://localhost:5173/rpc -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"bridge:codeCompletion","params":{"prefix":"Va"},"id":1}' | jq '.result.completions'
```

**Test refactor**:
```bash
curl -X POST http://localhost:5173/rpc -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"bridge:refactor","params":{"filePath":"app.cs","refactoringType":"rename"},"id":1}' | jq '.result'
```

### Expected Response Shapes

**Success (any handler)**:
```json
{ "jsonrpc": "2.0", "result": { "data": "..." }, "id": 1 }
```

**Error**:
```json
{ "jsonrpc": "2.0", "error": { "code": -32602, "message": "..." }, "id": 1 }
```

**Subscription event**:
```json
{ "jsonrpc": "2.0", "method": "bridge:onEventName", "params": { "data": "..." } }
```

---

## Next Steps

1. **Execute all test scenarios** from handler matrix (Sections 1–5).
2. **Validate performance gates** against Step 112 baselines.
3. **Test all 4 integration workflows** (Section 3).
4. **Fill in RELEASE-READINESS-CHECKLIST.md** with results.
5. **Gate approval** by QA Lead + Release Manager (Step 115).

**Version**: 1.0  
**Last Updated**: 2024-01-15  
**Status**: Ready for QA execution
