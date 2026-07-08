# npm Dependency Cache Strategy

**Location**: `docs/npm-cache-strategy.md`  
**Version**: 1.0  
**Last Updated**: 2024-01-15  
**Owner**: Bridge Architecture Team  
**Related**: Steps 5, 11, 12, 34, 35, 37

---

## Overview

The ContinueVS Bridge uses an **npm package cache strategy** to manage Continue npm packages reliably across development, CI/CD, and air-gapped environments. This document defines the architecture, download policies, validation procedures, and recovery strategies.

### Goals

- ✅ **Reproducible builds**: Exact package versions cached and verified
- ✅ **Offline capability**: Works without npm registry access (CI/CD, air-gapped)
- ✅ **Fast startup**: Local cache checked before registry
- ✅ **Security**: SHA256 validation ensures integrity
- ✅ **Maintainability**: Clear version tracking and downgrade support

---

## Architecture Overview

### Cache Layers

The bridge implements a **three-layer fallback chain**:

```
┌─────────────────────────────────────────┐
│ Layer 1: Local Cache (.cache/npm-packages/)
│ Status: Fast, offline-capable, validated
└─────────────────────────────────────────┘
                    ↓
         (if package missing or invalid)
                    ↓
┌─────────────────────────────────────────┐
│ Layer 2: npm Registry
│ Status: Online, authoritative source
└─────────────────────────────────────────┘
                    ↓
         (if both layers fail)
                    ↓
┌─────────────────────────────────────────┐
│ Layer 3: Error & Recovery
│ Status: Fail gracefully with instructions
└─────────────────────────────────────────┘
```

### Directory Structure

```
.cache/
└── npm-packages/
    ├── v2.0.0/                          # Cache for Continue v2.0.0
    │   ├── continue-v2.0.0.tgz          # Binary package
    │   ├── continue-v2.0.0.tgz.sha256   # Checksum file
    │   └── .metadata/
    │       └── cache-manifest.json      # Status and validation metadata
    └── v2.1.0/                          # Future: Cache for v2.1.0
        ├── continue-v2.1.0.tgz
        ├── continue-v2.1.0.tgz.sha256
        └── .metadata/
            └── cache-manifest.json
```

---

## Download Strategies

### Strategy 1: Cache-First (Default for Development)

**When**: Developer machine, first use, offline environment

**Flow**:
1. Check `.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz`
2. If present and valid (SHA256 matches): Load from cache
3. If missing or invalid: Download from npm registry → validate → cache
4. If registry unreachable: Error with offline instructions

**Code Path**: Step 11 (npm cache download on first use)

```
cache-first:
  → check local cache
    → VALID: use cached package
    → INVALID: delete + re-download + validate
    → MISSING: download + validate + cache
```

### Strategy 2: Online-First (Default for CI/CD)

**When**: CI/CD pipeline, explicit offline=false

**Flow**:
1. Query npm registry for latest v2.0.0 metadata
2. Compare local cache version: If match, skip download
3. If version mismatch: Download from registry → validate → cache
4. If registry unreachable: Fallback to cache-first

**Code Path**: Step 35 (download & verify)

```
online-first:
  → query npm registry
    → version match local cache: SKIP
    → version mismatch: download + validate + update cache
    → registry unreachable: fallback to cache-first
```

### Strategy 3: Offline-Only (Air-Gapped Environments)

**When**: Air-gapped deployment, no internet access

**Requirements**:
- Pre-populated `.cache/` with validated packages
- `cache-manifest.json` status = "validated"

**Flow**:
1. Check `.cache/npm-packages/v2.0.0/` exists
2. Validate SHA256 matches `.tgz.sha256`
3. Load from cache; never attempt registry
4. Error if package invalid or missing

**Code Path**: Step 12 (npm package validation on startup)

---

## Validation & Integrity Verification

### SHA256 Checksum Validation

Every cached `.tgz` file must have a corresponding `.sha256` checksum file:

**File Format: `continue-v2.0.0.tgz.sha256`**
```
abc123def456...abcdef  continue-v2.0.0.tgz
```

**Validation at Startup** (Step 12):
```
1. Read .tgz.sha256 file
2. Compute SHA256 of .tgz file
3. Compare: computed == file value
4. If match: status = "validated"
5. If mismatch: delete .tgz + re-download
```

### Manifest Validation

The `cache-manifest.json` tracks download and validation state:

**Schema**:
```json
{
  "version": "2.0.0",
  "created_at": "2024-01-15T10:30:00Z",
  "packages": [
    {
      "name": "continue",
      "version": "2.0.0",
      "filename": "continue-v2.0.0.tgz",
      "status": "pending_download|downloaded|validated",
      "sha256": "<hash_or_null>",
      "downloaded_at": "<iso8601_or_null>",
      "size_bytes": "<int_or_null>"
    }
  ]
}
```

**Status Values**:
- `pending_download` — Created by Step 5; awaiting download
- `downloaded` — After download (Step 11/35); validation pending
- `validated` — After SHA256 verification (Step 12); ready for use

---

## Version Pinning & Dependency Locking

### package.json Strategy

Each bridge version has explicit dependency versions in `src/versions/vX.Y.Z/package.json`:

```json
{
  "version": "2.0.0",
  "dependencies": {
    "continue": "2.0.0"
  },
  "engines": {
    "node": ">=18.0.0"
  }
}
```

**Rationale**:
- Exact semver (no `^` or `~`) ensures reproducible builds
- Enables rollback to previous versions (Step 10)
- Simplifies CI/CD caching and validation
- Prevents surprise breaking changes

### Downgrade Support

Cache structure supports multiple versions coexisting:

```
.cache/npm-packages/
├── v2.0.0/  → downgrade target
├── v2.0.1/  → security patch
└── v2.1.0/  → current version
```

**Downgrade Scenario** (Step 10):
1. User selects "Downgrade to v2.0.0"
2. Check `.cache/npm-packages/v2.0.0/` exists
3. Validate SHA256 matches
4. Update configuration to use v2.0.0
5. Next startup loads v2.0.0

---

## CI/CD Integration

### Pre-Populated Cache Pattern

In CI/CD agents, pre-populate `.cache/` to avoid registry access:

**CI Configuration** (e.g., GitHub Actions):
```yaml
- name: Pre-populate npm cache
  run: |
    mkdir -p .cache/npm-packages/v2.0.0/.metadata
    curl -L https://registry.npmjs.org/continue/2.0.0/-/continue-2.0.0.tgz \
      -o .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz
    sha256sum .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz > \
      .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz.sha256
```

**Benefits**:
- Faster builds (avoid npm registry latency)
- Reliable in rate-limited environments
- Reproducible across CI runs
- Works offline if registry temporarily down

### Artifact Caching

Cache `.cache/` as CI artifact for subsequent runs:

```yaml
- uses: actions/cache@v3
  with:
    path: .cache/npm-packages/
    key: npm-cache-${{ hashFiles('src/versions/*/package.json') }}
```

---

## Air-Gapped Deployments

### Pre-Populate for Air-Gapped Environments

In environments with **no internet access**:

1. **On connected machine**:
   - Run Step 35 to download packages
   - Generate Step 37 checksums
   - Create archive: `.cache/npm-packages/v2.0.0/`

2. **Distribute to air-gapped environment**:
   - Copy `.cache/npm-packages/` to target machine
   - Copy `cache-manifest.json` files

3. **On air-gapped machine**:
   - Startup (Step 12) validates SHA256 from cache
   - Offline strategy enabled (never attempts registry)
   - Loads packages directly from local cache

**Example Pre-Population Script**:
```bash
#!/bin/bash
# Download and validate packages for air-gapped deployment

mkdir -p .cache/npm-packages/v2.0.0/.metadata

# Download
npm pack continue@2.0.0 --pack-destination .cache/npm-packages/v2.0.0/
mv .cache/npm-packages/v2.0.0/continue-2.0.0.tgz .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz

# Generate checksums
sha256sum .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz > \
  .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz.sha256

# Create manifest
cat > .cache/npm-packages/v2.0.0/.metadata/cache-manifest.json <<EOF
{
  "version": "2.0.0",
  "created_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "packages": [{
    "name": "continue",
    "version": "2.0.0",
    "filename": "continue-v2.0.0.tgz",
    "status": "validated",
    "sha256": "$(cut -d' ' -f1 .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz.sha256)",
    "downloaded_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "size_bytes": $(stat -f%z .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz)
  }]
}
EOF
```

---

## Troubleshooting & Recovery

### Scenario 1: Cache Corruption

**Symptom**: SHA256 mismatch at startup

**Recovery**:
1. Delete corrupted `.tgz` file
2. Update `cache-manifest.json` status = `pending_download`
3. Re-download using cache-first strategy
4. Validate new checksum

**Command**:
```powershell
# PowerShell
Remove-Item .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz
# Next startup will re-download and validate
```

### Scenario 2: Missing Cache on Startup

**Symptom**: `cache-manifest.json` status = `pending_download` but `.tgz` missing

**Recovery**:
1. Check Step 11 logs for download failures
2. Attempt manual download:
   ```bash
   npm install continue@2.0.0 --save-dev
   cp node_modules/continue/package.json .cache/npm-packages/v2.0.0/
   ```
3. Generate checksums (Step 37)
4. Update `cache-manifest.json` status = `validated`

### Scenario 3: Registry Unreachable, Cache Invalid

**Symptom**: Cannot download; local cache invalid; offline

**Recovery**:
1. Manual validation: Compare `.tgz` MD5 to npm registry package info
2. If valid: Edit `cache-manifest.json` status = `validated` (manual override)
3. Force offline strategy in configuration
4. Report issue to team

### Scenario 4: Multiple Versions Conflict

**Symptom**: Bridge uses v2.0.0 but cache has v2.0.1

**Recovery**:
1. Check `src/versions/vX.Y.Z/package.json` for expected version
2. Download correct version to `.cache/npm-packages/vX.Y.Z/`
3. Ensure `cache-manifest.json` lists correct filename and hash
4. Force downgrade/upgrade via version selector (Step 9)

---

## Implementation Roadmap

| Step | Component | Description |
|------|-----------|---|
| **Step 5** | Cache directory structure | Create `.cache/npm-packages/vX.Y.Z/` layout |
| **Step 11** | Cache download | Implement cache-first strategy |
| **Step 12** | Validation | SHA256 verification on startup |
| **Step 35** | Package download | Download from npm registry |
| **Step 37** | Checksum generation | Generate `.tgz.sha256` files |
| **Step 38** | .gitignore | Exclude binary packages from git |

---

## Best Practices

1. **Always validate**: Never trust cache without SHA256 verification
2. **Lock versions**: Use exact semver, not ranges (`2.0.0`, not `^2.0.0`)
3. **Pre-populate CI**: Cache packages in CI agents for speed and reliability
4. **Monitor downloads**: Log all package downloads for audit trail
5. **Keep manifests**: `.cache-manifest.json` helps diagnose issues
6. **Clean regularly**: Remove old versions when downgrades no longer needed
7. **Document air-gaps**: Test offline scenarios in development

---

## References

- **Step 5**: npm cache directory structure
- **Step 11**: npm cache download on first use
- **Step 12**: npm package validation on startup
- **Step 35**: Download & verify Continue npm package
- **Step 37**: Generate checksums for npm packages
- **docs/npm-dependency-matrix.md**: Package inventory and compatibility
