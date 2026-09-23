#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

git pull --rebase
if ($LASTEXITCODE -ne 0) { throw "git pull failed" }

$manifest = "src\VSIXProject1\source.extension.vsixmanifest"
$content  = Get-Content $manifest -Raw

$identity = [regex]::Match($content, '<Identity[^>]*?Version="([0-9]+\.[0-9]+\.[0-9]+)"')
if (-not $identity.Success) { throw "Could not find Identity version in $manifest" }

$oldVer  = $identity.Groups[1].Value
$parts   = $oldVer -split '\.'
$parts[2] = [int]$parts[2] + 1
$newVer  = $parts -join '.'

# Write the new version back into the manifest
$content = $content.Replace("Version=`"$oldVer`"", "Version=`"$newVer`"")
Set-Content -Path $manifest -Value $content

Write-Output "Bumping $oldVer -> $newVer"

git add $manifest
git commit -m "Bump version from $oldVer to $newVer"
if ($LASTEXITCODE -ne 0) { throw "git commit failed" }

git tag "v$newVer"
git push
git push --tags
