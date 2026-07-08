# Bridge Version Selection UI

**Location**: Tools → Options → Continue → Bridge  
**Status**: Implemented (Step 9)  
**Last Updated**: 2024-01-15

---

## Overview

The **Continue Bridge Version Selection UI** provides a read-only display of the currently active npm-based Continue bridge version. This document explains:

- **Why it's read-only**: Safety-first design to prevent accidental version switching that could break the extension
- **How it works**: Version discovery, storage, and validation
- **Integration points**: Interaction with the installer (Step 35) and downgrade warnings (Step 10)
- **For developers**: How to extend version management

---

## Architecture

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| **VersionSelectorService** | `Services/VersionSelectorService.cs` | Discovers available versions from `src/versions/vX.Y.Z/` directories |
| **VersionManager** | `Services/VersionManager.cs` | Loads/persists active version to VS registry; validates availability |
| **ContinueOptionsPage** | `Settings/ContinueOptionsPage.cs` | Displays active version in read-only UI |
| **ContinueVSPackage** | `ContinueVSPackage.cs` | Initializes VersionManager during extension startup |

### Data Flow

```
Startup (ContinueVSPackage)
    ↓
Initialize VersionSelector + VersionManager
    ↓
Load Active Version from Registry
    ↓
Validate Against Available Versions
    ↓
Display in Options Page (Read-Only)
    ↓
Make Available to BridgeConfiguration (Step 18)
```

---

## Directory Structure

Version directories must follow this layout:

```
src/versions/
├── v2.0.0/
│   ├── manifest.json          ← Required; contains version metadata
│   ├── package.json           ← npm package definition
│   ├── core-server.js         ← Entry point
│   ├── handlers/              ← Handler modules
│   ├── lib/                   ← Utilities & transports
│   └── tests/                 ← Test suite
├── v2.1.0/
│   └── manifest.json
└── v3.0.0/
    └── manifest.json
```

### manifest.json Schema

```json
{
  "version": "2.0.0",
  "continueVersion": "0.4.x",
  "status": "stable",
  "releaseDate": "2024-01-15",
  "requirements": {
    "node": ">=18.0.0",
    "npm": ">=9.0.0"
  },
  "handlers": [
    "getEditorState",
    "onEditorStateChange",
    "search"
  ],
  "transportModes": ["stdio"]
}
```

**Key fields**:
- `version`: Semantic version string (required)
- `continueVersion`: Compatible Continue CLI version
- `status`: "stable", "beta", or "deprecated"
- `releaseDate`: ISO date format
- `handlers`: Available handlers in this version

---

## Version Selection Workflow

### 1. First-Time Launch

On extension startup:

1. **VersionSelectorService** scans `src/versions/` for directories with valid `manifest.json` files
2. **VersionManager** queries the registry for a previously saved version
3. If registry is empty or version is invalid, defaults to **2.0.0**
4. Version is cached in memory and displayed in UI

### 2. Version Update (via Installer)

When a new version is installed (Step 35):

1. New version directory is created: `src/versions/vX.Y.Z/`
2. Manifest is placed in new directory
3. **VersionSelectorService** discovers the new version on next call
4. User (or system) can request version switch via **VersionManager.SetActiveVersion()**
5. Version change is persisted to registry: `HKEY_CURRENT_USER\Software\ContinueVS\ActiveBridgeVersion`

### 3. Extension Restart Loads New Version

1. **BridgeConfiguration** (Step 18) queries **VersionManager.GetActiveVersion()**
2. Correct handler set for that version is loaded from `src/versions/vX.Y.Z/handlers/`
3. Stdio transport connects to the correct npm server

---

## Why Read-Only?

### Safety Reasons

Each version is **self-contained** with:
- Different handler implementations
- Different npm package dependencies
- Different transport protocols
- Different manifest structures

**Uncontrolled switching risks**:
- ❌ Loading wrong handler code for active version
- ❌ Breaking RPC protocol mismatch
- ❌ Extension crash or hang

### Controlled Switching

Version switching is only safe when:
- ✅ New version is fully installed & validated (Step 35)
- ✅ Downgrade warnings are shown (Step 10)
- ✅ Extension is restarted (handlers reloaded)
- ✅ User explicitly requests version change

---

## Integration with Related Steps

### Step 10: Version Downgrade Warning

**Status**: ✅ Implemented  
**Location**: `src/VSIXProject1/Services/DowngradeWarningService.cs`  
**Documentation**: [`step-10-downgrade-warning.md`](adr/step-10-downgrade-warning.md)

**Triggering condition**: Installer (Step 35) requests version switch to older version

**Architecture**:
- `IVersionComparator` — Interface for version comparison (testability)
- `VersionComparator` — Semantic version comparison using `System.Version`
- `DowngradeWarningService` — Detects downgrades + shows warning dialog
- `DowngradeWarningException` — Thrown if user cancels downgrade

**Implementation in installer (Step 35)**:
```csharp
// In Step 35 (npm installer):
using ContinueVS.Services;
using ContinueVS.Exceptions;

var currentVersion = ContinueVSPackage.VersionManager?.GetActiveVersion();
var targetVersion = "2.0.0"; // Version being installed

// Check for downgrade warning
var downgradeWarning = ContinueVSPackage.DowngradeWarningService;
bool userConfirmed = await downgradeWarning.CheckDowngradeAsync(
    currentVersion: currentVersion,
    targetVersion: targetVersion);

if (!userConfirmed)
{
    // User cancelled the downgrade
    throw new DowngradeWarningException(currentVersion, targetVersion);
}

// Proceed with version switch
ContinueVSPackage.VersionManager?.SetActiveVersion(targetVersion);
ShowRestartPrompt();
```

**Semantic version comparison**:
- Uses `System.Version` for comparison
- Strips pre-release suffixes: `"2.1.0-beta"` → `"2.1.0"`
- Handles invalid formats gracefully (treats as equal, no warning)
- Examples: `2.1.0 > 2.0.0` (downgrade), `2.0.0 < 2.1.0` (upgrade)

**User experience**:
1. User clicks "Install v2.0.0" while on v2.1.0
2. Installer calls `DowngradeWarningService.CheckDowngradeAsync("2.1.0", "2.0.0")`
3. Warning dialog: "Downgrade from v2.1.0 to v2.0.0? Fewer features. Restart required. Continue?"
4. User clicks [Yes] → Proceeds with version switch + restart
5. User clicks [No] → Throws `DowngradeWarningException`, version unchanged

**Testing**:
- 14 `VersionComparatorTests` — Standard/pre-release versions, edge cases
- 8 `DowngradeWarningServiceTests` — Upgrade/downgrade detection, null handling
- Run: `dotnet test src/VSIXProject1.Tests/Services/VersionComparatorTests.cs`
- Run: `dotnet test src/VSIXProject1.Tests/Services/DowngradeWarningServiceTests.cs`

**Singleton access** (initialized in `ContinueVSPackage.InitializeAsync`):
```csharp
var service = ContinueVSPackage.DowngradeWarningService;
bool userConfirmed = await service.CheckDowngradeAsync("2.1.0", "2.0.0");
```

### Step 35: Installer & Downloader

**Responsibilities**:
- Download Continue npm package for target version
- Extract to `src/versions/vX.Y.Z/`
- Validate checksums and manifest.json
- Request VersionManager to set new active version

**Integration code** (Step 35):
```csharp
public async Task InstallVersionAsync(string version)
{
    // Download & extract version
    await DownloadNpmPackageAsync(version);

    // Validate
    if (!await ValidateVersionAsync(version))
        throw new InvalidOperationException("Validation failed");

    // Request version switch
    var success = VersionManager.SetActiveVersion(version);
    if (!success)
        throw new InvalidOperationException("Version not available");

    // Notify user to restart
    ShowRestartPrompt();
}
```

---

## Public API

### VersionSelectorService

```csharp
public virtual List<string> GetAvailableVersions()
  → Returns ["2.0.0", "2.1.0", "3.0.0"] in descending order

public virtual bool IsVersionAvailable(string version)
  → Checks if version dir + manifest.json exist

public JObject GetVersionManifest(string version)
  → Returns parsed manifest.json for validation

public VersionMetadata GetVersionMetadata(string version)
  → Returns version info (status, release date, etc.)
```

### VersionManager

```csharp
public string GetActiveVersion()
  → Returns current active version (from registry or default)

public bool SetActiveVersion(string version)
  → Validates & switches version (if available)

public void ResetToDefault()
  → Resets to 2.0.0

public void ClearCache()
  → Forces reload from registry (for testing)
```

### ContinueVSPackage

```csharp
public static VersionManager? VersionManager { get; }
  → Provides global access to version manager singleton
```

---

## Testing

### Unit Tests

**VersionSelectorServiceTests** (`Services/Tests/VersionSelectorServiceTests.cs`):
- Discovery of valid versions
- Ignoring directories without manifests
- Semantic version sorting (descending)
- Metadata extraction

**VersionManagerTests** (`Services/Tests/VersionManagerTests.cs`):
- Registry persistence round-trip
- Fallback to default version
- Validation of requested versions
- Cache behavior

### Running Tests

```powershell
dotnet test src/VSIXProject1/VSIXProject1.csproj
```

---

## Migration Path: Current Translator → Bridge

For developers migrating from the translator architecture:

1. **Old**: Each version was a translator output in the same assembly
2. **New**: Each version is a separate npm package in `src/versions/vX.Y.Z/`

**Impact on development**:
- No need to rebuild translator for version updates
- Version switching is safe & non-breaking
- Handler code is isolated per version

---

## Troubleshooting

### Version shows "2.0.0" but should be different

**Cause**: Registry entry missing or corrupted

**Solution**:
1. Delete registry key: `HKEY_CURRENT_USER\Software\ContinueVS\ActiveBridgeVersion`
2. Restart VS
3. Extension re-initializes to default 2.0.0

### "Bridge version X.Y.Z is not available" error

**Cause**: Version directory missing or manifest.json invalid

**Solution**:
1. Check `src/versions/vX.Y.Z/` exists
2. Validate `manifest.json` has `version` field
3. Run installer for that version (Step 35)

### Extension crashes when switching versions

**Cause**: Restart required to load new handler set

**Solution**:
1. Close all continue panels
2. Restart VS completely
3. Handler set for new version loads on startup

---

## Future Enhancements

- **Step 120**: Upgrade-path documentation (which versions can upgrade to which)
- **Performance**: Cache manifest metadata to avoid repeated file I/O
- **Telemetry**: Track version adoption, downgrade reasons
- **Rollback**: Auto-downgrade on crash (Step 103)

---

**Related Documents**:
- [`VERSIONS.md`](../VERSIONS.md) — Version registry & manifests
- [Step 18: BridgeConfiguration Implementation](../adr/bridge-configuration.md)
- [Step 35: npm Installer & Downloader](../adr/npm-installer.md)
- [Step 10: Version Downgrade Warning](./version-downgrade-warning.md)
