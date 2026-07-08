# npm Integrity Check Utility — Usage Guide

**Module**: `src/versions/v2.0.0/lib/integrity.js`  
**Type**: Node.js ESM module  
**Status**: Step 8 (Created)  
**Related Steps**: 11 (cache download), 12 (startup validation), 37 (checksum generation)

---

## Overview

The integrity utility provides programmatic validation of npm package integrity through:
- **SHA256 checksum verification** — compares computed hash against expected value
- **Manifest metadata validation** — ensures version, Continue compatibility, and required fields
- **Structured result objects** — all operations return predictable JSON responses

## API Reference

### Main Exports

#### `validatePackageIntegrity(versionDir, version)`

**Purpose**: Orchestrator function for complete package validation (checksum + manifest)

**Parameters**:
- `versionDir` (string): Absolute path to version directory (e.g., `.cache/npm-packages/v2.0.0`)
- `version` (string): Version string (e.g., `'2.0.0'` or `'v2.0.0'`)

**Returns**: Promise resolving to result object:
```javascript
{
  valid: boolean,              // true if both checksum AND manifest valid
  version: string,             // normalized version (e.g., '2.0.0')
  versionDir: string,          // input directory path
  packagePath: string,         // resolved path to .tgz file
  manifestPath: string,        // resolved path to manifest.json
  checksumValid: boolean,      // true if SHA256 matches
  manifestValid: boolean,      // true if manifest structure ok
  metadata: {                  // from manifest (null if invalid)
    version: string,
    continueVersion: string,   // e.g., '0.4.x'
    releaseDate: string,       // ISO 8601 timestamp
    status: string,            // e.g., 'stable', 'tested'
    checksums: {
      sha256: string           // expected package hash
    }
  },
  errors: string[]             // detailed error messages (empty if valid)
}
```

**Usage Example**:
```javascript
import { validatePackageIntegrity } from './integrity.js';

const result = await validatePackageIntegrity(
  'E:\\GitRepos\\ContinueVS\\.cache\\npm-packages\\v2.0.0',
  '2.0.0'
);

if (result.valid) {
  console.log(`✅ Package ready: Continue ${result.metadata.continueVersion}`);
  // Step 12: Proceed to bridge launch
  // Step 11: Use cache, skip download
} else {
  console.error('❌ Validation failed:');
  result.errors.forEach(err => console.error(`  - ${err}`));
  // Step 11: Download from npm registry as fallback
}
```

---

#### `validatePackageChecksum(packagePath, checksumPath)`

**Purpose**: Validate SHA256 checksum of package .tgz file

**Parameters**:
- `packagePath` (string): Absolute path to `.tgz` file
- `checksumPath` (string): Absolute path to `.tgz.sha256` file

**Returns**: Promise resolving to result object:
```javascript
{
  valid: boolean,              // true if hashes match
  packagePath: string,         // input package path
  checksumPath: string,        // input checksum path
  computedHash: string,        // SHA256 of actual file (64 hex chars)
  expectedHash: string,        // SHA256 from checksum file
  error: string | null         // error message if invalid
}
```

**Checksum File Format**:
```
abc123def456...abcdef0123456789abcdef0123456789abcdef  continue-v2.0.0.tgz
```
(Hash, space separator, filename)

**Usage Example** (Step 37 — checksum generation):
```javascript
import { validatePackageChecksum } from './integrity.js';

const result = await validatePackageChecksum(
  '.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz',
  '.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz.sha256'
);

if (result.valid) {
  console.log(`✅ Checksum verified: ${result.computedHash.substring(0, 16)}...`);
} else {
  console.error(`❌ ${result.error}`);
}
```

---

#### `validateManifest(manifestPath, expectedVersion)`

**Purpose**: Validate manifest.json structure and metadata

**Parameters**:
- `manifestPath` (string): Absolute path to `manifest.json`
- `expectedVersion` (string): Expected version (e.g., `'2.0.0'`)

**Returns**: Promise resolving to result object:
```javascript
{
  valid: boolean,              // true if all validations pass
  manifestPath: string,        // input path
  version: string | null,      // parsed version (null if invalid)
  continueVersion: string | null, // Continue version (null if invalid)
  metadata: {                  // null if invalid
    version: string,
    continueVersion: string,
    releaseDate: string,
    status: string,
    checksums: { sha256: string }
  },
  error: string | null         // error message if invalid
}
```

**Manifest JSON Example**:
```json
{
  "version": "2.0.0",
  "continueVersion": "0.4.x",
  "releaseDate": "2024-01-15T10:30:00Z",
  "status": "stable",
  "checksums": {
    "sha256": "abc123def456...abcdef0123456789abcdef0123456789abcdef"
  }
}
```

---

### Error Types

All custom errors extend from `IntegrityError`:

```javascript
import { IntegrityError, ChecksumError, ManifestError } from './integrity.js';
```

- **`IntegrityError`**: Base class for all integrity validation failures
- **`ChecksumError`**: SHA256 mismatch (includes `expected` and `computed` properties)
- **`ManifestError`**: Manifest structure or JSON parse errors

---

## Integration Points

### Step 11: Cache Download Fallback

**When**: User opens Continue panel, cache validation runs

**Flow**:
```javascript
const result = await validatePackageIntegrity('.cache/npm-packages/v2.0.0', '2.0.0');

if (result.valid) {
  // Cache is good, skip download
  console.log('Using cached package');
} else {
  // Cache invalid, download from npm registry
  console.log('Cache invalid, downloading...');
  // ... fetch from npm registry
  // ... after download, regenerate checksums (Step 37)
}
```

---

### Step 12: Startup Validation

**When**: Bridge process initialization, before launching node process

**Flow**:
```javascript
// C# bridge initialization code calls this
const result = await validatePackageIntegrity(cacheDir, version);

if (!result.valid) {
  // Block bridge launch, display errors to user
  throw new Error(`Bridge initialization failed: ${result.errors.join('; ')}`);
}

// All checks passed, safe to launch
launchBridgeProcess(result.packagePath);
```

---

### Step 37: Checksum Generation

**When**: After downloading package from npm registry, before caching

**Flow**:
```javascript
import { validatePackageChecksum } from './integrity.js';
import crypto from 'crypto';
import fs from 'fs/promises';

// 1. Download package (omitted)
const downloadedPath = '...continue-v2.0.0.tgz';

// 2. Compute and save checksum
const hash = require('crypto')
  .createHash('sha256')
  .update(await fs.readFile(downloadedPath))
  .digest('hex');

const checksumPath = downloadedPath + '.sha256';
await fs.writeFile(checksumPath, `${hash}  continue-v2.0.0.tgz\n`);

// 3. Verify checksum was computed correctly
const result = await validatePackageChecksum(downloadedPath, checksumPath);
if (result.valid) {
  console.log('✅ Checksum generated and verified');
} else {
  throw new Error(`Checksum generation failed: ${result.error}`);
}
```

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Stateless (no caching)** | Allows detecting file changes (corruption, updates) |
| **Single-package validation** | Simpler, sufficient for startup checks (no parallelism needed) |
| **Manifest required** | Ensures version metadata is always available for bridge |
| **Silent operation** | Return structured results only; callers handle logging |
| **ESM module** | Native Node.js 18+ support, no transpilation needed |
| **No external deps** | Crypto, fs, path are built-ins; reduces attack surface |

---

## File Locations

**Module**: `src/versions/v2.0.0/lib/integrity.js`

**Expected Cache Structure**:
```
.cache/npm-packages/v2.0.0/
├── continue-v2.0.0.tgz              # Package binary
├── continue-v2.0.0.tgz.sha256       # Checksum file
└── manifest-v2.0.0.json             # Metadata manifest
```

---

## Testing Strategy (Step 31)

Test cases should cover:

1. **Happy Path**
   - Valid package with correct checksum
   - Valid manifest with all required fields

2. **Checksum Failures**
   - File missing (ENOENT)
   - Malformed checksum file format
   - Hash mismatch (corrupted package)
   - Invalid hash format (not 64 hex chars)

3. **Manifest Failures**
   - File missing
   - Invalid JSON
   - Missing required fields (version, continueVersion, etc.)
   - Version mismatch

4. **Error Handling**
   - Clear error messages with full paths
   - No console logging (silent mode)
   - Structured error arrays

---

## Migration from PowerShell

The Node.js integrity module replaces/supplements the PowerShell validation script:

| Check | PowerShell Script | Node.js Module |
|-------|-------------------|---|
| Package exists | ✅ Test-Path | ✅ fs.promises.readFile |
| SHA256 validation | ✅ Get-FileHash | ✅ crypto.createHash |
| Manifest JSON | ✅ ConvertFrom-Json | ✅ JSON.parse |
| Version check | ✅ String comparison | ✅ normalize + compare |

**Why Node.js version needed**:
- Steps 11, 12, 37 run in Node.js context (not PowerShell)
- Eliminates subprocess overhead
- Same logic, native performance

---

**Next**: Step 9 (version selection UI), Step 11 (cache download), Step 12 (startup validation)
