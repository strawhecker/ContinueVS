# ADR: Step 11 - npm Cache Download on First Use

**Decision ID**: ADR-11  
**Title**: Cache-First Download Strategy for Continue npm Packages  
**Status**: Accepted  
**Date**: 2024-01-15  
**Related Steps**: 8 (integrity validation), 12 (startup validation), 35 (download & verify)  

---

## Problem Statement

ContinueVS Bridge requires automatic download of Continue npm packages when not cached locally. The solution must:

1. **Avoid unnecessary downloads** — Check local cache first using Step 8 validation
2. **Be resilient** — Handle network failures, timeouts, and corruption gracefully
3. **Be secure** — Validate integrity of all downloaded packages
4. **Support offline use** — Allow pre-populated caches in air-gapped environments (Step 12)
5. **Be maintainable** — Use only Node.js built-ins, no external npm dependencies

**Technical constraints**:
- Step 8 (`integrity.js`) provides validation API
- Step 12 requires that packages are available before startup
- Step 35 will use this module to fetch official v2.0.0 package
- Must work in offline environments without falling back to registry

---

## Decision: npm Registry as Source (Cache-First Strategy)

### Chosen Approach

Implement **cache-first download** using npm registry as authoritative source:

1. **Check local cache** — Call Step 8 `validatePackageIntegrity()` on cached package
2. **Return cached if valid** — No download needed, fast startup
3. **Download if missing/invalid** — Fetch from `https://registry.npmjs.org/continue/-/continue-{version}.tgz`
4. **Validate after download** — Re-run Step 8 validation to confirm integrity
5. **Persist to cache** — Store .tgz and .sha256 for future use

**File**: `src/versions/v2.0.0/lib/cache-download.js`

**Main Function**:
```javascript
await downloadPackageIfNeeded(version, cacheDir, options)
→ returns { cached, valid, packagePath, downloadTime, errors }
```

**Helper Functions**:
```javascript
await downloadPackageFromRegistry(packageName, version, targetDir, options)
await generateChecksum(filePath)
```

---

## Rationale

### Why npm Registry (not direct tarball URL)?

| Criterion | npm Registry | Direct Tarball URL | VSIX Bundle |
|-----------|--------------|-------------------|-------------|
| **Source Reliability** | Official, replicated | Subject to URL changes | Static, no updates |
| **Metadata Available** | Full package info | No metadata | No updates possible |
| **VSIX Size** | Small (~50 bytes metadata) | N/A | ~50+ MB bloated |
| **Offline Fallback** | ✅ Via Step 12 cache | ✗ Single point of failure | ✓ Included |
| **Update Support** | ✅ Easy new versions | ✗ Hard to support v2.1.0 | ✗ Requires re-release |
| **Security** | ✅ npm integrity checks | ⚠️ Manual verification | ✓ Static package |

**Winner**: npm registry balances reliability, maintainability, and security.

### Why Cache-First (not Online-First)?

| Criterion | Cache-First | Online-First | Offline-Only |
|-----------|-------------|--------------|--------------|
| **Startup Speed** | ~0ms (cached) | ~2-10s (network) | ~0ms (cached) |
| **User Experience** | Fast on repeat use | Slow always | Only if pre-cached |
| **Offline Support** | Partial (cached copy) | None | Full |
| **Latest Package** | Requires manual update | Always fresh | Stale packages |

**Winner**: Cache-first provides best user experience for typical workflows while supporting offline via Step 12.

### Why Not Bundled in VSIX?

- VSIX extension would be **50+ MB** (current: ~5 MB)
- Cannot support version updates without re-releasing extension
- Users cannot delete or swap packages easily
- Download strategy is more flexible and maintainable

---

## Implementation Details

### Download Workflow

```
User opens VS
  ↓
Bridge initialization (Step 12)
  ↓
Call: downloadPackageIfNeeded('2.0.0', '.cache/npm-packages/v2.0.0')
  ├─→ validatePackageIntegrity() [Step 8]
  │    ├─→ VALID: return { cached: true, valid: true, ... } ✅
  │    └─→ INVALID/MISSING: proceed
  │
  ├─→ downloadPackageFromRegistry()
  │    ├─→ https.get(npm_url) → temp file
  │    ├─→ ON TIMEOUT (60s): error, retry once
  │    ├─→ ON 404/5xx: error, no retry
  │    └─→ ON SUCCESS: rename temp to final location
  │
  ├─→ generateChecksum() [new file: .sha256]
  │    └─→ SHA256 of .tgz → write npm format
  │
  ├─→ validatePackageIntegrity() [Step 8 again]
  │    ├─→ VALID: return { cached: false, valid: true, ... } ✅
  │    └─→ INVALID: delete .tgz, return error
  │
  └─→ Bridge starts if valid=true, else fail gracefully
```

### Error Handling

All errors are collected in `errors: []` array. No exceptions thrown from `downloadPackageIfNeeded()`:

| Error | Handling | Recovery |
|-------|----------|----------|
| **Network timeout** | Log, retry once, collect error | Manual retry or Step 12 fallback |
| **404 / 5xx HTTP** | Log, fail immediately | Check version, manual fix |
| **Empty download** | Delete temp file, collect error | Retry or Step 12 fallback |
| **Checksum mismatch** | Delete, retry once, collect error | Auto-retry or manual |
| **No write permission** | Collect error, don't crash | Check directory permissions |

**Logging**: All operations logged to `.cache/npm-packages/.download-log` with ISO timestamp.

### Timeout & Retry Strategy

- **Download timeout**: 60 seconds per attempt (configurable)
- **Checksum mismatch**: Auto-retry once (single retry only)
- **HTTP errors**: No retry (immediate failure)
- **Network errors**: Retry once (connection refused, etc.)

**Rationale**: Balances robustness (retry once) with user experience (don't wait 2+ minutes).

---

## Alternatives Considered

### Alternative 1: Online-First (Always Check Registry)

**Pros**:
- Always latest version
- No disk space for cache

**Cons**:
- Slow startup if network congested (2-10 seconds wait)
- Fails in offline environments
- User cannot control local cache

**Rejected**: Cache-first better for typical workflows.

### Alternative 2: Bundled .tgz in VSIX

**Pros**:
- Guaranteed offline availability
- No registry dependency
- Fast initial extraction

**Cons**:
- VSIX becomes 50+ MB (bloated)
- Cannot update package without re-releasing extension
- No support for user version selection (Step 9)

**Rejected**: Too inflexible; cache-first + Step 12 validation provides better solution.

### Alternative 3: Git LFS for Binary Storage

**Pros**:
- Version control integration
- Easy package updates in repo

**Cons**:
- Requires Git LFS setup (adds dependency)
- .cache/ directory should be ignored anyway
- Complicates CI/CD

**Rejected**: Direct registry download simpler.

---

## Trade-offs

| Trade-off | Decision | Rationale |
|-----------|----------|-----------|
| **First-time latency** | 2-10s download acceptable | Acceptable for one-time cost; mitigated by caching |
| **Network dependency** | Offline fallback via Step 12 | Cache-first + validation supports air-gapped via pre-population |
| **Partial failure handling** | Fail gracefully, collect errors | Better UX than throwing exceptions |
| **Retry logic** | Single retry only | Balances robustness with user patience |

---

## Success Criteria

✅ **Cache hit** — Existing valid package used, no download (< 10ms overhead)  
✅ **Cache miss** — Package downloaded, validated, cached (2-10s first time)  
✅ **Corruption detection** — Invalid package detected and re-downloaded  
✅ **Offline support** — Step 12 can validate pre-cached packages without registry  
✅ **Error resilience** — Network errors don't crash Bridge; logged for diagnostics  
✅ **No external deps** — Only Node.js built-ins (https, fs, crypto)  
✅ **Testability** — Mock-friendly https API enables unit testing  

---

## Risks & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|-----------|
| **npm registry downtime** | Low | Bridge launch fails | Step 12 fallback (offline mode) |
| **Package corruption** | Very Low | Detected & re-downloaded | SHA256 validation after download |
| **Slow network** | Medium | User wait 2-10s | Acceptable first-time cost; improvements post-GA |
| **Disk full during download** | Low | Partial file left | Temp file cleanup on error |
| **User cache directory read-only** | Low | Clear error message | Directory permission check in Step 12 |

---

## Implementation Status

**Created**: `src/versions/v2.0.0/lib/cache-download.js`

**Exports**:
- `downloadPackageIfNeeded()` — Main entry point
- `downloadPackageFromRegistry()` — Network download
- `generateChecksum()` — SHA256 + .sha256 file

**Testing**:
- Unit tests: `src/versions/v2.0.0/test/cache-download.test.js`
- Integration: Step 30 (end-to-end with real npm registry)

**Dependencies**:
- ✅ Step 8: `integrity.js` (validates packages)
- ⏳ Step 12: Uses this module to ensure package available
- ⏳ Step 35: Uses this module to fetch official v2.0.0

---

## Future Enhancements (Post-GA)

1. **Progress reporting** — Stream download progress to UI
2. **Compression** — Optional gzip compression for slower networks
3. **P2P fallback** — Peer-to-peer fallback if registry unavailable (v2.1.0)
4. **Version auto-upgrade** — Automatic migration to new versions
5. **Regional mirrors** — npm CDN for faster downloads in different regions

---

## References

- **Step 8**: `docs/npm-integrity-utility.md` — Validation API
- **Step 12**: `docs/adr/step-12-startup-validation.md` — Uses this module
- **Step 35**: Download & verify official package
- **Strategy**: `docs/npm-cache-strategy.md` — Cache architecture

---

**Decision**: ✅ **Accepted** by Architecture Team  
**Implementation**: Step 11 (this ADR)  
**Validation**: Step 30 integration tests  
**Next Step**: Step 12 (npm package validation on startup)

