param(
    [string]$Version = 'v2.0.0',
    [string]$VersionDir = '',
    [string]$CacheDir = '',
    [switch]$DryRun,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

# Resolve solution root
if (Test-Path 'E:\GitRepos\ContinueVS\src\versions\v2.0.0\manifest.json') {
    $solutionRoot = 'E:\GitRepos\ContinueVS'
} else {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $solutionRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
}

if (-not $VersionDir) { $VersionDir = Join-Path $solutionRoot 'src\versions\v2.0.0' }
if (-not $CacheDir) { $CacheDir = Join-Path $solutionRoot '.cache\npm-packages\v2.0.0' }

$manifestPath = Join-Path $VersionDir 'manifest.json'
$validatorModulePath = Join-Path $VersionDir 'lib\npm-package-validator.mjs'
$packagePath = Join-Path $CacheDir "continue-$Version.tgz"

function Write-Log {
    param([string]$Msg)
    if (-not $Quiet) {
        $ts = Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ'
        Write-Host "[$ts] $Msg"
    }
}

function Write-Success {
    param([string]$Msg)
    Write-Host "✅ $Msg" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Msg)
    Write-Host "❌ $Msg" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Msg)
    Write-Host "⚠️  $Msg" -ForegroundColor Yellow
}

# -------------------------------------------------------
# Step 36: Verify npm Package Contents
# -------------------------------------------------------

Write-Log "Step 36: Verify Continue npm Package Contents"

# Validate paths exist
if (-not (Test-Path $manifestPath)) {
    Write-Error-Custom "Manifest not found: $manifestPath"
    exit 2
}

if (-not (Test-Path $validatorModulePath)) {
    Write-Error-Custom "Validator module not found: $validatorModulePath"
    exit 2
}

if (-not (Test-Path $packagePath)) {
    Write-Error-Custom "Package not found: $packagePath"
    Write-Log "  Expected location (from Step 35): $packagePath"
    exit 2
}

Write-Log "✓ Paths validated"

# -------------------------------------------------------
# Invoke Node.js Validator
# -------------------------------------------------------

$tempScript = [System.IO.Path]::GetTempFileName() + '.mjs'

# Escape paths for JavaScript
$cacheDirJs = $CacheDir -replace '\\', '/'
$manifestJs = $manifestPath -replace '\\', '/'
$packageJs = $packagePath -replace '\\', '/'
$versionDirJs = $VersionDir -replace '\\', '/'

# Node.js script that imports and runs the validator
$nodeScript = @"
import { validatePackageContents, PackageValidationError, ArchiveError, MetadataError } from '$($validatorModulePath -replace '\\', '/')';

async function main() {
  try {
    const result = await validatePackageContents('$packageJs', '$manifestJs');

    // Output result as JSON for PowerShell parsing
    console.log(JSON.stringify(result, null, 2));

    // Exit code: 0 = success, 1 = warnings, 2 = errors
    if (result.errors && result.errors.length > 0) {
      process.exit(2);
    } else if (result.warnings && result.warnings.length > 0) {
      process.exit(1);
    } else {
      process.exit(0);
    }
  } catch (err) {
    console.error(JSON.stringify({
      error: err.message,
      name: err.name,
      details: err.details,
      stack: err.stack
    }, null, 2));
    process.exit(2);
  }
}

main();
"@

try {
    Set-Content $tempScript $nodeScript

    Write-Log "Validating package contents..."
    $output = & node $tempScript 2>&1
    $exitCode = $LASTEXITCODE

    # Parse JSON output
    $result = try {
        $output | ConvertFrom-Json
    } catch {
        @{
            error = "Failed to parse validation output"
            rawOutput = $output -join "`n"
        }
    }

    # Display results
    if ($exitCode -eq 0) {
        Write-Success "Package validation successful"

        if ($result.fileCount) {
            Write-Log "  Files checked: $($result.fileCount)"
        }

        if ($result.summary.requiredFiles) {
            Write-Log "  Required files: $(($result.summary.requiredFiles | Measure-Object).Count) present"
        }

        if ($result.summary.validationDuration) {
            Write-Log "  Duration: $($result.summary.validationDuration)ms"
        }

        exit 0
    }
    elseif ($exitCode -eq 1) {
        Write-Warning "Package validation completed with warnings"

        if ($result.warnings) {
            Write-Log "Warnings:"
            $result.warnings | ForEach-Object { Write-Log "  - $_" }
        }

        exit 1
    }
    else {
        Write-Error-Custom "Package validation failed"

        if ($result.error) {
            Write-Log "  Error: $($result.error)"
        }

        if ($result.errors) {
            Write-Log "  Details:"
            $result.errors | ForEach-Object { Write-Log "    - $_" }
        }

        if ($result.details) {
            Write-Log "  Additional context:"
            $result.details | Get-Member -Type NoteProperty | ForEach-Object {
                $name = $_.Name
                $value = $result.details.$name
                Write-Log "    $name : $value"
            }
        }

        exit 2
    }
}
catch {
    Write-Error-Custom "Failed to execute validator: $_"
    Write-Log "  At: $($_.InvocationInfo.ScriptName):$($_.InvocationInfo.ScriptLineNumber)"
    exit 2
}
finally {
    Remove-Item $tempScript -Force -ErrorAction SilentlyContinue
}
