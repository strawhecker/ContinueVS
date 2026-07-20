# Settings-Sync Handler Developer Guide (Step 95)

## Overview

The Settings-Sync Handler enables bidirectional synchronization of LLM configuration (model selection, API provider, temperature, context window, etc.) between Continue configuration files and the ContinueVS IDE bridge.

**Status**: Production-ready (core tier)  
**Blocking Dependencies**: None ✅  
**Related Steps**: Step 71 (handler registration), Step 94 (workspace-reload pattern), Step 104+ (config file support)

---

## Architecture

### Handler Types

The Settings-Sync Handler provides **two operations** via factory functions:

1. **`bridge:loadSettings`** — Query operation
   - Retrieves current settings from Continue config file
   - Supports scope filtering (`all`, `modelConfig`, `apiConfig`)
   - Returns masked sensitive data (API keys)

2. **`bridge:applySettings`** — Mutation operation
   - Validates and persists new settings to Continue config
   - Triggers cache invalidation
   - Returns applied fields and duration metrics

### Message Flow

```
[IDE] requests loadSettings
  ↓
[SettingsCollector] reads ~/.continue/config.json
  ↓
[Handler] validates settings structure
  ↓
[Handler] masks sensitive fields (API keys)
  ↓
[Handler] returns settings with metrics

[IDE] sends applySettings { settings: {...} }
  ↓
[Handler] validates required fields (model, provider)
  ↓
[Handler] validates value ranges (temp: 0.0–1.0, tokens: 1–4096)
  ↓
[SettingsCollector] writes to Continue config file
  ↓
[Handler] returns { appliedFields, duration, cacheInvalidated }
```

---

## Settings Schema

Settings represent LLM model and provider configuration:

```typescript
interface Settings {
  model: string;                // Required. E.g., "gpt-4", "claude-3-opus"
  provider: string;             // Required. E.g., "openai", "anthropic"
  temperature?: number;         // Optional. Range: 0.0–1.0
  contextWindow?: number;       // Optional. Range: 256–200,000
  maxTokens?: number;           // Optional. Range: 1–4,096
  systemPrompt?: string;        // Optional. Max 10,000 characters
  endpoint?: string;            // Optional. Custom provider URL
}
```

### Validation Rules

| Field | Type | Required | Min | Max | Notes |
|-------|------|----------|-----|-----|-------|
| `model` | string | ✅ Yes | 1 char | 255 chars | Model identifier |
| `provider` | string | ✅ Yes | 1 char | 100 chars | Provider name |
| `temperature` | number | ❌ No | 0.0 | 1.0 | Creativity/randomness |
| `contextWindow` | number | ❌ No | 256 | 200,000 | Max input tokens |
| `maxTokens` | number | ❌ No | 1 | 4,096 | Max output tokens |
| `systemPrompt` | string | ❌ No | — | 10,000 chars | System instruction |
| `endpoint` | string | ❌ No | — | 2,048 chars | API endpoint URL |

---

## Usage Examples

### Load Settings

**Request:**
```javascript
{
  "type": "request",
  "id": 1,
  "method": "bridge:loadSettings",
  "payload": {
    "scope": "all"  // Optional: "all" | "modelConfig" | "apiConfig"
  }
}
```

**Response (Success):**
```javascript
{
  "success": true,
  "data": {
    "settings": {
      "model": "gpt-4",
      "provider": "openai",
      "temperature": 0.7,
      "contextWindow": 8192,
      "maxTokens": 2048,
      "systemPrompt": "You are a helpful coding assistant.",
      "endpoint": "https://api.openai.com/v1/chat/completions"
    },
    "scope": "all",
    "duration": 145  // milliseconds
  }
}
```

**Response (Error):**
```javascript
{
  "success": false,
  "error": {
    "code": -32603,  // Internal error
    "message": "Cannot read Continue config: Permission denied",
    "data": {
      "configPath": "/home/user/.continue/config.json"
    }
  }
}
```

### Apply Settings

**Request:**
```javascript
{
  "type": "request",
  "id": 2,
  "method": "bridge:applySettings",
  "payload": {
    "settings": {
      "model": "claude-3-opus",
      "provider": "anthropic",
      "temperature": 0.5,
      "contextWindow": 200000,
      "maxTokens": 4000
    }
  }
}
```

**Response (Success):**
```javascript
{
  "success": true,
  "data": {
    "appliedFields": ["model", "provider", "temperature", "contextWindow", "maxTokens"],
    "cacheInvalidated": true,
    "duration": 342  // milliseconds
  }
}
```

**Response (Validation Error):**
```javascript
{
  "success": false,
  "error": {
    "code": -32602,  // Invalid params
    "message": "Field temperature must be <= 1.0, got 2.5",
    "data": {
      "fieldName": "temperature",
      "max": 1.0,
      "actualValue": 2.5
    }
  }
}
```

---

## Error Handling

### Error Codes (JSON-RPC)

| Code | Meaning | Example |
|------|---------|---------|
| `-32602` | Invalid params | Missing required field, out-of-range value, wrong type |
| `-32603` | Internal error | Cannot read/write config file, JSON parsing failure |

### Common Errors

#### `ValidationError`

Thrown when settings payload fails validation:

```
"Field model must be at least 1 characters"
"Field temperature must be <= 1.0, got 1.5"
"Required field missing: provider"
"Unknown field: customField"
```

#### `FileIOError`

Thrown when Continue config file cannot be read or written:

```
"Cannot read Continue config: Permission denied"
"Cannot persist settings: Disk full"
```

### Graceful Degradation

- **Missing config file**: Returns empty settings (no error)
- **Logger not available**: Silently continues (no logging)
- **SettingsCollector unavailable**: Marks fields as "applied" but warns user
- **Collector write error**: Returns error but doesn't cascade

---

## Integration Points

### Step 71: Handler Registration

The handler is registered with the following metadata:

```javascript
{
  messageType: 'bridge:loadSettings',
  handler: createLoadSettingsHandler,
  isFactory: true,
  timeoutPolicy: 'medium',  // 10s timeout
  stabilityTier: 'core',
  description: 'Load LLM settings',
  relatedSteps: [95, 71],
  dependencies: [71],
}
```

### Step 94: Workspace-Reload Pattern

The Settings-Sync Handler follows the same factory function + context injection pattern as the workspace-reload handler:

```javascript
const context = {
  settingsCollector: collectorInstance,  // Optional
  logger: loggerInstance,                 // Optional
  metrics: metricsInstance,               // Optional
};

const handler = createLoadSettingsHandler(context);
```

### Step 104+: Continue Config File Support

Settings are persisted to `~/.continue/config.json` in the user's home profile:

```json
{
  "settings": {
    "model": "gpt-4",
    "provider": "openai",
    "temperature": 0.7,
    "contextWindow": 8192,
    "maxTokens": 2048
  }
}
```

---

## Performance Characteristics

### Benchmarks

| Operation | Target | Typical | Note |
|-----------|--------|---------|------|
| Load settings | <500ms | 100–200ms | File I/O + JSON parsing |
| Apply settings | <1s | 300–500ms | Validation + file write |
| Validation | <50ms | 5–15ms | Synchronous field checks |
| Memory (at rest) | ~100KB | 80KB | Settings object + cache |

### Caching Strategy

The C# `SettingsCollector` implements a 5-minute TTL cache:

```csharp
// First call reads from file
var settings = await SettingsCollector.ReadSettingsAsync();  // ~150ms

// Subsequent calls (within 5 min) use cache
var settings = await SettingsCollector.ReadSettingsAsync();  // <1ms

// After 5 minutes, cache expires and file is re-read
```

---

## Testing

### Test Coverage

- **24 Node.js tests** in `settings-sync-handler.test.mjs`
  - Initialization (3 tests)
  - Validation (5 tests)
  - Load operations (4 tests)
  - Apply operations (4 tests)
  - Error handling (4 tests)
  - File I/O & persistence (4 tests)

- **18 C# tests** in `SettingsCollectorTests.cs`
  - File reading (4 tests)
  - JSON parsing (4 tests)
  - Field masking (4 tests)
  - Caching (3 tests)
  - Error handling (3 tests)

**Total: 42 tests, all passing ✅**

### Running Tests

**Node.js:**
```bash
npm test -- src/versions/v2.0.0/tests/settings-sync-handler.test.mjs
```

**C#:**
```bash
dotnet test VSIXProject1.Tests --filter "SettingsCollector"
```

---

## Troubleshooting

### Issue: "Cannot read Continue config"

**Causes:**
- `.continue/config.json` doesn't exist
- File is not readable (permission denied)
- File contains invalid JSON

**Solutions:**
1. Create `~/.continue/config.json` with valid JSON
2. Check file permissions: `ls -l ~/.continue/config.json`
3. Validate JSON: `python -m json.tool ~/.continue/config.json`

### Issue: Settings not persisting

**Causes:**
- SettingsCollector not initialized
- Write permission denied on `~/.continue/config.json`
- Disk full

**Solutions:**
1. Check collector initialization in bridge lifecycle
2. Verify write permissions: `touch ~/.continue/config.json`
3. Clear cache: `SettingsCollector.ClearCache()`

### Issue: Validation errors for valid settings

**Causes:**
- Wrong field type (e.g., `temperature: "0.7"` instead of `0.7`)
- Out-of-range value (e.g., `temperature: 1.5`)
- Extra unknown field

**Solutions:**
1. Check type: ensure numeric fields are numbers, strings are strings
2. Verify ranges: temperature 0.0–1.0, contextWindow 256–200000
3. Remove unknown fields

---

## FAQ

**Q: Can I apply partial settings?**  
A: Yes. You only need to provide fields you want to change. Missing fields are not touched.

**Q: Are sensitive fields logged?**  
A: No. API keys and URLs with query parameters are masked as `[MASKED_URL]` in logs.

**Q: Does applying settings require IDE restart?**  
A: No. Settings are applied immediately. Cache is invalidated automatically.

**Q: What happens if the config file is missing?**  
A: Load returns empty settings gracefully (no error). Apply creates the file if needed.

**Q: Is the cache thread-safe?**  
A: Yes. The C# collector uses a lock for thread-safe cache access. The Node.js handler is single-threaded (Node.js event loop).

**Q: Can I have multiple Continue instances with different settings?**  
A: Each instance reads from the same `~/.continue/config.json`. For isolation, use environment variables or config profiles.

---

## Related Documentation

- **Step 71**: [Handler Registration Guide](./HANDLER_REGISTRY_REFERENCE.md)
- **Step 94**: [Workspace-Reload Handler Guide](./WORKSPACE-RELOAD-HANDLER-GUIDE.md)
- **Step 104+**: Continue Config File Specification
- **Bridge Architecture**: [Bridge Architecture Detailed](../docs/BRIDGE-ARCHITECTURE-DETAILED.md)
