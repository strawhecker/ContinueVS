# npm Cache Download Manager — Usage Guide

**Module**: `src/versions/v2.0.0/lib/cache-download.js`  
**Version**: 1.0  
**Type**: Node.js ESM module  
**Status**: Step 11 (Created)  
**Related Steps**: 8 (integrity validation), 12 (startup validation), 35 (download & verify)  

---

## Overview

The npm cache download manager provides automatic download and caching of Continue npm packages. It implements a **cache-first strategy**: check local cache first, download from npm registry only if missing or invalid.

### When to Use

- **Step 12**: Bridge initialization calls this to ensure package available before startup
- **Step 35**: Fetches official Continue v2.0.0 package and verifies integrity
- **C# Bridge**: `ContinueVSPackage.cs` can invoke via Node.js child process or PowerShell wrapper

### Key Features

✅ **Fast startup** — Cached packages used without download (< 10ms overhead)  
✅ **Automatic download** — Fetches from npm registry if missing  
✅ **Corruption detection** — Invalid packages deleted and re-downloaded  
✅ **Error resilience** — Network errors collected, not thrown  
✅ **Offline support** — Pre-cached packages work without registry access  
✅ **Logging** — All operations logged to `.download-log` for diagnostics  

---

## API Reference

### `downloadPackageIfNeeded(version, cacheDir, options)`

**Main entry point**. Implements cache-first strategy: check local cache, download if needed, validate, return result.

**Parameters**:
- `version` (string): Package version (e.g., `'2.0.0'` or `'v2.0.0'`). Leading 'v' optional; will be normalized.
- `cacheDir` (string): Absolute path to cache directory. Will be created if missing. Example: `E:\GitRepos\ContinueVS\.cache\npm-packages\v2.0.0`
- `options` (Object, optional):
  - `timeout` (number): Download timeout in milliseconds. Default: `60000` (60 seconds)
  - `maxRetries` (number): Max retry attempts on checksum mismatch. Default: `1`

**Returns**: Promise resolving to result object:
```javascript
{
  cached: boolean,           // true if returned from local cache
  valid: boolean,            // true if package valid and ready to use
  packagePath: string,       // absolute path to continue-{version}.tgz
  downloadTime: number,      // milliseconds (0 if cached, or download duration)
  errors: string[]           // error messages (empty if successful)
}
```

**Returns**:
- `valid=true` → Package ready to use (either cached or newly downloaded)
- `valid=false` → Package validation failed; check `errors[]` for details
- `cached=true` → Package loaded from local cache, no network access
- `cached=false` → Package downloaded from npm registry
- `errors=[]` → No errors; operation successful

**Behavior**:
- Never throws exceptions; always returns result object
- If cache directory not writable, error collected in `errors[]`
- All operations logged to `.cache/npm-packages/.download-log`

**Example**:
```javascript
import { downloadPackageIfNeeded } from './cache-download.js';

// Check cache, download if needed
const result = await downloadPackageIfNeeded(
  '2.0.0',
  'E:\\GitRepos\\ContinueVS\\.cache\\npm-packages\\v2.0.0'
);

if (result.valid) {
  console.log(`✅ Package ready: ${result.packagePath}`);
  console.log(`   Cached: ${result.cached}, Time: ${result.downloadTime}ms`);

  // Proceed to next step (e.g., extract, launch)
  launchBridge(result.packagePath);
} else {
  console.error('❌ Package not ready:');
  result.errors.forEach(err => console.error(`   - ${err}`));

  // Step 12 will handle fallback (offline mode or user prompt)
}
```

**Advanced Usage** (custom timeout/retries):
```javascript
const result = await downloadPackageIfNeeded(
  '2.0.0',
  cacheDir,
  { timeout: 30000, maxRetries: 2 } // 30s timeout, 2 retries on corruption
);
```

---

### `downloadPackageFromRegistry(packageName, version, targetDir, options)`

**Lower-level function**. Downloads package from npm registry using `https.get()`. Called internally by `downloadPackageIfNeeded()`.

**Parameters**:
- `packageName` (string): Package name with version (e.g., `'continue-v2.0.0'`)
- `version` (string): Version string for normalization
- `targetDir` (string): Absolute path to write .tgz file
- `options` (Object, optional):
  - `timeout` (number): Download timeout in milliseconds. Default: `60000`

**Returns**: Promise resolving to result object:
```javascript
{
  success: boolean,          // true if download completed
  filePath: string,          // absolute path to .tgz file (empty if failed)
  downloadTime: number,      // milliseconds
  error: string              // error message (empty if successful)
}
```

**Network Details**:
- URL: `https://registry.npmjs.org/continue/-/continue-{version}.tgz`
- Streams response to temporary file (`.tgz.tmp`)
- On success, renames temporary to final path
- On error, cleans up temporary file

**Error Scenarios**:
| Error | Message | Recovery |
|-------|---------|----------|
| **HTTP 404** | `HTTP 404 from npm registry: https://...` | Check version number; retry with correct version |
| **HTTP 5xx** | `HTTP 500+ from npm registry: https://...` | Registry issue; retry later or use cached version |
| **Timeout** | `Download timeout after 60000ms` | Network slow; increase timeout option |
| **Connection refused** | `Network error: ECONNREFUSED ...` | Network issue or registry down |
| **Empty file** | `Downloaded file is empty` | Corrupted response; retry |

**Note**: This function does NOT retry. Retry logic is in `downloadPackageIfNeeded()`.

**Example** (internal usage, but shown for reference):
```javascript
import { downloadPackageFromRegistry } from './cache-download.js';

const result = await downloadPackageFromRegistry(
  'continue-v2.0.0',
  '2.0.0',
  'E:\\cache\\npm-packages\\v2.0.0',
  { timeout: 60000 }
);

if (result.success) {
  console.log(`Downloaded to: ${result.filePath}`);
  console.log(`Time: ${result.downloadTime}ms`);
} else {
  console.error(`Download failed: ${result.error}`);
}
```

---

### `generateChecksum(filePath)`

**Low-level function**. Computes SHA256 hash of a file and writes `.sha256` file in npm standard format.

**Parameters**:
- `filePath` (string): Absolute path to .tgz file

**Returns**: Promise resolving to result object:
```javascript
{
  hash: string,              // SHA256 hash in lowercase hex (64 characters)
  checksumPath: string       // absolute path to .sha256 file (e.g., .tgz.sha256)
}
```

**Checksum File Format** (npm standard):
```
abc123def456...abcdef0123456789abcdef0123456789abcdef  continue-v2.0.0.tgz
```
(Hash, two spaces, filename, newline)

**Throws**: `CacheDownloadError` if file cannot be read or written.

**Example**:
```javascript
import { generateChecksum } from './cache-download.js';

try {
  const result = await generateChecksum(
    'E:\\cache\\npm-packages\\v2.0.0\\continue-v2.0.0.tgz'
  );

  console.log(`SHA256: ${result.hash}`);
  console.log(`Written: ${result.checksumPath}`);
} catch (error) {
  console.error(`Failed to generate checksum: ${error.message}`);
}
```

---

## Integration with C# Bridge

### Via PowerShell Wrapper

C# code in `ContinueVSPackage.cs` can call this module through PowerShell:

```csharp
private async Task<bool> EnsurePackageCachedAsync()
{
    // Invoke Node.js to run cache-download.js
    var scriptPath = Path.Combine(
        Environment.GetEnvironmentVariable("VSIODE_PATH"),
        "src", "versions", "v2.0.0", "lib", "cache-download-wrapper.ps1"
    );

    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                       $"-Version \"v2.0.0\" " +
                       $"-CacheDir \"{cacheDir}\" " +
                       $"-Quiet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        }
    };

    process.Start();
    string jsonOutput = await process.StandardOutput.ReadToEndAsync();
    process.WaitForExit();

    // Parse JSON result
    var result = JsonDocument.Parse(jsonOutput);
    var valid = result.RootElement.GetProperty("valid").GetBoolean();
    var packagePath = result.RootElement.GetProperty("packagePath").GetString();

    if (valid)
    {
        _logger.LogInformation($"✅ Package ready: {packagePath}");
        return true;
    }
    else
    {
        var errors = result.RootElement.GetProperty("errors").EnumerateArray();
        foreach (var error in errors)
        {
            _logger.LogError($"❌ {error.GetString()}");
        }
        return false;
    }
}
```

### Via Direct Node.js Invocation

Alternatively, create a C# wrapper that directly invokes Node.js with cache-download.js:

```csharp
private async Task<(bool valid, string packagePath)> GetCachedPackageAsync()
{
    var modulePath = Path.Combine(solutionRoot, "src", "versions", "v2.0.0", "lib", "cache-download.js");
    var cacheDir = Path.Combine(solutionRoot, ".cache", "npm-packages", "v2.0.0");

    // Create Node.js wrapper script that outputs JSON
    var wrapperScript = $@"
import {{ downloadPackageIfNeeded }} from '{modulePath}';
const result = await downloadPackageIfNeeded('2.0.0', '{cacheDir}');
console.log(JSON.stringify(result));
";

    // Write wrapper to temp file
    var tempFile = Path.GetTempFileName();
    await File.WriteAllTextAsync(tempFile, wrapperScript);

    try
    {
        var process = new Process
        {{
            StartInfo = new ProcessStartInfo
            {{
                FileName = "node.exe",
                Arguments = $"--input-type=module {tempFile}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }}
        }};

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        process.WaitForExit();

        var result = JsonDocument.Parse(output).RootElement;
        var valid = result.GetProperty("valid").GetBoolean();
        var packagePath = result.GetProperty("packagePath").GetString();

        return (valid, packagePath);
    }
    finally
    {
        File.Delete(tempFile);
    }
}
```

---

## Error Scenarios & Recovery

### Scenario 1: Cache Hit (Normal Case)

**What happens**:
1. User opens Continue panel
2. `downloadPackageIfNeeded()` called
3. Local cache validated (✅ valid)
4. Returns immediately with `{ cached: true, valid: true, downloadTime: 0 }`

**User experience**: No delay; Bridge launches instantly (< 10ms overhead)

**Log entries**:
```
[2024-01-15T10:23:45.123Z] Checking local cache for version 2.0.0
[2024-01-15T10:23:45.124Z] Cache hit: E:\...\continue-v2.0.0.tgz
```

---

### Scenario 2: Cache Miss, Successful Download

**What happens**:
1. Local cache missing or invalid
2. Download starts from npm registry
3. Checksum generated (`.tgz.sha256` file written)
4. Package re-validated
5. Returns with `{ cached: false, valid: true, downloadTime: 5234 }`

**User experience**: First launch takes 5-10 seconds; subsequent launches use cache

**Log entries**:
```
[2024-01-15T10:23:45.200Z] Checking local cache for version 2.0.0
[2024-01-15T10:23:45.201Z] Cache miss or invalid. Downloading version 2.0.0...
[2024-01-15T10:23:50.435Z] Generated checksum: abc123def456...
[2024-01-15T10:23:50.440Z] Validation passed after download. Time: 5240ms
```

---

### Scenario 3: Download Timeout, Offline Fallback

**What happens**:
1. Network is slow; download timeout (60s) reached
2. Retry once, also times out
3. Returns `{ valid: false, errors: ["Download timeout after 60000ms"] }`
4. Step 12 detects failure, enables offline mode or prompts user

**User experience**: User waits 120s, then gets clear message ("Network unavailable; using offline mode")

**Log entries**:
```
[2024-01-15T10:23:45.300Z] Cache miss or invalid. Downloading version 2.0.0...
[2024-01-15T10:24:45.800Z] Download attempt 1/2: continue-v2.0.0
[2024-01-15T10:24:46.100Z] Network error: Download timeout after 60000ms
[2024-01-15T10:24:46.150Z] Retrying... (1 retries remaining)
[2024-01-15T10:25:46.600Z] Download attempt 2/2: continue-v2.0.0
[2024-01-15T10:25:47.050Z] Network error: Download timeout after 60000ms
[2024-01-15T10:25:47.100Z] Cleaned up invalid package: E:\...\continue-v2.0.0.tgz
```

---

### Scenario 4: Checksum Mismatch (Corruption)

**What happens**:
1. Download succeeds
2. Checksum generated
3. Re-validation finds mismatch
4. Package deleted, retry attempts to re-download
5. If retry also fails, returns error

**User experience**: Transparent; user may not notice. If both attempts fail, gets error message.

**Log entries**:
```
[2024-01-15T10:23:50.440Z] Validation failed on attempt 1: Checksum mismatch...
[2024-01-15T10:23:50.500Z] Cleaned up invalid package: E:\...\continue-v2.0.0.tgz
[2024-01-15T10:23:50.550Z] Retrying... (1 retries remaining)
[2024-01-15T10:24:56.700Z] Validation failed on attempt 2: Checksum mismatch...
[2024-01-15T10:24:56.750Z] Cleaned up invalid package: E:\...\continue-v2.0.0.tgz
```

---

### Scenario 5: No Write Permission

**What happens**:
1. Cache directory is read-only or inaccessible
2. Directory creation fails
3. Returns `{ valid: false, errors: ["Cannot create cache directory: EACCES ..."] }`

**User experience**: Clear error message; user can fix permissions or choose alternate cache location

**Log entries**:
```
[2024-01-15T10:23:45.300Z] Cannot create cache directory: EACCES: permission denied, mkdir 'E:\cache'
```

---

## Logging & Diagnostics

All operations logged to `.cache/npm-packages/.download-log`:

**Log Location**: `{cacheDir}/.download-log`  
**Format**: ISO 8601 timestamp + message  
**Entries**: One per operation (cache check, download start, validation, error, etc.)

**Viewing logs**:
```bash
# PowerShell
Get-Content E:\GitRepos\ContinueVS\.cache\npm-packages\.download-log -Tail 50

# Linux/macOS
tail -50 ~/.cache/continue-vs/npm-packages/.download-log
```

**Example log content**:
```
[2024-01-15T10:23:45.123Z] Checking local cache for version 2.0.0
[2024-01-15T10:23:45.124Z] Cache hit: E:\GitRepos\ContinueVS\.cache\npm-packages\v2.0.0\continue-v2.0.0.tgz
```

**Diagnostic checklist** (if launch fails):
1. Check `.download-log` for error messages
2. Verify cache directory is writable: `ls -la .cache/npm-packages/`
3. Check network: `ping registry.npmjs.org`
4. Verify npm package exists: Visit https://registry.npmjs.org/continue
5. Check Node.js version: `node --version` (need ≥18.0.0)

---

## Testing

### Unit Tests

Test file: `src/versions/v2.0.0/test/cache-download.test.js`

Run tests:
```bash
cd E:\GitRepos\ContinueVS
node src/versions/v2.0.0/test/cache-download.test.js
```

Tests cover:
- ✅ Cache hit (valid local package, no download)
- ✅ Cache miss (download from registry)
- ✅ Network timeout (retry logic)
- ✅ Checksum mismatch (delete and retry)
- ✅ File system errors (no write permission)

### Integration Tests

Step 30 will run end-to-end tests with real npm registry.

---

## Performance Baseline

| Scenario | Time | Notes |
|----------|------|-------|
| **Cache hit** | < 10ms | Validation only; no I/O |
| **Cache miss (fast network)** | ~2–3s | Download + validation |
| **Cache miss (slow network)** | ~8–10s | Download + validation |
| **Retry after timeout** | ~120s | Two 60s timeouts + validation |

---

## See Also

- **Step 8**: `docs/npm-integrity-utility.md` — Validation API
- **Step 12**: `docs/adr/step-12-startup-validation.md` — Uses this module
- **Step 35**: Download & verify official v2.0.0
- **ADR**: `docs/adr/step-11-cache-download.md` — Design decisions

