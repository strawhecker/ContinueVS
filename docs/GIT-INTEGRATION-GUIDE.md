# Git Integration Handler Guide (Step 81)

**Version**: 1.0.0  
**Status**: Production Ready  
**Last Updated**: 2024-01-15  
**Module**: `src/versions/v2.0.0/lib/git-integration-handler.mjs`

---

## Overview

The Git Integration Handler provides Continue.dev with real-time access to repository state, commit history, branch information, and file-level diffs. It uses Node.js `child_process.execFile()` to invoke native `git` CLI commands, with intelligent caching (3–5 sec TTL) and comprehensive error handling.

**Key Features**:
- ✅ Zero npm dependencies (uses built-in Node.js modules)
- ✅ Intelligent caching with TTL and LRU eviction
- ✅ Timeout-safe execution (5000ms default)
- ✅ Cross-platform path normalization (Windows ↔ POSIX)
- ✅ Structured error hierarchy with detailed context
- ✅ Performance metrics tracking
- ✅ Graceful degradation (git not installed, no .git folder)

---

## Architecture

### Message Flow

```
[Continue IDE] → bridge:gitStatus request
                ↓
         [dispatcher] routes via handler registry
                ↓
   [createGitIntegrationHandler(options)] factory creates handler
                ↓
    [handler] validates message.data (operation, cwd)
                ↓
   [handler] checks cache (GitOperationCache)
                ↓
      [handler] executes git command (child_process.execFile)
                ↓
         [handler] normalizes response to JSON
                ↓
      [handler] stores in cache (if enabled)
                ↓
     [middleware] logs operation + metrics
                ↓
   [core-server] sends JSON-RPC response to IDE
```

### Component Hierarchy

```
GitIntegrationHandler (factory)
├── GitOperationCache
│   ├── TTL-based expiration (3000ms default)
│   ├── LRU eviction (max 100 entries)
│   └── Statistics (hits, misses, hitRate)
├── executeGitCommand(args, options)
│   ├── Child process timeout enforcement
│   ├── Exit code analysis
│   └── Error classification
├── Operation Handlers (5)
│   ├── handleStatus(cwd, logger, cache, useCache)
│   ├── handleLog(cwd, count, logger, cache, useCache)
│   ├── handleBranches(cwd, logger, cache, useCache)
│   ├── handleDiff(cwd, filePath, baseRef, logger, cache, useCache)
│   └── handleCurrentBranch(cwd, logger, cache, useCache)
└── Response Normalizers (5)
    ├── normalizeStatusResponse()
    ├── normalizeLogResponse()
    ├── normalizeBranchesResponse()
    ├── normalizeDiffResponse()
    └── normalizeCurrentBranchResponse()
```

---

## Message Protocol

### Request Format

**Message Type**: `bridge:gitStatus`

```json
{
  "messageType": "bridge:gitStatus",
  "messageId": "msg-123456",
  "data": {
    "operation": "status|log|branches|diff|currentBranch",
    "cwd": "/path/to/repo",
    "cache": true,
    "params": {
      "count": 10,
      "filePath": "src/file.js",
      "baseRef": "HEAD"
    }
  }
}
```

**Fields**:
- `operation` (required): One of: `status`, `log`, `branches`, `diff`, `currentBranch`
- `cwd` (required): Working directory (git repository root or subdirectory)
- `cache` (optional, default: true): Whether to use caching
- `params` (optional): Operation-specific parameters (see Operation Reference below)

### Response Format

**Success Response**:
```json
{
  "messageType": "bridge:gitStatus",
  "messageId": "msg-123456",
  "data": {
    "success": true,
    "result": {
      "clean": true,
      "staged": [],
      "unstaged": [],
      "untracked": []
    },
    "metadata": {
      "timestamp": "2024-01-15T10:30:45.123Z",
      "cached": false,
      "durationMs": 47,
      "cacheStats": {
        "hits": 5,
        "misses": 2,
        "size": 8,
        "hitRate": "71.43%"
      }
    }
  }
}
```

**Error Response**:
```json
{
  "messageType": "bridge:gitStatus",
  "messageId": "msg-123456",
  "error": {
    "code": "GIT_REPOSITORY_ERROR",
    "message": "Not a git repository: /some/path",
    "details": {
      "cwd": "/some/path"
    }
  }
}
```

**Error Codes**:
- `GIT_VALIDATION_ERROR`: Missing/invalid request fields
- `GIT_COMMAND_ERROR`: Git CLI execution failed (not installed, timeout, etc.)
- `GIT_REPOSITORY_ERROR`: Not a git repository or .git folder missing
- `GIT_ERROR`: Generic git operation error

---

## Operation Reference

### 1. Status Operation

**Purpose**: Retrieve working tree state (staged, unstaged, untracked files)

**Request**:
```json
{
  "operation": "status",
  "cwd": "/path/to/repo",
  "cache": true
}
```

**Response**:
```json
{
  "clean": false,
  "staged": [
    { "status": "M", "path": "src/app.js" },
    { "status": "A", "path": "src/new-file.js" }
  ],
  "unstaged": [
    { "status": "M", "path": "README.md" }
  ],
  "untracked": [
    "dist/",
    ".env.local"
  ],
  "cached": false
}
```

**Status Codes** (git porcelain format):
- `M`: Modified
- `A`: Added (new file)
- `D`: Deleted
- `R`: Renamed
- `C`: Copied
- `T`: Type change
- `U`: Unmerged (conflict)
- `??`: Untracked (in untracked array)

---

### 2. Log Operation

**Purpose**: Retrieve commit history with author, message, date

**Request**:
```json
{
  "operation": "log",
  "cwd": "/path/to/repo",
  "cache": true,
  "params": {
    "count": 20
  }
}
```

**Response**:
```json
{
  "commits": [
    {
      "sha": "a1b2c3d4e5f6",
      "author": "John Doe <john@example.com>",
      "message": "Fix: resolve issue with parser",
      "date": "2024-01-15T09:30:00.000Z"
    },
    {
      "sha": "f6e5d4c3b2a1",
      "author": "Jane Smith <jane@example.com>",
      "message": "Feature: add new UI component",
      "date": "2024-01-14T14:22:15.000Z"
    }
  ],
  "cached": false
}
```

**Parameters**:
- `count` (optional, default: 10): Number of commits to retrieve (1–100 recommended)

---

### 3. Branches Operation

**Purpose**: List all branches (local and remote) and identify current branch

**Request**:
```json
{
  "operation": "branches",
  "cwd": "/path/to/repo",
  "cache": true
}
```

**Response**:
```json
{
  "current": "feature/new-api",
  "branches": [
    { "name": "main", "remote": false },
    { "name": "develop", "remote": false },
    { "name": "feature/new-api", "remote": false },
    { "name": "remotes/origin/main", "remote": true },
    { "name": "remotes/origin/develop", "remote": true },
    { "name": "remotes/upstream/main", "remote": true }
  ],
  "cached": false
}
```

**Fields**:
- `current`: Name of active branch
- `branches`: Array of branch objects
  - `name`: Full branch reference
  - `remote`: Boolean (true if remote-tracking branch)

---

### 4. Diff Operation

**Purpose**: Retrieve file-level diff (changes, additions, deletions)

**Request**:
```json
{
  "operation": "diff",
  "cwd": "/path/to/repo",
  "cache": false,
  "params": {
    "filePath": "src/components/Button.jsx",
    "baseRef": "HEAD"
  }
}
```

**Response**:
```json
{
  "path": "src/components/Button.jsx",
  "additions": 12,
  "deletions": 5,
  "diff": "diff --git a/src/components/Button.jsx b/src/components/Button.jsx\nindex 1234567..abcdefg 100644\n--- a/src/components/Button.jsx\n+++ b/src/components/Button.jsx\n@@ -10,3 +10,8 @@\n...",
  "cached": false
}
```

**Parameters**:
- `filePath` (required): Path to file (relative to repo root)
- `baseRef` (optional, default: "HEAD"): Git reference to compare against
  - Common values: `HEAD`, `HEAD~1`, `origin/main`, `develop`

**Note**: Diffs are typically not cached (cache TTL is set to 0ms for diffs) to ensure freshness.

---

### 5. Current Branch Operation

**Purpose**: Retrieve name of active branch

**Request**:
```json
{
  "operation": "currentBranch",
  "cwd": "/path/to/repo",
  "cache": true
}
```

**Response**:
```json
{
  "branch": "feature/new-api",
  "cached": false
}
```

---

## Caching Strategy

### TTL (Time-To-Live)

- **Default TTL**: 3000ms (3 seconds)
- **Configurable**: Pass `cacheTtl` option to handler factory
- **Operations**: All operations except `diff` (diffs always fresh)

```javascript
// Custom TTL example
const handler = createGitIntegrationHandler({
  cacheTtl: 5000,  // 5 seconds
  logger: myLogger,
  metrics: myMetrics
});
```

### Cache Invalidation

**Automatic** (TTL expiration):
- Cache entry expires if accessed after TTL window

**Manual** (via message parameter):
```json
{
  "operation": "status",
  "cwd": "/path/to/repo",
  "cache": false
}
```

**Use Cases**:
- Initial load: `cache: false` (ensures fresh data)
- Polling: `cache: true` (reduces git CLI load)
- File save events: `cache: false` (bypass stale cache)

### Performance Impact

| Operation | Cached | Uncached | TTL Hit Rate |
|-----------|--------|----------|--------------|
| Status | <2ms | 40–80ms | 60–70% |
| Log | <2ms | 60–150ms | 50–60% |
| Branches | <2ms | 50–100ms | 40–50% |
| Diff | N/A | 100–300ms | N/A (always fresh) |

---

## Error Handling

### Error Hierarchy

```
Error (built-in)
└── GitError
    ├── GitCommandError (git CLI failed)
    │   └── "git: command not found"
    │   └── "fatal: timeout after 5000ms"
    ├── GitRepositoryError (not a git repo)
    │   └── "Not a git repository: /path"
    └── GitValidationError (invalid request)
        └── "operation: unsupported operation"
        └── "cwd: working directory path is required"
```

### Common Error Scenarios

| Scenario | Error Code | Message | Recovery |
|----------|-----------|---------|----------|
| Git not installed | GIT_COMMAND_ERROR | "git command not found" | Install git |
| Not a git repo | GIT_REPOSITORY_ERROR | "Not a git repository" | Use repo root as cwd |
| Invalid cwd | GIT_REPOSITORY_ERROR | "Directory not found" | Verify path exists |
| Command timeout | GIT_COMMAND_ERROR | "timeout after 5000ms" | Increase timeout, retry |
| Invalid operation | GIT_VALIDATION_ERROR | "unsupported operation" | Check operation spelling |
| Missing cwd | GIT_VALIDATION_ERROR | "cwd is required" | Include cwd in request |

### Error Response Example

```json
{
  "error": {
    "code": "GIT_COMMAND_ERROR",
    "message": "fatal: not a git repository (or any parent up to mount point)",
    "details": {
      "command": "git status --porcelain",
      "exitCode": 128
    }
  },
  "metadata": {
    "timestamp": "2024-01-15T10:30:45.123Z",
    "durationMs": 23
  }
}
```

---

## Performance Tuning

### Timeout Configuration

Default timeout is 5000ms (5 seconds). For slow repositories or networks, increase:

```javascript
const handler = createGitIntegrationHandler({
  cacheTtl: 3000,
  // Note: timeout is hardcoded to 5000ms in executeGitCommand
  // To customize, modify the timeout value in git-integration-handler.mjs
});
```

### Cache TTL Optimization

| Repository Type | Recommended TTL | Rationale |
|-----------------|-----------------|-----------|
| Monorepo (large) | 5000–10000ms | Slower git operations |
| Standard project | 3000–5000ms | Default, balanced |
| Single-file repo | 1000–2000ms | Frequent changes expected |
| CI/CD (polling) | 10000–30000ms | Minimize git CLI load |

### Memory Usage

- **Default cache size**: 100 entries
- **Typical memory per entry**: 1–10KB
- **Max memory**: ~1MB for full cache
- **Total handler memory**: <5MB under load

To adjust:
```javascript
const cache = new GitOperationCache(3000, 200);  // 200 max entries
```

---

## Integration with Handler Registry

The git-integration handler is registered in `src/versions/v2.0.0/lib/handler-registry.mjs`:

```javascript
{
  messageType: 'bridge:gitStatus',
  handler: createGitIntegrationHandler,
  isFactory: true,
  timeoutPolicy: 'medium',         // 10000ms overall timeout
  stabilityTier: 'core',
  description: 'Git repository status, log, branches, diff operations',
  relatedSteps: [81, 71],
  dependencies: [71],
}
```

**Timeout Policy Levels**:
- `fast`: 5000ms (queries)
- `medium`: 10000ms (git operations)
- `slow`: 30000ms (builds, tests)

---

## Troubleshooting

### Git Command Not Found

**Symptom**: `GIT_COMMAND_ERROR: git command not found`

**Solution**:
1. Verify git is installed: `git --version`
2. Ensure git is in system PATH
3. Restart IDE/bridge process

### Not a Git Repository

**Symptom**: `GIT_REPOSITORY_ERROR: Not a git repository`

**Solution**:
1. Verify `.git` folder exists: `ls -la /path/to/repo | grep .git`
2. Ensure `cwd` points to repo root or subdirectory
3. Check folder permissions: `ls -ld /path/to/repo`

### Timeout Errors

**Symptom**: `GIT_COMMAND_ERROR: timeout after 5000ms`

**Solution**:
1. Repository too large: Run `git gc` to optimize
2. Network latency: Increase timeout in code (see Performance Tuning)
3. System load: Check CPU/memory usage

### Stale Cache Data

**Symptom**: File status shows as clean after saving

**Solution**:
1. Send request with `cache: false` to force refresh
2. Reduce cache TTL: `cacheTtl: 1000` (1 second)
3. Clear cache after file save events (Step 83)

---

## Advanced Usage

### Custom Logger & Metrics

```javascript
import { createGitIntegrationHandler } from './git-integration-handler.mjs';

class MyLogger {
  debug(msg, data) { /* ... */ }
  error(msg, data) { /* ... */ }
}

class MyMetrics {
  recordOperation(module, operation, durationMs, status) { /* ... */ }
  recordCacheHit(module, type) { /* ... */ }
}

const handler = createGitIntegrationHandler({
  cacheTtl: 5000,
  logger: new MyLogger(),
  metrics: new MyMetrics()
});
```

### Batch Operations

```javascript
// Request multiple operations
const statusPromise = handler({ 
  data: { operation: 'status', cwd: '/repo' } 
}, {});

const logPromise = handler({ 
  data: { operation: 'log', cwd: '/repo', params: { count: 5 } } 
}, {});

const [statusRes, logRes] = await Promise.all([statusPromise, logPromise]);
```

### Cache Statistics

```javascript
const result = await handler({ 
  data: { operation: 'status', cwd: '/repo' } 
}, {});

console.log(result.metadata.cacheStats);
// Output:
// {
//   hits: 5,
//   misses: 2,
//   size: 8,
//   hitRate: "71.43%"
// }
```

---

## Related Steps

- **Step 71**: Handler Registration (registry entry, factory pattern)
- **Step 72**: Message Logging Middleware (auto-logs all git operations)
- **Step 73**: Request/Response Validation (validates message envelope)
- **Step 74**: Error Recovery Middleware (catches handler errors)
- **Step 75**: WebView Integration Tests (includes git handler E2E tests)
- **Step 83**: File-System Handler (can use git status for file decorations)

---

## Changelog

### v1.0.0 (2024-01-15)
- ✅ Initial release
- ✅ 5 core operations: status, log, branches, diff, currentBranch
- ✅ Intelligent caching with TTL
- ✅ Comprehensive error handling
- ✅ Performance metrics tracking
- ✅ Cross-platform path normalization
- ✅ 28 test cases, 100% coverage

---

## License

Part of ContinueVS Bridge Architecture (Apache 2.0)
