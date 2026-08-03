# Quick Start: Token Limit Settings

## In 2 Minutes

### Option 1: Manual (No Scripts)

1. Open Notepad
2. Paste this:
   ```json
   {
     "maxContextTokens": 131072,
     "reserveTokensForResponse": 8192,
     "charsPerToken": 4
   }
   ```
3. Save as: `C:\Users\<YourUsername>\.continue\vsx-settings.json`
4. Restart Visual Studio ✓

### Option 2: PowerShell (Automated)

```powershell
# Copy & paste this entire block into PowerShell
$continueDir = Join-Path $env:USERPROFILE ".continue"
New-Item -ItemType Directory -Path $continueDir -Force | Out-Null
$settings = @{
    maxContextTokens = 131072
    reserveTokensForResponse = 8192
    charsPerToken = 4
} | ConvertTo-Json
Set-Content -Path (Join-Path $continueDir "vsx-settings.json") -Value $settings
Write-Host "✓ Settings created at $continueDir\vsx-settings.json"
```

## Configuration Preset

Choose your model and settings:

| Model | Max Context | Reserve | JSON |
|-------|-------------|---------|------|
| **GPT-4 Turbo** | 128,000 | 20,000 | [See below](#gpt-4-turbo-preset) |
| **Claude 3** | 200,000 | 30,000 | [See below](#claude-3-preset) |
| **Llama 2 70B** | 4,096 | 1,024 | [See below](#llama-2-preset) |
| **Mistral Large** | 32,000 | 4,000 | [See below](#mistral-preset) |

### GPT-4 Turbo Preset
```json
{
  "maxContextTokens": 128000,
  "reserveTokensForResponse": 20000,
  "charsPerToken": 4
}
```

### Claude 3 Preset
```json
{
  "maxContextTokens": 200000,
  "reserveTokensForResponse": 30000,
  "charsPerToken": 4
}
```

### Llama 2 Preset
```json
{
  "maxContextTokens": 4096,
  "reserveTokensForResponse": 1024,
  "charsPerToken": 4
}
```

### Mistral Preset
```json
{
  "maxContextTokens": 32000,
  "reserveTokensForResponse": 4000,
  "charsPerToken": 4
}
```

## File Location

Save your JSON file here:
```
~/.continue/vsx-settings.json
```

Or on Windows:
```
C:\Users\<YourUsername>\.continue\vsx-settings.json
```

## Verify It's Working

1. Open Visual Studio Debug Output (Debug → Windows → Output)
2. Look for:
   ```
   [b24-TOKEN-SETTINGS] Loaded settings: maxContext=131072, reserve=8192...
   ```

## Never Worked?

- ✓ Restart Visual Studio after changes
- ✓ Check file is in `.continue` folder (not `.continue-config` or similar)
- ✓ Validate JSON format (no trailing commas, quotes around keys)
- ✓ Check debug output for error messages

## Help & Full Guide

See `TOKEN_LIMITS_GUIDE.md` for complete documentation and advanced options.
