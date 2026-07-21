# Continue Configuration Manager Guide (Step 104)

## Overview

**Step 104** creates a bridge-side configuration persistence layer for managing Continue SDK config files (`~/.continue/config.json`). This is **independent of Step 95** (settings-sync handler), providing clean separation of concerns:

- **Step 95** (settings-sync handler): IDE ↔ Continue via JSON-RPC messages for bidirectional settings sync
- **Step 104** (config manager): Bridge ↔ Filesystem for config lifecycle operations (read/write/merge/validate)

## Architecture

### Separation of Concerns

```
IDE
  ↓
[Step 95: settings-sync handler] ←→ JSON-RPC bridge messages ←→ [Node.js core-server.js]
  ↓
[Continue SDK]                    [Step 104: config manager] ←→ ~/.continue/config.json
```

**Step 95** handles asynchronous settings updates initiated from the IDE. **Step 104** handles direct filesystem operations for config management, used by handlers, E2E tests, and upgrade scenarios.

### Configuration File Structure

**Location**: `~/.continue/config.json` (user home directory)

**Schema**:

```json
{
  "models": [
    {
      "title": "GPT-4",
      "provider": "openai",
      "model": "gpt-4",
      "apiKey": "sk-...",
      "apiBase": "https://api.openai.com/v1"
    },
    {
      "title": "Claude-3-Opus",
      "provider": "anthropic",
      "model": "claude-3-opus-20240229",
      "apiKey": "sk-ant-..."
    },
    {
      "title": "Local-Llama",
      "provider": "local",
      "model": "llama2-7b"
    }
  ]
}
```

**Required Fields**:
- `models` (array): List of LLM model configurations
- `title` (string): Unique model identifier (e.g., "GPT-4")
- `provider` (string): Provider name (e.g., "openai", "anthropic", "local")
- `model` (string): Model identifier (e.g., "gpt-4")

**Optional Fields**:
- `apiKey` (string): API authentication key
- `apiBase` (string): Custom API endpoint URL

## API Reference

### C# (Host-side: `ContinueConfigurationManager.cs`)

#### ReadConfigAsync()

```csharp
public static async Task<ContinueConfig> ReadConfigAsync(CancellationToken cancellationToken = default)
```

**Purpose**: Read and parse `~/.continue/config.json`

**Returns**: `ContinueConfig` with `Models` array

**Behavior**:
- Returns empty config if file not found (graceful degradation)
- Validates schema after parsing
- Thread-safe with lock

**Throws**:
- `ConfigurationException` — JSON parsing errors
- `SchemaValidationException` — Invalid schema

**Example**:

```csharp
var config = await ContinueConfigurationManager.ReadConfigAsync();
foreach (var model in config.Models)
{
    Console.WriteLine($"Model: {model.Title} ({model.Provider})");
}
```

#### WriteConfigAsync()

```csharp
public static async Task WriteConfigAsync(ContinueConfig config, CancellationToken cancellationToken = default)
```

**Purpose**: Write and serialize config file

**Parameters**:
- `config` — `ContinueConfig` object to write

**Behavior**:
- Creates `~/.continue` directory if not present
- Creates backup of existing file
- Validates schema before writing
- Thread-safe with lock

**Throws**:
- `ConfigurationException` — File I/O errors
- `SchemaValidationException` — Invalid schema

**Example**:

```csharp
var config = new ContinueConfig
{
    Models = new List<ContinueConfigModel>
    {
        new ContinueConfigModel
        {
            Title = "GPT-4",
            Provider = "openai",
            Model = "gpt-4",
            ApiKey = "sk-..."
        }
    }
};
await ContinueConfigurationManager.WriteConfigAsync(config);
```

#### MergeModelsAsync()

```csharp
public static async Task<ContinueConfig> MergeModelsAsync(
    ContinueConfig config, 
    IEnumerable<ContinueConfigModel> modelsToMerge, 
    CancellationToken cancellationToken = default)
```

**Purpose**: Add/update models in config by title (case-insensitive)

**Behavior**:
- Finds existing model by title (case-insensitive)
- Updates if found, appends if new
- Preserves model order
- Validates schema after merge

**Example**:

```csharp
var modelToAdd = new ContinueConfigModel
{
    Title = "Claude",
    Provider = "anthropic",
    Model = "claude-3"
};
var merged = await ContinueConfigurationManager.MergeModelsAsync(config, new[] { modelToAdd });
```

#### RemoveModelsAsync()

```csharp
public static async Task<ContinueConfig> RemoveModelsAsync(
    ContinueConfig config, 
    IEnumerable<string> modelTitles, 
    CancellationToken cancellationToken = default)
```

**Purpose**: Remove models from config by title (case-insensitive)

**Example**:

```csharp
var result = await ContinueConfigurationManager.RemoveModelsAsync(config, new[] { "GPT-4" });
```

### Node.js (Bridge-side: `continue-config-manager.mjs`)

#### new ContinueConfigManager(logger?, metrics?)

```javascript
const manager = new ContinueConfigManager(logger, metrics);
// or
import { createContinueConfigManager } from './continue-config-manager.mjs';
const manager = createContinueConfigManager(logger, metrics);
```

**Parameters**:
- `logger` (optional): Object with `.log(level, message)` method
- `metrics` (optional): Object with `.record(name, value)` method

#### manager.readConfig()

```javascript
const config = await manager.readConfig();
```

**Returns**: Promise<Object> with `{ models: [...] }`

**Behavior**:
- Returns empty config if file not found
- Validates schema after parsing
- Records metrics if provided

**Throws**: `ConfigError`, `ValidationError`, `FileIOError`

**Example**:

```javascript
const manager = createContinueConfigManager();
const config = await manager.readConfig();
console.log(`Loaded ${config.models.length} models`);
```

#### manager.writeConfig(config)

```javascript
await manager.writeConfig(config);
```

**Parameters**: `config` — Configuration object to write

**Behavior**:
- Creates `~/.continue` directory if needed
- Backs up existing file
- Validates schema before writing
- Indents with 2 spaces

**Throws**: `ConfigError`, `ValidationError`, `FileIOError`

**Example**:

```javascript
const config = {
  models: [
    {
      title: 'GPT-4',
      provider: 'openai',
      model: 'gpt-4',
      apiKey: 'sk-...'
    }
  ]
};
await manager.writeConfig(config);
```

#### manager.mergeModels(config, modelsToMerge)

```javascript
const merged = await manager.mergeModels(config, [
  { title: 'Claude', provider: 'anthropic', model: 'claude-3' }
]);
```

**Behavior**:
- Case-insensitive title matching
- Updates existing, appends new
- Validates schema after merge

**Returns**: Promise<Object> — Merged config

#### manager.removeModels(config, modelTitles)

```javascript
const result = await manager.removeModels(config, ['GPT-4', 'Claude']);
```

**Returns**: Promise<Object> — Config with models removed

#### manager.validateSchema(config)

```javascript
manager.validateSchema(config); // Throws if invalid
```

**Throws**: `ValidationError` if schema invalid

## Error Handling

### Exception Hierarchy (C#)

```
ConfigurationException (base)
├── SchemaValidationException
└── (file I/O errors with code: FILE_IO_ERROR, JSON_PARSE_ERROR)
```

### Exception Hierarchy (Node.js)

```
ConfigError (base)
├── ValidationError
└── FileIOError
```

### Common Errors

| Error Code | Meaning | Recovery |
|------------|---------|----------|
| `MISSING_TITLE` | Model title is empty or missing | Add non-empty title |
| `MISSING_PROVIDER` | Model provider is empty or missing | Add non-empty provider |
| `MISSING_MODEL` | Model field is empty or missing | Add non-empty model identifier |
| `DUPLICATE_TITLE` | Two models have same title | Rename one model |
| `JSON_PARSE_ERROR` | Config file is corrupted JSON | Restore from backup or recreate |
| `FILE_IO_ERROR` | Cannot read/write config file | Check file permissions, disk space |
| `VALIDATION_ERROR` | Schema validation failed | See error details for field path |

### Graceful Degradation

**Missing config file**: Returns empty config, does NOT throw

```csharp
// Returns { Models: new List<ContinueConfigModel>() } if file doesn't exist
var config = await ContinueConfigurationManager.ReadConfigAsync();
```

```javascript
// Returns { models: [] } if file doesn't exist
const config = await manager.readConfig();
```

**Optional dependencies**: Logger/metrics are null-safe

```csharp
var manager = new ContinueConfigurationManager(); // No logger/metrics
// Still works, just doesn't log/record metrics
```

```javascript
const manager = createContinueConfigManager(null, null); // No logger/metrics
// Still works, gracefully degraded
```

## Performance Characteristics

| Operation | Target | Typical | Notes |
|-----------|--------|---------|-------|
| readConfig() | <200ms | ~50ms | Depends on file size, disk I/O |
| writeConfig() | <500ms | ~100ms | Includes validation + backup |
| validateSchema() | <50ms | ~10ms | Linear with model count |
| mergeModels() | <100ms | ~20ms | Depends on merge size |
| removeModels() | <50ms | ~10ms | Linear with model count |

**Memory**: Config with 100 models ~ 50KB (JSON + parsed object)

## Integration Examples

### Use Case 1: Load and Display Available Models

**C#**:

```csharp
var config = await ContinueConfigurationManager.ReadConfigAsync();
foreach (var model in config.Models)
{
    Console.WriteLine($"{model.Title}: {model.Model} ({model.Provider})");
}
```

**Node.js**:

```javascript
const manager = createContinueConfigManager(logger);
const config = await manager.readConfig();
config.models.forEach(m => {
  console.log(`${m.title}: ${m.model} (${m.provider})`);
});
```

### Use Case 2: Add a New Model and Save

**C#**:

```csharp
var config = await ContinueConfigurationManager.ReadConfigAsync();

var newModel = new ContinueConfigModel
{
    Title = "Claude-3-Opus",
    Provider = "anthropic",
    Model = "claude-3-opus-20240229",
    ApiKey = "sk-ant-..."
};

var merged = await ContinueConfigurationManager.MergeModelsAsync(config, new[] { newModel });
await ContinueConfigurationManager.WriteConfigAsync(merged);
```

**Node.js**:

```javascript
const manager = createContinueConfigManager(logger, metrics);
const config = await manager.readConfig();

const newModel = {
  title: 'Claude-3-Opus',
  provider: 'anthropic',
  model: 'claude-3-opus-20240229',
  apiKey: 'sk-ant-...'
};

const merged = await manager.mergeModels(config, [newModel]);
await manager.writeConfig(merged);
```

### Use Case 3: Validate User Input (Handler Pattern)

**Node.js** (in handler context):

```javascript
import { ContinueConfigManager, ValidationError } from './continue-config-manager.mjs';

export const addModelHandler = async (request, logger, metrics) => {
  const manager = createContinueConfigManager(logger, metrics);

  try {
    const { model } = request.payload;

    // Validate before merge
    manager.validateSchema({ models: [model] });

    const config = await manager.readConfig();
    const merged = await manager.mergeModels(config, [model]);
    await manager.writeConfig(merged);

    return { success: true, model };
  } catch (err) {
    if (err instanceof ValidationError) {
      return { success: false, error: `Invalid model: ${err.message}`, field: err.fieldPath };
    }
    return { success: false, error: err.message };
  }
};
```

## Related Steps & Integration

### Step 95: Settings-Sync Handler

Step 95 handles **IDE ↔ Continue** settings synchronization via JSON-RPC messages. Step 104 can be used by Step 95 to persist loaded settings back to the config file.

**Example integration**:

```javascript
// In settings-sync handler
const configManager = createContinueConfigManager(logger, metrics);

async function loadSettings(scope = 'all') {
  const config = await configManager.readConfig();
  // Extract settings from config.models[0] and return
}

async function applySettings(settingsPayload) {
  const config = await configManager.readConfig();
  // Merge new settings into config.models
  const merged = await configManager.mergeModels(config, [newModel]);
  await configManager.writeConfig(merged);
}
```

### Step 103: Crash Recovery

Step 103 (crash recovery) can use config backups created by Step 104's write operations. Backup file: `~/.continue/config.json.backup`

### Step 110+: E2E Scenario Tests

Step 110+ (end-to-end tests) can use config manager to set up multi-model test scenarios:

```javascript
// In E2E test setup
async function setupTestConfig() {
  const manager = createContinueConfigManager();
  const testConfig = {
    models: [
      { title: 'GPT-4', provider: 'openai', model: 'gpt-4' },
      { title: 'Claude', provider: 'anthropic', model: 'claude-3' },
      { title: 'Local', provider: 'local', model: 'llama-7b' }
    ]
  };
  await manager.writeConfig(testConfig);
}
```

### Step 112+: Regression Test Suite

Step 112+ (regression tests) can use config manager with fixture configs to verify backward compatibility:

```javascript
import { validConfigs, invalidConfigs } from './mocks/continue-config-fixtures.mjs';

describe('Configuration Regression Suite', () => {
  for (const [name, config] of Object.entries(validConfigs)) {
    it(`should handle ${name}`, async () => {
      const manager = createContinueConfigManager();
      manager.validateSchema(config);
      await manager.writeConfig(config);
      const read = await manager.readConfig();
      // Assertions...
    });
  }
});
```

## Testing

### C# Tests

Run xUnit tests:

```bash
dotnet test src/VSIXProject1.Tests.csproj --filter "ContinueConfigurationManagerTests"
```

**Coverage**: 25+ tests across 5 suites (File I/O, Schema Validation, Merging, Write Operations, Error Handling)

### Node.js Tests

Run Mocha tests:

```bash
mocha src/versions/v2.0.0/tests/continue-config-manager.test.mjs --require esm
```

**Coverage**: 30+ tests across 6 suites (Initialization, File I/O, Schema Validation, Merging, Performance, Error Handling)

### Using Fixtures

```javascript
import {
  validConfigs,
  invalidConfigs,
  mergeScenarios,
  createMockLogger,
  createMockMetrics
} from './mocks/continue-config-fixtures.mjs';

it('should handle valid configs', () => {
  for (const config of Object.values(validConfigs)) {
    manager.validateSchema(config); // Should not throw
  }
});

it('should reject invalid configs', () => {
  for (const scenario of Object.values(invalidConfigs)) {
    assert.throws(() => manager.validateSchema(scenario.value));
  }
});
```

## Troubleshooting

### "Cannot read config: Permission denied"

**Cause**: No read permissions on `~/.continue/config.json` or parent directory

**Solution**:

```bash
# Check permissions
ls -la ~/.continue/config.json
# Fix if needed
chmod 644 ~/.continue/config.json
chmod 755 ~/.continue
```

### "Invalid JSON in config"

**Cause**: Config file is corrupted or edited incorrectly

**Solution**:

1. Check `~/.continue/config.json.backup` for restored version
2. Restore from backup:

```bash
cp ~/.continue/config.json.backup ~/.continue/config.json
```

3. Or recreate with valid JSON

### "Duplicate model title"

**Cause**: Two models have the same title

**Solution**: Edit config file and rename one model's title, or use `removeModels()` to remove duplicate:

```javascript
const manager = createContinueConfigManager();
const config = await manager.readConfig();
const cleaned = await manager.removeModels(config, ['DuplicateTitle']);
// Manual rename in config file needed for other duplicate
```

## Dependencies

**C#**: None beyond .NET Framework 4.7.2+ (System.Text.Json, System.IO, System.Threading)

**Node.js**: None (uses fs/promises, os, path built-ins)

## Version Compatibility

**Step 104 Version**: 1.0.0

**Continue SDK Compatibility**: v2.0.0+ (requires models array in config)

**Bridge Compatibility**: npm-based Continue bridge v2.1+

---

**Step 104 Complete**: Bridge-side configuration persistence layer for Continue SDK config files. Integrated with Step 95 (settings-sync), Step 103 (crash recovery), and Steps 110+ (E2E/regression tests).
