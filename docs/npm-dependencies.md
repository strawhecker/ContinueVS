# npm Dependencies Reference

**Location**: `docs/npm-dependencies.md`  
**Version**: 1.0  
**Last Updated**: 2024-01-15  
**Owner**: Bridge Architecture Team  
**Primary Contact**: ContinueVS Development Team  
**Related Docs**: [npm-cache-strategy.md](npm-cache-strategy.md) | [npm-dependency-matrix.md](npm-dependency-matrix.md) | [npm-install-script.md](npm-install-script.md)

---

## Quick Reference

| Item | Value |
|------|-------|
| **Primary Dependency** | `continue@2.0.5` (npm package) |
| **Node.js Requirement** | ≥18.0.0 |
| **Package Size** | 8–12 MB (compressed) / 25–35 MB (extracted) |
| **Installation Time** | 5–60 seconds (depends on cache hit) |
| **Expected Download Time** | <30 seconds (10 Mbps connection) |
| **Validation Overhead** | ~2–5 seconds (SHA256 checksum) |

---

## TL;DR for Operators

1. **Bridge requires ONE npm package**: Continue v2.0.5  
2. **Node.js** must be installed and ≥18.0.0  
3. **First launch** downloads and caches the package automatically  
4. **Validation** happens at startup (checksum verification)  
5. **If it fails**: See [Troubleshooting Guide](#troubleshooting-guide) below  

---

## Overview

The ContinueVS Bridge depends on a **single npm package** (`continue`) downloaded from the npm registry and cached locally for fast, offline-capable startup. This document serves as the **primary reference** for understanding:

- ✅ What npm packages the bridge needs
- ✅ How packages are downloaded and validated
- ✅ Troubleshooting when something goes wrong
- ✅ Safe upgrade and rollback procedures
- ✅ Health checks and CI/CD integration

**For deep dives**, see the related documents listed above.

---

## Document Structure

This guide is organized for different audiences:

- **Operators / DevOps**: Jump to [Installation & Validation](#installation--validation-workflow) and [Troubleshooting](#troubleshooting-guide)
- **Developers**: See [Architecture Overview](#architecture-overview) and [Health Check Reference](#health-check-reference)
- **CI/CD Teams**: Check [CI/CD Integration](#cicd-integration-best-practices)
- **Support / QA**: Review [Troubleshooting](#troubleshooting-guide) and [Upgrade Procedures](#safe-upgrade-procedures)

---

## Architecture Overview

### Dependency Resolution Flow

The bridge uses a **three-layer fallback chain** to resolve npm packages:

```
┌────────────────────────────────────────────────────────────┐
│ Layer 1: Check Local Cache (.cache/npm-packages/)          │
│ - Status: Fast, offline-capable, pre-validated             │
│ - Time: <1 second                                           │
└────────────────────────────────────────────────────────────┘
                           ↓
                 (Package found and valid?)
                    YES → Use cached            NO ↓
                    package
                           ↓
┌────────────────────────────────────────────────────────────┐
│ Layer 2: Download from npm Registry                        │
│ - Status: Online, authoritative source                     │
│ - Time: 5–30 seconds (depends on connection)               │
│ - URL: registry.npmjs.org/continue                         │
└────────────────────────────────────────────────────────────┘
                           ↓
                 (Download successful?)
                    YES ↓        NO ↓

         (Validate           ┌─────────────────┐
          checksum,    →     │ Layer 3: Error  │
          save to cache)     │ & Recovery      │
                             │ - Fail graceful │
                             │ - Print diags   │
                             └─────────────────┘
                                     ↓
                            (User action required)
                            - Check connectivity
                            - Retry or restore
```

### Component Interactions

```
VS IDE (C# VSIX)
  ↓
  └─→ BridgeManager (Step 45)
       ↓
       ├─→ npm Install Script (Step 7)
       │    ├─ Check .cache/npm-packages/v2.0.0/
       │    ├─ Validate SHA256
       │    └─ Verify Node.js ≥18.0.0
       │
       └─→ Node.js Runtime (v18+)
            ↓
            └─→ core-server.js
                 ↓
                 └─→ continue npm package (v2.0.5)
                      ├─ LSP implementation
                      ├─ Handler dispatcher
                      └─ Bridge protocol adapter
```

### Version Lifecycle

```
continue@2.0.0 ─────→ continue@2.0.1 ─────→ continue@2.1.0
   (current)         (patch release)       (minor release)
   [cached]          [future]              [future]
   [active]          [can coexist]         [can coexist]
```

Each version maintains its own cache directory and manifest, enabling:
- ✅ Safe upgrades (old version remains available)
- ✅ Rollback support (downgrade without re-downloading)
- ✅ A/B testing (test new version before rollout)

---

## Dependency Matrix Summary

### Runtime Dependencies

The bridge declares **ONE primary dependency**:

| Package | Version | Purpose | Source | Type |
|---------|---------|---------|--------|------|
| **continue** | 2.0.5 | LSP server, handler implementations, protocol adapter | npm registry | Runtime (required) |

**Location**: `src/versions/v2.0.0/package.json`

```json
{
  "dependencies": {
    "continue": "2.0.5"
  }
}
```

### Development Dependencies

These are **NOT** distributed with the bridge; they are only used during build/test:

| Package | Version | Purpose | Type |
|---------|---------|---------|------|
| mocha | ^10.2.0 | Test runner (optional) | DevDependency |

### System Requirements

| Component | Minimum | Tested | Notes |
|-----------|---------|--------|-------|
| **Node.js** | 18.0.0 | 18.17, 19.x, 20.x | Must be on PATH |
| **npm** | 8.0.0 (optional) | 8.x, 9.x, 10.x | Optional; pre-cached recommended |
| **.NET Runtime** | 4.7.2 or 10.0 | 4.7.2, 10.0 | Host VSIX extension |
| **Disk Space** | 30 MB | 40 MB | Cache + extraction buffer |

### Version Compatibility Matrix

#### Node.js Compatibility

| Continue Version | Min Node.js | Max Node.js | Tested Node.js |
|---|---|---|---|
| 2.0.0 | 18.0.0 | 20.x | 18.17, 19.x, 20.x |
| 2.0.5 (current) | 18.0.0 | 20.x | 18.17, 19.x, 20.x |
| 2.1.0 (future) | 18.0.0 | 22.x | 18.17, 20.x, 22.x |

#### Windows/Linux/macOS Compatibility

| Continue Version | Windows | Linux | macOS |
|---|---|---|---|
| 2.0.5 (current) | ✅ (x64, ARM64) | ✅ (x64, ARM64) | ✅ (Intel, Apple Silicon) |

#### ContinueVS Bridge Compatibility

| Bridge Version | Continue Version | Status | Support |
|---|---|---|---|
| v1.0.0 | Continue 1.x | Deprecated | EOL |
| v2.0.0 (current) | Continue 2.0.5 | Active | Full support |
| v2.1.0 (planned) | Continue 2.0.5+ | In development | Beta |

### Dependency Tree

```
ContinueVS Bridge
  └── continue@2.0.5
       └── [bundled dependencies]
            ├── Language Server Protocol (LSP) impl
            ├── Message handler system
            ├── Configuration management
            └── Telemetry collection

Notes:
- continue@2.0.5 includes NO external npm dependencies
- Bridge distributes the pre-compiled JavaScript bundle
- All transitive deps resolved and bundled by Continue maintainers
```

### Cross-Reference

**For detailed compatibility and version matrix**, see:
- 📄 [npm-dependency-matrix.md](npm-dependency-matrix.md) — Full version compatibility tables
- 📄 [npm-cache-strategy.md](npm-cache-strategy.md) — Cache layers and download strategies

---

## Download & Cache Strategy

### Three-Layer Cache Architecture

The bridge implements a **cache-first** strategy to minimize download time and support offline operation:

```
Layer 1: Local Cache
  Path: .cache/npm-packages/v2.0.0/
  Contains:
    ├── continue-v2.0.0.tgz (8–12 MB)
    ├── continue-v2.0.0.tgz.sha256
    └── .metadata/cache-manifest.json

Layer 2: npm Registry (fallback)
  URL: registry.npmjs.org/continue
  Status: Online, authoritative source
  Fallback: If Layer 1 invalid or missing

Layer 3: Error Recovery (last resort)
  Status: Fail with diagnostics and recovery instructions
  Action: User intervention required
```

### Cache Location and Disk Requirements

| Item | Size |
|------|------|
| Compressed `.tgz` file | 8–12 MB |
| Extracted package | 25–35 MB |
| Cache manifest + checksums | ~2 KB |
| **Total per version** | ~30–40 MB |
| **Multi-version (5 versions)** | ~150–200 MB |

### Download Time Estimates

| Connection Speed | Time to Download v2.0.0 |
|---|---|
| 1 Mbps (cellular) | ~60–100 seconds |
| 10 Mbps (broadband) | ~6–10 seconds |
| 100 Mbps (fiber) | <1 second |
| 1 Gbps (enterprise) | <1 second |

### Download Strategies

**Strategy 1: Cache-First (Default)**
- Check `.cache/npm-packages/v2.0.0/` first
- If present and valid (SHA256 matches): Load from cache (~1 second)
- If missing or invalid: Download from npm registry (~10–30 seconds)

**Strategy 2: Force Registry (Update)**
- Always download latest from registry
- Useful for forced updates or CI/CD cache refresh
- Validates and updates local cache

**Strategy 3: Offline-Only**
- Never contact npm registry
- Fail immediately if cache missing/invalid
- Used in air-gapped environments

### Pre-Population for CI/CD

Pre-cache the npm package on CI agents to eliminate download delays:

```bash
# On CI agent setup:
mkdir -p .cache/npm-packages/v2.0.0/
# Download and verify (see Step 35 for details)
# Result: Cache hit on every build (~<1 second)
```

**See detailed strategy**: [npm-cache-strategy.md](npm-cache-strategy.md)

---

## Installation & Validation Workflow

### Step-by-Step Installation Process

When the user opens the Continue panel in Visual Studio:

```
1. User clicks "Continue Chat" (Ctrl+Shift+J)
   ↓
2. ContinueToolWindowControl.OnLoaded() triggers
   ↓
3. WebView2 initializes and calls npm validation script
   ↓
4. .\scripts\install-bridge-npm.ps1 executes (Step 7)

   4.1: Check .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz
   4.2: If missing → Download from npm registry
   4.3: Validate SHA256 checksum
   4.4: Verify Node.js ≥18.0.0 installed
   4.5: Update cache-manifest.json status = "validated"
   ↓
5. Launch: node.exe core-server.js
   ↓
6. stdio transport connects to VS IDE
   ↓
7. WebView ↔ Bridge messaging active
```

**Typical duration**: 5–60 seconds (depends on cache hit)

### Validation Checklist

Before the bridge starts, verify all conditions are met:

- ✅ `.tgz` file exists: `.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz`
- ✅ Checksum file exists: `.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz.sha256`
- ✅ SHA256 computed value matches file contents
- ✅ Cache manifest exists: `.metadata/cache-manifest.json`
- ✅ Manifest status field = `"validated"`
- ✅ Node.js ≥18.0.0 installed and on PATH
- ✅ File permissions allow read access to cache
- ✅ Disk space ≥30 MB available for extraction buffer

### Installation Commands

**Manual validation** (if needed):

```powershell
# Validate current v2.0.0
PS> .\scripts\install-bridge-npm.ps1

# Validate specific version
PS> .\scripts\install-bridge-npm.ps1 -Version "v2.0.1"

# Quiet mode (errors only)
PS> .\scripts\install-bridge-npm.ps1 -Quiet
```

**See detailed usage**: [npm-install-script.md](npm-install-script.md)

---

## Troubleshooting Guide

This section covers the **8 most common npm dependency issues** with diagnostics and resolution steps.

### Issue 1: Node.js Version Mismatch

**Symptoms**:
- Error: `node: command not found` or `node is not recognized`
- Error: `Node.js v16.x found, but ≥18.0.0 required`
- Bridge fails to start after `install-bridge-npm.ps1` validation

**Root Cause**:
- Node.js is not installed
- Installed version is too old (< 18.0.0)
- Node.js binary not on system PATH

**Diagnosis**:

```powershell
# Check if Node.js is installed
PS> node --version
# Expected: v18.x, v19.x, or v20.x

# Check PATH
PS> Get-Command node
# Should show path to node.exe
```

**Resolution**:

1. **Install Node.js 18.x or later**:
   - Download from https://nodejs.org (LTS recommended: 18.17.x or 20.x)
   - Run installer, ensure "Add to PATH" is checked
   - Restart PowerShell/Command Prompt
   - Verify: `node --version`

2. **Update PATH** (if Node.js already installed but not on PATH):
   - Windows: Control Panel → System → Environment Variables
   - Add Node.js install directory to PATH (e.g., `C:\Program Files\nodejs`)
   - Restart Visual Studio

3. **Verify installation**:
   ```powershell
   PS> node --version  # v18.x or later
   PS> npm --version   # npm 8.x or later
   ```

---

### Issue 2: Continue Package Download Failed

**Symptoms**:
- Error: `Failed to download continue package from npm registry`
- Error: `npm ERR! 404 Not Found`
- Error: `ENOTFOUND registry.npmjs.org` (network error)
- Cache validation fails with "Package not found"

**Root Cause**:
- No internet connection or connectivity issue
- npm registry temporarily unavailable
- Firewall/proxy blocking npm registry access
- Disk full (insufficient space to download)

**Diagnosis**:

```powershell
# Test connectivity to npm registry
PS> Test-NetConnection registry.npmjs.org -Port 443
# Expected: TcpTestSucceeded = True

# Check available disk space
PS> Get-Volume C: | Select-Object SizeRemaining
# Need at least 30 MB free

# Check if curl/Invoke-WebRequest can reach npm
PS> Invoke-WebRequest https://registry.npmjs.org/continue -UseBasicParsing | Select-Object StatusCode
# Expected: StatusCode = 200
```

**Resolution**:

1. **Check network connectivity**:
   - Verify internet connection is active
   - Check if npm registry is accessible: `ping registry.npmjs.org`
   - If behind corporate proxy, configure npm: `npm config set registry https://your-proxy.com/npm/`

2. **Check disk space**:
   - Free up at least 30 MB on the drive containing `.cache/npm-packages/`
   - If cache is on limited-space drive, relocate: see `npm-cache-strategy.md`

3. **Try download again**:
   ```powershell
   # Force re-download from registry (bypasses cache)
   PS> .\scripts\install-bridge-npm.ps1 -ForceDownload
   ```

4. **Retry with timeout increase**:
   - Edit `install-bridge-npm.ps1`, increase `$downloadTimeoutSeconds` (default: 120)
   - Useful for slow connections

---

### Issue 3: SHA256 Validation Failed (Cache Corrupted)

**Symptoms**:
- Error: `SHA256 validation failed: checksum mismatch`
- Error: `Cache corrupted; computed hash ≠ expected hash`
- Message: `Attempting to recover by re-downloading...`
- Package mysteriously breaks after successful previous session

**Root Cause**:
- Partial download (incomplete .tgz file)
- Disk corruption or bit-flip in cached file
- Power loss during download/write
- Antivirus software corrupting cache during scan

**Diagnosis**:

```powershell
# Check cache manifest status
PS> Get-Content .cache/npm-packages/v2.0.0/.metadata/cache-manifest.json | ConvertFrom-Json | Select-Object status

# Manually compute SHA256
PS> (Get-FileHash .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz -Algorithm SHA256).Hash
# Compare to file contents
PS> Get-Content .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz.sha256
```

**Resolution**:

1. **Clear corrupted cache**:
   ```powershell
   PS> Remove-Item -Path ".cache/npm-packages/v2.0.0/continue-v2.0.0.tgz*"
   PS> Remove-Item -Path ".cache/npm-packages/v2.0.0/.metadata/cache-manifest.json"
   ```

2. **Re-download and validate**:
   ```powershell
   PS> .\scripts\install-bridge-npm.ps1 -ForceDownload
   ```

3. **Prevent antivirus interference** (if recurring):
   - Add `.cache/npm-packages/` to antivirus exclusion list
   - Disable real-time scanning during bridge initialization (if permitted)

---

### Issue 4: Bridge Fails to Load `core-server.js`

**Symptoms**:
- Error: `Cannot find module 'continue'` or `ERR_MODULE_NOT_FOUND`
- Error: `core-server.js failed to initialize`
- Bridge process exits immediately with exit code 1
- stdout/stderr shows module errors

**Root Cause**:
- `.tgz` package incomplete or corrupted during extraction
- Partial npm package download
- File permissions prevent script execution
- Extraction to temporary directory failed

**Diagnosis**:

```powershell
# Check if extraction directory exists
PS> Test-Path .cache/npm-packages/v2.0.0/extracted/
# If present, check contents
PS> Get-ChildItem .cache/npm-packages/v2.0.0/extracted/ -Recurse | Measure-Object
```

**Resolution**:

1. **Validate package integrity**:
   ```powershell
   PS> .\scripts/install-bridge-npm.ps1 -ValidateOnly
   ```

2. **Clear and re-download**:
   ```powershell
   PS> Remove-Item -Recurse ".cache/npm-packages/v2.0.0/"
   PS> .\scripts/install-bridge-npm.ps1
   ```

3. **Check file permissions**:
   - `.cache/npm-packages/` must be readable by VS process
   - On Windows: Run Visual Studio as Administrator if permissions issue persists

---

### Issue 5: Bridge Hangs During npm Initialization

**Symptoms**:
- Visual Studio becomes unresponsive after clicking "Continue Chat"
- Process appears to hang for >60 seconds during npm validation
- No error message; just waiting...
- CPU usage drops to 0%

**Root Cause**:
- Very slow network connection (timeout too short)
- npm registry experiencing slowness or stalled connection
- Node.js child process deadlocked
- DNS resolution hanging

**Diagnosis**:

```powershell
# Check if npm registry is responding
PS> Measure-Command { Test-NetConnection registry.npmjs.org -Port 443 }
# Note the elapsed time; should be <5 seconds

# Check DNS resolution
PS> Measure-Command { [System.Net.Dns]::GetHostAddresses("registry.npmjs.org") }
# Should be <1 second
```

**Resolution**:

1. **Increase timeout threshold** in `install-bridge-npm.ps1`:
   ```powershell
   $downloadTimeoutSeconds = 300  # Increase from default 120 to 300
   ```

2. **Restart Visual Studio** and retry (forces fresh state)

3. **Check network**:
   - Run speed test: https://speedtest.net
   - If <1 Mbps, wait longer or use wired connection
   - Test npm registry availability: `npm ping` (if npm installed)

4. **If persistent**:
   - Pre-download package on fast connection
   - Manually copy `.tgz` file to `.cache/npm-packages/v2.0.0/`
   - Run validation: `.\scripts/install-bridge-npm.ps1`

---

### Issue 6: Continue Package Version Mismatch

**Symptoms**:
- Error: `continue version mismatch: expected 2.0.0, got 2.0.5`
- Bridge loads wrong version unexpectedly
- Configuration says v2.0.0 but system is using v2.0.1
- After upgrade, old version mysteriously comes back

**Root Cause**:
- User previously upgraded, cache contains multiple versions
- Configuration file points to wrong version
- Version selection UI did not persist choice
- Automatic downgrade triggered (Step 10)

**Diagnosis**:

```powershell
# List all cached versions
PS> Get-ChildItem .cache/npm-packages/ -Directory

# Check active version in config
PS> Select-String -Path "src/VSIXProject1/Properties/version.config" "continue-version"

# Check cache manifest
PS> Get-Content .cache/npm-packages/v2.0.0/.metadata/cache-manifest.json | ConvertFrom-Json | Select-Object version
```

**Resolution**:

1. **Verify which version you need**:
   - Check project requirements: typically `v2.0.0` initially
   - See `VERSIONS.md` for current recommendation

2. **Use version selection UI** (Step 9):
   - Open Continue panel → Settings → Select Version
   - Choose required version
   - Restart Visual Studio

3. **If downgrade needed** (rollback from v2.0.1 to v2.0.0):
   - If v2.0.0 cache exists: Update config, restart
   - If v2.0.0 cache missing: Re-download via `install-bridge-npm.ps1`

---

### Issue 7: Permission Denied — Cannot Access Cache

**Symptoms**:
- Error: `Access denied: .cache/npm-packages/v2.0.0/`
- Error: `Permission denied` on Windows or Linux
- Error: `File locked` (Windows exclusive file lock)
- Bridge fails even after clean cache clear

**Root Cause**:
- Windows UAC (User Account Control) restrictions
- File locked by antivirus or indexing service
- Incorrect NTFS file permissions
- File locked by previous VS process (not fully closed)

**Diagnosis**:

```powershell
# Check file attributes and permissions
PS> Get-Acl .cache/npm-packages/v2.0.0/ | Format-List

# Check if file is locked
PS> Get-Item .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz | Select-Object FullName
# If error "file is in use", something has it locked
```

**Resolution**:

1. **Run Visual Studio as Administrator**:
   - Right-click VS in Start menu → "Run as administrator"
   - Retry bridge initialization

2. **Close all VS instances** (including Task Manager verification):
   ```powershell
   PS> Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force
   ```

3. **Clear locked state**:
   ```powershell
   PS> cmd /c "attrib -R .cache/npm-packages/v2.0.0/*.*"  # Remove read-only
   ```

4. **Restart computer** (last resort):
   - Clears all file locks and system cache

---

### Issue 8: Insufficient Disk Space for Package Extraction

**Symptoms**:
- Error: `Not enough space on device` or `ENOSPC`
- Error: `Disk full` during `.tgz` extraction
- Message appears after successful `.tgz` download
- Partial extracted files left in temp directory

**Root Cause**:
- Disk where `.cache/` is located is nearly full
- Temp extraction directory (`%TEMP%` or `/tmp`) is full
- Multiple versions accumulate (each 30+ MB)

**Diagnosis**:

```powershell
# Check free space on cache drive
PS> Get-Volume C: | Select-Object DriveLetter, SizeRemaining, Size
# Calculate percentage: (SizeRemaining / Size) * 100

# Check temp directory usage
PS> (Get-ChildItem $env:TEMP -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB
```

**Resolution**:

1. **Free up disk space**:
   - Delete old backups, unused projects, or temp files
   - Run Disk Cleanup: `Disk Cleanup` (Windows Start menu)
   - Target: At least 1 GB free

2. **Clear temp directory**:
   ```powershell
   PS> Remove-Item $env:TEMP\* -Recurse -Force -ErrorAction SilentlyContinue
   ```

3. **Move cache to drive with more space** (if applicable):
   - Edit configuration to point cache to alternate drive (see Step 18)
   - Example: `.cache/` on C: → D:\cache\npm-packages\

4. **Retry download**:
   ```powershell
   PS> .\scripts/install-bridge-npm.ps1
   ```

---

## Safe Upgrade Procedures

This section guides you through upgrading the Continue npm package safely with rollback support.

### Pre-Upgrade Checklist

Before upgrading, verify:

- ✅ All developers notified of upcoming upgrade
- ✅ Test environment upgraded first (dry-run validation)
- ✅ Internet connectivity to npm registry confirmed
- ✅ Disk space ≥50 MB available (for new cache + old cache + buffer)
- ✅ Node.js ≥18.0.0 installed and on PATH
- ✅ Visual Studio not currently using bridge (no active connections)
- ✅ Backup of current `.cache/npm-packages/v2.0.0/` (optional but recommended)

### Upgrade Path: v2.0.0 → v2.0.5

**Step 1: Download New Package**

```powershell
# Create new cache directory for v2.0.5
PS> New-Item -Path ".cache/npm-packages/v2.0.5" -ItemType Directory -Force

# Download and verify
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5"

# Verify download succeeded
PS> Get-ChildItem .cache/npm-packages/v2.0.5/
# Expected: continue-v2.0.5.tgz, continue-v2.0.5.tgz.sha256, .metadata/
```

**Step 2: Validate New Package**

```powershell
# Test new version without switching production
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5" -ValidateOnly
# Expected: Success, no errors
```

**Step 3: Test in Sandbox (Optional but Recommended)**

Create isolated test VS instance or use staging environment:

```powershell
# Copy v2.0.5 to test location
PS> Copy-Item .cache/npm-packages/v2.0.5 -Destination ".cache/npm-packages/test-v2.0.5" -Recurse

# Run bridge with test version
PS> .\scripts/install-bridge-npm.ps1 -Version "test-v2.0.5"

# Test in isolated VS environment (separate installation)
# Verify: Bridge starts, basic chat works, no crashes
```

**Step 4: Deploy to Production**

```powershell
# Update configuration to point to v2.0.5
PS> $config = Get-Content "src/VSIXProject1/Properties/version.config" -Raw | ConvertFrom-Json
PS> $config.'continue-version' = "v2.0.5"
PS> $config | ConvertTo-Json | Set-Content "src/VSIXProject1/Properties/version.config"

# Restart Visual Studio
# Bridge will load v2.0.5 on next "Continue Chat" click
```

**Step 5: Validate Production Deployment**

```powershell
# Check active version
PS> Get-Content .cache/npm-packages/v2.0.5/.metadata/cache-manifest.json | ConvertFrom-Json | Select-Object version

# Monitor for errors in first hour
# Check VS Activity Log: View → Output → Show output from: Bridge
```

### Rollback Procedure: v2.0.5 → v2.0.0

If v2.0.5 is unstable, revert to v2.0.0:

**Step 1: Verify Old Version Cache Exists**

```powershell
PS> Get-ChildItem .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz
# If missing, see "Restore from Backup" below
```

**Step 2: Validate Old Version**

```powershell
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.0" -ValidateOnly
# Expected: Success (if cache not corrupted)
```

**Step 3: Switch Configuration Back**

```powershell
PS> $config = Get-Content "src/VSIXProject1/Properties/version.config" -Raw | ConvertFrom-Json
PS> $config.'continue-version' = "v2.0.0"
PS> $config | ConvertTo-Json | Set-Content "src/VSIXProject1/Properties/version.config"

# Restart Visual Studio
```

**Step 4: Verify Rollback**

```powershell
# Check active version
PS> Get-Content .cache/npm-packages/v2.0.0/.metadata/cache-manifest.json | ConvertFrom-Json | Select-Object version
# Expected: v2.0.0
```

### Restore from Backup

If old version cache was deleted and you need to rollback:

**Option A: Re-Download Old Version**

```powershell
# If v2.0.0 not in cache, re-download it
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.0" -ForceDownload

# Verify
PS> Get-ChildItem .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz
```

**Option B: Restore from Git or Backup**

If you backed up `.cache/npm-packages/` before upgrade:

```powershell
# Restore from backup
PS> Copy-Item "D:\backups\npm-packages\v2.0.0" -Destination ".cache/npm-packages/v2.0.0" -Recurse

# Validate restored cache
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.0" -ValidateOnly
```

### Breaking Changes and Compatibility

**What to watch for during upgrades**:

| Scenario | Breaking? | Impact | Action |
|----------|-----------|--------|--------|
| **v2.0.0 → v2.0.5** | No | Patch release; backward compatible | Routine upgrade |
| **v2.0.x → v2.1.0** | Possibly | Minor version; check release notes | Test thoroughly |
| **v2.x → v3.0.0** | Yes | Major version; likely incompatible | Plan migration |

**Before upgrading major version (e.g., 2.x → 3.x)**:

1. ✅ Read release notes: https://github.com/continuedev/continue/releases
2. ✅ Check breaking changes section
3. ✅ Run in test environment for 1 week
4. ✅ Update any dependent code/configs
5. ✅ Plan rollback if needed

### Upgrade Timeline

| Phase | Duration | Action |
|-------|----------|--------|
| **Planning** | 1 day | Notify team, prepare environment |
| **Testing** | 2–3 days | Sandbox validation, basic smoke tests |
| **Deployment** | <1 hour | Update config, restart, monitor |
| **Validation** | 1 hour | Check errors, confirm functionality |
| **Stabilization** | 24 hours | Monitor logs, gather feedback |

---

## Health Check Reference

Use these commands to validate npm dependency health at runtime or in CI/CD pipelines.

### Health Check Command

```powershell
# Run full health check
PS> .\scripts/install-bridge-npm.ps1 -HealthCheck

# Or via npm script
PS> npm run health-check  # in src/versions/v2.0.0/

# Quiet mode (exit code only, no output)
PS> .\scripts/install-bridge-npm.ps1 -HealthCheck -Quiet
```

### Expected Health Check Output

```
=== ContinueVS Bridge npm Health Check ===

✓ Node.js Version: v20.10.0 (required: ≥18.0.0) — PASS
✓ Cache Directory: .cache/npm-packages/v2.0.0 — EXISTS
✓ Package File: continue-v2.0.0.tgz — EXISTS (11.5 MB)
✓ Checksum File: continue-v2.0.0.tgz.sha256 — EXISTS
✓ SHA256 Validation: PASS (computed hash matches)
✓ Cache Manifest: .metadata/cache-manifest.json — VALID
✓ Package Permissions: READABLE — OK
✓ Disk Space: 45.2 GB available — OK (need 30 MB)

Result: All checks passed (8/8)
Status Code: 0
```

### Exit Codes

| Code | Meaning | Action |
|------|---------|--------|
| **0** | All checks passed | Proceed; bridge ready |
| **1** | Node.js version mismatch | Install Node.js ≥18.0.0 |
| **2** | Cache missing or invalid | Re-download package |
| **3** | SHA256 validation failed | Clear cache; re-download |
| **4** | Insufficient disk space | Free 30+ MB |
| **5** | Permission denied | Run as Administrator |
| **99** | Unknown error | Check logs; contact support |

### Automated Health Check in CI/CD

**PowerShell (Azure Pipelines)**:

```yaml
- task: PowerShell@2
  displayName: 'Validate npm Dependencies'
  inputs:
    targetType: 'inline'
    script: |
      $result = & ".\scripts\install-bridge-npm.ps1" -HealthCheck -Quiet
      if ($LASTEXITCODE -ne 0) {
        Write-Error "Health check failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
      }
      Write-Host "✓ npm dependencies healthy"
```

**Bash (GitHub Actions)**:

```bash
./scripts/install-bridge-npm.sh --health-check
if [ $? -ne 0 ]; then
  echo "Health check failed"
  exit 1
fi
```

### Continuous Health Monitoring

For long-running CI/CD agents, health check should run:
- ✅ **Before each bridge initialization** (catch cache corruption early)
- ✅ **After cache updates** (validate integrity)
- ✅ **In scheduled maintenance jobs** (weekly, catch bit-rot)

---

## CI/CD Integration Best Practices

### Pre-Populate Cache on CI Agents

**Goal**: Eliminate download delays on every CI build by caching the npm package locally.

**One-Time Setup** (per CI agent):

```powershell
# On CI agent provisioning script:
New-Item -Path ".cache/npm-packages/v2.0.0" -ItemType Directory -Force

# Download once
.\scripts/install-bridge-npm.ps1 -Version "v2.0.0"

# Verify
.\scripts/install-bridge-npm.ps1 -HealthCheck
```

**Result**: Every subsequent build will cache-hit (~1 second initialization)

### Build Pipeline Integration

**Stage 1: Validate Dependencies**

```yaml
# Azure Pipelines
- job: ValidateDependencies
  displayName: 'Validate npm Dependencies'
  steps:
    - task: PowerShell@2
      inputs:
        targetType: 'inline'
        script: |
          .\scripts\install-bridge-npm.ps1 -HealthCheck
          if ($LASTEXITCODE -ne 0) { exit 1 }
```

**Stage 2: Build and Test**

```yaml
- job: BuildAndTest
  displayName: 'Build + Test Bridge'
  dependsOn: ValidateDependencies
  steps:
    - task: DotNetCoreCLI@2
      inputs:
        command: 'build'
        arguments: '--force'
    - task: DotNetCoreCLI@2
      inputs:
        command: 'test'
```

**Result**: Dependencies validated before expensive build steps

### Cache Artifacts in Build Logs

Archive cache status for audit trail:

```powershell
# After build completes
Get-ChildItem .cache/npm-packages/ -Recurse | 
  Select-Object FullName, Length, LastWriteTime | 
  Export-Csv "$(Build.ArtifactStagingDirectory)/cache-manifest.csv"
```

### Offline Deployment Strategy

For air-gapped or restricted-network environments:

1. **Pre-stage**: Download npm package on unrestricted machine
2. **Distribute**: Copy `.cache/npm-packages/v2.0.0/` to offline network
3. **Deploy**: Place in production path before launching bridge
4. **Validate**: Run health check to confirm integrity

Example (USB flash drive transfer):

```powershell
# On connected machine
PS> Robocopy ".cache/npm-packages/v2.0.0" "E:\transfer\npm-packages\v2.0.0" /E

# On offline machine
PS> Robocopy "E:\transfer\npm-packages\v2.0.0" ".cache/npm-packages\v2.0.0" /E
PS> .\scripts/install-bridge-npm.ps1 -HealthCheck
```

### Cache Refresh Strategy

When upgrading to new Continue version:

```powershell
# Keep old version for rollback
# Download new version alongside
.\scripts/install-bridge-npm.ps1 -Version "v2.0.5"

# Pre-populate CI agents with new version
# (gradual rollout: canary → staging → production)

# After week of stability, can delete old version to free space
Remove-Item .cache/npm-packages/v2.0.0/ -Recurse
```

---

## Related Documentation

This document is part of the ContinueVS Bridge documentation suite. For deeper information on specific topics, see:

### Cache Management
- **[npm-cache-strategy.md](npm-cache-strategy.md)** — Complete cache architecture, download strategies, and recovery procedures
- **[npm-integrity-utility.md](npm-integrity-utility.md)** — SHA256 validation and package integrity checking

### Dependencies & Versions
- **[npm-dependency-matrix.md](npm-dependency-matrix.md)** — Detailed version compatibility matrices, Node.js support, platform support
- **[VERSIONS.md](../VERSIONS.md)** — Official version history and recommendations

### Installation & Automation
- **[npm-install-script.md](npm-install-script.md)** — Installation script documentation, usage examples, error handling
- **[BRIDGE-ARCHITECTURE-DETAILED.md](BRIDGE-ARCHITECTURE-DETAILED.md)** — Complete bridge architecture overview

### Implementation Steps
- **Step 6**: npm dependency cache strategy documentation
- **Step 7**: npm install script creation
- **Step 12**: npm package validation on startup
- **Step 34**: npm dependency documentation (this file)
- **Step 35**: Download & verify Continue npm package
- **Step 37**: Generate checksums for npm packages
- **Step 39**: npm update guide

---

## Support & Contact

**Questions or issues with npm dependencies?**

- **Development Team**: See [BRIDGE-ARCHITECTURE-DETAILED.md](BRIDGE-ARCHITECTURE-DETAILED.md) for architecture questions
- **Operations/CI-CD**: Refer to [CI/CD Integration](#cicd-integration-best-practices) section above
- **Troubleshooting**: Jump to [Troubleshooting Guide](#troubleshooting-guide) for 8 common issues
- **GitHub Issues**: https://github.com/strawhecker/ContinueVS/issues

**Last Updated**: 2024-01-15  
**Maintained By**: ContinueVS Bridge Architecture Team


