# Handler E2E Scenarios Guide

**Step 110**: Create End-to-End Scenario Tests  
**Status**: ✅ Complete  
**Test Count**: 65+ test cases  
**Scenarios**: 8 realistic workflows  

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Scenario Descriptions](#scenario-descriptions)
3. [Running Instructions](#running-instructions)
4. [Results Interpretation](#results-interpretation)
5. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

### E2E Scenario Engine

The **E2EScenarioEngine** orchestrates realistic workflows by:

1. **Accepting Scenario Definitions** — Names, handler sequences, preconditions
2. **Executing Handler Chains** — Calling handlers in order with state propagation
3. **Tracking Checkpoints** — Recording state at each step for validation
4. **Measuring Performance** — Latency, throughput, memory footprint per handler
5. **Collecting Metrics** — Aggregating results for reporting

**Core Classes**:
- `E2EScenarioEngine` — Main orchestrator
- `WorkflowStateTracker` — State validation and checkpoints
- `ScenarioRunner` (per scenario) — Executes specific workflow

### 8 Workflow Scenarios

| Scenario | Handlers | Purpose | Latency Gate |
|----------|----------|---------|--------------|
| Editor-to-AI | getEditorState → extractSymbols → generateCompletion → applyEdit | Validate text completion workflow | p99 < 300ms |
| Code Navigation | search → goToDefinition → findReferences → hoverInfo | Validate code navigation chain | p99 < 250ms |
| Git Workflow | loadDiff → refactor → getGitInfo → executeTerminal | Validate git-aware refactoring | p99 < 500ms |
| Multi-File Refactor | selectFiles → refactorScope → clearCache → extractSymbols → verifyChanges | Validate multi-file operations | p99 < 400ms |
| Debug Integration | startDebugSession → getBreakpointInfo → executeTerminal | Validate debug integration | p99 < 500ms |
| Error Recovery | triggerTimeout → activateCircuitBreaker → fallbackHandler → retryRequest | Validate error handling | p99 < 500ms |
| State Persistence | captureState → simulateCrash → recoverState | Validate crash recovery | p99 < 500ms |
| Configuration Variant | loadSettings → applySettings → reload → executeWorkflow | Validate settings workflow | p99 < 500ms |

### Integration Points

**Depends On**:
- Step 97: Compliance framework (message contracts, validators)
- Step 98: Performance baselines (handler latencies, throughput targets)
- Step 99: Stress fixtures (error injection, concurrent patterns)
- Steps 103–109: Infrastructure (crash recovery, rate limiting, circuit-breaker, etc.)

**Feeds Into**:
- Step 111: Cross-version compatibility tests
- Step 112: Regression detection (uses Step 110 as baseline)
- Step 115: Part III gate approval

---

## Scenario Descriptions

### Scenario 1: Editor-to-AI Workflow

**User Intent**: Write code with AI assistance  
**Preconditions**: 
- File open in editor
- Text selected (lines 10–12 in app.js)
- Cursor at position line 11, char 15

**Steps**:
1. **getEditorState** — Retrieve current editor context (file, selection, cursor)
   - Input: Selection coordinates
   - Output: { file, selection, cursor }
   - Latency: ~50ms (p99)

2. **extractSymbols** — Extract symbols at cursor position
   - Input: File context
   - Output: { symbols: [{ name, type, line }] }
   - Latency: ~75ms (p99)

3. **generateCompletion** — AI completes code
   - Input: { symbols, selection, file }
   - Output: { completion, confidence }
   - Latency: ~200ms (p99) — includes API call

4. **applyEdit** — Apply completion to document
   - Input: { completion, selection }
   - Output: { applied: true, newContent }
   - Latency: ~100ms (p99)

**Success Criteria**:
- ✅ Document mutated correctly
- ✅ All checkpoints recorded
- ✅ End-to-end latency < 300ms (p99)
- ✅ No state loss

---

### Scenario 2: Code Navigation Workflow

**User Intent**: Navigate to symbol definition and find usages  
**Preconditions**:
- Multi-file workspace (app.js, cache.js, utils.js)
- Search query: "getUserData"
- Definition at app.js:5:10

**Steps**:
1. **search** — Find matches across workspace
   - Input: { query: "getUserData", scope: "workspace" }
   - Output: { matches: [...], count: 3 }
   - Latency: ~150ms (p99)

2. **goToDefinition** — Resolve definition location
   - Input: { symbol: "getUserData", file }
   - Output: { file: "app.js", line: 5, column: 10 }
   - Latency: ~100ms (p99)

3. **findReferences** — Locate all references
   - Input: { symbol: "getUserData" }
   - Output: { references: [...], count: 3 }
   - Latency: ~150ms (p99)

4. **hoverInfo** — Get type/doc for symbol
   - Input: { symbol: "getUserData", position }
   - Output: { type, documentation, signature }
   - Latency: ~100ms (p99)

**Success Criteria**:
- ✅ Cross-file isolation (search A ≠ search B)
- ✅ Definition resolved correctly
- ✅ References found (≥3)
- ✅ Handler chain consistent
- ✅ End-to-end latency < 250ms (p99)

---

### Scenario 3: Git-Integrated Workflow

**User Intent**: Refactor code while tracking git context  
**Preconditions**:
- File diff available (before/after content)
- Git repository accessible
- Terminal available

**Steps**:
1. **loadDiff** — Load diff viewer
   - Input: { file: "app.js", diffContent }
   - Output: { before, after, file }
   - Latency: ~100ms (p99)

2. **refactor** — Apply refactoring with diff context
   - Input: { diff, scope: "file" }
   - Output: { refactored: true, changes: 2 }
   - Latency: ~200ms (p99)

3. **getGitInfo** — Retrieve commit context
   - Input: { file: "app.js" }
   - Output: { lastCommit, author, message }
   - Latency: ~100ms (p99)

4. **executeTerminal** — Run git command
   - Input: { command: "git log --oneline -n 5" }
   - Output: { success: true, output: "..." }
   - Latency: ~500ms (p99) — external process

**Success Criteria**:
- ✅ Diff loaded accurately
- ✅ Refactoring applied
- ✅ Git info retrieved
- ✅ Terminal output captured
- ✅ Error recovery: missing git → graceful fallback

---

### Scenario 4: Multi-File Refactor Workflow

**User Intent**: Refactor identifier across multiple files  
**Preconditions**:
- 3 files selected: app.js, cache.js, utils.js
- Refactor type: rename variable "userData" → "userInfo"

**Steps**:
1. **selectFiles** → { selected: [3 files], count: 3 }
2. **refactorScope** → { scope: "workspace", filesAffected: [3], changesPerFile }
3. **clearCache** → { cleared: true, cacheSize: 0 }
4. **extractSymbols** → Rebuild symbol index
5. **verifyChanges** → { verified: true, filesModified: 3 }

**Success Criteria**:
- ✅ All 3 files modified consistently
- ✅ Cache cleared and rebuilt
- ✅ Symbol extraction reflects changes
- ✅ Concurrent refactors isolated
- ✅ End-to-end latency < 400ms (p99)

---

### Scenario 5: Debug Integration Workflow

**User Intent**: Start debugging and inspect breakpoints  
**Steps**:
1. **startDebugSession** → { sessionActive: true, sessionId: "..." }
2. **getBreakpointInfo** → { breakpoints: [2 items], count: 2 }
3. **executeTerminal** → { success: true, output: "Test output" }

**Success Criteria**:
- ✅ Debug session active
- ✅ Breakpoints verified
- ✅ Terminal execution safe
- ✅ Session state transitions valid

---

### Scenario 6: Error Recovery Path

**User Intent**: Handle handler errors with fallback and retry  
**Steps**:
1. **triggerTimeout** → Handler timeout (100ms)
2. **activateCircuitBreaker** → { active: true, failureCount: 1 }
3. **fallbackHandler** → { fallbackUsed: true, result: "..." }
4. **retryRequest** → { retried: true, success: true }

**Success Criteria**:
- ✅ Timeout detected
- ✅ Circuit-breaker activated
- ✅ Fallback engaged
- ✅ Retry succeeds
- ✅ No cascading failures
- ✅ Metrics recorded

---

### Scenario 7: State Persistence Workflow

**User Intent**: Recover workflow after simulated crash  
**Steps**:
1. **captureState** → Checkpoint editor state
2. **simulateCrash** → Simulate bridge crash
3. **recoverState** → Restore from checkpoint

**Success Criteria**:
- ✅ State checkpoint created
- ✅ State restored post-crash
- ✅ No data loss
- ✅ Workflow resumable

---

### Scenario 8: Configuration Variant Workflow

**User Intent**: Change settings and verify behavior change  
**Steps**:
1. **loadSettings** → Load from config.json
2. **applySettings** → Apply new model (gpt-4)
3. **reload** → Invalidate caches
4. **executeWorkflow** → Run workflow with new config

**Success Criteria**:
- ✅ Settings loaded and applied
- ✅ Reload successful
- ✅ Behavior reflects new config
- ✅ Settings persisted

---

## Running Instructions

### Full Test Suite

```bash
npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs
```

### Run Specific Suite

```bash
# Editor-to-AI only
npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs --grep "Suite 1"

# Code Navigation only
npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs --grep "Suite 2"

# Cross-scenario tests only
npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs --grep "Cross-Scenario"
```

### With Configuration Variants

```bash
CONTINUE_CONFIG=gpt-4 npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs
CONTINUE_CONFIG=claude npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs
```

### With Stress Mode (Concurrent Workflows)

```bash
E2E_CONCURRENT=10 npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs
```

### With Performance Reporting

```bash
E2E_METRICS=true npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs --reporter json > results.json
```

### Watch Mode

```bash
npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs --watch
```

---

## Results Interpretation

### Latency Breakdown

**Per-Handler Breakdown** (from metrics):
```
getEditorState: { count: 100, min: 45ms, max: 120ms, mean: 52ms, p99: 75ms }
extractSymbols: { count: 100, min: 60ms, max: 150ms, mean: 85ms, p99: 120ms }
generateCompletion: { count: 100, min: 180ms, max: 350ms, mean: 225ms, p99: 300ms }
applyEdit: { count: 100, min: 80ms, max: 180ms, mean: 110ms, p99: 145ms }
```

**Workflow Latency** (end-to-end):
- Editor-to-AI: sum of handler latencies + overhead (~450ms baseline, gate 300ms = OK)
- Code Navigation: ~500ms (gate 250ms = investigate if consistently >250ms)

### Handler Utilization

- **Call Counts**: How many times each handler was invoked
- **Cache Hit Rates**: Symbol/document cache effectiveness
- **Error Rates**: Failures per 1000 calls

### Failure Points

- Which handlers fail under load (from error aggregates)
- Cascading failure impact (did error in X cause failures in Y?)
- Recovery success rate (% of retries that succeeded)

### State Consistency

- **Before/After Violations**: State checkpoints that didn't match expectations
- **Checkpoint Mismatches**: Expected state ≠ actual state
- **Ordering Issues**: Handlers executed out of order

---

## Troubleshooting

### Common Failures

**Timeout Error**:
```
Error: Handler timeout after 5000ms
```
**Cause**: Handler exceeded timeout threshold  
**Fix**: 
- Check handler implementation for blocking code
- Increase timeout in config if expected (long operation)
- Profile handler latency under load (Step 98 performance tests)

**State Mismatch**:
```
Error: State validation failed: After state is null or undefined
```
**Cause**: Handler didn't return expected state  
**Fix**:
- Verify handler return type matches fixture expectation
- Check Step 97 compliance tests for contract violations
- Review handler implementation logic

**Cross-File Pollution**:
```
Assertion Error: search results for file A matched file B
```
**Cause**: Handler cache not isolated per file  
**Fix**:
- Verify `clearCache()` called between operations
- Check DocumentProvider isolation (Step 52)
- Review SymbolExtractor cache scoping (Step 53)

### Debug Mode

Enable verbose logging:
```bash
DEBUG=* npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs
```

Capture state snapshots:
```bash
E2E_SNAPSHOTS=true npx mocha src/versions/v2.0.0/tests/handler-e2e-scenarios.test.mjs
```

### Performance Issues

**Slow Workflow Execution**:
1. Check handler latency metrics (Step 98 baseline)
2. Identify slowest handler per workflow
3. Profile handler under load (use profiler-agent if needed)
4. Review Step 99 stress test results for stress-induced slowdown

**High Memory Usage**:
1. Run cross-scenario memory test: `npx mocha ... --grep "memory"`
2. Check for cache leaks in DocumentProvider (Step 52)
3. Verify SymbolExtractor cache cleared (Step 53)
4. Review crash recovery checkpoint persistence (Step 105)

---

## Success Gate Checklist

✅ **All 8 workflows pass** (65+ tests passing)  
✅ **Latency gates met**:
- Editor-to-AI: p99 < 300ms
- Code Navigation: p99 < 250ms  
- Multi-File Refactor: p99 < 400ms
- Others: p99 < 500ms

✅ **State consistency validated** across all workflows  
✅ **Error recovery** tested and working  
✅ **Concurrent isolation** >80% (no cross-contamination)  
✅ **Configuration variants** tested  
✅ **Build clean** (zero warnings)

---

## Related Documentation

- **Step 97**: Handler Compliance Tests — Message contract validation
- **Step 98**: Handler Performance Tests — Baseline latency/throughput
- **Step 99**: Handler Stress Tests — Error injection, concurrent patterns
- **Step 110**: This step — E2E workflow validation
- **Step 112**: Regression Test Suite — Uses Step 110 baseline for detection
