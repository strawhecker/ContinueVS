# Handler Compliance Guide

**Version**: 1.0.0  
**Status**: Complete  
**Related Steps**: 76-95 (handlers), 97 (compliance tests), 98-99 (perf/stress tests), 113 (manual testing)

---

## Overview

This guide documents the handler compliance specification for all 20 handlers implemented in Steps 76-95.

**Compliance** means verifying that handlers conform to a unified contract across 10 dimensions:

1. **Registration** — Handler is registered in Step 71 HandlerRegistry
2. **Message Acceptance** — Handler accepts valid JSON-RPC messages without error
3. **Message Correlation** — Response messageId correlates to request messageId (Step 63 adapter)
4. **Response Schema** — Response matches expected schema (success object or error object)
5. **Error Codes** — Error codes conform to JSON-RPC standards (-32602, -32603, etc.)
6. **Timeout Policy** — Handler respects TimeoutManager policies (fast/medium/slow, Step 64)
7. **Middleware Integration** — Handler integrates with middleware chain (Steps 72-74 hooks)
8. **Graceful Degradation** — Handler doesn't crash with null optional dependencies
9. **Metrics/Logging** — Metrics and logs recorded on success/error paths
10. **Concurrency Safety** — Handler state is isolated; no race conditions

---

## Message Contract

### Valid Request Envelope

All handlers accept JSON-RPC 2.0 messages with this structure:

```json
{
  "id": 1,
  "method": "handler:action",
  "params": {
    "param1": "value1",
    "param2": "value2"
  }
}
```

**Required Fields**:
- `id` (number): Message ID for correlation
- `method` (string): Handler method name
- `params` (object): Handler-specific parameters

### Success Response

```json
{
  "id": 1,
  "result": {
    "success": true,
    "data": {}
  }
}
```

**Required Fields**:
- `id` (number): Correlates to request
- `result` (object): Handler response data

### Error Response

```json
{
  "id": 1,
  "error": {
    "code": -32603,
    "message": "Internal error",
    "data": {}
  }
}
```

**Required Fields**:
- `id` (number): Correlates to request
- `error.code` (number): JSON-RPC error code
- `error.message` (string): Error description

### Standard Error Codes

| Code | Name | Meaning |
|------|------|---------|
| -32600 | INVALID_REQUEST | Request is malformed |
| -32602 | INVALID_PARAMS | Method parameter(s) invalid |
| -32603 | INTERNAL_ERROR | Handler execution failed |
| -32008 | TIMEOUT | Handler exceeded timeout |
| -32001 | NOT_FOUND | Resource not found |
| -32000 to -32099 | SERVER_ERROR | Handler implementation error |

---

## Handler Compliance Matrix

**20 Handlers × 10 Contract Dimensions = 200 compliance requirements**

| Handler | Tier | Timeout | Status | Registration | Message | Correlation | Schema | Error Codes | Timeout Policy | Middleware | Degradation |
|---------|------|---------|--------|--------------|---------|-------------|--------|-------------|-----------------|-----------|------------|
| refactor-handler (76) | core | medium | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| fix-suggestion-handler (77) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| apply-edit-handler (78) | core | medium | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| format-document-handler (79) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| tree-sitter-handler (80) | optional | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| git-integration-handler (81) | core | medium | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| terminal-handler (82) | core | slow | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| file-system-handler (83) | core | medium | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| project-info-handler (84) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| inline-message-handler (85) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| sidebar-ui-handler (86) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| context-window-handler (87) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| model-info-handler (88) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| streaming-response-handler (89) | core | slow | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| code-lens-handler (90) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| snippet-handler (91) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| diff-viewer-handler (92) | core | medium | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| refactor-tests-handler (93) | core | medium | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| workspace-reload-handler (94) | core | slow | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| settings-sync-handler (95) | core | fast | ✅ PENDING | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

**Legend**:
- ✓ = Requirement mapped by test suite
- ✅ PENDING = All tests ready, pending execution
- ✅ PASS = Tests passing
- ❌ FAIL = Tests failing

---

## Test Execution

### Running Compliance Tests

```bash
# Run all compliance tests
npm test -- src/versions/v2.0.0/tests/handler-compliance.test.mjs

# Run compliance tests for specific handler
npm test -- src/versions/v2.0.0/tests/handler-compliance.test.mjs --grep "Refactor Handler"

# Generate compliance report
node src/versions/v2.0.0/lib/handler-compliance-report.mjs
```

### Test Organization

Tests are organized by handler type:

- **Factories** (76-79): Refactor, Fix Suggestion, Apply Edit, Format Document
- **Subscriptions** (50-51): getEditorState, onEditorStateChange (via registry)
- **Bidirectional** (61, 82): Debug Session, Terminal
- **Caches** (52-54): Document Provider, Symbol Extractor, Diagnostics Collector
- **Navigation** (55-57): Search, Go-to-Definition, Find References
- **Analysis** (58-59): Code Completion, Hover Info
- **UI** (85-86): Inline Message, Sidebar UI
- **Optional** (80): Tree Sitter (feature-flag gated)
- **Metadata** (84, 88, 89): Project Info, Model Info, Streaming Response

### Test Results Interpretation

**Pass Rate >= 100%**: All handlers compliant, proceed to Step 98 (performance tests)  
**Pass Rate 90-99%**: Minor issues, review warnings, proceed with caution  
**Pass Rate < 90%**: Critical issues, fix failures before proceeding

---

## Common Failures & Remediation

### Missing Required Fields

**Symptom**: `ContractViolationError: Handler 'X' violated contract requirement: Handler must accept valid JSON-RPC messages`

**Cause**: Handler doesn't validate required fields (id, method, params)

**Fix**: Add input validation to handler:
```javascript
export async function myHandler(message) {
  if (!message.id) throw new Error('Missing id');
  if (!message.method) throw new Error('Missing method');
  // ... rest of handler
}
```

### Wrong Error Code

**Symptom**: `ErrorCodeMismatchError: Handler 'X' returned unexpected error code. Expected -32602, got -32000`

**Cause**: Handler returns non-standard error code

**Fix**: Use JSON-RPC standard codes:
```javascript
const errorResponse = {
  id: message.id,
  error: {
    code: -32602,  // Use standard code
    message: 'Invalid parameters',
  },
};
```

### Middleware Hook Not Executed

**Symptom**: `ContractViolationError: Handler must integrate with middleware chain`

**Cause**: Handler doesn't call middleware hooks (Steps 72-74)

**Fix**: Wrap handler with middleware:
```javascript
async function handleWithMiddleware(message, middlewareChain) {
  // Pre-hooks
  await middlewareChain.executePreHooks(message);

  // Handle request
  const response = await handler(message);

  // Post-hooks
  await middlewareChain.executePostHooks(response);

  return response;
}
```

### Null Dependency Crash

**Symptom**: `TypeError: Cannot read property 'X' of undefined` on graceful degradation test

**Cause**: Handler doesn't null-check optional dependencies

**Fix**: Add null checks:
```javascript
export async function myHandler(message, context) {
  const logger = context.logger || createDefaultLogger();
  const metrics = context.metrics || createDefaultMetrics();

  // Use logger and metrics safely
  await logger.info('Handler executing');
}
```

### Timeout Exceeded

**Symptom**: `ComplianceError: Handler exceeded timeout policy`

**Cause**: Handler latency exceeds policy limit (fast=100ms, medium=500ms, slow=2000ms)

**Fix**: Optimize handler or adjust timeout policy:
```javascript
// Option 1: Optimize handler
async function optimizedHandler(message) {
  // Use caching, batch operations, etc.
}

// Option 2: Adjust timeout policy in fixture
export const fixture = {
  metadata: { timeout: 'slow' },  // Increase timeout
  // ...
};
```

---

## Integration with Steps 98-99

**Step 98 (Performance Tests)** builds on compliance baseline:
- Compliance must be ✅ PASS before running perf tests
- Perf tests measure latency, memory, throughput
- Uses same fixtures from Step 97

**Step 99 (Stress Tests)** validates under load:
- Compliance baseline required for valid stress results
- Tests concurrency, error rates, recovery
- Relies on middleware integration from Step 97

---

## Integration with Step 113 (Manual Testing Guide)

Compliance test fixtures and results are used to create manual testing guide (Step 113):
- Test cases from handler-compliance-fixtures.mjs become manual test steps
- Expected results from compliance tests provide success criteria
- Compliance report highlights which handlers need manual verification

---

## Glossary

**ComplianceValidator**: Framework class that validates handlers against contract requirements

**Contract**: Set of 10 requirements that all handlers must satisfy

**Fixture**: Test data containing valid/invalid messages for a handler

**HandlerRegistry**: Central registry (Step 71) where all handlers are registered

**Middleware Chain**: Pre/post processing hooks (Steps 72-74) applied to all messages

**TimeoutManager**: Service (Step 64) that enforces per-handler timeout policies

**JSON-RPC Standard**: Message protocol used for handler communication

---

## Additional Resources

- **Framework**: `src/versions/v2.0.0/lib/handler-compliance-framework.mjs`
- **Fixtures**: `src/versions/v2.0.0/tests/mocks/handler-compliance-fixtures.mjs`
- **Test Suite**: `src/versions/v2.0.0/tests/handler-compliance.test.mjs`
- **Report Generator**: `src/versions/v2.0.0/lib/handler-compliance-report.mjs`
- **Handler Registry**: `src/versions/v2.0.0/lib/handler-registry.mjs` (Step 71)
- **Bridge Protocol Adapter**: `src/versions/v2.0.0/lib/bridge-protocol-adapter.mjs` (Step 63)
- **Timeout Manager**: `src/versions/v2.0.0/lib/timeout-manager.mjs` (Step 64)
- **Middleware**: `src/versions/v2.0.0/lib/message-logging-middleware.mjs`, etc. (Steps 72-74)
