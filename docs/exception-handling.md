# Bridge Exception Handling Architecture

**Step 22 Implementation**  
**Status**: Complete  
**Tests**: 29/29 passing  
**Build**: Warning-free

---

## Overview

The bridge exception hierarchy provides structured, strongly-typed error handling for the Continue npm bridge. All exceptions inherit from `BridgeException`, which enforces consistent error reporting via `ErrorCode`, `Context` dictionary, and inner exception chaining.

This foundation enables:
- **Error recovery middleware** (Step 74) to route errors based on type and error code
- **Telemetry collection** (Step 26) to aggregate errors for monitoring
- **Lifecycle management** (Step 45) to respond to process and transport failures
- **Debug diagnostics** with rich context information

---

## Exception Hierarchy

```
Exception (System)
└── BridgeException (abstract, internal, ContinueVS.Exceptions)
    ├── ProcessException
    ├── TransportException
    ├── ConfigurationException
    ├── ProtocolException
    ├── TimeoutException
    └── HealthCheckException
```

---

## Core Exception: BridgeException

**File**: `src/VSIXProject1/Exceptions/BridgeException.cs`

**Purpose**: Abstract base class for all bridge-specific errors.

**Properties**:
- `ErrorCode` (string) — Machine-readable error identifier (e.g., "PROC_START_FAILED")
- `Context` (IReadOnlyDictionary<string, string>) — Debugging metadata (e.g., processId, exitCode)
- `Message` (inherited) — Human-readable error message
- `InnerException` (inherited) — Root cause exception

**Constructors**:
```csharp
protected BridgeException(string message, string errorCode)
protected BridgeException(string message, string errorCode, Exception? innerException)
protected BridgeException(string message, string errorCode, Dictionary<string, string>? context)
protected BridgeException(string message, string errorCode, Exception? innerException, Dictionary<string, string>? context)
```

**Example**:
```csharp
// ProcessException inherits these constructors
var ex = new ProcessException(
    message: "npm process failed to start",
    errorCode: ProcessException.ErrorCodes.ProcessStartFailed,
    innerException: new IOException("Cannot find npm.exe"),
    context: new Dictionary<string, string> { { "npmPath", "C:\\npm\\npm.exe" } }
);

// Produces:
// Message: "npm process failed to start"
// ErrorCode: "PROC_START_FAILED"
// Context: { "npmPath": "C:\npm\npm.exe" }
// InnerException: IOException
```

---

## ProcessException

**File**: `src/VSIXProject1/Exceptions/ProcessException.cs`  
**Namespace**: `ContinueVS.Exceptions`  
**Visibility**: Internal sealed

**Purpose**: Subprocess lifecycle failures (start, exit, shutdown, stream init).

**Used By**:
- ProcessManager (Step 19) — process spawn, kill
- StdioTransport (Step 20) — lifecycle events
- Bridge lifecycle manager (Step 45)

**Error Codes** (`ProcessException.ErrorCodes.*`):

| Code | Meaning |
|------|---------|
| `ProcessStartFailed` | npm process failed to start (executable not found, spawn error) |
| `ProcessExitedUnexpectedly` | Process crashed or was terminated externally |
| `ProcessStopTimeout` | Graceful shutdown exceeded timeout |
| `ProcessKillTimeout` | Forced kill exceeded timeout |
| `StreamInitializationFailed` | stdin/stdout setup failed |
| `ProcessNotRunning` | Operation requires running process but process is stopped |
| `ProcessAlreadyRunning` | Operation requires stopped process but process is running |

**Example**:
```csharp
try
{
    _process = Process.Start(_startInfo);
}
catch (Exception ex)
{
    throw new ProcessException(
        $"Failed to start npm process at '{_config.NpmExecutablePath}'",
        ProcessException.ErrorCodes.ProcessStartFailed,
        ex,
        new Dictionary<string, string>
        {
            { "npmPath", _config.NpmExecutablePath },
            { "workingDirectory", _config.WorkingDirectory }
        }
    );
}
```

---

## TransportException

**File**: `src/VSIXProject1/Exceptions/TransportException.cs`  
**Namespace**: `ContinueVS.Exceptions`  
**Visibility**: Internal sealed

**Purpose**: Message I/O failures (send, receive, serialization, stream state).

**Used By**:
- StdioTransport (Step 20) — send/receive message
- MessageBufferer — buffering, deserialization
- Error recovery middleware (Step 74)

**Error Codes** (`TransportException.ErrorCodes.*`):

| Code | Meaning |
|------|---------|
| `SendFailed` | Message send failed (serialization or stream write) |
| `ReceiveFailed` | Message receive failed (deserialization or stream read) |
| `SerializationFailed` | JSON encoding error |
| `DeserializationFailed` | JSON parsing or validation error |
| `StreamClosed` | Stream closed unexpectedly (EOF, disposed) |
| `InvalidStreamState` | Stream in invalid state (disposed, not initialized) |
| `BufferingFailed` | Queue overflow or buffer corruption |
| `NotConnected` | Transport not running |

**Example**:
```csharp
try
{
    var json = JsonConvert.SerializeObject(message);
    await writer.WriteAsync(json);
}
catch (JsonSerializationException ex)
{
    throw new TransportException(
        $"Failed to serialize message of type '{message.MessageType}'",
        TransportException.ErrorCodes.SerializationFailed,
        ex,
        new Dictionary<string, string>
        {
            { "messageType", message.MessageType },
            { "messageId", message.MessageId }
        }
    );
}
```

---

## ConfigurationException

**File**: `src/VSIXProject1/Exceptions/ConfigurationException.cs`  
**Namespace**: `ContinueVS.Exceptions`  
**Visibility**: Internal sealed

**Purpose**: Invalid or incomplete configuration (paths, version, timeouts).

**Used By**:
- BridgeConfiguration (Step 18) — validation
- Bridge setup flows

**Error Codes** (`ConfigurationException.ErrorCodes.*`):

| Code | Meaning |
|------|---------|
| `InvalidVersionFormat` | Bridge version format invalid (not semver) |
| `InvalidNpmPath` | npm executable path missing or invalid |
| `InvalidWorkingDirectory` | Working directory missing or inaccessible |
| `PackageNotFound` | Continue npm package not found at expected path |
| `InvalidTimeout` | Timeout value invalid (negative, zero, exceeds max) |
| `MissingParameter` | Required configuration parameter null or empty |
| `IncompatibleVersion` | npm package version incompatible with extension |
| `IntegrityCheckFailed` | npm package checksum mismatch |

**Example**:
```csharp
if (!Version.TryParse(version, out _))
{
    throw new ConfigurationException(
        $"Version '{version}' does not match semver format",
        ConfigurationException.ErrorCodes.InvalidVersionFormat,
        context: new Dictionary<string, string>
        {
            { "version", version },
            { "expectedFormat", "MAJOR.MINOR.PATCH" }
        }
    );
}
```

---

## ProtocolException

**File**: `src/VSIXProject1/Exceptions/ProtocolException.cs`  
**Namespace**: `ContinueVS.Exceptions`  
**Visibility**: Internal sealed

**Purpose**: JSON-RPC message validation failures.

**Used By**:
- JsonRpcProtocol (Step 21) — message validation
- MessageBufferer — deserialization
- Handlers (Steps 50+) — request/response validation
- Protocol adapter (Step 63)

**Error Codes** (`ProtocolException.ErrorCodes.*`):

| Code | Meaning |
|------|---------|
| `MalformedMessage` | JSON-RPC message malformed (invalid structure) |
| `MissingRequiredField` | Required field missing or null (messageType, messageId, data) |
| `InvalidFieldValue` | Field has invalid type or value |
| `MessageIdMismatch` | Response messageId doesn't match request |
| `UnknownMessageType` | Message type not recognized |
| `HandlerNotFound` | No handler registered for this message type |
| `IncompatibleVersion` | Protocol version incompatible |
| `InvalidRequest` | Request payload validation failed |
| `InvalidResponse` | Response payload validation failed |

**Example**:
```csharp
if (string.IsNullOrEmpty(message.MessageType))
{
    throw new ProtocolException(
        "Message missing required 'messageType' field",
        ProtocolException.ErrorCodes.MissingRequiredField,
        context: new Dictionary<string, string>
        {
            { "messageId", message.MessageId ?? "null" },
            { "missingField", "messageType" }
        }
    );
}
```

---

## TimeoutException

**File**: `src/VSIXProject1/Exceptions/TimeoutException.cs`  
**Namespace**: `ContinueVS.Exceptions`  
**Visibility**: Internal sealed

**Purpose**: Operation timeout (RPC, health check, process lifecycle).

**Used By**:
- Timeout manager (Step 64) — RPC call timeout enforcement
- Health check service (Step 24) — health probe timeout
- ProcessManager — process start/stop timeout
- StdioTransport — send/receive timeout

**Properties**:
- `ElapsedMs` — actual elapsed time (milliseconds)
- `TimeoutMs` — timeout limit (milliseconds)

**Error Codes** (`TimeoutException.ErrorCodes.*`):

| Code | Meaning |
|------|---------|
| `RpcCallTimeout` | RPC handler did not respond in time |
| `HealthCheckTimeout` | Health probe did not complete in time |
| `ProcessStartTimeout` | npm server initialization exceeded timeout |
| `ProcessShutdownTimeout` | Graceful shutdown exceeded timeout |
| `ProcessKillTimeout` | Forced kill exceeded timeout |
| `SendTimeout` | Message send exceeded timeout |
| `ReceiveTimeout` | Message receive exceeded timeout |

**Example**:
```csharp
var stopwatch = Stopwatch.StartNew();
try
{
    await _process.WaitForExitAsync(timeout: 3000);
}
catch (OperationCanceledException ex)
{
    stopwatch.Stop();
    throw new TimeoutException(
        $"Process shutdown exceeded {_config.ShutdownTimeoutMs}ms timeout",
        TimeoutException.ErrorCodes.ProcessShutdownTimeout,
        elapsedMs: stopwatch.ElapsedMilliseconds,
        timeoutMs: _config.ShutdownTimeoutMs,
        innerException: ex,
        context: new Dictionary<string, string>
        {
            { "processId", _process.Id.ToString() },
            { "component", "ProcessManager" }
        }
    );
}
```

---

## HealthCheckException

**File**: `src/VSIXProject1/Exceptions/HealthCheckException.cs`  
**Namespace**: `ContinueVS.Exceptions`  
**Visibility**: Internal sealed

**Purpose**: Process health monitoring failures.

**Used By**:
- Health check service (Step 24) — health probe
- Bridge lifecycle manager (Step 45) — process degradation
- Circuit breaker (Step 108) — consecutive failure tracking

**Properties**:
- `FailureCount` — number of consecutive health check failures (defaults to 1 if ≤ 0)

**Error Codes** (`HealthCheckException.ErrorCodes.*`):

| Code | Meaning |
|------|---------|
| `InvalidProbeResponse` | Health probe response malformed or unexpected |
| `ProcessNotResponding` | Process not responding to health check |
| `ProbeFailed` | Health probe failed with transport error |
| `ProcessDegraded` | Process claimed healthy but showing degradation |
| `CircuitBreakerTriggered` | Multiple consecutive health check failures |
| `StateInconsistent` | Process state inconsistent (crashed but marked running) |
| `CheckDisabled` | Health check disabled but required for operation |

**Example**:
```csharp
if (consecutiveFailures >= _circuitBreakerThreshold)
{
    throw new HealthCheckException(
        $"Health check circuit breaker triggered after {consecutiveFailures} failures",
        HealthCheckException.ErrorCodes.CircuitBreakerTriggered,
        failureCount: consecutiveFailures,
        context: new Dictionary<string, string>
        {
            { "threshold", _circuitBreakerThreshold.ToString() },
            { "lastProbeTime", _lastProbeTime?.ToString("O") ?? "never" }
        }
    );
}
```

---

## Exception Chaining Pattern

Exceptions can be chained to preserve root cause information:

```csharp
// Low-level failure (transport)
TransportException transportEx = new TransportException(
    "Send failed",
    TransportException.ErrorCodes.SendFailed,
    new IOException("Pipe broken")
);

// Mid-level failure (process)
ProcessException processEx = new ProcessException(
    "Process communication failure",
    ProcessException.ErrorCodes.StreamInitializationFailed,
    transportEx  // ← inner exception
);

// High-level failure (configuration)
ConfigurationException configEx = new ConfigurationException(
    "Failed to initialize bridge",
    ConfigurationException.ErrorCodes.InvalidNpmPath,
    processEx  // ← inner exception
);

// All context preserved:
// configEx.InnerException → processEx
// processEx.InnerException → transportEx
// transportEx.InnerException → IOException("Pipe broken")
```

---

## Integration Points

### Step 24: Health Check Service
Uses `HealthCheckException` to report probe failures. FailureCount drives circuit breaker logic.

### Step 26: Telemetry Collector
Captures ErrorCode from all exceptions for structured event logging. Example:
```csharp
telemetry.TrackException(bridgeException, new Dictionary<string, string>
{
    { "errorCode", bridgeException.ErrorCode },
    { "exceptionType", bridgeException.GetType().Name }
});
// Enables aggregation by error code in monitoring dashboard
```

### Step 45: Bridge Lifecycle Manager
Catches exceptions by type to decide recovery strategy:
```csharp
try { /* ... */ }
catch (ProcessException ex) => RestartProcess();
catch (TransportException ex) => ReconnectTransport();
catch (ConfigurationException ex) => ShutdownWithError();
```

### Step 64: Timeout Manager
Throws TimeoutException with ElapsedMs for RPC call timeout enforcement.

### Step 74: Error Recovery Middleware
Routes exceptions based on ErrorCode to enable targeted recovery:
```csharp
var errorCode = bridgeException.ErrorCode;
if (errorCode == ProcessException.ErrorCodes.ProcessNotRunning)
    await RecoverWithProcessRestart();
else if (errorCode == TransportException.ErrorCodes.StreamClosed)
    await RecoverWithTransportReinitialize();
```

---

## Testing

**Test File**: `src/VSIXProject1.Tests/Exceptions/BridgeExceptionTests.cs`  
**Framework**: xUnit  
**Coverage**: 29 tests, all passing

**Test Categories**:
1. **Base class validation** (null checks, abstract enforcement)
2. **Property verification** (ErrorCode, Context)
3. **Constructor variants** (all overloads)
4. **Inner exception chaining**
5. **Error code constants** (all defined and non-empty)
6. **Inheritance hierarchy** (all inherit from BridgeException)
7. **Complex scenarios** (nested exception chains, context propagation)

Run tests:
```powershell
dotnet test VSIXProject1.slnx --filter "ContinueVS.Tests.Exceptions"
# Result: 29 passed
```

---

## Best Practices

1. **Always provide ErrorCode**: Use constants from exception's `ErrorCodes` nested class.
2. **Include Context**: Populate Context dictionary with debugging info (paths, IDs, timeouts).
3. **Preserve InnerException**: Chain inner exceptions to preserve root cause.
4. **Null-check inputs**: All constructor parameters are validated (throw ArgumentNullException if null).
5. **Choose appropriate exception type**: Select exception matching the failure domain.
6. **Log with telemetry**: Capture ErrorCode for structured monitoring.

---

## Summary

The bridge exception hierarchy provides:
- ✅ Strongly-typed errors by domain (process, transport, config, protocol, timeout, health)
- ✅ Structured error codes for routing and aggregation
- ✅ Rich debugging context via dictionary
- ✅ Full exception chaining for root-cause analysis
- ✅ 29 comprehensive unit tests
- ✅ Integration points for telemetry (Step 26), health checks (Step 24), lifecycle (Step 45), recovery (Step 74)

**Status**: Ready for use in Steps 24–45 and 74+.
