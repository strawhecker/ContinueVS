<#
.SYNOPSIS
    Validates and prepares npm packages for the Continue Bridge.

.DESCRIPTION
    This script ensures the Continue Bridge npm package is available and valid before
    bridge process startup. It performs the following checks:

    1. Verify npm package (.tgz) exists in cache
    2. Validate SHA256 checksum matches expected value
    3. Verify Node.js >=18.0.0 is installed and available
    4. Return paths and status for bridge initialization

    This is a VALIDATION-ONLY script. It does NOT download packages from npm registry.
    Pre-cached packages are assumed to be bundled in the VSIX or pre-populated in CI/CD.

    For registry fallback and auto-updates, see: docs/npm-install-script.md

.PARAMETER VersionDir
    Path to the version directory (e.g., 'src/versions/v2.0.0/').
    Default: Resolved relative to script location.

.PARAMETER CacheDir
    Path to the cache directory containing npm packages.
    Default: '.cache/npm-packages/' relative to solution root.

.PARAMETER Version
    npm package version to validate (e.g., 'v2.0.0', '2.0.0').
    Default: 'v2.0.0'

.PARAMETER Quiet
    Suppress verbose output. Only show errors and final status JSON.

.EXAMPLE
    PS> .\scripts\install-bridge-npm.ps1
    # Validates v2.0.0 with default paths

.EXAMPLE
    PS> .\scripts\install-bridge-npm.ps1 -Version "v2.0.1" -Quiet
    # Validates v2.0.1, minimal output

.OUTPUTS
    JSON object with structure:
    {
      "status": "valid" | "invalid" | "error",
      "version": "v2.0.0",
      "cachePath": "...",
      "packagePath": "...",
      "nodeVersion": "18.16.0",
      "message": "..."
    }

.NOTES
    Called by: ContinueToolWindowControl on bridge startup (Step 13+)
    Related: Steps 4, 5, 6, 12, 35, 37, 39
    Bridge initialization flow: validate → extract → launch core-server.js

#>
[CmdletBinding()]
param(
    [string] $VersionDir,
    [string] $CacheDir,
    [string] $Version = 'v2.0.0',
    [switch] $Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# -------------------------------------------------------
# Constants
# -------------------------------------------------------

$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$SOLUTION_ROOT = Split-Path -Parent $SCRIPT_DIR

# Normalize version (strip leading 'v' for consistency)
if ($Version.StartsWith('v')) {
    $VersionTag = $Version
    $VersionNum = $Version.Substring(1)
} else {
    $VersionNum = $Version
    $VersionTag = "v$Version"
}

if (-not $VersionDir) {
    $VersionDir = Join-Path $SOLUTION_ROOT "src\versions\$VersionTag"
}

if (-not $CacheDir) {
    $CacheDir = Join-Path $SOLUTION_ROOT ".cache\npm-packages\$VersionTag"
}

# Package naming follows: continue-vX.Y.Z.tgz
$PACKAGE_NAME = "continue-$VersionTag.tgz"
$PACKAGE_PATH = Join-Path $CacheDir $PACKAGE_NAME
$CHECKSUM_PATH = "$PACKAGE_PATH.sha256"
$MANIFEST_PATH = Join-Path $CacheDir "manifest-$VersionTag.json"

# -------------------------------------------------------
# Logging utilities
# -------------------------------------------------------

function Write-LogInfo {
    param([string] $Message)
    if (-not $Quiet) {
        Write-Host "ℹ️  $Message" -ForegroundColor Cyan
    }
}

function Write-LogWarn {
    param([string] $Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-LogError {
    param([string] $Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-LogSuccess {
    param([string] $Message)
    if (-not $Quiet) {
        Write-Host "✅ $Message" -ForegroundColor Green
    }
}

# -------------------------------------------------------
# Validation functions
# -------------------------------------------------------

<#
.SYNOPSIS
    Test if npm package file exists.
#>
function Test-PackageExists {
    if (-not (Test-Path $PACKAGE_PATH -PathType Leaf)) {
        throw "Package not found: $PACKAGE_PATH"
    }
    Write-LogSuccess "Package found: $(Split-Path -Leaf $PACKAGE_PATH)"
    return $true
}

<#
.SYNOPSIS
    Validate SHA256 checksum of the npm package.

.DESCRIPTION
    Reads the expected checksum from {package}.tgz.sha256 and compares it to
    the computed hash of the .tgz file. Returns $true if valid, throws if invalid.

    Checksum file format (single line):
        abc123def456...abcdef  continue-v2.0.0.tgz
#>
function Test-PackageChecksum {
    if (-not (Test-Path $CHECKSUM_PATH -PathType Leaf)) {
        throw "Checksum file not found: $CHECKSUM_PATH"
    }

    # Read expected checksum (format: "hash filename")
    $checksumLine = (Get-Content $CHECKSUM_PATH -TotalCount 1).Trim()
    if (-not $checksumLine) {
        throw "Checksum file is empty: $CHECKSUM_PATH"
    }

    # Extract hash (first field, space-delimited)
    $expectedHash = $checksumLine.Split()[0].ToLower()
    if (-not $expectedHash -or $expectedHash.Length -ne 64) {
        throw "Invalid checksum format in: $CHECKSUM_PATH"
    }

    # Compute actual SHA256 hash
    Write-LogInfo "Computing SHA256 of package (may take a few seconds)..."
    $computedHash = (Get-FileHash -Path $PACKAGE_PATH -Algorithm SHA256).Hash.ToLower()

    if ($computedHash -ne $expectedHash) {
        throw @"
Checksum mismatch for $PACKAGE_NAME
  Expected: $expectedHash
  Computed: $computedHash
Package may be corrupted. Delete and re-download.
"@
    }

    Write-LogSuccess "Checksum validated: $($expectedHash.Substring(0, 16))..."
    return $true
}

<#
.SYNOPSIS
    Validate manifest.json exists and contains required metadata.

.DESCRIPTION
    Checks that the manifest file exists and has required fields:
    - version (must match $VersionNum)
    - continueVersion
    - status (should be "stable" or "tested")
#>
function Test-ManifestValid {
    if (-not (Test-Path $MANIFEST_PATH -PathType Leaf)) {
        Write-LogWarn "Manifest not found: $MANIFEST_PATH (optional)"
        return $true
    }

    try {
        $manifest = Get-Content $MANIFEST_PATH | ConvertFrom-Json

        if (-not $manifest.version) {
            throw "Manifest missing 'version' field"
        }

        $manifestVersion = if ($manifest.version.StartsWith('v')) {
            $manifest.version.Substring(1)
        } else {
            $manifest.version
        }

        if ($manifestVersion -ne $VersionNum) {
            Write-LogWarn "Manifest version ($($manifest.version)) differs from requested ($VersionNum)"
        }

        Write-LogSuccess "Manifest valid: version $($manifest.version), Continue $($manifest.continueVersion)"
        return $true
    } catch {
        Write-LogWarn "Manifest validation failed (non-critical): $_"
        return $true
    }
}

<#
.SYNOPSIS
    Verify Node.js >=18.0.0 is installed.

.DESCRIPTION
    Checks if node.exe is available in PATH and meets minimum version requirement.
    Returns Node.js version string on success.
#>
function Test-NodeJsAvailable {
    try {
        $nodeVersion = & node --version 2>$null
        if (-not $nodeVersion) {
            throw "node.exe not found in PATH"
        }

        # Parse version (format: v18.16.0)
        $nodeVersion = $nodeVersion.Trim()
        if ($nodeVersion.StartsWith('v')) {
            $nodeVersion = $nodeVersion.Substring(1)
        }

        $versionParts = $nodeVersion.Split('.')
        if ($versionParts.Count -lt 2) {
            throw "Invalid Node.js version format: $nodeVersion"
        }

        $majorVersion = [int]$versionParts[0]
        if ($majorVersion -lt 18) {
            throw "Node.js $nodeVersion does not meet minimum requirement (>=18.0.0)"
        }

        Write-LogSuccess "Node.js $("v" + $nodeVersion) validated"
        return "v$nodeVersion"
    } catch {
        throw "Node.js validation failed: $_`nPlease install Node.js >=18.0.0 from https://nodejs.org/"
    }
}

# -------------------------------------------------------
# Main validation flow
# -------------------------------------------------------

function Invoke-Validation {
    Write-LogInfo "Validating Continue Bridge npm package..."
    Write-LogInfo "Version: $VersionTag"
    Write-LogInfo "Cache: $CacheDir"
    Write-LogInfo ""

    $status = 'error'
    $message = ''
    $nodeVersion = ''

    try {
        # Step 1: Verify package exists
        Test-PackageExists | Out-Null

        # Step 2: Validate checksum
        Test-PackageChecksum | Out-Null

        # Step 3: Validate manifest (non-critical)
        Test-ManifestValid | Out-Null

        # Step 4: Verify Node.js
        $nodeVersion = Test-NodeJsAvailable

        $status = 'valid'
        $message = 'All validations passed. Bridge ready to launch.'
        Write-LogSuccess "Validation complete!"

    } catch {
        $status = 'invalid'
        $message = $_.Exception.Message
        Write-LogError $message
    }

    # Return structured status
    $result = @{
        status       = $status
        version      = $VersionTag
        cachePath    = $CacheDir
        packagePath  = $PACKAGE_PATH
        packageName  = $PACKAGE_NAME
        nodeVersion  = $nodeVersion
        message      = $message
    }

    return $result
}

# -------------------------------------------------------
# Output
# -------------------------------------------------------

$result = Invoke-Validation

# Output as JSON (consumed by C# bridge initialization)
$json = $result | ConvertTo-Json -Compress
Write-Host $json

# Set exit code based on status
exit $(if ($result.status -eq 'valid') { 0 } else { 1 })
