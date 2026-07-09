# npm Update Guide

**Location**: `docs/npm-update-guide.md`  
**Version**: 1.0  
**Last Updated**: 2024-01-15  
**Owner**: Bridge Architecture Team  
**Primary Contact**: ContinueVS Development Team  
**Related Docs**: [npm-dependencies.md](npm-dependencies.md) | [step-10-downgrade-warning.md](adr/step-10-downgrade-warning.md) | [version-upgrade.js](../src/versions/v2.0.0/lib/version-upgrade.js)

---

## Quick Start

**Estimated Time**: 5–10 minutes  
**Difficulty**: Beginner  
**Safety**: Safe — validate before restarting VS

### Pre-Upgrade Checklist

Before upgrading, verify all of the following:

```powershell
# 1. Check disk space (need ≥50 MB free)
PS> [Math]::Round((Get-Volume).SizeRemaining / 1MB, 0)

# 2. Verify Node.js is installed and ≥18.0.0
PS> node --version
# Expected output: v18.0.0 or higher (e.g., v20.10.0)

# 3. Verify Visual Studio is NOT running
PS> Get-Process devenv -ErrorAction SilentlyContinue
# Expected: No output (process not found)

# 4. Verify current cached version exists
PS> Get-ChildItem .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz
# Expected: File exists with size ~8–12 MB
```

If any check fails, see [Troubleshooting](#troubleshooting) below.

### Download & Install

```powershell
# 1. Download new version (v2.0.5 in this example)
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5"

# Expected output:
# [INFO] Downloading continue@v2.0.5 from npm registry...
# [INFO] Download complete: .cache/npm-packages/v2.0.5/continue-v2.0.5.tgz (11.2 MB)
# [INFO] Extracting...
# [INFO] Integrity check passed (SHA256 match)
# [SUCCESS] Version v2.0.5 installed successfully
```

### Validation

```powershell
# 1. Validate the new version (dry-run, no switching yet)
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5" -ValidateOnly

# Expected output:
# [INFO] Validating v2.0.5...
# [SUCCESS] Package integrity verified
# [SUCCESS] Feature compatibility check passed
# [SUCCESS] Ready to deploy
```

### Restart & Verify

```powershell
# 1. Close all Visual Studio instances (or restart)
PS> Get-Process devenv | Stop-Process -Force

# 2. Start Visual Studio
PS> Start devenv

# 3. Open Tools → Options → Continue → Bridge
#    Verify the version display shows: "Continue Bridge v2.0.5"

# 4. Trigger the bridge (Tools → Continue → Open Chat)
#    Verify chat loads without errors
```

✅ **Upgrade Complete!** You're now running the latest version.

**Need to rollback?** See [Rollback Procedures](#rollback-procedures) below.

---

## Safe Upgrade Procedures

### Patch Upgrade (v2.0.0 → v2.0.5)

**Risk Level**: 🟢 Low | **Compatibility**: Backward compatible | **Downtime**: <2 minutes

Patch upgrades (e.g., 2.0.0 → 2.0.5) are routine releases that fix bugs and improve stability. Breaking changes are **NOT expected**. Proceed directly from [Quick Start](#quick-start) above.

**Additional notes**:
- All v2.0.x versions are fully backward compatible
- Configuration changes are NOT required
- Rollback to v2.0.0 is safe and straightforward
- Typical upgrade time: 3–5 minutes

---

### Minor Upgrade (v2.0.x → v2.1.0)

**Risk Level**: 🟡 Medium | **Compatibility**: Mostly compatible | **Downtime**: 5–10 minutes

Minor version upgrades (e.g., 2.0.5 → 2.1.0) introduce new features and may have **minor breaking changes**. Test thoroughly before deploying.

**Pre-Upgrade**:

```powershell
# 1. Read the release notes
#    https://github.com/continuedev/continue/releases/tag/v2.1.0
#    ⚠️ Check "Breaking Changes" section carefully

# 2. Check feature status in the manifest
PS> $manifest = Get-Content .cache/npm-packages/v2.0.0/manifest.json | ConvertFrom-Json
PS> $manifest.features.experimental
# Output: List of experimental features (may disappear in v2.1.0)

# 3. Verify test environment is available
#    (optional but recommended: test in isolated VS instance)
```

**Upgrade Steps**:

```powershell
# 1. Download and validate v2.1.0
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.1.0" -ValidateOnly

# 2. Test in staging/lab environment
#    - Deploy to test VS instance
#    - Run through: chat, code completion, diagnostics
#    - Monitor for errors (View → Output → Bridge)

# 3. If stable, proceed to production
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.1.0"

# 4. Restart VS and verify
PS> Stop-Process -Name devenv -Force
PS> Start devenv
```

**Post-Upgrade Validation**:

```powershell
# Check active version
PS> Get-Content .cache/npm-packages/v2.1.0/.metadata/cache-manifest.json | ConvertFrom-Json | Select-Object version
# Expected: 2.1.0

# Monitor first hour for issues
#    View → Output → Show output from: Bridge
#    Look for: [WARN], [ERROR], [CRITICAL]
```

**If Issues Occur**: See [Rollback Procedures](#rollback-procedures) below.

---

### Major Upgrade (v2.x → v3.0.0)

**Risk Level**: 🔴 High | **Compatibility**: Breaking changes likely | **Downtime**: 30+ minutes + migration

Major version upgrades (e.g., 2.x → 3.0.0) introduce significant changes and **will have breaking changes**. Plan migration carefully.

**Pre-Upgrade (Week Before)**:

```powershell
# 1. Read release notes and migration guide
#    https://github.com/continuedev/continue/releases/tag/v3.0.0
#    Read the entire "Migration Guide" section

# 2. Extract all breaking changes
$manifest = Get-Content .cache/npm-packages/v2.0.0/manifest.json | ConvertFrom-Json
# Check fields: .features.deprecated, .breakingChanges (if present)

# 3. Document current configuration
PS> Copy-Item "src/VSIXProject1/Properties/version.config" -Destination "backups/version.config.v2.x.bak"

# 4. Notify team and plan downtime window
```

**Pre-Upgrade (Day Before)**:

```powershell
# 1. Backup current cache and config
PS> Copy-Item ".cache/npm-packages/v2.0.0" -Destination "backups/npm-packages-v2.0.0" -Recurse
PS> Copy-Item "src/VSIXProject1/Properties/" -Destination "backups/properties-v2.x" -Recurse

# 2. Download v3.0.0 to separate cache location
PS> .\scripts/install-bridge-npm.ps1 -Version "v3.0.0" -ValidateOnly

# 3. If validation fails, abort and report issues
#    Do NOT proceed to production
```

**Upgrade Steps (During Downtime Window)**:

```powershell
# 1. Close all VS instances and related processes
PS> Get-Process devenv | Stop-Process -Force
PS> Get-Process node | Stop-Process -Force

# 2. Apply v3.0.0
PS> .\scripts/install-bridge-npm.ps1 -Version "v3.0.0"

# 3. Update configuration per migration guide
#    Edit: src/VSIXProject1/Properties/version.config
#    Follow: Breaking Changes section from release notes

# 4. Start VS
PS> Start devenv
```

**Post-Upgrade Validation (Critical)**:

```powershell
# 1. Check active version
PS> Get-Content .cache/npm-packages/v3.0.0/.metadata/cache-manifest.json | ConvertFrom-Json | Select-Object version
# Expected: 3.0.0

# 2. Test core features (manual, 15 minutes)
#    - Open Continue Chat (Tools → Continue → Open Chat)
#    - Test: code completion, go-to-definition, diagnostics
#    - Run suite of manual tests from migration guide

# 3. Monitor logs for 1 hour
#    View → Output → Show output from: Bridge
#    Look for: [ERROR], [CRITICAL]

# 4. Run automated tests if available
PS> dotnet test VSIXProject1.Tests.slnx
```

**If Critical Issues Occur**:

```powershell
# IMMEDIATE ROLLBACK (before more users upgrade)
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.x" -ValidateOnly
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.x"
PS> Stop-Process -Name devenv -Force
PS> Start devenv

# Report issue to team and Continue project
#    https://github.com/continuedev/continue/issues
```

**Rollback to v2.x**: See [Rollback Procedures](#rollback-procedures) below.

---

## Rollback Procedures

Use these steps to revert to a previous version if an upgrade causes issues.

**Estimated Time**: 5–10 minutes  
**Safety**: Safe — old packages remain cached

### Step 1: Verify Old Version Cache Exists

```powershell
# Check if v2.0.0 cache still exists
PS> Get-ChildItem .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz

# Expected: File listing with size ~11 MB
# If missing, see "Restore from Backup" below
```

### Step 2: Validate Old Version Package

```powershell
# Test the old version without switching yet
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.0" -ValidateOnly

# Expected output:
# [INFO] Validating v2.0.0...
# [SUCCESS] Package integrity verified
# [SUCCESS] Feature compatibility check passed
```

**If validation fails**:
- Cache may be corrupted
- Proceed to "Restore from Backup" section below

### Step 3: Switch Configuration Back

```powershell
# Update version config to point to old version
PS> $config = Get-Content "src/VSIXProject1/Properties/version.config" -Raw | ConvertFrom-Json
PS> $config.'continue-version' = "v2.0.0"
PS> $config | ConvertTo-Json | Set-Content "src/VSIXProject1/Properties/version.config"

# Verify change
PS> Get-Content "src/VSIXProject1/Properties/version.config" | Select-String "continue-version"
# Expected: "continue-version": "v2.0.0"
```

### Step 4: Verify Rollback with Health Check

```powershell
# 1. Close VS
PS> Get-Process devenv | Stop-Process -Force

# 2. Restart VS
PS> Start devenv

# 3. Open Tools → Options → Continue → Bridge
#    Verify version display shows: "Continue Bridge v2.0.0"

# 4. Open Continue Chat (Tools → Continue → Open Chat)
#    Verify chat loads and works normally

# 5. Monitor logs for errors
#    View → Output → Show output from: Bridge
#    Expected: No [ERROR] or [CRITICAL] messages in first minute
```

**Health Check Commands**:

```powershell
# Automated health check (if implemented)
PS> .\scripts/health-check-bridge.ps1 -Version "v2.0.0"

# Expected:
# [SUCCESS] v2.0.0 is active
# [SUCCESS] Package integrity verified
# [SUCCESS] Bridge process responding
# [SUCCESS] Chat feature functional
```

✅ **Rollback Complete!** Old version is now active.

---

### Restore from Backup

If the old version cache was deleted or corrupted and you need to rollback:

**Option A: Re-Download Old Version**

```powershell
# Download v2.0.0 fresh from npm registry
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.0" -ForceDownload

# Verify
PS> Get-ChildItem .cache/npm-packages/v2.0.0/continue-v2.0.0.tgz

# Then proceed to Step 3 above (Switch Configuration Back)
```

**Option B: Restore from File Backup**

If you backed up `.cache/npm-packages/` before the failed upgrade:

```powershell
# Restore from backup directory
PS> Copy-Item "D:\backups\npm-packages\v2.0.0" `
    -Destination ".cache/npm-packages/v2.0.0" -Recurse -Force

# Validate restored cache
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.0" -ValidateOnly

# Then proceed to Step 3 above (Switch Configuration Back)
```

**Option C: Restore from Git History**

If the npm packages are version-controlled (in Git):

```powershell
# Check Git log for when v2.0.0 was committed
PS> git log --all --oneline -- ".cache/npm-packages/v2.0.0/" | head -5

# Restore from specific commit
PS> git checkout <commit-hash> -- .cache/npm-packages/v2.0.0/

# Validate
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.0" -ValidateOnly

# Then proceed to Step 3 above (Switch Configuration Back)
```

**If all backups are unavailable**:
- Last resort: Contact the Continue development team for a fresh package download
- Link: https://github.com/continuedev/continue/releases
- Download the release tarball manually and extract to `.cache/npm-packages/v2.0.0/`

---

## Troubleshooting

Common issues and resolutions for npm package updates.

### Network Timeout During Download

**Symptom**:
```
[ERROR] Download timed out after 30s
[ERROR] Failed to fetch from https://registry.npmjs.org
```

**Root Cause**:
- Intermittent internet connectivity
- npm registry temporarily unavailable
- Slow network (>10 Mbps)
- Corporate firewall blocking npm registry

**Resolution**:

```powershell
# 1. Check internet connectivity
PS> Test-NetConnection registry.npmjs.org -Port 443
# Expected: TcpTestSucceeded = True

# 2. Retry download with longer timeout
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5" -TimeoutSeconds 120

# 3. If still failing, try manual download
PS> $url = "https://registry.npmjs.org/continue/-/continue-2.0.5.tgz"
PS> $out = ".cache/npm-packages/v2.0.5/continue-v2.0.5.tgz"
PS> Invoke-WebRequest -Uri $url -OutFile $out -TimeoutSec 300

# 4. Verify manual download
PS> Get-FileHash $out -Algorithm SHA256
# Compare against published checksum on npm registry
```

**If using corporate proxy**:

```powershell
# Configure npm to use proxy
PS> npm config set proxy http://[username]:[password]@proxy.company.com:8080
PS> npm config set https-proxy http://[username]:[password]@proxy.company.com:8080

# Then retry
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5"
```

---

### Insufficient Disk Space

**Symptom**:
```
[ERROR] Write failed: No space left on device
```

**Root Cause**:
- Disk full or <50 MB free
- Old npm caches consuming space
- Temporary extraction files not cleaned up

**Resolution**:

```powershell
# 1. Check available disk space
PS> [Math]::Round((Get-Volume C:).SizeRemaining / 1MB, 0)
# Need ≥50 MB free

# 2. Clean old npm caches
PS> Remove-Item ".cache/npm-packages/v1.x" -Recurse -Force  # if using v1.x
PS> Get-ChildItem ".cache/npm-packages" | Where-Object Name -NotMatch "v2.0.0|v2.0.5" | Remove-Item -Recurse -Force

# 3. Empty temp directories
PS> Remove-Item $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue
PS> New-Item $env:TEMP -ItemType Directory

# 4. Retry download
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5"
```

**To prevent in future**:

```powershell
# Set up automatic cache cleanup (monthly)
# Add to your CI/CD pipeline or scheduled task:
Get-ChildItem ".cache/npm-packages" -Directory | 
  Where-Object LastAccessTime -LT (Get-Date).AddMonths(-3) | 
  Remove-Item -Recurse -Force
```

---

### Checksum Mismatch

**Symptom**:
```
[ERROR] SHA256 mismatch
[ERROR] Expected: 706c205d23ac76046ca51d3e38de6df08afae48c13349ccda8f5ae26c4449ae2
[ERROR] Got:      a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a7b8c9d0
```

**Root Cause**:
- Corrupted download (network interruption during transfer)
- Package was tampered with (security risk)
- Checksum list is outdated

**Resolution** (Low Risk):

```powershell
# 1. Delete corrupted file
PS> Remove-Item ".cache/npm-packages/v2.0.5/continue-v2.0.5.tgz"

# 2. Re-download fresh
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5" -ForceDownload

# 3. Verify checksum matches expected value
PS> $file = ".cache/npm-packages/v2.0.5/continue-v2.0.5.tgz"
PS> (Get-FileHash $file -Algorithm SHA256).Hash
# Compare against: docs/checksums.json or src/versions/v2.0.5/manifest.json
```

**If checksum STILL mismatches** (Security Risk ⚠️):

```powershell
# DO NOT INSTALL — potential tampering
# 1. Report to Continue team
#    https://github.com/continuedev/continue/issues/security
#
# 2. Check npm registry directly
#    https://registry.npmjs.org/continue/2.0.5
#    Verify in "dist.tarball" and "dist.shasum" fields
#
# 3. If npm registry shows different checksum:
#    - Your local checksum list is outdated
#    - Update manifest.json from the repo
#    - Try again with fresh manifest
```

---

### Process Already Running

**Symptom**:
```
[ERROR] Cannot write to .cache/npm-packages/v2.0.5
[ERROR] File in use by another process
```

**Root Cause**:
- Visual Studio or npm still accessing old package
- Node.js process still running
- File is locked by antivirus scanner

**Resolution**:

```powershell
# 1. Close Visual Studio
PS> Get-Process devenv | Stop-Process -Force

# 2. Kill any Node.js processes
PS> Get-Process node -ErrorAction SilentlyContinue | Stop-Process -Force

# 3. Clear VS cache (optional)
PS> Remove-Item "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_*\ComponentModelCache" -Recurse -Force -ErrorAction SilentlyContinue

# 4. Disable antivirus scanning temporarily (if using antivirus)
#    Scan: .cache/npm-packages directory
#    Disable: Real-time scanning for 5 minutes
#    (Consult your IT/security team before doing this)

# 5. Retry
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5" -ForceDownload

# 6. Re-enable antivirus and restart normally
```

---

### Node.js Version Incompatible

**Symptom**:
```
[ERROR] Node.js v16.0.0 is not supported
[ERROR] Minimum required: v18.0.0
```

**Root Cause**:
- Node.js installed is older than minimum required (v18.0.0)
- Multiple Node.js versions installed; wrong one on PATH

**Resolution**:

```powershell
# 1. Check current Node.js version
PS> node --version
# Expected: v18.0.0 or higher (e.g., v18.15.0, v20.10.0)

# 2. If < v18.0.0, upgrade Node.js
#    Download: https://nodejs.org/
#    Or use package manager:
PS> choco upgrade nodejs  # if using Chocolatey
#    or
PS> winget upgrade OpenJS.NodeJS  # if using Windows Package Manager

# 3. Verify upgrade
PS> node --version
# Expected: ≥v18.0.0

# 4. Retry
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5"
```

**If multiple Node.js versions installed**:

```powershell
# 1. List all Node.js installations
PS> Get-ChildItem "$env:ProgramFiles\nodejs" -ErrorAction SilentlyContinue
PS> Get-ChildItem "$env:LOCALAPPDATA\nvm" -ErrorAction SilentlyContinue  # if using nvm

# 2. Verify which Node.js is on PATH
PS> (Get-Command node).Source
# Expected: Path to v18+ installation

# 3. If wrong version, update PATH
#    Remove old Node.js from PATH (Environment Variables → System → Path)
#    Add new Node.js (e.g., C:\Program Files\nodejs)
#    Restart terminal and verify

# 4. Retry
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.0.5"
```

---

## Version Compatibility Matrix

Use this table to understand version support, platform requirements, and upgrade safety.

| Version | Release Date | Node.js Min | VS Min | Status | Breaking Changes? | Notes |
|---------|--------------|-------------|--------|--------|-------------------|-------|
| **1.9.5** | 2023-06-01 | 14.0.0 | 2019.x | 🔴 EOL | — | Unsupported; do not use. Upgrade to v2.0.0 |
| **2.0.0** | 2024-01-01 | 18.0.0 | 2022.x | 🟢 Current | No | Stable; many deployments. Default version |
| **2.0.5** | 2024-01-15 | 18.0.0 | 2022.x | 🟢 Current | No | Patch release; bug fixes only |
| **2.1.0** | 2024-Q2 (TBD) | 18.0.0 | 2022.x | 🟡 Staging | Possible | New features; minor changes. Test first |
| **3.0.0** | 2024-Q4 (TBD) | 20.0.0 | 2024.x | 🟢 Planning | Yes | Major rewrite; plan migration. Upgrade guide required |

### Status Legend

- **🟢 Current**: Recommended, actively supported, safe to deploy
- **🟡 Extended Support / Staging**: Available but approaching EOL or in testing; test before deployment
- **🔴 End-of-Life (EOL)**: No longer supported; upgrade required; security vulnerabilities may exist

### Safe Upgrade Paths

```
v1.9.5 (EOL)
   ↓ (requires: complete migration)
v2.0.0 (Current)
   ↓ (safe: patch, backward compatible)
v2.0.5 (Current, latest patch)
   ↓ (requires: testing, minor incompatibilities possible)
v2.1.0 (Future, staging)
   ↓ (requires: migration planning, major breaking changes)
v3.0.0 (Future, planning)
```

### Upgrade Decision Tree

```
Current Version?
├─ v1.9.5 (EOL)
│  └─ **MUST UPGRADE** to v2.0.0 → See Step 4 (Major Upgrade)
│
├─ v2.0.0
│  ├─ Want latest patch? → v2.0.5 → See Step 3 (Patch Upgrade)
│  ├─ Want new features? → v2.1.0 → See Step 5 (Minor Upgrade)
│  └─ else → Stay on v2.0.0
│
├─ v2.0.5
│  ├─ Want new features? → v2.1.0 → See Step 5 (Minor Upgrade)
│  └─ else → Stay on v2.0.5 (latest patch)
│
└─ v2.1.0
   ├─ Critical issue? → Rollback to v2.0.5 → See Section 4 (Rollback)
   └─ else → Stay on v2.1.0
```

### Feature Availability by Version

| Feature | v1.9.5 | v2.0.0 | v2.0.5 | v2.1.0 | v3.0.0 |
|---------|--------|--------|--------|--------|--------|
| Code Completion | ✅ | ✅ | ✅ | ✅ | ✅ |
| Go-to-Definition | ✅ | ✅ | ✅ | ✅ | ✅ |
| Find References | ✅ | ✅ | ✅ | ✅ | ✅ |
| Diagnostics | ✅ | ✅ | ✅ | ✅ | ✅ |
| WebView Messaging | ❌ | 🧪 Exp | 🧪 Exp | ❌ Removed | ✅ Stable |
| Advanced Symbol Search | ❌ | 🧪 Exp | 🧪 Exp | ✅ Stable | ✅ Stable |
| Refactor Handler | ❌ | ❌ | ❌ | ✅ New | ✅ |
| Tree-Sitter Support | ❌ | ❌ | ❌ | 🧪 Exp | ✅ Stable |

Legend: ✅ Stable | 🧪 Experimental | ❌ Not Available | (Removed) Feature deprecated/removed

### Breaking Changes by Version

**v1.9.5 → v2.0.0** (Major):
- Configuration file format changed (JSON → JSON-RPC)
- Handler interface completely redesigned
- Node.js minimum: 14.0.0 → 18.0.0 (required)
- Visual Studio minimum: 2019.x → 2022.x (required)
- Old configuration must be migrated manually

**v2.0.0 → v2.0.5** (Patch):
- No breaking changes
- Fully backward compatible
- Safe drop-in replacement

**v2.0.x → v2.1.0** (Minor):
- WebView Messaging feature removed (deprecated in v2.0.5)
- Advanced Symbol Search promoted from experimental to stable
- Handler API unchanged (backward compatible)
- Optional: Migrate off deprecated features before upgrading

**v2.x → v3.0.0** (Major — TBD):
- Expected: Significant handler API redesign
- Expected: Configuration changes
- Node.js minimum: likely 20.0.0
- Migration guide will be provided with release

### Version Lifecycle

```
v1.9.5:   Released Jun 2023 → Deprecated Jan 2024 → EOL Jun 2024 🔴
          ├─ Maintenance: Critical fixes only (ended)
          └─ Support: Community only

v2.0.0:   Released Jan 2024 → Current with v2.0.5 → EOL Jan 2026 🟢
          ├─ Maintenance: Bug fixes, security patches (ongoing)
          ├─ Support: Full (3+ years from release)
          └─ Migration: v2.1.0 planned Q2 2024

v2.0.5:   Released Jan 2024 → Current latest patch → EOL Jan 2026 🟢
          ├─ Maintenance: Bug fixes (ongoing)
          └─ Support: Full (same as v2.0.0)

v2.1.0:   Expected Q2 2024 → Minor update → EOL Jan 2027 🟡
          ├─ Maintenance: (planned)
          └─ Support: (planned, 3+ years from release)

v3.0.0:   Expected Q4 2024 → Major update → EOL Q4 2027 🟢
          ├─ Maintenance: (planned)
          └─ Support: (planned, 3+ years from release)
```

---

## Programmatic Examples

Integrate version upgrade logic into your automation scripts using the `version-upgrade.js` module (Step 32).

### Example 1: Dry-Run Validation with simulateUpgrade()

Simulate an upgrade without making permanent changes.

```javascript
// File: scripts/preview-upgrade.mjs
// Purpose: Preview an upgrade before committing

import { simulateUpgrade } from '../src/versions/v2.0.0/lib/version-upgrade.js';

async function previewUpgrade(fromVersion, toVersion) {
  console.log(`[INFO] Simulating upgrade: ${fromVersion} → ${toVersion}`);

  const result = await simulateUpgrade(fromVersion, toVersion, {
    manifestRegistry: {
      // In production, load these from src/versions/*/manifest.json
      'v2.0.0': {
        version: '2.0.0',
        features: {
          stable: ['coreEditorIntegration', 'codeCompletion'],
          experimental: ['webviewMessaging']
        }
      },
      'v2.0.5': {
        version: '2.0.5',
        features: {
          stable: ['coreEditorIntegration', 'codeCompletion'],
          experimental: ['webviewMessaging']
        }
      }
    },
    dryRun: true
  });

  if (!result.success) {
    console.error('[ERROR] Simulation failed:', result.errors);
    process.exit(1);
  }

  const report = result.simulationResults.report;
  console.log('\n=== UPGRADE REPORT ===');
  console.log(`From: v${report.sections.overview.from}`);
  console.log(`To:   v${report.sections.overview.to}`);
  console.log(`Breaking Changes: ${report.sections.overview.hasBreakingChanges ? 'YES' : 'NO'}`);
  console.log(`Risks: ${report.sections.overview.hasRisks ? 'YES' : 'NO'}`);
  console.log('\nRecommendations:');
  report.sections.recommendations.forEach(rec => console.log(`  - ${rec}`));

  if (report.sections.breakingChanges.length > 0) {
    console.log('\nBreaking Changes:');
    report.sections.breakingChanges.forEach(change => {
      console.log(`  - [${change.severity}] ${change.description}`);
    });
  }

  if (report.sections.risks.length > 0) {
    console.log('\nRisks:');
    report.sections.risks.forEach(risk => {
      console.log(`  - [${risk.level}] ${risk.description}`);
    });
  }
}

// Usage
await previewUpgrade('v2.0.0', 'v2.0.5');
// Output:
// [INFO] Simulating upgrade: v2.0.0 → v2.0.5
// === UPGRADE REPORT ===
// From: v2.0.0
// To:   v2.0.5
// Breaking Changes: NO
// Risks: NO
// Recommendations:
//   - Safe to upgrade
```

### Example 2: Check Compatibility with validateUpgradePath()

Verify if an upgrade path is safe before proceeding.

```javascript
// File: scripts/validate-upgrade-path.mjs
// Purpose: Check compatibility matrix before upgrade

import { validateUpgradePath } from '../src/versions/v2.0.0/lib/version-upgrade.js';

function checkUpgrade(fromVersion, toVersion) {
  const manifestRegistry = {
    'v2.0.0': { version: '2.0.0', nodeVersions: ['18.0.0', '20.0.0'] },
    'v2.0.5': { version: '2.0.5', nodeVersions: ['18.0.0', '20.0.0'] },
    'v2.1.0': { version: '2.1.0', nodeVersions: ['18.0.0', '20.0.0'] },
    'v3.0.0': { version: '3.0.0', nodeVersions: ['20.0.0'] }
  };

  const result = validateUpgradePath(fromVersion, toVersion, manifestRegistry);

  console.log(`\nValidating: ${fromVersion} → ${toVersion}`);
  console.log(`Valid: ${result.valid}`);

  if (!result.valid) {
    console.log('Errors:');
    result.errors.forEach(err => console.log(`  ❌ ${err}`));
    return false;
  }

  console.log('✅ Safe to upgrade');
  return true;
}

// Usage examples
checkUpgrade('v2.0.0', 'v2.0.5');  // Output: Valid: true ✅
checkUpgrade('v2.0.5', 'v2.0.0');  // Output: Valid: false ❌ (downgrade)
checkUpgrade('v2.0.0', 'v2.1.0');  // Output: Valid: true ✅
checkUpgrade('v2.0.0', 'v4.0.0');  // Output: Valid: false ❌ (not available)
```

### Example 3: Generate Human-Readable Report with generateUpgradeReport()

Create a formatted summary for review before deployment.

```javascript
// File: scripts/generate-upgrade-report.mjs
// Purpose: Create a detailed upgrade report for team review

import { generateUpgradeReport, checkBreakingChanges, getUpgradeRisks } from 
  '../src/versions/v2.0.0/lib/version-upgrade.js';

function generateReport(fromVersion, toVersion) {
  // In production, load manifests from src/versions/*/manifest.json
  const fromManifest = {
    version: '2.0.0',
    features: {
      stable: ['coreEditorIntegration', 'codeCompletion', 'diagnostics'],
      experimental: ['webviewMessaging'],
      deprecated: []
    }
  };

  const toManifest = {
    version: '2.1.0',
    features: {
      stable: ['coreEditorIntegration', 'codeCompletion', 'diagnostics', 'advancedSearch'],
      experimental: ['treeSitter'],
      deprecated: ['webviewMessaging']
    }
  };

  const breakingChanges = checkBreakingChanges(fromVersion, toVersion, fromManifest, toManifest);
  const risks = getUpgradeRisks(fromVersion, toVersion, toManifest);

  const report = generateUpgradeReport(
    fromVersion,
    toVersion,
    breakingChanges.changes,
    risks.risks
  );

  // Format for markdown output (for Slack, email, PR description)
  const markdown = `
## 🔄 Upgrade Report: ${report.summary}

**Status**: ${report.sections.overview.hasBreakingChanges ? '⚠️ Breaking Changes' : '✅ Safe'}

### Overview
- **From**: v${report.sections.overview.from}
- **To**: v${report.sections.overview.to}
- **Breaking Changes**: ${report.sections.overview.hasBreakingChanges ? 'YES' : 'NO'}
- **Risks**: ${report.sections.overview.hasRisks ? 'YES' : 'NO'}

### Recommendations
${report.sections.recommendations.map(r => `- ${r}`).join('\n')}

${report.sections.breakingChanges.length > 0 ? `### Breaking Changes
${report.sections.breakingChanges.map(bc => `- [${bc.severity}] ${bc.description}`).join('\n')}
` : ''}

${report.sections.risks.length > 0 ? `### Risks
${report.sections.risks.map(r => `- [${r.level}] ${r.description}`).join('\n')}
` : ''}
  `;

  return markdown;
}

// Usage
const report = generateReport('v2.0.0', 'v2.1.0');
console.log(report);
```

### Example 4: Handle Error Types

Properly catch and handle upgrade errors.

```javascript
// File: scripts/safe-upgrade.mjs
// Purpose: Perform upgrade with proper error handling

import {
  UpgradeError,
  DowngradeBlockedError,
  BreakingChangeError,
  validateUpgradePath,
  simulateUpgrade
} from '../src/versions/v2.0.0/lib/version-upgrade.js';

async function safeUpgrade(fromVersion, toVersion) {
  try {
    // 1. Validate upgrade path
    const manifestRegistry = {
      'v2.0.0': { version: '2.0.0' },
      'v2.0.5': { version: '2.0.5' },
      'v2.1.0': { version: '2.1.0' }
    };

    const pathValidation = validateUpgradePath(fromVersion, toVersion, manifestRegistry);
    if (!pathValidation.valid) {
      throw new UpgradeError(
        `Invalid upgrade path: ${fromVersion} → ${toVersion}`,
        { path: `${fromVersion} → ${toVersion}`, errors: pathValidation.errors }
      );
    }

    // 2. Simulate the upgrade
    const simulation = await simulateUpgrade(fromVersion, toVersion, { manifestRegistry });
    if (!simulation.success) {
      throw new UpgradeError(
        `Upgrade simulation failed: ${simulation.errors.join('; ')}`,
        { simulationErrors: simulation.errors }
      );
    }

    // 3. Check for breaking changes
    const breakingChanges = simulation.simulationResults.breakingChanges;
    if (breakingChanges.changes.length > 0) {
      console.warn('[WARN] Breaking changes detected during upgrade:');
      breakingChanges.changes.forEach(change => {
        console.warn(`  - [${change.severity}] ${change.description}`);
      });
      // Decide: proceed with warning or abort
      // For this example, we proceed but log warnings
    }

    console.log(`[SUCCESS] Safe to upgrade ${fromVersion} → ${toVersion}`);
    return true;

  } catch (error) {
    if (error instanceof DowngradeBlockedError) {
      console.error(`[ERROR] Downgrade blocked: ${error.message}`);
      console.error(`  Current: ${error.currentVersion}`);
      console.error(`  Target:  ${error.targetVersion}`);
      console.error(`  Hint: Use rollback procedures in docs/npm-update-guide.md`);
      return false;

    } else if (error instanceof BreakingChangeError) {
      console.error(`[ERROR] Breaking changes would break stability: ${error.message}`);
      console.error(`  Changes: ${error.breakingChanges.join(', ')}`);
      return false;

    } else if (error instanceof UpgradeError) {
      console.error(`[ERROR] Upgrade error: ${error.message}`);
      console.error(`  Details:`, error.details);
      return false;

    } else {
      console.error(`[ERROR] Unexpected error during upgrade:`, error);
      return false;
    }
  }
}

// Usage
const success = await safeUpgrade('v2.0.0', 'v2.0.5');
if (success) {
  console.log('Proceed with deployment');
} else {
  console.log('Upgrade blocked due to errors');
  process.exit(1);
}
```

---

## See Also

For actual deployment, combine these programmatic patterns with PowerShell scripts:

- `scripts/install-bridge-npm.ps1` — Performs download + installation
- `scripts/validate-bridge-health.ps1` — Health check after deployment
- CI/CD integration: See [Automation Examples](#automation-examples) below

---

## Automation Examples

Integrate npm package updates into CI/CD pipelines and scheduled tasks for automated, repeatable deployments.

### GitHub Actions: Daily Upgrade Check

Automatically check for new versions and create pull requests for updates.

**File**: `.github/workflows/check-npm-updates.yml`

```yaml
name: Check npm Package Updates
on:
  schedule:
    - cron: '0 9 * * *'  # Daily at 9 AM UTC
  workflow_dispatch:  # Allow manual trigger

jobs:
  check-updates:
    runs-on: windows-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v3
        with:
          fetch-depth: 0

      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '20'

      - name: Check for new Continue versions
        id: check
        shell: pwsh
        run: |
          # Fetch latest version from npm registry
          $latestVersion = npm view continue version
          $currentVersion = (Get-Content 'src/versions/v2.0.0/manifest.json' | ConvertFrom-Json).version

          Write-Output "Latest: $latestVersion"
          Write-Output "Current: $currentVersion"

          if ($latestVersion -ne $currentVersion) {
            Write-Output "::notice::New version available: $latestVersion"
            Write-Output "has_update=true" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
            Write-Output "new_version=$latestVersion" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
          } else {
            Write-Output "Already on latest version"
            Write-Output "has_update=false" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
          }

      - name: Validate new version
        if: steps.check.outputs.has_update == 'true'
        shell: pwsh
        run: |
          $version = "${{ steps.check.outputs.new_version }}"
          Write-Output "Validating version: $version"

          # Download and validate (non-breaking)
          .\scripts/install-bridge-npm.ps1 -Version "v$version" -ValidateOnly

          if ($LASTEXITCODE -ne 0) {
            Write-Error "Validation failed for version $version"
            exit 1
          }

      - name: Create pull request
        if: steps.check.outputs.has_update == 'true'
        uses: peter-evans/create-pull-request@v5
        with:
          token: ${{ secrets.GITHUB_TOKEN }}
          commit-message: 'chore: upgrade Continue npm package to v${{ steps.check.outputs.new_version }}'
          title: 'Upgrade Continue Bridge to v${{ steps.check.outputs.new_version }}'
          body: |
            ## Upgrade Proposal

            **Current Version**: v${{ steps.check.outputs.current_version }}  
            **New Version**: v${{ steps.check.outputs.new_version }}

            ### Validation Status
            ✅ Package integrity verified
            ✅ Feature compatibility check passed

            ### Next Steps
            1. Review breaking changes (if any)
            2. Run integration tests
            3. Manual testing in staging environment
            4. Approve and merge

            See [npm-update-guide.md](docs/npm-update-guide.md) for detailed upgrade procedures.
          branch: chore/npm-upgrade-${{ steps.check.outputs.new_version }}
          delete-branch: true
```

---

### PowerShell Scheduled Task: Weekly Validation

Automatically validate installed npm packages weekly.

**File**: `scripts/schedule-npm-validation.ps1`

```powershell
# Run this script ONCE to set up the scheduled task
# Then it runs automatically every Monday at 2 AM

$taskName = "ContinueVS Bridge - Weekly npm Validation"
$scriptPath = "$PSScriptRoot\validate-bridge-health.ps1"
$taskDescription = "Validates Continue Bridge npm package integrity and functionality"

# Create trigger: Every Monday at 2 AM
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At 02:00

# Create action: Run validation script
$action = New-ScheduledTaskAction -Execute "powershell.exe" `
  -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""

# Create task settings
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -RunOnlyIfNetworkAvailable

# Register task
Register-ScheduledTask -TaskName $taskName `
  -Trigger $trigger `
  -Action $action `
  -Settings $settings `
  -Description $taskDescription `
  -Force

Write-Host "Scheduled task created: $taskName"
Write-Host "Next run: Monday, 2:00 AM"
```

**File**: `scripts/validate-bridge-health.ps1`

```powershell
# Automated health check script (runs on schedule)
param(
  [string]$Version = "v2.0.0",
  [string]$LogPath = "logs/npm-validation.log"
)

function Write-Log {
  param([string]$Message, [string]$Level = "INFO")
  $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
  $logEntry = "[$timestamp] [$Level] $Message"
  Write-Host $logEntry
  Add-Content -Path $LogPath -Value $logEntry
}

try {
  Write-Log "Starting npm validation for $Version"

  # 1. Verify package exists
  $pkgPath = ".cache/npm-packages/$Version/continue-$Version.tgz"
  if (-not (Test-Path $pkgPath)) {
    Write-Log "ERROR: Package not found at $pkgPath" "ERROR"
    exit 1
  }
  Write-Log "✅ Package file exists"

  # 2. Verify checksum
  .\scripts/install-bridge-npm.ps1 -Version $Version -ValidateOnly
  if ($LASTEXITCODE -ne 0) {
    Write-Log "ERROR: Package validation failed" "ERROR"
    exit 1
  }
  Write-Log "✅ Package integrity verified"

  # 3. Check file age
  $pkgAge = (Get-Item $pkgPath).LastWriteTime
  $daysSinceUpdate = (Get-Date) - $pkgAge | Select-Object -ExpandProperty Days
  Write-Log "Package age: $daysSinceUpdate days"

  if ($daysSinceUpdate -gt 90) {
    Write-Log "WARNING: Package is $daysSinceUpdate days old (>90 days)" "WARN"
    Write-Log "Consider checking for updates" "WARN"
  }

  Write-Log "Validation complete: SUCCESS"
  exit 0

} catch {
  Write-Log "Exception: $($_.Exception.Message)" "ERROR"
  exit 1
}
```

**To activate**:
```powershell
PS> .\scripts/schedule-npm-validation.ps1
# Task scheduled successfully!
```

---

### Docker Container: Auto-Pull & Restart

Automatically update npm package inside a Docker container and restart the bridge.

**File**: `Dockerfile`

```dockerfile
FROM mcr.microsoft.com/windows/servercore:ltsc2022

# Install Node.js (required for bridge)
RUN powershell -Command \
    Invoke-WebRequest -Uri https://nodejs.org/download/release/v20.10.0/node-v20.10.0-win-x64.zip -OutFile node.zip; \
    Expand-Archive -Path node.zip -DestinationPath 'C:\Program Files'; \
    Remove-Item node.zip

ENV PATH="C:\Program Files\node-v20.10.0-win-x64:${PATH}"

# Copy ContinueVS repo
COPY . /ContinueVS
WORKDIR /ContinueVS

# Install npm dependencies
RUN npm config set registry https://registry.npmjs.org/
RUN .\scripts\install-bridge-npm.ps1 -Version "v2.0.0"

# Health check
HEALTHCHECK --interval=60s --timeout=10s --start-period=30s --retries=3 \
    CMD powershell -Command "Test-Path '.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz'"

# Default command: validate health
CMD ["powershell", "-Command", ".\scripts\validate-bridge-health.ps1 -Version 'v2.0.0'"]
```

**File**: `entrypoint-with-upgrade.ps1`

```powershell
# Entrypoint script that checks for updates before starting

param([string]$CurrentVersion = "v2.0.0")

function Check-NewVersion {
  $latest = npm view continue version
  return $latest -ne $CurrentVersion.TrimStart('v')
}

function Upgrade-Package {
  param([string]$NewVersion)
  Write-Host "[INFO] Upgrading to v$NewVersion"
  .\scripts/install-bridge-npm.ps1 -Version "v$NewVersion"
  return $LASTEXITCODE -eq 0
}

try {
  # 1. Check for updates
  Write-Host "[INFO] Checking for npm updates..."
  if (Check-NewVersion) {
    $newVer = npm view continue version
    Write-Host "[INFO] New version available: v$newVer"

    if (Upgrade-Package -NewVersion $newVer) {
      Write-Host "[SUCCESS] Upgrade completed"
    } else {
      Write-Host "[WARN] Upgrade failed, continuing with current version"
    }
  }

  # 2. Validate current version
  Write-Host "[INFO] Validating $CurrentVersion"
  .\scripts/install-bridge-npm.ps1 -Version $CurrentVersion -ValidateOnly

  if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Validation failed"
    exit 1
  }

  Write-Host "[SUCCESS] Bridge ready to start"

} catch {
  Write-Host "[ERROR] Startup failed: $_"
  exit 1
}
```

**To build and run**:
```powershell
PS> docker build -t continuevs-bridge .
PS> docker run --name bridge-container continuevs-bridge
```

The container will:
1. ✅ Automatically check for new npm versions on startup
2. ✅ Upgrade if a new version is available
3. ✅ Validate package integrity
4. ✅ Run health checks periodically
5. ✅ Restart automatically on failure

---

## Breaking Changes & Feature Status

Understand how features are managed across versions and what breaks during upgrades.

### Understanding Feature Status in manifest.json

Each version manifest defines feature status:

```json
{
  "version": "2.0.5",
  "features": {
    "stable": [
      "coreEditorIntegration",
      "codeCompletion",
      "goToDefinition",
      "findReferences",
      "search",
      "diagnosticsCollection"
    ],
    "experimental": [
      "webviewMessaging",
      "advancedSymbolSearch"
    ],
    "deprecated": []
  }
}
```

**Status Definitions**:

| Status | Meaning | Upgrade Impact | Backward Compat |
|--------|---------|-----------------|-----------------|
| **Stable** | Fully supported, production-ready | Safe to use, guaranteed support | Yes, always |
| **Experimental** | Testing phase, may change or disappear | Use at your own risk | No guarantee |
| **Deprecated** | Planned for removal in next major version | Stop using immediately | May still work |

---

### Reading Feature Changes Across Versions

Compare manifests to identify what changes between versions:

**v2.0.0 → v2.0.5 (Patch)**:

```javascript
// v2.0.0
{
  "stable": ["coreEditorIntegration", "codeCompletion", "goToDefinition"],
  "experimental": ["webviewMessaging"],
  "deprecated": []
}

// v2.0.5 (identical)
{
  "stable": ["coreEditorIntegration", "codeCompletion", "goToDefinition"],
  "experimental": ["webviewMessaging"],
  "deprecated": []
}

// Result: No breaking changes ✅
```

**v2.0.5 → v2.1.0 (Minor)**:

```javascript
// v2.0.5
{
  "stable": ["coreEditorIntegration", "codeCompletion", "goToDefinition"],
  "experimental": ["webviewMessaging", "advancedSymbolSearch"],
  "deprecated": []
}

// v2.1.0
{
  "stable": ["coreEditorIntegration", "codeCompletion", "goToDefinition", "advancedSymbolSearch"],
  "experimental": ["treeSitter"],
  "deprecated": ["webviewMessaging"]
}

// Changes:
// ✅ advancedSymbolSearch: experimental → stable (backward compatible)
// ✅ treeSitter: new experimental feature (no impact if not using)
// ⚠️  webviewMessaging: stable → deprecated (will be removed in v3.0.0)

// Breaking Changes: NONE (yet), but deprecation warning issued
```

**v2.x → v3.0.0 (Major)**:

```javascript
// v2.1.0
{
  "stable": ["coreEditorIntegration", "codeCompletion", "goToDefinition"],
  "experimental": ["treeSitter"],
  "deprecated": ["webviewMessaging"]
}

// v3.0.0
{
  "stable": ["coreEditorIntegration", "codeCompletion", "goToDefinition", "treeSitter"],
  "experimental": [],
  "deprecated": []
}

// Changes:
// ✅ treeSitter: experimental → stable (safe upgrade)
// ❌ webviewMessaging: removed entirely (BREAKING CHANGE)

// Breaking Changes: YES
// Migration required if you were using webviewMessaging
```

---

### Feature Status Impact Table

**Impact Matrix**: How feature status changes affect your upgrade

| Status Change | Example | Impact | Required Action |
|---------------|---------|--------|-----------------|
| stable → stable | (no change) | No impact | None |
| stable → deprecated | webviewMessaging (v2.0→v2.1) | Warning only; feature still works | Plan migration away from feature |
| stable → removed | webviewMessaging (v2.1→v3.0) | Breaking change; feature gone | Must migrate immediately |
| experimental → stable | advancedSymbolSearch (v2.0→v2.1) | API stabilized; safe to rely on | Update code to use stable API |
| experimental → deprecated | (not common) | Feature being removed | Stop using; migrate away |
| experimental → removed | (rare) | Feature cancelled | Update code if you were using it |
| new feature | treeSitter (v2.1 new) | New functionality available | Optional; consider if beneficial |

---

### Feature Timeline: v2.0.0 → v3.0.0

Track features across all versions to plan your migration:

```
v2.0.0 (Jan 2024)
├─ stable: coreEditorIntegration, codeCompletion, goToDefinition, findReferences, search, diagnosticsCollection
├─ experimental: webviewMessaging, advancedSymbolSearch
└─ deprecated: (none)
        │
        ├─ webviewMessaging: ⚠️ DEPRECATED (will be removed)
        └─ advancedSymbolSearch: 🎓 STABILIZING

v2.0.5 (Jan 2024 — patch, no changes)
├─ stable: [same as v2.0.0]
├─ experimental: [same as v2.0.0]
└─ deprecated: [same as v2.0.0]

v2.1.0 (Q2 2024 — minor, new features)
├─ stable: coreEditorIntegration, codeCompletion, goToDefinition, findReferences, search, diagnosticsCollection, advancedSymbolSearch ✅
├─ experimental: treeSitter (NEW)
└─ deprecated: webviewMessaging ⚠️ (FINAL: will be removed in v3.0.0)
        │
        └─ Action: If using webviewMessaging, plan migration before v3.0.0

v3.0.0 (Q4 2024 — major, breaking)
├─ stable: [all previous] + treeSitter ✅
├─ experimental: (none; all experimental → stable or removed)
└─ deprecated: (none; all deprecated → removed)
        │
        └─ webviewMessaging: ❌ REMOVED (breaking change)
```

---

### Deprecation Warnings During Upgrade

When upgrading, watch for deprecation warnings:

```powershell
# Example output when upgrading v2.0.5 → v2.1.0
PS> .\scripts/install-bridge-npm.ps1 -Version "v2.1.0"

[INFO] Validating v2.1.0...
[SUCCESS] Package integrity verified
[WARN] ⚠️  DEPRECATION NOTICE:
[WARN] The following features are deprecated and will be removed in v3.0.0:
[WARN]   - webviewMessaging
[WARN]
[WARN] Action Required:
[WARN]   If your code uses webviewMessaging, update it before upgrading to v3.0.0
[WARN]   See migration guide: https://github.com/continuedev/continue/releases/tag/v2.1.0#migration
[WARN]
[SUCCESS] Ready to deploy
```

### Handling Deprecation in Your Code

If you're using a deprecated feature, update your code:

**Old Code (v2.0.x) — Using deprecated webviewMessaging**:

```javascript
// ❌ This will break in v3.0.0
import { webviewMessaging } from 'continue-bridge';

export function sendWebViewMessage(message) {
  webviewMessaging.send(message);
}
```

**New Code (v2.1.0+) — Using stable advancedSymbolSearch**:

```javascript
// ✅ This is stable and will work in v3.0.0
import { advancedSymbolSearch } from 'continue-bridge';

export async function searchSymbols(query) {
  return advancedSymbolSearch.find(query);
}
```

**Migration Path**:

```
v2.0.x (webviewMessaging is experimental) → Works, but not recommended
    ↓
v2.1.0 (webviewMessaging deprecated, advancedSymbolSearch stable) → Update your code
    ↓
v3.0.0 (webviewMessaging removed) → Your updated code works safely
```

---

## Maintenance Template

Use this template to add documentation for new versions without rewriting the entire guide.

### Adding a New Version (e.g., v2.0.1)

**Step 1: Update Version Compatibility Matrix**

```markdown
# In "Version Compatibility Matrix" section, add new row:

| **2.0.1** | 2024-02-01 | 18.0.0 | 2022.x | 🟢 Current | No | Patch release; critical security fix |
```

**Step 2: Create Manifest Entry**

```bash
# Create new directory
mkdir -p src/versions/v2.0.1

# Copy previous version as template
cp src/versions/v2.0.0/manifest.json src/versions/v2.0.1/manifest.json

# Edit the manifest
```

**Template Manifest** (`src/versions/v2.0.1/manifest.json`):

```json
{
  "version": "2.0.1",
  "releaseDate": "2024-02-01T14:30:00Z",
  "npmPackage": {
    "name": "continue",
    "version": "2.0.1",
    "tarballUrl": "https://registry.npmjs.org/continue/-/continue-2.0.1.tgz",
    "registry": "https://registry.npmjs.org"
  },
  "checksums": {
    "sha256": "REPLACE_WITH_ACTUAL_SHA256_FROM_npm_REGISTRY",
    "sha512": "REPLACE_WITH_ACTUAL_SHA512"
  },
  "compatibility": {
    "vsCodeVersions": [
      "1.80.0",
      "1.81.0",
      "1.82.0",
      "1.83.0",
      "1.84.0"
    ],
    "nodeVersions": [
      "18.0.0",
      "20.0.0"
    ],
    "platforms": [
      "win32"
    ]
  },
  "features": {
    "stable": [
      "coreEditorIntegration",
      "diagnosticsCollection",
      "goToDefinition",
      "findReferences",
      "codeCompletion",
      "search"
    ],
    "experimental": [
      "advancedSymbolSearch",
      "webviewMessaging"
    ],
    "deprecated": []
  },
  "dependencies": {
    "minBridgeVersion": "1.0.0",
    "previousVersions": ["2.0.0"]
  },
  "releaseNotes": {
    "summary": "Security patch: fixes CVE-2024-XXXXX, performance improvements",
    "breakingChanges": [],
    "securityFixes": [
      "CVE-2024-XXXXX: Description of vulnerability"
    ],
    "bugFixes": [
      "Fixed: Issue #123 — Chat window freezing",
      "Fixed: Issue #124 — Memory leak in code completion"
    ],
    "enhancements": [
      "Improved: Code completion response time by 20%"
    ]
  }
}
```

**Step 3: Generate Checksums**

```powershell
# Download the package from npm registry
PS> $url = "https://registry.npmjs.org/continue/-/continue-2.0.1.tgz"
PS> $output = "src/versions/v2.0.1/continue-v2.0.1.tgz"
PS> Invoke-WebRequest -Uri $url -OutFile $output

# Generate checksums
PS> $sha256 = (Get-FileHash $output -Algorithm SHA256).Hash
PS> $sha512 = (Get-FileHash $output -Algorithm SHA512).Hash

Write-Host "SHA256: $sha256"
Write-Host "SHA512: $sha512"

# Update manifest.json with these values
```

**Step 4: Update Safe Upgrade Procedures**

Add guidance for v2.0.0 → v2.0.1:

```markdown
# In "Safe Upgrade Procedures" section, add:

### Patch Upgrade (v2.0.0 → v2.0.1)

**Risk Level**: 🟢 Low | **Compatibility**: Backward compatible | **Downtime**: <2 minutes

**Security Patch**: This release fixes critical vulnerability CVE-2024-XXXXX.  
**Recommendation**: Upgrade immediately, especially if running v2.0.0 in production.

Follow [Quick Start](#quick-start) above. No configuration changes required.
```

**Step 5: Update Breaking Changes Section**

Add entries to feature timeline:

```markdown
# In "Breaking Changes & Feature Status" section, update timeline:

v2.0.0 (Jan 2024)
├─ stable: [...]
├─ experimental: [...]
└─ deprecated: [none]
        │
v2.0.1 (Feb 2024 — patch, no feature changes)
├─ stable: [same as v2.0.0]
├─ experimental: [same as v2.0.0]
└─ deprecated: [same as v2.0.0]
        │
v2.0.5 (Jan 2024)
...
```

**Step 6: Update Troubleshooting (if applicable)**

If this version fixes a known issue, add resolution:

```markdown
# In "Troubleshooting" section, add:

### Issue Fixed in v2.0.1: Chat Window Freezing

**Symptom** (in v2.0.0):
```
[ERROR] Chat window becomes unresponsive
```

**Resolution**:
Upgrade to v2.0.1, which includes fix for issue #123.
See [Patch Upgrade (v2.0.0 → v2.0.1)](#patch-upgrade-v200--v201) above.
```

**Step 7: Update Version Matrix in "See Also"**

Ensure checklist reflects all versions:

```markdown
# In "See Also" section, update version list:

- v2.0.0 (Jan 2024) — Current base version
- v2.0.1 (Feb 2024) — Patch: security fix
- v2.0.5 (Jan 2024) — Patch: performance
- v2.1.0 (Q2 2024) — Minor: new features
- v3.0.0 (Q4 2024) — Major: breaking changes
```

### Template for Major Version (e.g., v3.0.0)

Use the same process but add:

**Breaking Changes Section**:

```json
{
  "version": "3.0.0",
  "breakingChanges": [
    {
      "type": "handler-api",
      "severity": "HIGH",
      "description": "Handler interface completely redesigned",
      "details": "All handlers must be rewritten to use new BaseHandler class",
      "migrationGuide": "https://github.com/continuedev/continue/releases/tag/v3.0.0#migration-guide"
    },
    {
      "type": "feature-removal",
      "severity": "MEDIUM",
      "description": "webviewMessaging feature removed",
      "details": "Use advancedSymbolSearch or new webviewMessaging2 instead",
      "alternative": "advancedSymbolSearch (stable)"
    }
  ]
}
```

**Update Safe Upgrade Procedures**:

```markdown
### Major Upgrade (v2.x → v3.0.0)

**Risk Level**: 🔴 High | **Compatibility**: Breaking changes | **Downtime**: 30+ minutes + migration

See [Major Upgrade section](#major-upgrade-vx--v300) for detailed procedures.

**Additional Notes for v3.0.0 specifically**:
- Configuration file format has changed
- Update scripts/install-bridge-npm.ps1 to handle v3.0.0 specifics
- All handlers must be rewritten (see migration guide)
- Node.js minimum bumped to v20.0.0
```

---

## Checklist for Adding New Versions

Copy this checklist when adding a new version to the guide:

```markdown
## Adding Version vX.Y.Z

- [ ] Create directory: src/versions/vX.Y.Z/
- [ ] Create manifest.json (use previous version as template)
- [ ] Generate checksums (SHA256, SHA512)
- [ ] Update Version Compatibility Matrix section
- [ ] Add upgrade/downgrade procedures in "Safe Upgrade Procedures"
- [ ] Update feature timeline in "Breaking Changes & Feature Status"
- [ ] Add troubleshooting entries for any known issues fixed
- [ ] Update "See Also" footer with new version
- [ ] Test upgrade/downgrade paths with older versions
- [ ] Review for consistency (version numbers, Node.js requirements, breaking changes)
- [ ] Cross-reference related documents (npm-dependencies.md, release notes)
```

---

## See Also

### Related Documentation

**Architecture & Strategy**:
- [npm-dependencies.md](npm-dependencies.md) — TL;DR, npm package overview, architecture rationale
- [npm-cache-strategy.md](npm-cache-strategy.md) — Cache directory structure, offline capabilities
- [npm-dependency-matrix.md](npm-dependency-matrix.md) — Complete dependency tree and version resolution

**Implementation Details**:
- [adr/step-10-downgrade-warning.md](adr/step-10-downgrade-warning.md) — Downgrade blocking UI and logic
- [src/versions/v2.0.0/lib/version-upgrade.js](../src/versions/v2.0.0/lib/version-upgrade.js) — Upgrade validation module
- [src/versions/v2.0.0/manifest.json](../src/versions/v2.0.0/manifest.json) — Version metadata schema

**Operations & Scripts**:
- `scripts/install-bridge-npm.ps1` — Main npm installer script
- `scripts/health-check-bridge.ps1` — Bridge health verification
- `scripts/validate-bridge-health.ps1` — Automated validation for scheduled tasks

**External References**:
- [Continue GitHub Releases](https://github.com/continuedev/continue/releases) — Official release notes and upgrade announcements
- [Continue npm Package Registry](https://registry.npmjs.org/continue) — Published versions and metadata
- [Node.js Version Releases](https://nodejs.org/en/download/releases/) — Node.js version compatibility

---

### Version Quick Reference

| Version | Release | Status | Node Min | Action |
|---------|---------|--------|----------|--------|
| v2.0.0 | Jan 2024 | 🟢 Current | 18.0.0 | Upgrade from v2.0.x |
| v2.0.5 | Jan 2024 | 🟢 Current | 18.0.0 | Latest patch, use this |
| v2.1.0 | Q2 2024 | 🟡 Coming | 18.0.0 | Test before deploy |
| v3.0.0 | Q4 2024 | 🟢 Planned | 20.0.0 | Plan major migration |
| v1.9.5 | Jun 2023 | 🔴 EOL | 14.0.0 | Migrate immediately |

---

### Key Procedures

**5-Minute Tasks**:
- [Quick Start](#quick-start) — Standard upgrade for most users
- [Patch Upgrade](#patch-upgrade-v200--v205) — Apply security/bug fixes
- [Rollback Procedures](#rollback-procedures) — Revert to previous version

**30-Minute Tasks**:
- [Minor Upgrade](#minor-upgrade-v20x--v210) — Test and deploy new features
- [Troubleshooting](#troubleshooting) — Diagnose common failures

**Multi-Hour Tasks**:
- [Major Upgrade](#major-upgrade-vx--v300) — Plan and execute breaking changes
- [Automation Setup](#automation-examples) — Configure CI/CD for automated updates

---

### For Different Roles

**Developers**:
- Start with [Quick Start](#quick-start)
- Reference [Programmatic Examples](#programmatic-examples) for integration
- Use [Troubleshooting](#troubleshooting) for local issues

**DevOps / SRE**:
- Review [Safe Upgrade Procedures](#safe-upgrade-procedures) for planning
- Implement [Automation Examples](#automation-examples) for CI/CD
- Monitor [Version Compatibility Matrix](#version-compatibility-matrix) for EOL dates

**Release Managers**:
- Follow [Major Upgrade](#major-upgrade-vx--v300) procedures step-by-step
- Track [Breaking Changes & Feature Status](#breaking-changes--feature-status)
- Use [Maintenance Template](#maintenance-template) to document new versions

**Operators**:
- Use [Quick Start](#quick-start) for routine updates
- Follow [Rollback Procedures](#rollback-procedures) if issues occur
- Run [Troubleshooting](#troubleshooting) for diagnostic help

---

### Questions & Feedback

- **Bug reports**: https://github.com/continuedev/continue/issues
- **Security issues**: https://github.com/continuedev/continue/security/advisories
- **Version questions**: Consult [Version Compatibility Matrix](#version-compatibility-matrix) or ask in team chat
- **Upgrade issues**: See [Troubleshooting](#troubleshooting) or [Rollback Procedures](#rollback-procedures)

---

**Last Updated**: 2024-01-15  
**Document Version**: 1.0  
**Maintenance**: Update when new npm versions are released (use [Maintenance Template](#maintenance-template))

