# Code-Lens Handler Guide (Step 90)

## Overview

Code lenses are **inline IDE UI elements** that appear in the editor as clickable text. They provide quick access to contextual commands without requiring menu navigation.

**Examples**:
- "Run Test" — Execute a test method
- "Debug Test" — Debug a test method  
- "View References" — Find all references to a symbol
- "View Implementations" — Find interface implementations
- "Go to Definition" — Navigate to definition (VS built-in)
- "Peek Definition" — Peek at definition (VS built-in)

This handler generates code lenses by analyzing symbols in source files and producing VS-compatible lens objects. It's designed for performance (< 50ms queries) with aggressive caching.

---

## Architecture

### Message Flow

```
IDE User (editor focus)
  ↓
VS CodeLensProvider (C#)
  ├─ Requests lenses via GetCodeLensesAsync
  └─ Receives mapped CodeLens objects
      ↓
  CodeLensService (Step 90, C#)
  ├─ Checks cache (valid + not expired)
  │  └─ Return cached result (< 1ms)
  └─ Call bridge:getCodeLenses (cache miss)
      ↓
  core-server.js
  ├─ MessageValidator (Step 73)
  ├─ TimeoutManager (Step 64)
  └─ CodeLensHandler (Step 90, Node)
      ├─ Symbol extraction (Step 53)
      ├─ Document queries (Step 52)
      └─ Lens generation
          ↓
  Response: { lenses: [ { line, command, title, data } ] }
      ↓
  CodeLensService caches + maps
      ↓
  VS displays lenses in editor
```

### Component Layers

1. **Node.js Handler** (`code-lens-handler.mjs`)
   - Stateless query handler
   - Validates input (file path, range, exclude types)
   - Calls SymbolExtractor + DocumentProvider
   - Generates lens objects
   - Records metrics

2. **C# Service** (`CodeLensService.cs`)
   - Bridge integration point
   - Caches results (TTL = 5s)
   - Handles errors gracefully
   - Thread-safe via locks
   - Maps bridge objects to VS types

3. **Test Fixtures** (`code-lens-mock.mjs`)
   - Mock handler, extractor, provider
   - Test data builders
   - Valid message templates

---

## Message Contract

### Request Format

```javascript
{
  messageType: 'bridge:getCodeLenses',
  filePath: 'src/MyClass.cs',                    // required
  range: {                                        // optional
    start: { line: 0, char: 0 },
    end: { line: 100, char: 0 }
  },
  excludeTypes: ['peekDefinition']               // optional
}
```

### Response Format

```javascript
{
  success: true,
  data: {
    lenses: [
      {
        line: 12,
        command: 'runTest',
        title: 'Run Test',
        data: { symbolName: 'TestMethod', type: 'method', tags: [] }
      },
      {
        line: 20,
        command: 'viewReferences',
        title: 'View References',
        data: { symbolName: 'ProcessData', type: 'method' }
      },
      // ... more lenses
    ],
    count: 2,
    file: 'src/MyClass.cs',
    symbolsProcessed: 10
  }
}
```

### Error Response

```javascript
{
  success: false,
  error: {
    code: 'CODE_LENS_ERROR' | 'POSITION_ERROR' | 'INTERNAL_ERROR',
    message: 'Human-readable error description',
    operationType: 'validation' | 'symbol_extraction' | 'lens_generation' | 'filtering',
    details: { /* optional context */ }
  }
}
```

---

## Lens Types Reference

### Test Lenses
Generated for symbols where `isTest: true`:

- **runTest** — Execute test function/class
  - Title: "Run Test"
  - Data: `{ symbolName, type, tags }`
  - VS Command: `runTest` (custom)

- **debugTest** — Debug test function/class
  - Title: "Debug Test"
  - Data: `{ symbolName, type, tags }`
  - VS Command: `debugTest` (custom)

### Reference Lenses
Generated for public methods/properties:

- **viewReferences** — Find all references to symbol
  - Title: "View References"
  - Data: `{ symbolName, type }`
  - VS Command: `findReferences` (built-in)

### Implementation Lenses
Generated for interfaces/abstract members:

- **viewImplementations** — Find interface implementations
  - Title: "View Implementations"
  - Data: `{ symbolName, type }`
  - VS Command: `findImplementations` (built-in)

### Navigation Lenses
Generated for all public symbols:

- **goToDefinition** — Navigate to definition
  - Title: "Go to Definition"
  - Data: `{ symbolName, type }`
  - VS Command: `goToDefinition` (built-in)

- **peekDefinition** — Peek at definition
  - Title: "Peek Definition"
  - Data: `{ symbolName, type }`
  - VS Command: `peekDefinition` (built-in)

---

## Performance Characteristics

### Performance Gates

| Metric | Target | Notes |
|--------|--------|-------|
| Single-file query (cached) | < 1ms | Cache hit |
| Single-file query (no cache) | < 50ms | Small-to-medium files |
| Multi-file query | < 200ms | Not currently used |
| Cache TTL | 5s | Invalidated on document change |
| Cache hit rate | > 70% | On repeated calls to same file |

### Benchmarks

```
File Size           | Symbol Count | Query Time | Lenses Generated
Small (< 500 LOC)   | 10–50        | 5–15ms     | 30–150
Medium (500-2K)     | 50–200       | 15–40ms    | 150–600
Large (2K-10K)      | 200–1000     | 40–100ms   | 600–3000
Very Large (10K+)   | 1000+        | 100–200ms  | 3000+
```

### Optimization Strategies

1. **Symbol Extraction Caching** (Step 53)
   - Cache hit on repeated calls
   - Reduces processing time by ~80%

2. **Range Filtering**
   - Limit symbol extraction to requested range
   - Reduces symbol count for large files

3. **Exclude Types Filter**
   - Client can exclude expensive lens types
   - Reduces lens generation overhead

4. **Result Caching** (Step 90, C# service)
   - 5-second TTL
   - Invalidated on document change
   - Cache per file path

---

## Error Handling Patterns

### Invalid File Path

```javascript
// Request
{ messageType: 'bridge:getCodeLenses', filePath: '' }

// Response
{
  success: false,
  error: {
    code: 'INVALID_REQUEST',
    message: 'Missing required field: filePath',
    operationType: 'validation'
  }
}
```

### Invalid Range

```javascript
// Request
{
  messageType: 'bridge:getCodeLenses',
  filePath: 'src/Code.cs',
  range: {
    start: { line: 100, char: 0 },
    end: { line: 50, char: 0 }  // start > end: invalid
  }
}

// Response
{
  success: false,
  error: {
    code: 'POSITION_ERROR',
    message: 'Range start line cannot be after end line',
    operationType: 'validation',
    details: { /* range object */ }
  }
}
```

### Symbol Extraction Failure

```javascript
// Response
{
  success: false,
  error: {
    code: 'CODE_LENS_ERROR',
    message: 'Symbol extractor encountered an error',
    operationType: 'symbol_extraction',
    details: { /* original error context */ }
  }
}
```

### Recovery (C# Service)

- **Bridge timeout (> 3s)** → Log warning, return empty lenses
- **Bridge exception** → Log error, return empty lenses  
- **Null response** → Log warning, return empty lenses
- **Malformed response** → Log error, return partial results
- **Cache invalidation** → Clear entry, retry on next request

---

## C# Integration Guide

### Registration (in BridgeLifecycleManager or plugin initialization)

```csharp
// Create service
var transport = bridgeTransport;  // From StdioTransport (Step 19-21)
var logger = bridgeLogger;        // From BridgeLogger (Step 25)

var codeLensService = new CodeLensService(transport, logger);

// Register with VS CodeLens provider system
// (Platform-specific integration, not shown here)
```

### Usage Example

```csharp
public async Task<IEnumerable<CodeLens>> GetCodeLensesAsync(string filePath)
{
    var codeLenses = await codeLensService.GetCodeLensesAsync(
        filePath,
        range: (10, 100),  // Optional range
        cancellationToken: CancellationToken.None
    );

    // Map to VS CodeLens objects
    var vsLenses = codeLenses.Select(lens => new CodeLens
    {
        Line = lens.Line,
        Command = lens.Command,
        Title = lens.Title
    });

    return vsLenses;
}
```

### Cache Invalidation

```csharp
// Call when document changes
codeLensService.InvalidateCache("src/MyFile.cs");

// Or clear all caches
codeLensService.ClearCache();
```

### Error Handling

```csharp
try
{
    var lenses = await codeLensService.GetCodeLensesAsync("src/Code.cs");
    // Display lenses
}
catch (OperationCanceledException)
{
    // Timeout or cancellation
    logger.Warn("Code lens request cancelled");
    // Return empty lenses or use cached result
}
catch (Exception ex)
{
    // Unexpected error
    logger.Error($"Code lens error: {ex.Message}", ex);
    // Return empty lenses
}
```

---

## Testing Strategy

### Node Tests (22+ tests)

```bash
npm test -- code-lens-handler.test.mjs
```

**Coverage**:
- ✅ Initialization + dependency injection (3 tests)
- ✅ Message validation (4 tests)
- ✅ Lens generation (5 tests)
- ✅ Position queries (4 tests)
- ✅ Performance (3 tests)
- ✅ Error handling (3+ tests)

### C# Tests (8 tests)

```bash
dotnet test VSIXProject1.slnx --filter "CodeLensServiceTests"
```

**Coverage**:
- ✅ Constructor validation (2 tests) — Valid transport accepted, null transport rejected
- ✅ Cache operations (2 tests) — InvalidateCache and ClearCache do not throw
- ✅ Input validation (4 tests) — Null, empty, and valid file paths handled correctly
- ⚠️ *Future enhancements*: TTL expiry, concurrency stress, bridge response parsing, timeout behavior

### Test Fixtures

Use `code-lens-mock.mjs` for consistent test scenarios:

```javascript
import {
  createMockCodeLensHandler,
  createMockSymbolExtractor,
  getTestSymbols,
  getMockDependencies,
  TestDataBuilder
} from '../test/mocks/code-lens-mock.mjs';

// Example: Create mock handler
const handler = createMockCodeLensHandler({ lensCount: 10 });

// Example: Create mock extractor with test data
const symbols = getTestSymbols('mixed');
const extractor = createMockSymbolExtractor({ 'src/test.cs': symbols });

// Example: Build custom test data
const builder = new TestDataBuilder()
  .addTestMethod('TestCompile', 10)
  .addPublicMethod('ProcessData', 20)
  .addInterface('IService', 30);
const customSymbols = builder.build();
```

---

## Performance Tuning

### Reduce Query Time

1. **Use range filtering** (if possible)
   ```javascript
   { filePath: 'src/Code.cs', range: { start: { line: 0 }, end: { line: 50 } } }
   ```

2. **Exclude expensive lens types**
   ```javascript
   { filePath: 'src/Code.cs', excludeTypes: ['debugTest', 'viewImplementations'] }
   ```

3. **Increase cache TTL** (in CodeLensService)
   ```csharp
   private const int DefaultCacheTtlMs = 10000;  // 10 seconds
   ```

### Monitor Performance

- **Metrics** (in Node handler)
  - `codelens.count` — Number of lenses generated
  - `codelens.symbols` — Number of symbols processed
  - Handler latency (via `recordHandlerLatency`)

- **Logging** (in C# service)
  - Debug: Cache hits, requests sent
  - Warn: Timeouts, retries, missing data
  - Error: Bridge failures, malformed responses

---

## Troubleshooting

### Lenses Not Showing in Editor

1. **Check bridge connectivity**
   - Verify StdioTransport is running
   - Check HealthCheckService status

2. **Verify symbol extraction**
   - Ensure Step 53 (SymbolExtractor) is working
   - Test with manual bridge request: `{ messageType: 'bridge:getCodeLenses', filePath: 'src/Code.cs' }`

3. **Check cache invalidation**
   - Clear cache: `codeLensService.ClearCache()`
   - Retry request

4. **Review logs**
   - Search for `[CodeLensService]` or `[CodeLensHandler]` messages
   - Check for error codes (e.g., `CODE_LENS_ERROR`, `POSITION_ERROR`)

### Slow Performance

1. **Check file size**
   - Large files (> 10K LOC) may take 100–200ms
   - Use range filtering to limit scope

2. **Check symbol count**
   - Files with 1000+ symbols may be slow
   - Consider excluding expensive lens types

3. **Monitor cache hit rate**
   - If < 70%, document change events may be firing too frequently
   - Check for unintended invalidations

### Memory Usage

- Cache: ~100–200 bytes per file entry (on C# side)
- Handler: Stateless, minimal allocations
- Peak memory: During symbol extraction (Step 53)

---

## Related Steps

- **Step 14**: Handler Dispatcher — Routes bridge messages
- **Step 15**: Handler Adapter — Convenience wrapper
- **Step 52**: Document Provider — Queries document content
- **Step 53**: Symbol Extractor — Extracts symbols (core dependency)
- **Step 56**: Go-to-Definition Handler — Complementary navigation
- **Step 57**: Find References Handler — References context
- **Step 60**: Test Explorer Handler — Test metadata
- **Step 62**: Message Type Definitions — CodeLens typedef
- **Step 71**: Handler Registration — Registers all handlers
- **Step 90**: Code-Lens Handler — **This step**

---

## FAQ

**Q: Can I customize the lens titles?**
A: Yes, modify the title generation in `generateLensesForSymbol()` function in `code-lens-handler.mjs`.

**Q: How do I add new lens types?**
A: Add a new `if` branch in `generateLensesForSymbol()` for the symbol type, and update the `excludeTypes` filter.

**Q: What's the difference between "Go to Definition" and "Peek Definition"?**
A: Go to Definition navigates to the definition in a new editor view. Peek Definition shows it inline without changing focus.

**Q: Can I disable code lenses for performance?**
A: Yes, via user settings in VS or by returning empty lenses from the bridge handler.

**Q: How long does the cache persist?**
A: 5 seconds by default, or until document change. Adjust `DefaultCacheTtlMs` in `CodeLensService.cs`.

**Q: Is the handler thread-safe?**
A: Yes, the Node handler is stateless (thread-safe), and the C# service uses locks for cache access.

---

## Appendix: Full Error Codes

| Code | Meaning | Handling |
|------|---------|----------|
| `INVALID_REQUEST` | Missing or invalid request field | Validate input before sending |
| `POSITION_ERROR` | Invalid range/position bounds | Ensure `start.line < end.line` |
| `CODE_LENS_ERROR` | General handler error | Check logs, retry, or use cache |
| `INTERNAL_ERROR` | Unexpected exception | Log error, return empty lenses |

---

**Step 90 Status**: ✅ Complete  
**Version**: 1.0.0  
**Last Updated**: 2024-01-15
