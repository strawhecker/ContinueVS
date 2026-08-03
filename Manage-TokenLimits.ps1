#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Manages ContinueVS token limit settings in ~/.continue/vsx-settings.json

.DESCRIPTION
    This script provides a simple interface to view, create, and modify
    token limit settings for the ContinueVS extension.

.PARAMETER Action
    The action to perform: List, Create, Update, Reset, or Validate

.PARAMETER MaxContextTokens
    Maximum context tokens (for Create/Update)

.PARAMETER ReserveTokens
    Tokens to reserve for response (for Create/Update)

.PARAMETER CharsPerToken
    Characters per token ratio (for Create/Update)

.PARAMETER ModelName
    Optional description/model name for the settings

.EXAMPLE
    .\Manage-TokenLimits.ps1 -Action List
    # Show current settings

.EXAMPLE
    .\Manage-TokenLimits.ps1 -Action Create -MaxContextTokens 131072 -ReserveTokens 8192
    # Create settings with max 2^17 tokens

.EXAMPLE
    .\Manage-TokenLimits.ps1 -Action Reset
    # Reset to default settings
#>

param (
    [ValidateSet('List', 'Create', 'Update', 'Reset', 'Validate')]
    [string]$Action = 'List',

    [int]$MaxContextTokens = 131072,
    [int]$ReserveTokens = 8192,
    [int]$CharsPerToken = 4,
    [string]$ModelName = "ContinueVS Settings"
)

$continueDir = Join-Path $env:USERPROFILE ".continue"
$settingsFile = Join-Path $continueDir "vsx-settings.json"

function Ensure-Directory {
    if (-not (Test-Path $continueDir)) {
        Write-Host "📁 Creating .continue directory..." -ForegroundColor Cyan
        New-Item -ItemType Directory -Path $continueDir -Force | Out-Null
        Write-Host "✓ Directory created: $continueDir" -ForegroundColor Green
    }
}

function List-Settings {
    Write-Host "`n📋 Token Limit Settings" -ForegroundColor Cyan
    Write-Host ("=" * 50)

    if (Test-Path $settingsFile) {
        try {
            $settings = Get-Content $settingsFile -Raw | ConvertFrom-Json
            Write-Host "Settings file: $settingsFile" -ForegroundColor Gray
            Write-Host ""
            Write-Host "Max Context Tokens:     $($settings.maxContextTokens)" -ForegroundColor Yellow
            Write-Host "Reserve for Response:   $($settings.reserveTokensForResponse)" -ForegroundColor Yellow
            Write-Host "Chars Per Token:        $($settings.charsPerToken)" -ForegroundColor Yellow
            if ($settings.description) {
                Write-Host "Description:            $($settings.description)" -ForegroundColor Gray
            }

            # Calculate usable
            $usable = $settings.maxContextTokens - $settings.reserveTokensForResponse
            Write-Host ""
            Write-Host "Usable Context Tokens:  $usable" -ForegroundColor Green
            Write-Host "Reserve Percentage:     $([math]::Round($settings.reserveTokensForResponse * 100 / $settings.maxContextTokens, 1))%" -ForegroundColor Gray
        }
        catch {
            Write-Host "⚠️  Error reading settings file: $_" -ForegroundColor Red
        }
    }
    else {
        Write-Host "No settings file found. Using defaults:" -ForegroundColor Yellow
        Write-Host "Max Context Tokens:     131072" -ForegroundColor Gray
        Write-Host "Reserve for Response:   8192" -ForegroundColor Gray
        Write-Host "Chars Per Token:        4" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Run: .\Manage-TokenLimits.ps1 -Action Create" -ForegroundColor Cyan
    }
    Write-Host ""
}

function Create-Settings {
    Write-Host "`n➕ Creating Token Limit Settings" -ForegroundColor Cyan
    Write-Host ("=" * 50)

    Ensure-Directory

    if (Test-Path $settingsFile) {
        Write-Host "⚠️  Settings file already exists!" -ForegroundColor Yellow
        $response = Read-Host "Overwrite? (y/n)"
        if ($response -ne 'y') {
            Write-Host "Cancelled." -ForegroundColor Gray
            return
        }
    }

    $settings = @{
        maxContextTokens = $MaxContextTokens
        reserveTokensForResponse = $ReserveTokens
        charsPerToken = $CharsPerToken
        description = $ModelName
    }

    try {
        $settings | ConvertTo-Json | Set-Content -Path $settingsFile
        Write-Host "✓ Settings created successfully!" -ForegroundColor Green
        List-Settings
        Write-Host "❗ Restart Visual Studio for changes to take effect" -ForegroundColor Yellow
    }
    catch {
        Write-Host "✗ Error creating settings: $_" -ForegroundColor Red
    }
}

function Update-Settings {
    Write-Host "`n✏️  Updating Token Limit Settings" -ForegroundColor Cyan
    Write-Host ("=" * 50)

    if (-not (Test-Path $settingsFile)) {
        Write-Host "⚠️  Settings file not found. Create it first:" -ForegroundColor Yellow
        Write-Host ".\Manage-TokenLimits.ps1 -Action Create" -ForegroundColor Cyan
        return
    }

    try {
        $current = Get-Content $settingsFile -Raw | ConvertFrom-Json
        $current.maxContextTokens = $MaxContextTokens
        $current.reserveTokensForResponse = $ReserveTokens
        $current.charsPerToken = $CharsPerToken
        if ($ModelName) {
            $current.description = $ModelName
        }

        $current | ConvertTo-Json | Set-Content -Path $settingsFile
        Write-Host "✓ Settings updated successfully!" -ForegroundColor Green
        List-Settings
        Write-Host "❗ Restart Visual Studio for changes to take effect" -ForegroundColor Yellow
    }
    catch {
        Write-Host "✗ Error updating settings: $_" -ForegroundColor Red
    }
}

function Reset-Settings {
    Write-Host "`n🔄 Resetting Token Limit Settings" -ForegroundColor Cyan
    Write-Host ("=" * 50)

    $response = Read-Host "Reset to defaults? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Cancelled." -ForegroundColor Gray
        return
    }

    Ensure-Directory

    $defaults = @{
        maxContextTokens = 131072
        reserveTokensForResponse = 8192
        charsPerToken = 4
        description = "Default ContinueVS token limit settings"
    }

    try {
        $defaults | ConvertTo-Json | Set-Content -Path $settingsFile
        Write-Host "✓ Settings reset to defaults!" -ForegroundColor Green
        List-Settings
        Write-Host "❗ Restart Visual Studio for changes to take effect" -ForegroundColor Yellow
    }
    catch {
        Write-Host "✗ Error resetting settings: $_" -ForegroundColor Red
    }
}

function Validate-Settings {
    Write-Host "`n✓ Validating Token Limit Settings" -ForegroundColor Cyan
    Write-Host ("=" * 50)

    if (-not (Test-Path $settingsFile)) {
        Write-Host "⚠️  No settings file found (will use defaults)" -ForegroundColor Yellow
        return
    }

    try {
        $settings = Get-Content $settingsFile -Raw | ConvertFrom-Json

        $valid = $true

        if ($settings.maxContextTokens -le 0) {
            Write-Host "✗ MaxContextTokens must be > 0" -ForegroundColor Red
            $valid = $false
        } else {
            Write-Host "✓ MaxContextTokens = $($settings.maxContextTokens)" -ForegroundColor Green
        }

        if ($settings.reserveTokensForResponse -lt 0) {
            Write-Host "✗ ReserveTokensForResponse cannot be negative" -ForegroundColor Red
            $valid = $false
        } else {
            Write-Host "✓ ReserveTokensForResponse = $($settings.reserveTokensForResponse)" -ForegroundColor Green
        }

        if ($settings.charsPerToken -le 0) {
            Write-Host "✗ CharsPerToken must be > 0" -ForegroundColor Red
            $valid = $false
        } else {
            Write-Host "✓ CharsPerToken = $($settings.charsPerToken)" -ForegroundColor Green
        }

        $usable = $settings.maxContextTokens - $settings.reserveTokensForResponse
        if ($usable -lt 100) {
            Write-Host "⚠️  Warning: Reserve is almost equal to max (usable: $usable tokens)" -ForegroundColor Yellow
            $valid = $false
        }

        if ($valid) {
            Write-Host ""
            Write-Host "✓ All settings are valid!" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "✗ Invalid JSON in settings file: $_" -ForegroundColor Red
    }
}

# Main execution
switch ($Action) {
    'List' { List-Settings }
    'Create' { Create-Settings }
    'Update' { Update-Settings }
    'Reset' { Reset-Settings }
    'Validate' { Validate-Settings }
}
