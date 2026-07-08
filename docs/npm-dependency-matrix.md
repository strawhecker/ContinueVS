# npm Dependency Matrix

**Location**: `docs/npm-dependency-matrix.md`  
**Version**: 1.0  
**Last Updated**: 2024-01-15  
**Owner**: Bridge Architecture Team  
**Related**: Steps 6, 34, 35

---

## Bridge Package Inventory

### Continue npm Package

| Property | Value |
|----------|-------|
| **Package Name** | `continue` |
| **Current Version** | `2.0.0` |
| **Registry** | npmjs.org |
| **License** | MIT |
| **Repository** | https://github.com/continuedev/continue |
| **Estimated Size** | 25-35 MB (compressed: 8-12 MB as .tgz) |
| **Node.js Requirement** | ≥18.0.0 |
| **Download Time** | ~5-15 seconds (on 10 Mbps connection) |
| **Build/Install Time** | ~30-60 seconds (first time) |

### Package Purpose

The `continue` npm package provides:
- **Language Server Protocol (LSP)** implementation for bridge communication
- **Handler implementations** for VS Code webview → IDE bridges
- **Telemetry and analytics** collection
- **Configuration management** and hot-reload support
- **Streaming response** handling for long-running operations

---

## Version Compatibility Matrix

### Node.js Compatibility

| Continue Version | Min Node.js | Max Node.js | Tested Node.js |
|---|---|---|---|
| 2.0.0 | 18.0.0 | 20.x | 18.17, 19.x, 20.x |
| 2.1.0 (future) | 18.0.0 | 22.x | 18.17, 20.x, 22.x |

### Windows/Linux Compatibility

| Continue Version | Windows | Linux | macOS |
|---|---|---|---|
| 2.0.0 | ✅ (x64, ARM64) | ✅ (x64, ARM64) | ✅ (Intel, Apple Silicon) |
| 2.1.0 (future) | ✅ (x64, ARM64) | ✅ (x64, ARM64) | ✅ (Intel, Apple Silicon) |

### ContinueVS Bridge Compatibility

| Bridge Version | Continue Version | Status |
|---|---|---|
| v1.0.0 | Continue 1.x | Deprecated |
| v2.0.0 (current) | Continue 2.0.0 | Active |
| v2.1.0 (planned) | Continue 2.0.0+ | In development |

---

## Dependency Tree

### Continue v2.0.0 Dependencies

```
continue@2.0.0
├── @types/node@^18.0.0
│   └── (dev dependency; not bundled)
├── typescript@^5.0.0
│   └── (dev dependency; not bundled)
├── esbuild@^0.17.0
│   └── (build tool; not bundled)
└── [other development dependencies]

Note: The published npm package includes NO runtime dependencies.
The bridge distributes the compiled JavaScript bundle.
```

### Runtime Requirements

| Component | Version | Purpose |
|-----------|---------|---------|
| **Node.js Runtime** | ≥18.0.0 | Execute bridge server |
| **.NET Runtime** | 4.7.2 or 10.0 | Host VSIX extension |
| **npm** | ≥8.0.0 (optional) | Package management (optional; pre-cached recommended) |

---

## Cache Size and Download Estimates

### Disk Space Requirements

| Component | Size (Compressed) | Size (Uncompressed) | Notes |
|---|---|---|---|
| `continue-v2.0.0.tgz` | 8-12 MB | 25-35 MB | Main package |
| `.metadata/cache-manifest.json` | ~1 KB | ~1 KB | Metadata only |
| Total per version | ~8-12 MB | ~25-35 MB | Single version |
| Multi-version (5 versions) | ~50 MB | ~150 MB | Historical versions |

### Network Download Estimates

| Connection Speed | Time to Download v2.0.0 |
|---|---|
| 1 Mbps (cellular) | ~60-100 seconds |
| 10 Mbps (broadband) | ~6-10 seconds |
| 100 Mbps (fiber) | <1 second |
| 1 Gbps (enterprise) | <1 second |

### CI/CD Build Time Impact

| Scenario | Time |
|---|---|
| Cache hit (local, validated) | <1 second |
| Cache miss, download from registry | 10-30 seconds |
| First-time setup (download + validate) | 30-60 seconds |
| Pre-populated cache (CI agent) | <1 second |

---

## Installation & Validation Workflow

### Step-by-Step Installation

1. **Step 11 - Download on First Use** (5-30 seconds)
   - Check `.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz`
   - If missing: Download from npm registry
   - Save to cache directory

2. **Step 12 - Validate at Startup** (2-5 seconds)
   - Compute SHA256 of `.tgz` file
   - Compare to `.tgz.sha256`
   - Update `cache-manifest.json` status = `validated`

3. **Step 13+ - Load and Execute** (<1 second)
   - Extract `.tgz` to temp directory (if needed)
   - Load bridge server (`core-server.js`)
   - Begin message handling

### Validation Checklist

- ✅ `.tgz` file exists in cache
- ✅ `.tgz.sha256` file exists with checksum
- ✅ SHA256 computed value matches file value
- ✅ `cache-manifest.json` status = `validated`
- ✅ Node.js version ≥18.0.0
- ✅ File permissions allow read access

---

## Update and Rollback Strategy

### Upgrading to Continue 2.0.1 (Example)

1. Create new cache directory:
   ```
   .cache/npm-packages/v2.0.1/
   ```

2. Download new package (Step 35):
   ```
   → Registry: continue@2.0.1
   → Download: continue-v2.0.1.tgz
   → Cache: .cache/npm-packages/v2.0.1/
   ```

3. Generate checksums (Step 37):
   ```
   → SHA256: continue-v2.0.1.tgz.sha256
   ```

4. Validate (Step 12):
   ```
   → Verify checksums match
   → Update cache-manifest.json
   ```

5. Switch version (Step 9/10):
   ```
   → Update configuration
   → Next startup loads v2.0.1
   ```

### Rolling Back to Continue 2.0.0

1. Identify issue with v2.0.1
2. Check cache for v2.0.0:
   ```
   .cache/npm-packages/v2.0.0/
   ```
3. If cache valid: Switch configuration → restart
4. If cache invalid: Re-download v2.0.0 → validate → restart

---

## Security Considerations

### SHA256 Hash Verification

- Prevents tampering with cached packages
- Detects corrupted downloads
- Ensures reproducible builds

### npm Registry Verification

When downloading from npm registry:
- Verify HTTPS connection to npmjs.org
- Check npm package signature (if available)
- Compare published hash to local computation

### Air-Gapped Deployment

For air-gapped environments:
- Pre-validate all packages before distribution
- Document SHA256 hashes in manifest
- Store manifests with package archives
- Verify hashes match before loading

---

## Troubleshooting Reference

| Issue | Symptom | Root Cause | Solution |
|---|---|---|---|
| Cache miss | Download fails | `.tgz` not in cache | Step 11: trigger download |
| Invalid hash | SHA256 mismatch | Corrupted `.tgz` | Delete + re-download |
| Missing manifest | `cache-manifest.json` not found | Step 5 incomplete | Recreate metadata |
| Node.js version | Cannot load bridge | Node < 18.0.0 | Upgrade Node.js |
| Registry unreachable | Cannot download | Network issue | Use offline/cached |
| Version conflict | Wrong version loaded | Configuration mismatch | Verify `package.json` |

---

## Integration Points

### Step 5: Create npm cache directory structure
- Creates directory layout
- Initializes `cache-manifest.json`
- Sets initial status = `pending_download`

### Step 11: Create npm cache download on first use
- Implements download logic
- Saves `.tgz` to cache
- Updates manifest status = `downloaded`

### Step 12: Create npm package validation on startup
- Computes SHA256
- Validates against `.tgz.sha256`
- Updates manifest status = `validated`

### Step 35: Download & verify Continue npm package v2.0.0
- Queries npm registry
- Downloads specific version
- Generates checksums

### Step 37: Generate checksums for npm packages
- Creates `.tgz.sha256` files
- Includes in manifest

### Step 34: Create npm dependency documentation (Future)
- Expand on this matrix
- Add troubleshooting workflows
- Document custom build scenarios

---

## References

- **npm Registry**: https://www.npmjs.com/package/continue
- **Continue GitHub**: https://github.com/continuedev/continue
- **Node.js LTS Schedule**: https://nodejs.org/en/about/releases/schedule/
- **SHA256 Specification**: https://en.wikipedia.org/wiki/SHA-2
- **docs/npm-cache-strategy.md**: Strategy and architecture
