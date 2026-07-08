# ContinueVS Bridge — Version Registry

**Location**: Root directory  
**Last Updated**: 2024-01-15  
**Status**: Active

---

## Overview

This document catalogs all available versions of the npm-based Continue bridge, their availability, compatibility, and status. Each version is stored in `src/versions/` with downloadable npm packages cached in `.cache/npm-packages/`.

---

## Version Manifest Schema

Each version directory (`src/versions/vX.Y.Z/`) contains:

| Component | File/Dir | Purpose |
|-----------|----------|---------|
| **Core** | `core-server.js` | Entry point for bridge server (Step 13) |
| **Config** | `package.json` | npm dependencies & metadata (Step 2) |
| **Manifest** | `manifest.json` | Version metadata & checksums (Step 4) |
| **Handlers** | `handlers/` | Handler modules (Steps 50–95) |
| **Utilities** | `lib/` | Shared adapters, transports, validation |
| **Tests** | `tests/` | Unit & integration test suite |

### manifest.json Schema

```json
{
  "version": "2.0.0",
  "continueVersion": "0.4.x",
  "releaseDate": "2024-01-15",
  "status": "stable",
  "requirements": {
    "node": ">=18.0.0",
    "npm": ">=9.0.0"
  },
  "checksums": {
    "package_tarball": "sha256:..."
  },
  "handlers": [
    "getEditorState",
    "onEditorStateChange",
    "search",
    "goToDefinition",
    "findReferences"
  ],
  "transportModes": ["stdio"]
}
```

---

## Available Versions

### v2.0.0 (Current)

| Property | Value |
|----------|-------|
| **Status** | 🟡 In Development (Steps 1–45) |
| **Directory** | `src/versions/v2.0.0/` |
| **npm Package** | `.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz` |
| **Continue Target** | 0.4.x |
| **Node Requirement** | ≥18.0.0 |
| **Features** | Stdio transport, handler registry, webview integration |
| **Implementation** | 155-step master plan (Phases 1–5) |
| **Next Gate** | Part I tests pass (Step 45) |

**Steps Completed**: 1 (directory scaffold)  
**Steps In Progress**: 2–45 (Foundation & npm Setup)  
**Steps Pending**: 46–155 (WebView, handlers, release)

---

## Directory Structure

```
E:\GitRepos\ContinueVS\
│
├── src/
│   └── versions/
│       └── v2.0.0/                 ← Step 1 scaffold
│           ├── core-server.js       ← Step 13
│           ├── package.json         ← Step 2
│           ├── manifest.json        ← Step 4
│           ├── handlers/            ← Steps 50–95
│           ├── lib/
│           │   ├── transports/
│           │   ├── adapters/
│           │   └── utils/
│           └── tests/               ← Steps 27–31
│
└── .cache/
    └── npm-packages/
        └── v2.0.0/                  ← Step 5
            ├── continue-v2.0.0.tgz  ← Step 35
            ├── continue-v2.0.0.tgz.sha256
            └── manifest-v2.0.0.json
```

---

## Step Roadmap

| Phase | Steps | Scope | Gate |
|-------|-------|-------|------|
| **I. Foundation** | 1–45 | Directory scaffold, npm config, stdio transport | Step 45: All tests pass |
| **II. WebView** | 46–75 | Editor context, handler registration, integration | Step 75: E2E tests pass |
| **III. Handlers** | 76–115 | Refactoring, code completion, formatting, git | — |
| **IV. Advanced** | 116–140 | Metrics, crash recovery, persistence, compression | — |
| **V. Release** | 141–155 | Documentation, CI/CD, npm publishing, rollout | — |

---

## npm Dependency Cache Strategy

All Continue npm packages are cached locally and validated at startup:

**Key Principles**:
- ✅ **Cache-first**: Local cache checked before npm registry
- ✅ **Validated**: SHA256 checksums verified at startup (Step 12)
- ✅ **Offline-capable**: Works without internet in air-gapped environments
- ✅ **Multi-version**: Supports coexisting versions for downgrades

**Architecture**:
```
.cache/npm-packages/vX.Y.Z/
├── continue-vX.Y.Z.tgz              # Binary package (ignored in git)
├── continue-vX.Y.Z.tgz.sha256       # Checksum file (ignored in git)
└── .metadata/
    └── cache-manifest.json          # Status tracking (git-tracked)
```

**Download Flow**:
1. **Step 5**: Directory structure created
2. **Step 11**: Download on first use (cache-first strategy)
3. **Step 12**: Validate checksums on startup
4. **Step 35**: Download & verify from npm registry
5. **Step 37**: Generate SHA256 checksums

**For Details**: See `docs/npm-cache-strategy.md` and `docs/npm-dependency-matrix.md`

---

## Compatibility Matrix

| Bridge Ver | Continue | Node | npm | VS | Status |
|------------|----------|------|-----|----|----|
| v2.0.0 | 0.4.x | ≥18 | ≥9 | 2022+ | 🟡 In Dev |

---

## Future Versions (Planned)

- **v2.1.0**: Socket transport (optional, Step 100), compression (optional, Step 106)
- **v3.0.0**: Additional IDE support, enhanced diagnostics

---

## Usage

### For Developers

1. **Baseline Structure**: Step 1 creates empty directories
2. **Adding Features**: Each step populates directories per the 155-step plan
3. **Testing**: Unit tests live in `v2.0.0/tests/`
4. **npm Integration**: Configure in `package.json` (Step 2) → download via `npm install` (Step 7)

### For CI/CD

1. Validate manifest checksums (Step 37)
2. Run integrity checks on startup (Step 12)
3. Serve cached packages from `.cache/npm-packages/` (Step 35)

---

## References

- **Master Plan**: `docs/session-context.md` (155-step roadmap)
- **Protocol Design**: `docs/protocol.md`
- **Bridge Transport**: `src/VSIXProject1/IPC/StdioTransport.cs`
- **Continue Project**: [https://github.com/continuedev/continue](https://github.com/continuedev/continue)

---

## Next Steps

**Step 2**: Create `package.json` template for v2.0.0  
**Step 3**: Create version manifest schema (refine above)  
**Step 4**: Generate v2.0.0 manifest with checksums
