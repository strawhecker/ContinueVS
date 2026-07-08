# Version Manifest Schema Documentation

## Overview

The **manifest schema** (`manifest.schema.json`) is a JSON Schema (draft-07) that defines the structure and validation rules for all ContinueVS version manifest files. Each manifest file describes a released version of the Continue npm package, including metadata, integrity checksums, compatibility matrices, and feature flags.

**Location**: `src/versions/manifest.schema.json`  
**Format**: JSON Schema (draft-07)  
**Usage**: Every manifest file (e.g., `v2.0.0.json`, `v2.1.0.json`) must conform to this schema.

---

## Required Fields

All manifest files MUST include these top-level fields:

### 1. `version` (string, required)

Semantic version of the package in **X.Y.Z** format (no pre-release or build metadata).

- **Type**: `string`
- **Pattern**: `^\d+\.\d+\.\d+$`
- **Example**: `"2.0.0"`, `"1.85.2"`, `"3.0.0"`

```json
{
  "version": "2.0.0"
}
```

---

### 2. `releaseDate` (string, required)

ISO 8601 formatted timestamp when the package was released.

- **Type**: `string`
- **Format**: `date-time` (ISO 8601)
- **Example**: `"2024-01-15T10:30:00Z"`, `"2024-01-15T10:30:00+00:00"`

```json
{
  "releaseDate": "2024-01-15T10:30:00Z"
}
```

---

### 3. `npmPackage` (object, required)

Metadata about the npm package distribution, including name, version, and download URL.

#### npmPackage Properties

| Field | Type | Required | Pattern/Format | Description |
|-------|------|----------|---|---|
| `name` | string | ✓ | `^[@\w-]+(/[@\w-]+)?$` | Package name (scoped or unscoped) |
| `version` | string | ✓ | `^\d+\.\d+\.\d+$` | Package semantic version |
| `tarballUrl` | string | ✓ | URI (HTTPS) | Download URL for `.tgz` file |
| `registry` | string | ✗ | URI | NPM registry (defaults to npmjs.org) |

**Example:**

```json
{
  "npmPackage": {
    "name": "continue",
    "version": "2.0.0",
    "tarballUrl": "https://registry.npmjs.org/continue/-/continue-2.0.0.tgz",
    "registry": "https://registry.npmjs.org"
  }
}
```

---

### 4. `checksums` (object, required)

Cryptographic hashes for package integrity verification.

#### checksums Properties

| Field | Type | Required | Pattern | Description |
|-------|------|----------|---|---|
| `sha256` | string | ✓ | `^[a-f0-9]{64}$` | SHA-256 hash (64 hex chars, lowercase) |
| `sha512` | string | ✗ | `^[a-f0-9]{128}$` | SHA-512 hash (128 hex chars, lowercase) |

**Generation** (Node.js example):

```javascript
const crypto = require('crypto');
const fs = require('fs');

const fileBuffer = fs.readFileSync('continue-2.0.0.tgz');
const sha256 = crypto.createHash('sha256').update(fileBuffer).digest('hex');
const sha512 = crypto.createHash('sha512').update(fileBuffer).digest('hex');

console.log({
  sha256,
  sha512
});
```

**Example:**

```json
{
  "checksums": {
    "sha256": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",
    "sha512": "1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f"
  }
}
```

---

### 5. `compatibility` (object, required)

Compatibility matrix describing platform, Node.js, and VS Code version support.

#### compatibility Properties

| Field | Type | Items | Description |
|-------|------|-------|---|
| `vsCodeVersions` | array | string (semver) | VS Code versions this package works with |
| `nodeVersions` | array | string (semver) | Node.js versions required |
| `platforms` | array | enum: `win32`, `darwin`, `linux` | Supported operating systems |

**Example:**

```json
{
  "compatibility": {
    "vsCodeVersions": ["1.80.0", "1.81.0", "1.82.0", "1.83.0"],
    "nodeVersions": ["16.0.0", "18.0.0", "20.0.0"],
    "platforms": ["win32", "darwin", "linux"]
  }
}
```

---

## Optional Fields

### 6. `features` (object, optional)

Feature flags and stability markers for package capabilities.

#### features Properties

| Field | Type | Items | Description |
|-------|------|-------|---|
| `experimental` | array | string | Unstable features subject to breaking changes |
| `deprecated` | array | string | Features that will be removed in a future release |
| `stable` | array | string | Production-ready, stable features |

**Example:**

```json
{
  "features": {
    "experimental": [
      "webviewMessaging",
      "advancedSymbolSearch"
    ],
    "deprecated": [
      "legacyTransport",
      "oldConfigFormat"
    ],
    "stable": [
      "coreEditorIntegration",
      "diagnosticsCollection",
      "goToDefinition"
    ]
  }
}
```

---

### 7. `dependencies` (object, optional)

Version dependency rules and upgrade paths.

#### dependencies Properties

| Field | Type | Pattern | Description |
|-------|------|---|---|
| `minBridgeVersion` | string | `^\d+\.\d+\.\d+$` | Minimum Bridge version required |
| `previousVersions` | array | string (semver) | Previous version IDs that can migrate to this version |

**Example:**

```json
{
  "dependencies": {
    "minBridgeVersion": "1.0.0",
    "previousVersions": ["1.0.0", "1.1.0", "1.2.0"]
  }
}
```

---

## Complete Example Manifest

### `v2.0.0.json`

```json
{
  "version": "2.0.0",
  "releaseDate": "2024-01-15T10:30:00Z",
  "npmPackage": {
    "name": "continue",
    "version": "2.0.0",
    "tarballUrl": "https://registry.npmjs.org/continue/-/continue-2.0.0.tgz",
    "registry": "https://registry.npmjs.org"
  },
  "checksums": {
    "sha256": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",
    "sha512": "1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f"
  },
  "compatibility": {
    "vsCodeVersions": ["1.80.0", "1.81.0", "1.82.0", "1.83.0", "1.84.0"],
    "nodeVersions": ["16.0.0", "18.0.0", "20.0.0"],
    "platforms": ["win32", "darwin", "linux"]
  },
  "features": {
    "experimental": ["advancedSymbolSearch", "webviewMessaging"],
    "deprecated": ["legacyTransport"],
    "stable": [
      "coreEditorIntegration",
      "diagnosticsCollection",
      "goToDefinition",
      "findReferences",
      "codeCompletion"
    ]
  },
  "dependencies": {
    "minBridgeVersion": "1.0.0",
    "previousVersions": ["1.0.0", "1.1.0", "1.2.0"]
  }
}
```

---

### `v2.1.0.json` (Another Example)

```json
{
  "version": "2.1.0",
  "releaseDate": "2024-02-20T14:15:00Z",
  "npmPackage": {
    "name": "continue",
    "version": "2.1.0",
    "tarballUrl": "https://registry.npmjs.org/continue/-/continue-2.1.0.tgz",
    "registry": "https://registry.npmjs.org"
  },
  "checksums": {
    "sha256": "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b",
    "sha512": "2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a"
  },
  "compatibility": {
    "vsCodeVersions": ["1.84.0", "1.85.0", "1.86.0"],
    "nodeVersions": ["18.0.0", "20.0.0"],
    "platforms": ["win32", "darwin", "linux"]
  },
  "features": {
    "experimental": ["treeViewRefactor"],
    "deprecated": [],
    "stable": [
      "coreEditorIntegration",
      "diagnosticsCollection",
      "goToDefinition",
      "findReferences",
      "codeCompletion",
      "advancedSymbolSearch",
      "webviewMessaging"
    ]
  },
  "dependencies": {
    "minBridgeVersion": "1.1.0",
    "previousVersions": ["1.0.0", "1.1.0", "1.2.0", "2.0.0"]
  }
}
```

---

## Validation Rules

### Type Constraints

- **Semantic Versions**: All version fields (`version`, `npmPackage.version`, `dependencies.minBridgeVersion`) must follow **X.Y.Z** pattern (no pre-release or build metadata in this schema).
- **Checksums**: SHA-256 must be exactly **64 hex characters** (lowercase); SHA-512 must be exactly **128 hex characters** (lowercase).
- **Platforms**: Only `win32`, `darwin`, and `linux` are valid platform identifiers.

### Uniqueness & Cardinality

- **Platforms array** must not contain duplicates (enforced by `uniqueItems: true`).
- **Version fields** must be globally unique across all manifest files in the `src/versions/` directory.

### URL Validation

- **tarballUrl** must be a valid HTTPS URI pointing to a `.tgz` file.
- **registry** must be a valid HTTP(S) URI (typically `https://registry.npmjs.org`).

---

## Usage in Bridge Code

### C# Validation (Example)

After Step 17 (IBridgeConfiguration), a C# validator will be created:

```csharp
public class ManifestValidator
{
    private readonly string _schemaPath = "src/versions/manifest.schema.json";

    public ValidationResult ValidateManifest(string manifestJson)
    {
        var schema = JsonSchema.FromText(File.ReadAllText(_schemaPath));
        return schema.Validate(JObject.Parse(manifestJson));
    }
}
```

### Node.js Validation (Example)

In `core-server.js` or utilities:

```javascript
import Ajv from 'ajv';
import manifestSchema from './manifest.schema.json' assert { type: 'json' };

const ajv = new Ajv();
const validate = ajv.compile(manifestSchema);

function validateManifest(manifestJson) {
  const valid = validate(manifestJson);
  if (!valid) {
    throw new Error(`Invalid manifest: ${JSON.stringify(validate.errors)}`);
  }
  return manifestJson;
}
```

---

## Related Steps

- **Step 1**: Version management directory structure (creates `src/versions/` directory)
- **Step 4**: Create version manifest for v2.0.0 (will use this schema)
- **Step 12**: npm package validation on startup (will validate against this schema)
- **Step 37**: Generate checksums for npm packages (will populate checksum fields)

---

## Versioning the Schema

The schema itself uses `$id` for identification:

```json
{
  "$id": "https://continuevs.dev/schemas/version-manifest.schema.json"
}
```

If the schema structure changes in the future, increment a version in the `$id` URI:

```json
{
  "$id": "https://continuevs.dev/schemas/version-manifest.schema.v2.json"
}
```

Manifest files can reference a specific schema version if needed (future enhancement).

---

## References

- [JSON Schema Official Docs](https://json-schema.org/)
- [Draft 7 Specification](https://json-schema.org/draft-07/schema)
- [Semantic Versioning](https://semver.org/)
- [ISO 8601 Date/Time Format](https://en.wikipedia.org/wiki/ISO_8601)
