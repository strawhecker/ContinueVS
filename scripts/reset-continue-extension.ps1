# Reset ContinueVS Extension - Clean Slate
# This script clears all extension state, cache, and configuration
# Allows fresh initialization with model selection dialog

Write-Host "🔄 Resetting ContinueVS Extension..." -ForegroundColor Cyan

# Get user home directory
$userHome = $env:USERPROFILE

# Step 1: Stop VS Debug if running
Write-Host "`n1️⃣  Stopping Visual Studio debug session..."
Get-Process devenv -ErrorAction SilentlyContinue | ForEach-Object { 
    if ($_.ProcessName -eq "devenv") {
        Write-Host "   Closing experimental VS instances..."
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

# Step 2: Clear bridge state files
Write-Host "`n2️⃣  Clearing bridge state files..."
$bridgeStateFile = Join-Path $userHome ".continue\bridge-state.json"
if (Test-Path $bridgeStateFile) {
    Remove-Item $bridgeStateFile -Force
    Write-Host "   ✅ Removed: $bridgeStateFile"
} else {
    Write-Host "   ℹ️  No bridge state file found"
}

# Step 3: Clear VS extension cache (if it exists)
Write-Host "`n3️⃣  Clearing VS extension cache..."
$vsCacheDir = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio\17.0_*\ComponentModelCache"
$vsCacheDirs = Get-ChildItem -Path (Split-Path $vsCacheDir) -Filter "ComponentModelCache" -ErrorAction SilentlyContinue
if ($vsCacheDirs) {
    foreach ($dir in $vsCacheDirs) {
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "   ✅ Cleared: $dir"
    }
} else {
    Write-Host "   ℹ️  VS cache not found (will be recreated on startup)"
}

# Step 4: Remove .continue/config.json and recreate with Ollama settings
Write-Host "`n4️⃣  Resetting Continue configuration..."
$continueDir = Join-Path $userHome ".continue"
$configFile = Join-Path $continueDir "config.json"

# Ensure directory exists
if (-not (Test-Path $continueDir)) {
    New-Item -ItemType Directory -Path $continueDir -Force | Out-Null
}

# Create fresh Ollama config
$config = @{
    models = @(
        @{
            title = "Ollama-Llama-3.1-8B"
            provider = "ollama"
            model = "hf.co/bartowski/Meta-Llama-3.1-8B-Instruct-GGUF:Q4_K_M"
            apiBase = "http://127.0.0.1:11434"
        }
    )
} | ConvertTo-Json -Depth 10

Set-Content -Path $configFile -Value $config -Force
Write-Host "   ✅ Reset config: $configFile"

# Step 5: Verify Ollama is running
Write-Host "`n5️⃣  Verifying Ollama is responsive..."
try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:11434/api/tags" -TimeoutSec 5 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Host "   ✅ Ollama is running and responsive"
    } else {
        Write-Host "   ❌ Ollama returned status: $($response.StatusCode)"
        Write-Host "   💡 Start Ollama with: ollama serve"
    }
} catch {
    Write-Host "   ❌ Cannot reach Ollama at http://127.0.0.1:11434"
    Write-Host "   💡 Start Ollama with: ollama serve"
}

# Step 6: Summary
Write-Host "`n" -ForegroundColor Green
Write-Host "✅ ContinueVS Extension Reset Complete!" -ForegroundColor Green
Write-Host "`n📋 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Ensure Ollama is running: ollama serve"
Write-Host "   2. In Visual Studio, press F5 (Debug > Start Debugging)"
Write-Host "   3. The extension will show the model selection dialog"
Write-Host "   4. Select your Ollama model and proceed"
Write-Host "`n💾 Config file location: $configFile" -ForegroundColor Cyan
Write-Host "🌊 Bridge state location: $(Join-Path $userHome '.continue\bridge-state.json')" -ForegroundColor Cyan
