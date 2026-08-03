# Token Limit Settings Guide

## Overview

The ContinueVS extension now supports durable, user-configurable token limit settings stored in `~/.continue/vsx-settings.json`. These settings persist between sessions and control:

- **maxContextTokens**: Total available context window size (default: 131,072 = 2^17)
- **reserveTokensForResponse**: Tokens reserved for model output (default: 8,192 = 2^13) 
- **charsPerToken**: Estimated character-to-token ratio for estimation (default: 4)

## File Location

Settings are stored in your Continue configuration directory:
```
~/.continue/vsx-settings.json
```

On Windows, this typically expands to:
```
C:\Users\<YourUsername>\.continue\vsx-settings.json
```

## Default Settings

When the file doesn't exist, the extension uses these defaults:

```json
{
  "maxContextTokens": 131072,
  "reserveTokensForResponse": 8192,
  "charsPerToken": 4,
  "description": "ContinueVS token limit settings"
}
```

## Creating Custom Settings

### Method 1: Create the JSON File Manually

1. Open your text editor (Notepad, VS Code, etc.)
2. Create a file named `vsx-settings.json` in `~/.continue/`
3. Add custom token limits:

```json
{
  "maxContextTokens": 262144,
  "reserveTokensForResponse": 16384,
  "charsPerToken": 4,
  "description": "Custom settings for high-context models like GPT-4 Turbo"
}
```

4. Save the file
5. Restart Visual Studio for changes to take effect

### Method 2: Using PowerShell (Automated)

Run this PowerShell script to create settings:

```powershell
$continueDir = Join-Path $env:USERPROFILE ".continue"
if (-not (Test-Path $continueDir)) {
    New-Item -ItemType Directory -Path $continueDir | Out-Null
    Write-Host "Created .continue directory"
}

$settings = @{
    maxContextTokens = 262144
    reserveTokensForResponse = 16384
    charsPerToken = 4
    description = "Custom settings for high-context models"
}

$settingsPath = Join-Path $continueDir "vsx-settings.json"
$settings | ConvertTo-Json | Set-Content -Path $settingsPath
Write-Host "Settings saved to $settingsPath"
```

### Method 3: Programmatic (C#)

In your application code:

```csharp
using ContinueVS.Services;

// Create custom settings
var customSettings = new TokenLimitSettings.TokenLimitConfig
{
    MaxContextTokens = 262144,      // 2^18
    ReserveTokensForResponse = 16384, // 2^14
    CharsPerToken = 4,
    Description = "Custom settings"
};

// Save to ~/.continue/vsx-settings.json
await TokenLimitSettings.WriteSettingsAsync(customSettings);

// Read back from file
var loadedSettings = await TokenLimitSettings.ReadSettingsAsync();
Console.WriteLine($"Loaded max tokens: {loadedSettings.MaxContextTokens}");
```

## Configuration Examples

### Example 1: Conservative Settings (Smaller Models)
For models with 4K-8K context windows:

```json
{
  "maxContextTokens": 4096,
  "reserveTokensForResponse": 1000,
  "charsPerToken": 4,
  "description": "Conservative settings for 4K models"
}
```

### Example 2: High-Context Settings (GPT-4, Claude)
For large context window models (32K-100K+):

```json
{
  "maxContextTokens": 100000,
  "reserveTokensForResponse": 20000,
  "charsPerToken": 4,
  "description": "High-context settings for GPT-4 and Claude"
}
```

### Example 3: Very Large Context (Llama 2 70B, GPT-4 Turbo)
For ultra-large context windows:

```json
{
  "maxContextTokens": 2097152,
  "reserveTokensForResponse": 131072,
  "charsPerToken": 4,
  "description": "Ultra-large context for 128K+ models"
}
```

### Example 4: Optimized for Code (Higher Chars-Per-Token)
Adjust `charsPerToken` if your token estimates seem off:

```json
{
  "maxContextTokens": 131072,
  "reserveTokensForResponse": 8192,
  "charsPerToken": 3.5,
  "description": "Code-optimized with adjusted token ratio"
}
```

## How Settings Are Applied

1. **Startup**: ContextWindowCollector loads settings from `~/.continue/vsx-settings.json`
2. **Caching**: Settings are cached in memory for performance; restart VS to reload
3. **Fallback**: If file is missing or invalid, defaults are used automatically
4. **Compilation**: LlmCompileChatHandler uses these limits when pruning messages
5. **UI Display**: Continue panel shows available context based on these settings

## Monitoring Settings

Check the Visual Studio Debug Output window for token limit logs:

```
[b24-TOKEN-SETTINGS] Loaded settings: maxContext=131072, reserve=8192, charsPerToken=4
[b24-CONFIG] Using CONTINUE_MAX_CONTEXT_TOKENS=131072
```

## Compatibility with Environment Variables

Both the JSON file and environment variables are supported:

- **JSON file** (.continue/vsx-settings.json): Preferred, persists across sessions
- **Environment variables**: Override JSON if set
  - `CONTINUE_MAX_CONTEXT_TOKENS`
  - `CONTINUE_RESERVE_TOKENS`
  - `CONTINUE_CHARS_PER_TOKEN`

Priority order (highest to lowest):
1. Environment variables (if set)
2. JSON settings file (if exists)
3. Hardcoded defaults

##Troubleshooting

### Settings not loading?

1. **Check file path**: Ensure `~/.continue/vsx-settings.json` exists
   ```powershell
   Test-Path "$env:USERPROFILE\.continue\vsx-settings.json"
   ```

2. **Restart Visual Studio**: Settings are cached on load

3. **Check format**: Validate JSON syntax
   ```powershell
   Get-Content "$env:USERPROFILE\.continue\vsx-settings.json" | ConvertFrom-Json
   ```

4. **Check Debug Output**: Look for `[b24-TOKEN-SETTINGS]` messages in VS Debug window

### Values not taking effect?

1. Close and reopen Visual Studio
2. Clear browser cache if using web UI Continue
3. Check environment variables aren't overriding settings:
   ```powershell
   $env:CONTINUE_MAX_CONTEXT_TOKENS
   $env:CONTINUE_RESERVE_TOKENS
   ```

### JSON parsing errors?

Use an online JSON validator or:
```powershell
$settings = Get-Content "$env:USERPROFILE\.continue\vsx-settings.json" -Raw | ConvertFrom-Json
$settings | ConvertTo-Json | Set-Content "$env:USERPROFILE\.continue\vsx-settings.json"
```

## API Reference

### TokenLimitSettings Class

```csharp
// Read settings (cached, thread-safe)
var settings = await TokenLimitSettings.ReadSettingsAsync();

// Write settings
var customSettings = new TokenLimitSettings.TokenLimitConfig
{
    MaxContextTokens = 262144,
    ReserveTokensForResponse = 16384,
    CharsPerToken = 4
};
await TokenLimitSettings.WriteSettingsAsync(customSettings);

// Clear cache (force re-read from disk)
TokenLimitSettings.ClearCache();

// Get usable tokens (max - reserve)
int usable = TokenLimitSettings.GetUsableContextTokens(settings);
```

## Best Practices

1. **Set matching model limits**: Align token limits with your model's actual context window
2. **Reserve enough response tokens**: Leave 10-20% buffer for model output
3. **Match your char-per-token ratio**: Common values are 3-5 depending on language
4. **Test incrementally**: Start conservative, increase limits gradually
5. **Monitor logs**: Watch debug output for pruning decisions

## Related Configuration

Limits work in conjunction with environment variables for the backend handler:

```powershell
# Set these before launching Visual Studio for backend handler limits
$env:CONTINUE_MAX_CONTEXT_TOKENS = "131072"
$env:CONTINUE_RESERVE_TOKENS = "8192"
$env:CONTINUE_CHARS_PER_TOKEN = "4"
```
