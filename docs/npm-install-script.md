# npm Install Script — Documentation

**Location**: `scripts/install-bridge-npm.ps1` + `scripts/install-bridge-npm.bat`  
**Version**: 1.0 (Step 7)  
**Status**: Active (BRIDGE v2.1)  
**Related**: Steps 4, 5, 6, 12, 35, 37, 39  

---

## Overview

The **npm install script** validates and prepares npm packages for the Continue Bridge before process startup. It is called **at first runtime** when the user opens the Continue panel in Visual Studio.

### Purpose

1. ✅ Verify npm package (.tgz) exists in local cache
2. ✅ Validate SHA256 checksum matches expected value
3. ✅ Verify Node.js ≥18.0.0 is installed
4. ✅ Return structured status for bridge initialization
5. ❌ **NOT included**: Download from npm registry (see Step 39)

### Execution Timeline

```
User opens VS
  ↓
User clicks "Continue Chat" (Ctrl+Shift+J)
  ↓
ContinueToolWindowControl.OnLoaded()
  ↓
NavigateAsync() initializes WebView2
  ↓
[Step 7] Call: .\scripts\install-bridge-npm.ps1 ← VALIDATION HAPPENS HERE
  ↓
Validate package, checksum, Node.js
  ↓
Status: valid (exit code 0)
  ↓
[Step 13] Launch: node.exe core-server.js
  ↓
stdio transport connects
  ↓
WebView ↔ Bridge messaging active
```

---

## Usage

### PowerShell (Direct)

```powershell
# Default: validate v2.0.0
PS> .\scripts\install-bridge-npm.ps1

# Validate specific version
PS> .\scripts\install-bridge-npm.ps1 -Version "v2.0.1"

# Quiet mode (errors only)
PS> .\scripts\install-bridge-npm.ps1 -Quiet

# Custom cache directory
PS> .\scripts\install-bridge-npm.ps1 -CacheDir "E:\cache\npm-packages\v2.0.0"

# All options combined
PS> .\scripts\install-bridge-npm.ps1 -Version "v2.0.1" -CacheDir "..." -Quiet
```

### Batch (Compatibility)

```batch
REM Default: validate v2.0.0
C:\> scripts\install-bridge-npm.bat

REM Validate specific version
C:\> scripts\install-bridge-npm.bat --version v2.0.1

REM Quiet mode
C:\> scripts\install-bridge-npm.bat --quiet
```

### C# (From Bridge Initialization)

```csharp
// Example (Step 13+): Launch npm validation before bridge startup

using System.Diagnostics;
using System.Text.Json;

private async Task<bool> ValidateNpmPackageAsync()
{
    var scriptPath = Path.Combine(solutionRoot, "scripts", "install-bridge-npm.ps1");

    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Version v2.0.0 -Quiet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        }
    };

    process.Start();
    string jsonOutput = await process.StandardOutput.ReadToEndAsync();
    process.WaitForExit();

    // Parse JSON status
    var status = JsonDocument.Parse(jsonOutput);
    return status.RootElement.GetProperty("status").GetString() == "valid";
}
```

---

## Output Format

The script outputs a **JSON object** to stdout for consumption by C# code or testing:

```json
{
  "status": "valid",
  "version": "v2.0.0",
  "cachePath": "E:\\GitRepos\\ContinueVS\\.cache\\npm-packages\\v2.0.0",
  "packagePath": "E:\\GitRepos\\ContinueVS\\.cache\\npm-packages\\v2.0.0\\continue-v2.0.0.tgz",
  "packageName": "continue-v2.0.0.tgz",
  "nodeVersion": "v18.16.0",
  "message": "All validations passed. Bridge ready to launch."
}
```

### Status Codes

| Status | Exit Code | Meaning |
|--------|-----------|---------|
| `valid` | 0 | All checks passed, bridge ready |
| `invalid` | 1 | Validation failed (see message) |
| `error` | 1 | Unexpected error (see message) |

### Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | `valid`, `invalid`, or `error` |
| `version` | string | Validated version (e.g., `v2.0.0`) |
| `cachePath` | string | Absolute path to cache directory |
| `packagePath` | string | Absolute path to .tgz file |
| `packageName` | string | Filename (e.g., `continue-v2.0.0.tgz`) |
| `nodeVersion` | string | Installed Node.js version (e.g., `v18.16.0`), empty if check failed |
| `message` | string | Human-readable status or error message |

---

## Validation Steps

### Step 1: Package File Exists

**Check**: Does `.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz` exist?

**Success Message**:
```
✅ Package found: continue-v2.0.0.tgz
```

**Failure Message**:
```
❌ Package not found: E:\...\continue-v2.0.0.tgz
```

**Why it fails**:
- Package not pre-cached in VSIX
- VSIX extraction incomplete
- Manual deletion of `.cache/` directory

**Recovery**: See [Recovery Steps](#recovery-steps)

---

### Step 2: Checksum Validation

**Check**: Does SHA256 hash match `.tgz.sha256` file?

**File Format** (`continue-v2.0.0.tgz.sha256`):
```
abc123def456...abcdef0123456789abcdef0123456789abcdef  continue-v2.0.0.tgz
```

**Success Message**:
```
ℹ️  Computing SHA256 of package (may take a few seconds)...
✅ Checksum validated: abc123def456...
```

**Failure Message**:
```
❌ Checksum mismatch for continue-v2.0.0.tgz
  Expected: abc123def456...
  Computed: def789abc012...
Package may be corrupted. Delete and re-download.
```

**Why it fails**:
- .tgz file corrupted or truncated
- Downloaded incompletely
- Manual edits to package file

**Recovery**: Delete `.cache/npm-packages/v2.0.0/continue-v2.0.0.tgz` and reinstall extension

---

### Step 3: Manifest Validation (Optional)

**Check**: Does `manifest-v2.0.0.json` exist and contain valid metadata?

**Manifest Format**:
```json
{
  "version": "v2.0.0",
  "continueVersion": "0.4.x",
  "releaseDate": "2024-01-15",
  "status": "stable",
  "checksums": {
    "package_tarball": "sha256:abc123..."
  }
}
```

**Success Message**:
```
✅ Manifest valid: version v2.0.0, Continue 0.4.x
```

**Failure Message** (non-fatal):
```
⚠️  Manifest not found: ...\manifest-v2.0.0.json (optional)
```

**Note**: Manifest validation is **non-critical**. If manifest is missing or invalid, validation continues.

---

### Step 4: Node.js Version Check

**Check**: Is Node.js ≥18.0.0 installed and in PATH?

**Success Message**:
```
✅ Node.js v18.16.0 validated
```

**Failure Message**:
```
❌ Node.js validation failed: node.exe not found in PATH
Please install Node.js >=18.0.0 from https://nodejs.org/
```

**Alternative failure**:
```
❌ Node.js validation failed: Node.js v16.13.0 does not meet minimum requirement (>=18.0.0)
Please install Node.js >=18.0.0 from https://nodejs.org/
```

**Why it fails**:
- Node.js not installed
- Node.js version too old
- node.exe not in system PATH
- Node.js installation corrupted

**Recovery**: Download and install Node.js 18.x LTS from https://nodejs.org/

---

## Recovery Steps

### Scenario 1: Package Not Found

**Error**:
```
❌ Package not found: E:\GitRepos\ContinueVS\.cache\npm-packages\v2.0.0\continue-v2.0.0.tgz
```

**Solutions**:

1. **Reinstall the extension** (recommended)
   - Uninstall ContinueVS from Extensions → Manage Extensions
   - Restart Visual Studio
   - Reinstall from Marketplace

2. **Manually restore cache** (advanced)
   ```powershell
   # Step 1: Locate your VSIX installation
   $vsixPath = "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_xxx\Extensions\...\VSIXProject1\"

   # Step 2: Copy cache from VSIX to solution
   Copy-Item "$vsixPath\.cache" "$env:USERPROFILE\ContinueVS\.cache" -Recurse
   ```

---

### Scenario 2: Checksum Mismatch

**Error**:
```
❌ Checksum mismatch for continue-v2.0.0.tgz
  Expected: abc123...
  Computed: def789...
```

**Solutions**:

1. **Delete and reinstall** (recommended)
   ```powershell
   Remove-Item "E:\GitRepos\ContinueVS\.cache\npm-packages\v2.0.0" -Recurse -Force
   ```
   Then reinstall the extension.

2. **Verify manually**
   ```powershell
   # Check if .tgz file is corrupt
   Get-Item "E:\GitRepos\ContinueVS\.cache\npm-packages\v2.0.0\continue-v2.0.0.tgz" | 
     Select-Object Length

   # Expected size: ~8-12 MB (see docs/npm-dependency-matrix.md)
   ```

---

### Scenario 3: Node.js Not Found

**Error**:
```
❌ Node.js validation failed: node.exe not found in PATH
```

**Solutions**:

1. **Install Node.js 18.x LTS** (recommended)
   - Visit https://nodejs.org/
   - Download "LTS" version (≥18.0.0)
   - Run installer, accept defaults
   - Restart Visual Studio

2. **Verify installation**
   ```powershell
   node --version    # Should print: v18.x.x
   npm --version     # Should print: 9.x.x or higher
   ```

3. **Add to PATH manually** (if installation didn't auto-add)
   ```
   Control Panel → System → Environment Variables
   → Add C:\Program Files\nodejs to PATH
   → Restart applications
   ```

---

### Scenario 4: Node.js Version Too Old

**Error**:
```
❌ Node.js v16.13.0 does not meet minimum requirement (>=18.0.0)
```

**Solution**: Upgrade Node.js

```powershell
# Check current version
node --version    # Shows: v16.13.0

# Download and install v18.x LTS from https://nodejs.org/
# Installer will offer to upgrade

# Verify after install
node --version    # Should show: v18.x.x or higher
```

---

## Testing

### Manual Testing

```powershell
# Test 1: Valid cache
PS> .\scripts\install-bridge-npm.ps1
# Expected: status=valid, exit code 0

# Test 2: Quiet mode
PS> .\scripts\install-bridge-npm.ps1 -Quiet
# Expected: JSON output only, no colored messages

# Test 3: Invalid version (before Step 35 downloads)
PS> .\scripts\install-bridge-npm.ps1 -Version "v9.9.9"
# Expected: status=invalid, exit code 1

# Test 4: Check Node.js handling
PS> $env:PATH = "C:\Windows"; .\scripts\install-bridge-npm.ps1
# Expected: Node.js not found error
# Then restore: $env:PATH = [Environment]::GetEnvironmentVariable("PATH","Machine")
```

### Automated Testing

Unit tests created in **Steps 27–31** will cover:

- ✅ Valid package + checksum + Node.js
- ✅ Missing .tgz file
- ✅ Corrupted checksum
- ✅ Invalid Node.js version
- ✅ Node.js not in PATH
- ✅ JSON output format

**Test file location** (created in Step 31):
```
VSIXProject1.Tests/
└── NpmPackageValidationTests.cs
```

---

## Troubleshooting

### Q: Script runs but output is unclear

**A**: Run without `-Quiet` flag to see detailed colored messages:

```powershell
PS> .\scripts\install-bridge-npm.ps1
# Shows: ✅ ℹ️  ⚠️  ❌ colored output
```

---

### Q: How do I run this from C# code?

**A**: See usage section above. Example pattern:

```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = $"-ExecutionPolicy Bypass -File \"...\\install-bridge-npm.ps1\" -Quiet",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        CreateNoWindow = true
    }
};
process.Start();
string output = process.StandardOutput.ReadToEnd();
// Parse output as JSON
```

---

### Q: Can I skip Node.js validation?

**A**: No. Node.js ≥18.0.0 is required to run the bridge (`core-server.js`). Install it first.

---

### Q: What if I'm offline and can't install Node.js?

**A**: Node.js must be pre-installed before using the bridge. In **air-gapped environments**:

1. Pre-install Node.js 18.x on all target machines
2. Bundle Node.js with the VSIX (future enhancement, Step 39+)
3. Use offline installers: https://nodejs.org/en/download/

---

### Q: Can the script download from npm registry?

**A**: **No** (Step 7 is validation-only). Registry fallback is planned for **Step 39** (npm update guide).

In the meantime, if cache is missing:
1. Reinstall the extension (restores cache from VSIX)
2. Or manually copy from another installation

---

## Implementation Details

### File Structure

```
scripts/
├── install-bridge-npm.ps1       ← Main validation script (Step 7)
├── install-bridge-npm.bat       ← Batch wrapper (Step 7)
└── lib/
    └── (reserved for Step 7+ helper functions)

.cache/
└── npm-packages/
    └── v2.0.0/
        ├── continue-v2.0.0.tgz          ← Downloaded in Step 35
        ├── continue-v2.0.0.tgz.sha256   ← Generated in Step 37
        └── manifest-v2.0.0.json         ← Created in Step 4

docs/
├── npm-install-script.md        ← This file
├── npm-cache-strategy.md        ← Cache architecture (Step 6)
└── npm-dependency-matrix.md     ← Size & speed analysis
```

### PowerShell Execution Policy

The script uses `-ExecutionPolicy Bypass` to allow execution:

```batch
powershell.exe -ExecutionPolicy Bypass -File "script.ps1"
```

This is **safe** because:
- Bypass only applies to this specific invocation
- Script content is on the local disk (trusted)
- Batch wrapper (`install-bridge-npm.bat`) can be called from C#
- PowerShell is built-in to Windows (no external binary)

---

## Future Enhancements (Step 39+)

The following features are **NOT included in Step 7** but planned for Step 39:

- 🔄 **Auto-download from npm registry** (online-first fallback)
- ✨ **Auto-update to newer versions** (version selection UI)
- 📊 **Download progress reporting** (for slower connections)
- 🔐 **GPG signature verification** (for security-sensitive deployments)
- 📦 **Bundle Node.js with VSIX** (air-gapped environments)

See `docs/npm-update-guide.md` (created in Step 39) for details.

---

## Related Steps

| Step | Purpose | Status |
|------|---------|--------|
| 2 | Create Continue npm package.json | ✅ Prerequisite |
| 4 | Create version manifest (checksums) | ✅ Prerequisite |
| 5 | Create npm cache directory structure | ✅ Prerequisite |
| 6 | Document npm cache strategy | ✅ Prerequisite |
| 7 | **Create npm install script** | ✅ **THIS STEP** |
| 12 | npm package validation on startup | ⏳ Depends on Step 7 |
| 35 | Download & verify npm packages | ⏳ Creates initial cache |
| 37 | Generate checksums | ⏳ Creates .sha256 files |
| 39 | Create npm update guide | ⏳ Adds registry fallback |

---

## Summary

The **npm install script** is a lightweight validation utility that:

1. Ensures npm packages are cached and valid before bridge startup
2. Verifies Node.js is available and meets minimum version
3. Returns structured JSON status for C# integration
4. Works offline (no registry calls)
5. Provides clear error messages and recovery steps

**Validation-only design** keeps Step 7 simple and MVP-ready. Registry fallback and auto-updates are deferred to Step 39.

**Step 7 Complete** ✅

---

**Last Updated**: 2024-01-15  
**Version**: 1.0  
**Maintainer**: Bridge Architecture Team
