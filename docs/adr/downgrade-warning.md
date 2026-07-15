# Version Downgrade Warning — Integration Guide

**Location**: `src/VSIXProject1/Services/DowngradeWarningService.cs`  
**Status**: Implemented  
**Depends On**: None  
**Used By**: Version installer

---

## Overview

The **downgrade warning service** detects when a user attempts to switch to an older bridge version and prompts them to confirm. This is critical because:

- Older versions have fewer handlers
- Version switching requires VS restart
- Silent downgrades could break workflows

---

## Architecture

### Components

| Component | Purpose |
|-----------|---------|
| `IVersionComparator` | Interface for version comparison (testability) |
| `VersionComparator` | Semantic version comparison using `System.Version` |
| `DowngradeWarningService` | Detects downgrades and shows warning dialog |
| `DowngradeWarningException` | Thrown when user cancels downgrade |

### Data Flow

```
Installer requests version switch
    ↓
DowngradeWarningService.CheckDowngradeAsync(currentVersion, targetVersion)
    ↓
Compare versions using VersionComparator.IsDowngrade()
    ↓
If downgrade detected:
  Show MessageBox warning
  Return true (proceed) or false (cancel)
    ↓
Installer calls VersionManager.SetActiveVersion() or throws exception
```

---

## Usage in Version Installer

### Basic Integration

```csharp
using ContinueVS.Services;
using ContinueVS.Exceptions;

// In your installer code:
var currentVersion = ContinueVSPackage.VersionManager?.GetActiveVersion();
var targetVersion = "2.0.0"; // Version being installed

// Check for downgrade warning
var downgradeWarningService = ContinueVSPackage.DowngradeWarningService;
if (downgradeWarningService == null)
{
    // Fallback if service not available
    downgradeWarningService = new DowngradeWarningService();
}

bool userConfirmed = await downgradeWarningService.CheckDowngradeAsync(
    currentVersion: currentVersion,
    targetVersion: targetVersion);

if (!userConfirmed)
{
    // User cancelled the downgrade
    throw new DowngradeWarningException(currentVersion, targetVersion);
}

// Proceed with installation
var versionManager = ContinueVSPackage.VersionManager;
if (versionManager != null && versionManager.SetActiveVersion(targetVersion))
{
    // Prompt user to restart VS
    ShowRestartPrompt();
}
else
{
    throw new InvalidOperationException($"Failed to set active version to {targetVersion}");
}
```

### Complete Example with Error Handling

```csharp
public async Task InstallVersionAsync(string version)
{
    try
    {
        var versionManager = ContinueVSPackage.VersionManager;
        var downgradeWarning = ContinueVSPackage.DowngradeWarningService;

        if (versionManager == null || downgradeWarning == null)
            throw new InvalidOperationException("Version services not initialized");

        var currentVersion = versionManager.GetActiveVersion();

        // Download and validate version
        await DownloadNpmPackageAsync(version);
        if (!await ValidateVersionAsync(version))
            throw new InvalidOperationException($"Validation failed for version {version}");

        // Check for downgrade warning
        bool userConfirmed = await downgradeWarning.CheckDowngradeAsync(
            currentVersion: currentVersion,
            targetVersion: version);

        if (!userConfirmed)
        {
            CleanupDownloadedFiles(version);
            throw new DowngradeWarningException(currentVersion, version);
        }

        // Set active version
        if (!versionManager.SetActiveVersion(version))
            throw new InvalidOperationException($"Failed to activate version {version}");

        // Notify user
        ShowRestartPrompt("Continue Bridge updated. Please restart Visual Studio.");
    }
    catch (DowngradeWarningException ex)
    {
        // User cancelled downgrade — log and return gracefully
        Logger.Info($"Version downgrade cancelled: {ex.CurrentVersion} → {ex.TargetVersion}");
        throw;
    }
    catch (Exception ex)
    {
        Logger.Error($"Installation failed: {ex.Message}", ex);
        throw;
    }
}
```

---

## Public API

### DowngradeWarningService

```csharp
namespace ContinueVS.Services
{
    public class DowngradeWarningService
    {
        /// <summary>
        /// Initializes a new instance with optional version comparator.
        /// If null, uses VersionComparator.
        /// </summary>
        public DowngradeWarningService(IVersionComparator versionComparator = null)

        /// <summary>
        /// Checks if targetVersion is older than currentVersion.
        /// If yes, shows a warning dialog and returns user's response.
        /// </summary>
        /// <returns>
        /// True if user confirmed the downgrade or no downgrade detected.
        /// False if user cancelled the downgrade.
        /// </returns>
        public async Task<bool> CheckDowngradeAsync(
            string currentVersion,
            string targetVersion)
    }
}
```

### IVersionComparator

```csharp
namespace ContinueVS.Services
{
    public interface IVersionComparator
    {
        /// <summary>
        /// Compares two version strings.
        /// Returns: 1 if v1 > v2, -1 if v1 < v2, 0 if equal or invalid.
        /// </summary>
        int CompareVersions(string version1, string version2);

        /// <summary>
        /// Determines if targetVersion is older than currentVersion.
        /// </summary>
        bool IsDowngrade(string currentVersion, string targetVersion);
    }
}
```

### VersionComparator

```csharp
namespace ContinueVS.Services
{
    public class VersionComparator : IVersionComparator
    {
        public int CompareVersions(string version1, string version2)
        public bool IsDowngrade(string currentVersion, string targetVersion)
    }
}
```

### DowngradeWarningException

```csharp
namespace ContinueVS.Exceptions
{
    public class DowngradeWarningException : Exception
    {
        public string CurrentVersion { get; }
        public string TargetVersion { get; }

        public DowngradeWarningException(
            string currentVersion,
            string targetVersion,
            string message = null)
    }
}
```

---

## Semantic Versioning

The service uses `System.Version` for comparison:

### Version Parsing

- **Input**: `"2.1.0"`, `"2.0.0"`, `"2.1.0-beta"`
- **Pre-release handling**: Strip suffix before parsing (`"2.1.0-beta"` → `"2.1.0"`)
- **Comparison**: `new Version("2.1.0") > new Version("2.0.0")` → `true` (is downgrade)
- **Invalid versions**: Treated as equal (no downgrade warning)

### Examples

| Current | Target | Downgrade? | Reason |
|---------|--------|-----------|--------|
| 2.0.0 | 2.1.0 | ❌ No | Upgrade |
| 2.1.0 | 2.0.0 | ✅ Yes | Downgrade |
| 2.0.0 | 2.0.0 | ❌ No | Same version |
| 2.1.0-beta | 2.0.0 | ✅ Yes | 2.1.0 > 2.0.0 |
| 2.0.0-alpha | invalid | ❌ No | Invalid format |

---

## User Experience

### Downgrade Flow

```
User clicks "Install v2.0.0" while on v2.1.0
    ↓
Installer: await downgradeWarning.CheckDowngradeAsync("2.1.0", "2.0.0")
    ↓
VersionComparator detects: 2.1.0 > 2.0.0 (downgrade)
    ↓
MessageBox appears:
┌────────────────────────────────────────────────────┐
│ Continue Bridge — Downgrade Warning                │
├────────────────────────────────────────────────────┤
│ Downgrade Notice                                   │
│                                                    │
│ You are about to downgrade from Continue Bridge   │
│ v2.1.0 to v2.0.0.                                 │
│                                                    │
│ Downgraded versions have fewer handlers and       │
│ features. A restart of Visual Studio will be      │
│ required.                                         │
│                                                    │
│ Continue?                                         │
│                                                    │
│              [No]              [Yes]              │
└────────────────────────────────────────────────────┘
    ↓
User selects [No] → Return false → Throw DowngradeWarningException
     OR
User selects [Yes] → Return true → Proceed with SetActiveVersion()
```

### Upgrade Flow

```
User clicks "Install v2.2.0" while on v2.1.0
    ↓
Installer: await downgradeWarning.CheckDowngradeAsync("2.1.0", "2.2.0")
    ↓
VersionComparator detects: 2.1.0 < 2.2.0 (upgrade)
    ↓
No warning shown; return true immediately
    ↓
Proceed with SetActiveVersion("2.2.0") and restart prompt
```

---

## Testing

### Run All Tests

```powershell
# Navigate to solution root
cd E:\GitRepos\ContinueVS\

# Run all version comparison tests
dotnet test src/VSIXProject1.Tests/Services/VersionComparatorTests.cs

# Run all downgrade warning tests
dotnet test src/VSIXProject1.Tests/Services/DowngradeWarningServiceTests.cs

# Run both together
dotnet test src/VSIXProject1.Tests/Services/VersionComparatorTests.cs src/VSIXProject1.Tests/Services/DowngradeWarningServiceTests.cs
```

### Test Coverage

| Test Class | Tests | Scenarios |
|-----------|-------|-----------|
| `VersionComparatorTests` | 14 | Standard versions, pre-release, null, empty, invalid format |
| `DowngradeWarningServiceTests` | 8 | Upgrade, downgrade, null versions, default comparator |

---

## Known Limitations & Future Enhancements

### Current Limitations

- Dialog is modal and blocks on main thread (by design for safety)
- No logging of downgrade attempts (can add in telemetry)
- Feature comparison link not included

### Future Enhancements

- Link to detailed feature comparison in warning message
- Auto-downgrade on persistent crashes across multiple sessions
- Track downgrade attempts and user confirmations in telemetry
- Visualize downgrade patterns in diagnostics dashboard

---

## Troubleshooting

### Dialog Never Appears (Downgrade Not Detected)

**Symptom**: User downgrades version without seeing warning

**Cause**: Versions may be equal or invalid format

**Solution**:
1. Verify `VersionManager.GetActiveVersion()` returns valid semantic version
2. Check version directories exist in `src/versions/vX.Y.Z/manifest.json`
3. Test with `VersionComparator` directly: `new VersionComparator().IsDowngrade("2.1.0", "2.0.0")`

### Dialog Shows But User Confirmation Ignored

**Symptom**: Clicking "No" still switches versions

**Cause**: Installer not checking return value of `CheckDowngradeAsync()`

**Solution**:
```csharp
// WRONG
await downgradeWarning.CheckDowngradeAsync(current, target);
versionManager.SetActiveVersion(target);

// CORRECT
bool userConfirmed = await downgradeWarning.CheckDowngradeAsync(current, target);
if (!userConfirmed)
    throw new DowngradeWarningException(current, target);

versionManager.SetActiveVersion(target);
```

### Exception Handling Issues

**Symptom**: `DowngradeWarningException` not caught properly

**Solution**: Ensure catch block is ordered correctly (derived before base):
```csharp
try
{
    await InstallVersionAsync("2.0.0");
}
catch (DowngradeWarningException ex)
{
    // Handle downgrade cancellation
    Log($"User cancelled downgrade: {ex.CurrentVersion} → {ex.TargetVersion}");
}
catch (Exception ex)
{
    // Handle other errors
    Log($"Installation error: {ex.Message}");
}
```

---

## Related Documents

- [`BRIDGE-VERSION-SELECTION.md`](../BRIDGE-VERSION-SELECTION.md) — Version management overview
- [Version Installer](../adr/version-installer.md) — Installation workflow (creates usage)
- [BridgeConfiguration](../adr/bridge-configuration.md) — Configuration usage
- [Crash Recovery](../adr/crash-recovery.md) — Uses downgrade on crash
- [Feature Parity Matrix](../adr/feature-parity-matrix.md) — Link to feature comparison

---

**Created**: 2024-01-15  
**Phase**: Foundation & npm Setup  
**Status**: ✅ Complete  
**Tests**: 22 test cases (14 VersionComparator + 8 DowngradeWarningService)  
