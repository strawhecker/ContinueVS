# ContinueVS Bridge Testing Guide

## Overview

The ContinueVS bridge uses a dual-stack testing approach:

- **C# Tests** (xUnit): Test VSIX extension, IPC configuration, transport integration
- **Node.js Tests** (Mocha): Test core-server, stdio protocol, message handling

Both suites share a common test infrastructure designed for async operations, mocking, and timeout handling.

---

## C# Test Suite (xUnit)

### Location
```
src/VSIXProject1.Tests/
├── Infrastructure/
│   ├── AsyncTestHelper.cs        ← Retry/polling/assertion helpers
│   ├── ProcessMockBuilder.cs      ← Process mocking fluent builder
│   ├── TestOutputHelper.cs        ← Structured diagnostic logging
│   ├── TestFixtureBase.cs         ← Base class for all tests
│   ├── TestConstants.cs           ← Centralized test constants
│   ├── MockFactory.cs             ← Mock creation factory
│   └── TestDataBuilder.cs         ← Test data fluent builders
├── Fixtures/
│   ├── TempDirectoryFixture.cs    ← Temp directory management
│   ├── ProcessCleanupFixture.cs   ← Process cleanup
│   └── SharedFixtureCollection.cs ← xUnit collection definitions
└── [Feature Tests]/
    ├── *Tests.cs                  ← Feature-specific test classes
```

### Running C# Tests

#### From Command Line
```bash
# Run all tests
dotnet test

# Run tests in a specific project
dotnet test src/VSIXProject1.Tests/VSIXProject1.Tests.csproj

# Run specific test class
dotnet test --filter "ClassName=TransportTests"

# Run with verbose output
dotnet test --verbosity detailed

# Run with coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

#### From Visual Studio
1. Open **Test Explorer** (Test → Windows → Test Explorer)
2. Build solution to discover tests
3. Right-click test(s) → **Run Selected Tests**
4. View results with **Test Details** pane

### C# Test Infrastructure

#### AsyncTestHelper
Provides utilities for testing async operations:

```csharp
using ContinueVS.Tests.Infrastructure;

public class MyTests : TestFixtureBase
{
    [Fact]
    public async Task ShouldRetryOnFailure()
    {
        // Retry with exponential backoff
        await AsyncTestHelper.RetryAsync(
            async () => await _transport.StartAsync(cts.Token),
            maxAttempts: 3,
            delayMs: 100
        );
    }

    [Fact]
    public async Task ShouldPollForCondition()
    {
        // Poll for condition with timeout
        await AsyncTestHelper.WaitForAsync(
            () => _transport.IsRunning,
            timeoutMs: 5000
        );
    }

    [Fact]
    public async Task ShouldAssertCompletion()
    {
        // Assert task completes within timeout
        var task = _transport.HealthCheckAsync();
        await AsyncTestHelper.AssertCompletesAsync(task, TestConstants.DefaultTimeoutMs);
    }
}
```

#### ProcessMockBuilder
Creates fluent mocks of `Process` objects:

```csharp
using ContinueVS.Tests.Infrastructure;

var mockProcess = new ProcessMockBuilder()
    .WithFileName("node")
    .WithArguments("core-server.js")
    .WithExitCode(0)
    .WithStdoutLine("Server started")
    .WithStderrLine("Debug: Initializing...")
    .Build()
    .Object;

// Or use predefined factory methods
var runningServer = ProcessMockBuilder.CreateRunningServerMock().Object;
var failedStartup = ProcessMockBuilder.CreateFailedStartupMock().Object;
```

#### TestOutputHelper
Provides structured diagnostic logging:

```csharp
using ContinueVS.Tests.Infrastructure;

public class MyTests
{
    private readonly TestOutputHelper _output;

    public MyTests(ITestOutputHelper testOutput)
    {
        _output = new TestOutputHelper(testOutput);
    }

    [Fact]
    public async Task MyTest()
    {
        _output.LogInfo("Test started");
        _output.LogDebug("STATE", "isConnected=true");
        _output.LogPerformance("operation-x", 123);

        try
        {
            await _transport.SendAsync(msg);
        }
        catch (Exception ex)
        {
            _output.LogError("TRANSPORT", "Failed to send", ex);
            throw;
        }
    }
}
```

### Test Configuration Constants

All test constants are defined in `TestConstants.cs`:

```csharp
TestConstants.DefaultTimeoutMs       // 5 seconds (general operations)
TestConstants.StandardRpcTimeoutMs   // 100 ms (stdio operations)
TestConstants.ShortTimeoutMs         // 10 ms (rapid tests)
TestConstants.LongTimeoutMs          // 30 seconds (npm downloads, integration tests)
TestConstants.DefaultTestVersion     // "2.0.0"
```

---

## Node.js Test Suite (Mocha)

### Location
```
src/versions/v2.0.0/
├── test/
│   ├── test-helper.mjs              ← Async helpers, retry, polling
│   ├── mocks/
│   │   └── bridge-mocks.mjs         ← Mock factories and stubs
│   └── [feature tests]/
│       ├── core-server.test.mjs      ← Core server tests
│       ├── transport.test.mjs        ← Stdio transport tests
│       └── *.test.mjs                ← Feature tests
├── .mocharc.json                    ← Mocha configuration
└── package.json                     ← Includes "test" script
```

### Running Node.js Tests

#### From Command Line
```bash
# Run all tests
npm test

# Run with debug output
DEBUG=* npm test

# Run specific test file
npx mocha test/core-server.test.mjs --timeout 5000

# Run tests matching pattern
npx mocha test/**/*.test.mjs --grep "StdioTransport"

# Run with coverage (install nyc first)
npm install --save-dev nyc
nyc npm test
```

#### Mocha Configuration (`.mocharc.json`)
- **timeout**: 5000ms (default timeout for all tests)
- **reporter**: spec (human-readable output)
- **spec**: test/**/*.test.mjs (test file pattern)
- **exit**: true (process exits after tests complete)

### Node.js Test Infrastructure

#### test-helper.mjs
Provides async utilities for Node.js tests:

```javascript
import { 
  retryAsync, 
  waitFor, 
  waitForAsync,
  assertCompletes,
  assertRejects,
  assertEventFired,
  delay,
} from './test-helper.mjs';

// Retry with exponential backoff
await retryAsync(
  async () => {
    await server.start();
  },
  { maxAttempts: 3, delayMs: 100 }
);

// Poll for condition
await waitFor(
  () => server.isRunning,
  { timeoutMs: 5000, pollIntervalMs: 50 }
);

// Wait for event
const data = await assertEventFired(server, 'connection', { timeoutMs: 1000 });

// Assert promise rejects with expected error
await assertRejects(promise, TypeError, { timeoutMs: 5000 });

// Delay between operations
await delay(100);
```

#### bridge-mocks.mjs
Provides mock factories for bridge components:

```javascript
import {
  mockStdioTransport,
  mockContinueProcess,
  mockBridgeServer,
  createJsonRpcRequest,
  createJsonRpcResponse,
  isValidJsonRpc,
} from './mocks/bridge-mocks.mjs';

// Mock stdio transport
const transport = mockStdioTransport({ 
  lines: ['Server started'] 
});
await transport.send({ jsonrpc: '2.0', method: 'ping' });

// Mock Continue process
const process = mockContinueProcess({ pid: 5678, exitCode: 0 });
process.on('exit', (code) => console.log(`Process exited: ${code}`));
process.simulateExit(0);

// Mock bridge server
const server = mockBridgeServer();
server.registerHandler('test', async (params) => ({ ok: true }));
const response = await server.handleRequest(
  createJsonRpcRequest(1, 'test', {})
);

// Validate JSON-RPC messages
if (isValidJsonRpc(message)) {
  console.log('Valid JSON-RPC message');
}
```

---

## Writing Tests

### Test Structure

#### C# Test Pattern (Arrange-Act-Assert)
```csharp
[Fact]
public async Task WhenTransportStartsThenShouldBeRunning()
{
    // Arrange
    var config = new BridgeConfigurationBuilder()
        .WithVersion("2.0.0")
        .WithDebugMode(false)
        .Build();

    var mockTransport = new ProcessMockBuilder()
        .WithFileName("node")
        .WithExitCode(0)
        .Build();

    var bridge = new BridgeLifecycleManager(config, mockTransport.Object);

    // Act
    await bridge.StartAsync(CancellationToken.None);

    // Assert
    Assert.True(bridge.IsRunning);
    _output.LogInfo("Bridge started successfully");
}
```

#### Node.js Test Pattern (Mocha/BDD)
```javascript
import assert from 'assert';
import { mockBridgeServer, createJsonRpcRequest } from './mocks/bridge-mocks.mjs';
import { waitFor, assertEventFired } from './test-helper.mjs';

describe('BridgeServer', () => {
  describe('lifecycle', () => {
    it('should emit started event when started', async () => {
      // Arrange
      const server = mockBridgeServer();

      // Act
      const startPromise = server.start();

      // Assert
      await assertEventFired(server, 'started', { timeoutMs: 1000 });
      assert(server.isRunning);
    });

    it('should handle requests after starting', async () => {
      // Arrange
      const server = mockBridgeServer();
      server.registerHandler('test', async (params) => {
        return { result: 'ok', params };
      });

      // Act
      await server.start();
      const response = await server.handleRequest(
        createJsonRpcRequest(1, 'test', { input: 'data' })
      );

      // Assert
      assert.strictEqual(response.result.result, 'ok');
    });
  });
});
```

### Shared Test Patterns

1. **Async Operations with Timeout**
   ```csharp
   // C#
   await AsyncTestHelper.AssertCompletesAsync(operation(), timeoutMs: 1000);
   ```
   ```javascript
   // Node.js
   await assertCompletes(operation(), { timeoutMs: 1000 });
   ```

2. **Polling for Conditions**
   ```csharp
   // C#
   await AsyncTestHelper.WaitForAsync(
     () => server.IsRunning,
     timeoutMs: 5000
   );
   ```
   ```javascript
   // Node.js
   await waitFor(() => server.isRunning, { timeoutMs: 5000 });
   ```

3. **Retry with Backoff**
   ```csharp
   // C#
   await AsyncTestHelper.RetryAsync(
     async () => await server.ConnectAsync(),
     maxAttempts: 3,
     delayMs: 100
   );
   ```
   ```javascript
   // Node.js
   await retryAsync(
     async () => { await server.connect(); },
     { maxAttempts: 3, delayMs: 100 }
   );
   ```

---

## Coverage & CI/CD

### Code Coverage

#### C# Coverage
```bash
# Install dotnet-coverage tool
dotnet tool install -g dotnet-coverage

# Generate coverage report
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

#### Node.js Coverage
```bash
# Install nyc
npm install --save-dev nyc

# Generate coverage report
nyc npm test
```

### Continuous Integration

Tests run on:
- **Local builds**: Pre-commit validation
- **Pull requests**: Full test suite + coverage gates
- **Release**: Extended integration tests (Steps 28–30+)

Expected exit codes:
- `0`: All tests passed
- `1`: Test failures
- `2`: Build/tool errors

---

## Debugging Tests

### C# Debugging

1. **Set breakpoint** in test method
2. **Right-click test** in Test Explorer → **Debug Selected Tests**
3. Use **Debug → Windows → Immediate** for REPL evaluation
4. Check **Output → Tests** pane for diagnostic logs

### Node.js Debugging

1. **Add debug output**:
   ```javascript
   console.log('Debug:', variable);
   ```

2. **Use Node inspector**:
   ```bash
   node --inspect-brk ./node_modules/.bin/mocha test/core-server.test.mjs
   ```
   Then open `chrome://inspect` in Chrome DevTools

3. **Verbose Mocha output**:
   ```bash
   npx mocha test/**/*.test.mjs --reporter json > test-results.json
   ```

### Common Issues

| Issue | Solution |
|-------|----------|
| **Test timeout** | Increase `timeoutMs` in helper calls or `.mocharc.json` |
| **Process not exiting** | Ensure all streams are closed; use `process.exit(code)` |
| **Mock not called** | Verify mock setup matches actual method signatures |
| **Race conditions** | Use `waitFor` / `waitForAsync` instead of `Task.Delay` |
| **Port already in use** | Use unique ports per test; clean up listeners |

---

## Contributing Tests

When adding new tests:

1. **Choose the right framework**: C# tests for VSIX, Node.js tests for core-server
2. **Follow naming convention**: `WhenConditionThenExpectedOutcome` (C#) or `should do X when Y` (Node.js)
3. **Use existing mocks & helpers**: Don't duplicate mock creation
4. **Add diagnostics**: Use `_output.LogInfo()` or `console.log()` for debugging
5. **Cover edge cases**: Success, failure, timeout, cancellation paths
6. **Isolate tests**: No file I/O, no global state, no test interdependencies

---

## See Also

- [Step 27: Create Unit Test Infrastructure](../docs/session-context.md#step-27)
- [Step 28: Create StdioTransport Lifecycle Tests](../docs/session-context.md#step-28)
- [Step 29: Create StdioTransport Messaging Tests](../docs/session-context.md#step-29)
- [xUnit Documentation](https://xunit.net/)
- [Mocha Documentation](https://mochajs.org/)
