# Project-Info Handler Guide (Step 84)

**Status**: Implemented  
**Message Type**: `bridge:getProjectInfo`  
**Tier**: Core (stable)  
**Timeout Policy**: Slow (5000ms)  
**Factory Pattern**: Yes  
**Related Steps**: 48, 52, 71, 73

---

## Overview

The **project-info handler** exposes project and solution metadata from Visual Studio's DTE object model via IPC. Unlike streaming handlers (e.g., terminal, debug-session), this is a **stateless query handler**: the IDE sends a single request and receives a complete response with current project structure, build status, and workspace info.

### Use Cases

- **Project Explorer**: Display solution structure in Continue sidebar
- **Build Status Monitoring**: Show errors, warnings, last build time
- **Multi-Project Navigation**: List all projects and target frameworks
- **Git Integration**: Retrieve current branch name from workspace
- **Workspace Context**: Establish IDE project scope for language model analysis

---

## Message Contract

### Request

Minimal factory message (no payload required):

```json
{
  "messageId": "req-12345",
  "type": "bridge:getProjectInfo"
}
```

**Fields**:
- `messageId` (string, required): Unique request identifier for correlation
- `type` (string): Must be `bridge:getProjectInfo`

### Response (Success)

```json
{
  "messageId": "req-12345",
  "type": "bridge:getProjectInfo",
  "success": true,
  "data": {
    "solution": {
      "name": "ContinueVS",
      "path": "C:\\projects\\ContinueVS\\ContinueVS.sln",
      "projectCount": 3
    },
    "projects": [
      {
        "name": "VSIXProject1",
        "path": "C:\\projects\\ContinueVS\\src\\VSIXProject1\\VSIXProject1.csproj",
        "type": "C# Project",
        "targetFramework": "net472",
        "buildStatus": "Ready",
        "projectKind": "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
      },
      {
        "name": "VSIXProject1.Tests",
        "path": "C:\\projects\\ContinueVS\\src\\VSIXProject1.Tests\\VSIXProject1.Tests.csproj",
        "type": "C# Project",
        "targetFramework": "net10",
        "buildStatus": "Ready",
        "projectKind": "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
      }
    ],
    "workspace": {
      "rootPath": "C:\\projects\\ContinueVS",
      "gitBranch": "main"
    },
    "buildStatus": {
      "lastBuild": "2024-07-15T10:30:00Z",
      "isBuilding": false,
      "errors": 0,
      "warnings": 5
    }
  },
  "timestamp": "2024-07-15T10:35:00Z"
}
```

**Data Structure**:

| Field | Type | Notes |
|-------|------|-------|
| `solution.name` | string | Solution name (extracted from filename) |
| `solution.path` | string | Full path to .sln file |
| `solution.projectCount` | number | Number of projects in solution |
| `projects` | array | Array of project objects (see below) |
| `workspace.rootPath` | string | Root directory containing solution |
| `workspace.gitBranch` | string \| null | Current git branch or null if git unavailable |
| `buildStatus.lastBuild` | string \| null | ISO 8601 timestamp of last build or null |
| `buildStatus.isBuilding` | boolean | True if build is currently in progress |
| `buildStatus.errors` | number | Count of build errors |
| `buildStatus.warnings` | number | Count of build warnings |

**Project Object**:

```json
{
  "name": "ProjectName",
  "path": "C:\\path\\to\\project\\Project.csproj",
  "type": "C# Project",
  "targetFramework": "net8.0",
  "buildStatus": "Ready",
  "projectKind": "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
}
```

### Response (Error)

```json
{
  "messageId": "req-12345",
  "type": "bridge:getProjectInfo",
  "success": false,
  "error": {
    "code": -32603,
    "message": "Failed to collect project info from IDE: DTE.Solution is null",
    "errorCode": "NO_SOLUTION",
    "details": {
      "errorName": "ProjectInfoError",
      "originalError": null
    }
  },
  "timestamp": "2024-07-15T10:35:00Z"
}
```

**Error Codes** (JSON-RPC):

| Code | Meaning | Example |
|------|---------|---------|
| -32600 | Invalid Request | Missing `messageId`, invalid message format |
| -32603 | Internal Error | Collector not initialized, collection failed |

**Error Types**:

- `INVALID_MESSAGE`: Message structure is malformed
- `MISSING_MESSAGE_ID`: messageId is missing or not a string
- `COLLECTOR_NOT_INITIALIZED`: ProjectInfoCollector not available
- `PROJECT_INFO_ERROR`: DTE-level error (e.g., no solution loaded)
- `COLLECTION_ERROR`: Failed during project enumeration

---

## Error Handling

### Common Scenarios

**1. No Solution Loaded**
```
ErrorCode: NO_SOLUTION
Message: "DTE.Solution is null; no solution is loaded"
Action: Prompt user to open a solution file
```

**2. Collector Not Initialized**
```
ErrorCode: COLLECTOR_NOT_INITIALIZED
Message: "ProjectInfoCollector not initialized; C# bridge adapter may not be running"
Action: Check if Visual Studio IDE state is ready; restart bridge if needed
```

**3. Project Enumeration Failed**
```
ErrorCode: COLLECTION_ERROR
Message: "Failed to collect project info from IDE: ..."
Action: Log details; retry after a delay; may indicate IDE issue
```

### Null-Safety

All DTE property accesses are wrapped with null checks:
- Null `DTE.Solution` → throws `ProjectInfoError("NO_SOLUTION")`
- Null project properties → skipped in enumeration (logged as warning)
- Null `targetFramework` → returns "Unknown" (does not fail)
- Null git branch → returns `null` in response (graceful)

---

## Integration

### C# Collector (ProjectInfoCollector.cs)

**Location**: `src/VSIXProject1/Services/ProjectInfoCollector.cs`

**Public API**:

```csharp
public ProjectInfoCollector(DTE dte, IBridgeLogger? logger = null)
public ProjectInfo GetProjectInfo()
```

**Returns**: `ProjectInfo` object with nested structures:
- `ProjectInfo.Solution` (SolutionInfo)
- `ProjectInfo.Projects` (List<ProjectItemInfo>)
- `ProjectInfo.Workspace` (WorkspaceInfo)
- `ProjectInfo.BuildStatus` (BuildStatus)

**Example Usage** (C#):

```csharp
var collector = new ProjectInfoCollector(dte, logger);
try
{
    var projectInfo = collector.GetProjectInfo();
    // Use projectInfo.Solution.Name, projectInfo.Projects, etc.
}
catch (ProjectInfoError ex)
{
    logger.LogError($"Failed: {ex.Message}", new { errorCode = ex.ErrorCode });
}
```

### Node.js Handler (project-info-handler.mjs)

**Location**: `src/versions/v2.0.0/lib/project-info-handler.mjs`

**Factory Function**:

```javascript
import { createProjectInfoHandler } from './project-info-handler.mjs';

const handler = createProjectInfoHandler({
  logger: myLogger,           // optional
  metrics: myMetrics,         // optional
  collectorInstance: collector // required for production
});

// Call handler
const response = await handler(message, context);
```

**Options**:
- `logger`: ILogger-like object with `debug(msg, data)`, `info()`, `warning()`, `error()`
- `metrics`: Object with `recordEvent(eventName, eventData)` for telemetry
- `collectorInstance`: ProjectInfoCollector or mock; if null, returns error

**Integration in Bridge Dispatcher** (Step 71):

```javascript
// handler-registry.mjs
{
  messageType: 'bridge:getProjectInfo',
  handler: createProjectInfoHandler,
  isFactory: true,
  timeoutPolicy: 'slow',
  stabilityTier: 'core',
  description: 'Get project/solution metadata from IDE',
  relatedSteps: [84, 71],
  dependencies: [71],
}
```

---

## API Reference

### Handlers

#### Handler: bridge:getProjectInfo

**Request**:
- Type: Factory message
- Timeout: 5000ms
- No payload required

**Response**:
- Solution, projects, workspace, build status as JSON
- Normalized fields (all strings, numbers, booleans, or null)

**Example (JavaScript Client)**:

```javascript
const message = {
  messageId: 'req-' + Date.now(),
  type: 'bridge:getProjectInfo'
};

const response = await bridge.send(message, { timeoutMs: 5000 });

if (response.success) {
  const { solution, projects, workspace, buildStatus } = response.data;
  console.log(`Solution: ${solution.name} (${projects.length} projects)`);
  console.log(`Branch: ${workspace.gitBranch}`);
  console.log(`Build: ${buildStatus.isBuilding ? 'In progress' : 'Ready'}`);
} else {
  console.error(`Error: ${response.error.message}`);
}
```

### Error Codes

See **Error Handling** section above.

---

## Testing

### C# Unit Tests (xUnit)

**File**: `src/VSIXProject1.Tests/Services/ProjectInfoCollectorTests.cs`

**Test Suites** (18 tests total):

1. **Initialization & Null-Safety** (4 tests)
   - Constructor with null DTE → ArgumentNullException
   - Constructor with valid DTE → succeeds
   - GetProjectInfo with null Solution → ProjectInfoError

2. **Solution Info Queries** (4 tests)
   - GetProjectInfo with valid solution → returns correct name/path
   - GetProjectInfo with zero projects → projectCount=0
   - GetProjectInfo with multiple projects → correct count
   - GetProjectInfo with null FullName → handles gracefully

3. **Project Enumeration** (4 tests)
   - Enumeration of multiple projects
   - C# project type detection
   - Skips projects without name

4. **Build Status Collection** (3 tests)
   - Build status included in response
   - Solution building → isBuilding=true
   - Null SolutionBuild → defaults to false

5. **Error Propagation** (3 tests)
   - Project enumeration failure → CollectionError
   - Solution null → ProjectInfoError
   - ErrorCode present in exceptions

**Run Tests**:

```bash
dotnet test src/VSIXProject1.Tests/Services/ProjectInfoCollectorTests.cs
```

### Node.js Unit Tests (Mocha)

**File**: `src/versions/v2.0.0/tests/project-info-handler.test.mjs`

**Test Suites** (24 tests total):

1. **Initialization & Factory** (4 tests)
   - Handler creation with valid options
   - Logger and metrics injection
   - Error on invalid options

2. **Message Handling** (5 tests)
   - Valid request → success response
   - Null message → error response
   - Missing messageId → error
   - Invalid messageId type → error
   - Missing context → handled gracefully

3. **Collector Integration** (4 tests)
   - Collector getProjectInfo called
   - Context passed correctly
   - Collector errors wrapped as CollectionError
   - Uninitialized collector → error response

4. **Response Structure** (4 tests)
   - All required fields present
   - Solution info normalized
   - Projects array included
   - Workspace and buildStatus normalized

5. **Error Handling** (4 tests)
   - Collector throws → error response
   - Error details included
   - ProjectInfoError mapped correctly
   - MessageId preserved in error

6. **Logging & Metrics** (3 tests)
   - Success metrics recorded
   - Error metrics recorded
   - Graceful degradation without logger/metrics

**Run Tests**:

```bash
npm test -- tests/project-info-handler.test.mjs
```

### Mock Fixtures

**File**: `src/versions/v2.0.0/tests/mocks/project-info-collector-mock.mjs`

**Fixtures**:

- `getValidProjectInfoResponse()` — 3-project solution
- `getSingleProjectResponse()` — 1 project
- `getEmptySolutionResponse()` — 0 projects
- `getBuildStatusWithErrorsResponse()` — With errors/warnings
- `getBuildingStatusResponse()` — isBuilding=true
- `getNoGitBranchResponse()` — gitBranch=null
- `getMixedProjectTypesResponse()` — C#, VB, Web projects
- `getNetFrameworkResponse()` — .NET Framework 4.7.2
- `getMinimalResponse()` — Edge case, empty fields

**Factories**:

- `createMockCollector(overrides)` — Returns collector-like object
- `createFailingCollector(error)` — Throws specified error
- `createCustomCollector(response)` — Returns custom response

---

## Performance Considerations

### Query Performance

- **DTE Queries**: Synchronous (blocking main IDE thread)
- **Target Latency**: <500ms for typical solution
- **Timeout**: 5000ms (slow tier) to accommodate large solutions
- **Caching**: No built-in caching; each request queries fresh state

### Optimization Tips

1. **Large Solutions** (50+ projects): Consider implementing caching at handler level
2. **Git Branch Query**: Spawns subprocess; can be slow on network drives
3. **Target Framework Detection**: Tries multiple property keys; gracefully falls back

---

## Troubleshooting

### Issue: "DTE.Solution is null"

**Cause**: No solution is open in Visual Studio  
**Solution**: Open a .sln file before requesting project info

### Issue: "CollectionError: Failed to collect project info"

**Cause**: Unexpected error during DTE enumeration  
**Solution**: Check Visual Studio IDE logs; restart bridge; verify project files are valid

### Issue: "gitBranch is null even though repo is git-initialized"

**Cause**: `git` command not in PATH or repo is detached HEAD  
**Solution**: Ensure git is installed and in PATH; check git state manually

### Issue: "targetFramework is Unknown"

**Cause**: Project properties not accessible or missing  
**Solution**: Verify project file syntax; check for custom properties; try reloading solution

---

## Related Steps

| Step | Title | Relationship |
|------|-------|--------------|
| 48 | Create editor context collector | Similar pattern: DTE query → normalized response |
| 52 | Create document provider | Similar pattern: stateless query handler |
| 71 | Register all handlers with dispatcher | Registers bridge:getProjectInfo |
| 73 | Create request/response validation | Validates this handler's responses |
| 82 | Create terminal handler | Different pattern: streaming messages |

---

## References

- [Project Info Collector (C#)](src/VSIXProject1/Services/ProjectInfoCollector.cs)
- [Project Info Handler (Node.js)](src/versions/v2.0.0/lib/project-info-handler.mjs)
- [Handler Registry](src/versions/v2.0.0/lib/handler-registry.mjs)
- [C# Tests](src/VSIXProject1.Tests/Services/ProjectInfoCollectorTests.cs)
- [Node.js Tests](src/versions/v2.0.0/tests/project-info-handler.test.mjs)
- [Mock Fixtures](src/versions/v2.0.0/tests/mocks/project-info-collector-mock.mjs)
- [Step 84 Plan](docs/session-context.md#step-84-create-project-info-handler)

