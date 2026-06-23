<#
.SYNOPSIS
	Vendors the Continue GUI assets from a VSIX file into VSIXProject1/gui/.

.DESCRIPTION
	Clears VSIXProject1/gui/, then extracts the extension/gui/ subtree from the
	supplied VSIX (which is a ZIP) into that folder, stripping the extension/gui/
	prefix so that gui/index.html lands directly under VSIXProject1/gui/index.html.

	Run this once per Continue release upgrade. Commit the resulting gui/ folder.

.PARAMETER VsixPath
	Absolute or relative path to the Continue .vsix file to extract from.

.EXAMPLE
	.\tools\Vendor-GuiAssets.ps1 -VsixPath .\continue-1.2.0.vsix

.NOTES
	After running, update docs/adr/ADR-006-gui-assets-vendored.md with the
	pinned version printed at the end of this script.
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[string] $VsixPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------
$ScriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir
$GuiDest      = Join-Path $SolutionRoot 'VSIXProject1\gui'
$GuiPrefix    = 'extension/gui/'

if (-not (Test-Path $VsixPath -PathType Leaf)) {
	Write-Error "VSIX not found: $VsixPath"
	exit 1
}
$VsixPath = (Resolve-Path $VsixPath).Path

Write-Host "VSIX   : $VsixPath"
Write-Host "GuiDest: $GuiDest"

# ---------------------------------------------------------------------------
# Load ZIP support
# ---------------------------------------------------------------------------
Add-Type -Assembly 'System.IO.Compression.FileSystem'

# ---------------------------------------------------------------------------
# Detect Continue version from extension/package.json inside the VSIX
# ---------------------------------------------------------------------------
$version = $null
try {
	$zip = [System.IO.Compression.ZipFile]::OpenRead($VsixPath)
	try {
		$pkgEntry = $zip.Entries |
			Where-Object { $_.FullName -eq 'extension/package.json' } |
			Select-Object -First 1

		if ($pkgEntry) {
			$reader = New-Object System.IO.StreamReader($pkgEntry.Open())
			$json   = $reader.ReadToEnd()
			$reader.Dispose()

			if ($json -match '"version"\s*:\s*"([^"]+)"') {
				$version = $Matches[1]
			}
		}
	} finally {
		$zip.Dispose()
	}
} catch {
	Write-Warning "Could not read version from VSIX: $_"
}

if ($version) {
	Write-Host "Version: $version" -ForegroundColor Cyan
} else {
	Write-Warning 'Could not detect Continue version from VSIX.'
}

# ---------------------------------------------------------------------------
# Wipe existing gui folder
# ---------------------------------------------------------------------------
if (Test-Path $GuiDest) {
	Write-Host 'Removing existing gui/ folder...'
	Remove-Item $GuiDest -Recurse -Force
}
New-Item $GuiDest -ItemType Directory | Out-Null

# ---------------------------------------------------------------------------
# Extract extension/gui/** from the VSIX
# ---------------------------------------------------------------------------
Write-Host "Extracting '$GuiPrefix' entries..."
$extracted = 0

$zip = [System.IO.Compression.ZipFile]::OpenRead($VsixPath)
try {
	foreach ($entry in $zip.Entries) {
		if (-not $entry.FullName.StartsWith($GuiPrefix, [System.StringComparison]::Ordinal)) {
			continue
		}

		$relative = $entry.FullName.Substring($GuiPrefix.Length)
		if ([string]::IsNullOrEmpty($relative)) { continue }

		$destPath = Join-Path $GuiDest ($relative.Replace('/', '\'))

		# Directory entry — just ensure it exists
		if ($entry.FullName.EndsWith('/')) {
			New-Item $destPath -ItemType Directory -Force | Out-Null
			continue
		}

		$destDir = Split-Path $destPath
		if (-not (Test-Path $destDir)) {
			New-Item $destDir -ItemType Directory -Force | Out-Null
		}

		$srcStream  = $entry.Open()
		$destStream = [System.IO.File]::Create($destPath)
		try {
			$srcStream.CopyTo($destStream)
			$extracted++
		} finally {
			$destStream.Dispose()
			$srcStream.Dispose()
		}
	}
} finally {
	$zip.Dispose()
}

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Host ''
Write-Host "Done. $extracted files extracted to:" -ForegroundColor Green
Write-Host "  $GuiDest" -ForegroundColor Green

if ($version) {
	Write-Host ''
	Write-Host "Next steps:" -ForegroundColor Yellow
	Write-Host "  1. git add VSIXProject1/gui"
	Write-Host "  2. Update docs/adr/ADR-006-gui-assets-vendored.md — pinned version: $version"
	Write-Host "  3. Commit: git commit -m 'chore: vendor Continue GUI $version'"
}
