param([string]$Version = 'v2.0.0', [string]$VersionDir = '', [string]$CacheDir = '', [switch]$DryRun, [switch]$Quiet)
$ErrorActionPreference = 'Stop'
# Resolve solution root from current directory or use explicit path
if (Test-Path 'E:\GitRepos\ContinueVS\src\versions\v2.0.0\manifest.json') {
    $solutionRoot = 'E:\GitRepos\ContinueVS'
} else {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $solutionRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
}
if (-not $VersionDir) { $VersionDir = Join-Path $solutionRoot 'src\versions\v2.0.0' }
if (-not $CacheDir) { $CacheDir = Join-Path $solutionRoot '.cache\npm-packages\v2.0.0' }
$manifestPath = Join-Path $VersionDir 'manifest.json'
$downloadModulePath = Join-Path $VersionDir 'lib\npm-registry-download.mjs'
function Write-Log { param([string]$Msg); if (-not $Quiet) { $ts = Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ'; Write-Host "[$ts] $Msg" } }
Write-Log "Step 35: Download & Verify npm Package"
if (-not (Test-Path $manifestPath)) { Write-Error "Manifest not found: $manifestPath" }
if (-not (Test-Path $downloadModulePath)) { Write-Error "Module not found: $downloadModulePath" }
Write-Log "✓ Paths validated"
$tempScript = [System.IO.Path]::GetTempFileName() + '.mjs'
$dryRunJs = if ($DryRun) { 'true' } else { 'false' }
$cacheDirJs = $CacheDir -replace '\\', '/'
$manifestJs = $manifestPath -replace '\\', '/'
$dlModuleJs = $downloadModulePath -replace '\\', '/'
@"
import { downloadWithFallback } from '$dlModuleJs';
const result = await downloadWithFallback('$Version', '$cacheDirJs', '$manifestJs', { dryRun: $dryRunJs });
console.log(JSON.stringify(result, null, 2));
process.exit(result.success ? (result.fallbackUsed ? 1 : 0) : 2);
"@ | Set-Content $tempScript
try {
    $output = & node $tempScript 2>&1
    $exitCode = $LASTEXITCODE
    $result = $output | ConvertFrom-Json
    if (-not $Quiet) { Write-Host $output }
    if ($result.success) {
        Write-Log "✅ Complete"; if ($result.fallbackUsed) { Write-Log "   (Used cache)" }
    } else {
        Write-Log "❌ Failed: $($result.error)"
    }
    exit $exitCode
} finally {
    Remove-Item $tempScript -Force -ErrorAction SilentlyContinue
}
