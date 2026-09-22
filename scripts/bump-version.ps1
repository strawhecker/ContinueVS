param(
    [Parameter(Mandatory = $true)]
    [string]$NewVersion
)

$manifest = 'src/VSIXProject1/source.extension.vsixmanifest'
if (-not (Test-Path $manifest)) {
    Write-Error "Manifest not found: $manifest"
    exit 1
}

# Keep the original encoding / line endings intact where possible
$xml = [xml](Get-Content $manifest -Raw)
$xml.PackageManifest.Metadata.Identity.Version = $NewVersion

# Save with UTF-8 (no BOM) and XML declaration to keep the manifest valid
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$settings.OmitXmlDeclaration = $false

$writer = [System.Xml.XmlWriter]::Create($manifest, $settings)
$xml.Save($writer)
$writer.Close()

Write-Host "Bumped VSIX version to $NewVersion" -ForegroundColor Green
