/**
 * WebView Message Type Definitions — Bridge Communication Protocol
 * 
 * Comprehensive JSDoc type definitions for all WebView ↔ Bridge message types.
 * Defines the contract between:
 *   - C# WebView injection layer (Step 43: WebviewInjector)
 *   - Node.js handler system (Steps 50–61: individual handlers)
 *   - React frontend library (Continue.js IDE plugin)
 *
 * @module src/versions/v2.0.0/types/webview-messages.d.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 43: WebviewInjector (injects bridge protocol client)
 *   - Steps 46–61: Individual handlers (process bridge:* messages)
 *   - Step 62: This module (protocol contract)
 *   - Step 63: BridgeProtocolAdapter (validates/transforms messages)
 *   - Step 64: TimeoutManager (applies RPC timeouts)
 *   - Step 71: HandlerRegistry (routes messages to handlers)
 *   - Steps 75+: Integration tests
 */

/**
 * ============================================================================
 * REQUEST MESSAGE TYPES (WebView → Bridge)
 * ============================================================================
 */

/**
 * Base request envelope from WebView to bridge.
 * All requests follow this structure.
 * 
 * @typedef {Object} WebViewRequest
 * @property {string} messageType - Message type (e.g., "bridge:bootstrap", "bridge:getEditorState")
 * @property {string} messageId - Unique correlation ID (UUID v4) for tracking request/response
 * @property {*} [data] - Optional request payload; schema varies per messageType
 * @property {number} [timestamp] - Client timestamp (ISO 8601 or ms since epoch)
 * @property {Object} [metadata] - Optional metadata (source, user context, feature flags, etc.)
 */

/**
 * Bootstrap request — WebView queries bridge capabilities.
 * Sent immediately after continueVS bridge injection to negotiate features.
 * 
 * Message Type: "bridge:bootstrap"
 * 
 * @typedef {Object} BootstrapRequest
 * @property {string} messageType - Always "bridge:bootstrap"
 * @property {string} messageId - Correlation ID
 * @property {Object} [data]
 * @property {string} [data.clientVersion] - WebView client version (e.g., "2.0.0")
 * @property {string[]} [data.requestedHandlers] - List of handler names to query (optional; if omitted, bridge returns all)
 * @property {Object} [data.clientCapabilities] - WebView capabilities (streaming, subscriptions, etc.)
 * 
 * @example
 * // WebView sends:
 * {
 *   messageType: "bridge:bootstrap",
 *   messageId: "uuid-v4-here",
 *   data: {
 *     clientVersion: "2.0.0",
 *     requestedHandlers: ["bridge:getEditorState", "bridge:onEditorStateChange"],
 *     clientCapabilities: { streaming: true, subscriptions: true }
 *   }
 * }
 */

/**
 * Handler request — WebView calls a specific handler.
 * Used for queries (getEditorState) and mutations (applyEdit).
 * 
 * Message Type: "bridge:handler:<handlerName>" or "bridge:<handlerName>"
 * 
 * @typedef {Object} HandlerRequest
 * @property {string} messageType - Handler identifier (e.g., "bridge:getEditorState", "bridge:applyEdit")
 * @property {string} messageId - Correlation ID for request/response pairing
 * @property {*} data - Handler-specific request payload (see handlers.d.js for schema per handler)
 * @property {number} [timeout] - Optional request timeout in milliseconds (Step 64)
 * 
 * @example
 * // WebView sends getEditorState request:
 * {
 *   messageType: "bridge:getEditorState",
 *   messageId: "uuid-123",
 *   data: { includeContent: true },
 *   timeout: 5000
 * }
 */

/**
 * Subscription request — WebView subscribes to long-lived events.
 * Bridge maintains open channel; sends unsolicited SubscriptionEvent messages.
 * 
 * Message Type: "bridge:subscribe:<eventName>" or "bridge:onXXX"
 * 
 * @typedef {Object} SubscriptionRequest
 * @property {string} messageType - Subscription identifier (e.g., "bridge:onEditorStateChange", "bridge:onDiagnosticsChange")
 * @property {string} messageId - Correlation ID; reused for all events in this subscription
 * @property {Object} [data]
 * @property {string} [data.subscriptionId] - Optional persistent subscription ID (for resumption)
 * @property {number} [data.debounceMs] - Optional debounce interval (e.g., 100ms for state changes)
 * @property {Object} [data.filter] - Optional event filter (handler-specific)
 * 
 * @example
 * // WebView subscribes to editor state changes:
 * {
 *   messageType: "bridge:onEditorStateChange",
 *   messageId: "sub-uuid-456",
 *   data: { debounceMs: 100 }
 * }
 * 
 * // Bridge responds with SubscriptionStarted, then sends SubscriptionEvent messages
 */

/**
 * Configuration request — WebView configures bridge behavior.
 * 
 * Message Type: "bridge:configure"
 * 
 * @typedef {Object} ConfigurationRequest
 * @property {string} messageType - Always "bridge:configure"
 * @property {string} messageId - Correlation ID
 * @property {Object} [data]
 * @property {string} [data.logLevel] - Logger level (debug, info, warn, error)
 * @property {boolean} [data.enableTelemetry] - Enable/disable telemetry collection
 * @property {Object} [data.featureFlags] - Feature flags to enable/disable handlers
 * @property {number} [data.defaultTimeout] - Default RPC timeout in milliseconds
 * 
 * @example
 * // WebView configures bridge:
 * {
 *   messageType: "bridge:configure",
 *   messageId: "uuid-789",
 *   data: {
 *     logLevel: "debug",
 *     enableTelemetry: true,
 *     featureFlags: { terminal: false, codeActions: true }
 *   }
 * }
 */

/**
 * ============================================================================
 * RESPONSE MESSAGE TYPES (Bridge → WebView)
 * ============================================================================
 */

/**
 * Base response envelope from bridge to WebView.
 * All responses follow this structure.
 * 
 * @typedef {Object} WebViewResponse
 * @property {boolean} success - Whether operation succeeded
 * @property {string} messageId - Echoes request messageId for correlation
 * @property {*} [data] - Response payload (present if success=true)
 * @property {Object} [error] - Error details (present if success=false); see BridgeError
 * @property {number} [latency] - Round-trip latency in milliseconds
 * @property {number} [timestamp] - Server response timestamp
 */

/**
 * Success response from handler.
 * 
 * @typedef {Object} SuccessResponse
 * @property {boolean} success - Always true
 * @property {string} messageId - Request correlation ID
 * @property {*} data - Handler response payload (schema varies per handler)
 * @property {number} [latency] - Elapsed time in milliseconds
 * 
 * @example
 * // Bridge responds to getEditorState:
 * {
 *   success: true,
 *   messageId: "uuid-123",
 *   data: {
 *     activeFile: "/path/to/file.cs",
 *     cursorLine: 42,
 *     selectedText: "myVariable"
 *   },
 *   latency: 15
 * }
 */

/**
 * Error response from bridge.
 * Returned when handler throws, timeout occurs, validation fails, etc.
 * 
 * @typedef {Object} ErrorResponse
 * @property {boolean} success - Always false
 * @property {string} messageId - Request correlation ID
 * @property {BridgeError} error - Error details (code, message, context)
 * @property {number} [latency] - Elapsed time in milliseconds
 * 
 * @example
 * // Bridge responds with timeout error:
 * {
 *   success: false,
 *   messageId: "uuid-123",
 *   error: {
 *     code: "TIMEOUT",
 *     message: "Handler did not respond within 5000ms",
 *     context: { handler: "bridge:getEditorState", timeout: 5000 }
 *   },
 *   latency: 5015
 * }
 */

/**
 * Partial response — used for streaming/chunked responses.
 * Handler sends multiple PartialResponse messages, then final SuccessResponse.
 * 
 * @typedef {Object} PartialResponse
 * @property {boolean} success - Always true
 * @property {string} messageId - Request correlation ID (same across all chunks)
 * @property {*} chunk - Incremental response data (e.g., line of output, token of code completion)
 * @property {boolean} [isLast] - true for final chunk; if omitted/false, more chunks expected
 * @property {number} sequenceNumber - Chunk sequence (0, 1, 2, ...)
 * 
 * @example
 * // Bridge streams search results:
 * { success: true, messageId: "uuid-456", chunk: { file: "/src/A.cs", ... }, sequenceNumber: 0 }
 * { success: true, messageId: "uuid-456", chunk: { file: "/src/B.cs", ... }, sequenceNumber: 1 }
 * { success: true, messageId: "uuid-456", chunk: null, isLast: true, sequenceNumber: 2 }
 */

/**
 * Subscription event — unsolicited message from bridge to WebView.
 * Sent only after subscription request (SubscriptionRequest); maintains same messageId.
 * 
 * @typedef {Object} SubscriptionEvent
 * @property {boolean} success - Always true
 * @property {string} messageId - Subscription correlation ID (from original SubscriptionRequest)
 * @property {Object} event - Event payload (schema varies per subscription type)
 * @property {string} event.eventType - Event classification (e.g., "stateChange", "diagnostic", "telemetry")
 * @property {*} event.data - Event-specific data (see Event Types section)
 * @property {number} [timestamp] - Event timestamp (ISO 8601 or ms since epoch)
 * 
 * @example
 * // Bridge sends unsolicited editor state change:
 * {
 *   success: true,
 *   messageId: "sub-uuid-456",
 *   event: {
 *     eventType: "stateChange",
 *     data: {
 *       activeFile: "/path/to/newfile.cs",
 *       cursorLine: 10
 *     }
 *   },
 *   timestamp: 1705316400000
 * }
 */

/**
 * Subscription acknowledgment — bridge confirms subscription.
 * Sent immediately after SubscriptionRequest; has structure similar to SuccessResponse.
 * 
 * @typedef {Object} SubscriptionStarted
 * @property {boolean} success - Always true
 * @property {string} messageId - Subscription correlation ID
 * @property {Object} data
 * @property {string} data.subscriptionId - Server-assigned subscription ID
 * @property {number} data.debounceMs - Debounce interval in effect
 * @property {string} data.status - Always "subscribed"
 * 
 * @example
 * // Bridge acknowledges subscription:
 * {
 *   success: true,
 *   messageId: "sub-uuid-456",
 *   data: {
 *     subscriptionId: "sub-server-789",
 *     debounceMs: 100,
 *     status: "subscribed"
 *   }
 * }
 */

/**
 * ============================================================================
 * ERROR TYPES (Returned in ErrorResponse.error)
 * ============================================================================
 */

/**
 * Base error object in error responses.
 * All errors follow this structure.
 * 
 * @typedef {Object} BridgeError
 * @property {string} code - Error code (TIMEOUT, VALIDATION_ERROR, HANDLER_ERROR, TRANSPORT_ERROR, NOT_FOUND, etc.)
 * @property {string} message - Human-readable error message
 * @property {Object} [context] - Additional error context (handler name, input, timeout value, etc.)
 * @property {string} [stackTrace] - Stack trace (debug mode only)
 */

/**
 * Timeout error — RPC call did not complete within time limit (Step 64).
 * 
 * @typedef {Object} TimeoutError
 * @property {string} code - Always "TIMEOUT"
 * @property {string} message - e.g., "Handler 'bridge:getEditorState' did not respond within 5000ms"
 * @property {Object} context
 * @property {string} context.handler - Handler name
 * @property {number} context.timeout - Timeout in milliseconds
 * @property {number} context.elapsed - Actual elapsed time
 */

/**
 * Validation error — incoming message failed schema validation.
 * 
 * @typedef {Object} ValidationError
 * @property {string} code - Always "VALIDATION_ERROR"
 * @property {string} message - e.g., "Field 'data.timeout' must be a positive number"
 * @property {Object} context
 * @property {string} context.field - Field name that failed
 * @property {*} context.value - Actual value provided
 * @property {string} context.expected - Expected schema/type
 */

/**
 * Handler error — handler function threw an exception.
 * 
 * @typedef {Object} HandlerError
 * @property {string} code - Always "HANDLER_ERROR"
 * @property {string} message - e.g., "Handler threw: Cannot read property 'activeFile' of null"
 * @property {Object} context
 * @property {string} context.handler - Handler name
 * @property {string} context.originalError - Original exception message
 * @property {string} [context.stackTrace] - Stack trace (debug mode only)
 */

/**
 * Transport error — stdio communication failed.
 * 
 * @typedef {Object} TransportError
 * @property {string} code - Always "TRANSPORT_ERROR"
 * @property {string} message - e.g., "Bridge process exited unexpectedly with code 1"
 * @property {Object} context
 * @property {string} context.reason - Reason (process died, pipe closed, JSON parse failed, etc.)
 * @property {number} [context.exitCode] - Process exit code (if applicable)
 */

/**
 * Not found error — handler or resource not found.
 * 
 * @typedef {Object} NotFoundError
 * @property {string} code - Always "NOT_FOUND"
 * @property {string} message - e.g., "Handler 'bridge:unknownHandler' not registered"
 * @property {Object} context
 * @property {string} context.handler - Handler name requested
 * @property {string[]} context.available - List of registered handlers
 */

/**
 * ============================================================================
 * EVENT TYPES (Sent via SubscriptionEvent messages)
 * ============================================================================
 */

/**
 * State change event — editor state changed (cursor, selection, file, etc.).
 * Subscription type: "bridge:onEditorStateChange"
 * 
 * @typedef {Object} StateChangeEvent
 * @property {string} eventType - Always "stateChange"
 * @property {Object} data
 * @property {string} [data.activeFile] - Active file path (if changed)
 * @property {number} [data.cursorLine] - Cursor line (if changed)
 * @property {number} [data.cursorColumn] - Cursor column (if changed)
 * @property {string} [data.selectedText] - Selected text (if changed)
 * @property {string} [data.language] - Language ID (if changed)
 * @property {Object} [data.changes] - Map of changed properties (keys present if value changed)
 */

/**
 * Telemetry event — diagnostics/metrics from bridge operations.
 * Subscription type: "bridge:onTelemetry"
 * 
 * @typedef {Object} TelemetryEvent
 * @property {string} eventType - Always "telemetry"
 * @property {Object} data
 * @property {string} data.metric - Metric name (e.g., "handler_latency", "error_count", "cache_hit_rate")
 * @property {number} data.value - Metric value
 * @property {string} [data.unit] - Unit (ms, count, percentage, etc.)
 * @property {Object} [data.tags] - Optional tags (handler name, etc.)
 * @property {number} [data.timestamp] - Event timestamp
 */

/**
 * Bridge state event — bridge lifecycle change (started, error, recovery, shutdown).
 * Subscription type: "bridge:onBridgeStateChange"
 * 
 * @typedef {Object} BridgeStateEvent
 * @property {string} eventType - Always "bridgeState"
 * @property {Object} data
 * @property {string} data.state - State (initializing, ready, error, recovering, shutting_down, down)
 * @property {string} [data.message] - Optional state message (e.g., "Bridge recovered from crash")
 * @property {Object} [data.details] - State-specific details (handlers available, error info, etc.)
 */

/**
 * Diagnostics event — compiler/analyzer diagnostics updated.
 * Subscription type: "bridge:onDiagnosticsChange"
 * 
 * @typedef {Object} DiagnosticsEvent
 * @property {string} eventType - Always "diagnostics"
 * @property {Object} data
 * @property {string} data.file - File path
 * @property {Diagnostic[]} data.diagnostics - Array of Diagnostic objects (see handlers.d.js)
 * @property {number} data.count - Total diagnostic count
 * @property {number} [data.errorCount] - Error count
 * @property {number} [data.warningCount] - Warning count
 */

/**
 * Notification event — user-facing notification (info, warning, error).
 * Subscription type: "bridge:onNotification"
 * 
 * @typedef {Object} NotificationEvent
 * @property {string} eventType - Always "notification"
 * @property {Object} data
 * @property {string} data.level - Severity (info, warning, error)
 * @property {string} data.title - Notification title
 * @property {string} data.message - Notification message
 * @property {string} [data.action] - Optional action name (e.g., "Dismiss", "View Logs")
 * @property {number} [data.timeout] - Timeout in milliseconds before auto-dismiss
 */

/**
 * Debug state event — debugger state changed (paused, running, stopped).
 * Subscription type: "bridge:onDebugStateChange" (Step 61)
 * 
 * @typedef {Object} DebugStateEvent
 * @property {string} eventType - Always "debugState"
 * @property {Object} data
 * @property {string} data.state - State (stopped, running, paused)
 * @property {number} [data.frameIndex] - Current frame index (if paused)
 * @property {string} [data.file] - Current file (if paused)
 * @property {number} [data.line] - Current line (if paused)
 * @property {Object[]} [data.locals] - Local variables (if paused)
 */

/**
 * ============================================================================
 * MESSAGE FLOW TYPES (Documentation)
 * ============================================================================
 */

/**
 * Request-response flow — single RPC call.
 * WebView sends request, bridge sends response (success or error).
 * 
 * @typedef {Object} RequestResponseFlow
 * @property {string} flowType - Always "request-response"
 * @property {WebViewRequest} request - Initial request
 * @property {WebViewResponse} response - Response (success or error)
 * @property {number} roundTripLatency - Total latency in milliseconds
 * 
 * @example
 * // Flow:
 * 1. WebView sends: { messageType: "bridge:getEditorState", messageId: "uuid-123", ... }
 * 2. Bridge processes handler
 * 3. Bridge sends: { success: true, messageId: "uuid-123", data: { ... }, latency: 15 }
 * 4. WebView receives response; completes Promise
 */

/**
 * Subscription flow — long-lived event stream.
 * WebView sends subscription request, bridge sends acknowledgment, then streams events.
 * 
 * @typedef {Object} SubscriptionFlow
 * @property {string} flowType - Always "subscription"
 * @property {SubscriptionRequest} request - Subscription request
 * @property {SubscriptionStarted} acknowledgment - Bridge acknowledges subscription
 * @property {SubscriptionEvent[]} events - Array of unsolicited events
 * @property {number} uptime - Subscription duration in milliseconds
 * 
 * @example
 * // Flow:
 * 1. WebView sends: { messageType: "bridge:onEditorStateChange", messageId: "sub-uuid-456", ... }
 * 2. Bridge sends: { success: true, data: { subscriptionId: "sub-server-789", ... } }
 * 3. (user edits code)
 * 4. Bridge sends: { success: true, messageId: "sub-uuid-456", event: { ... } }
 * 5. Bridge sends: { success: true, messageId: "sub-uuid-456", event: { ... } }
 * (repeat for each state change, optionally debounced)
 */

/**
 * Streaming flow — chunked response over single RPC.
 * WebView sends request, bridge sends multiple PartialResponse, then final SuccessResponse.
 * 
 * @typedef {Object} StreamingFlow
 * @property {string} flowType - Always "streaming"
 * @property {WebViewRequest} request - Initial request
 * @property {PartialResponse[]} chunks - Array of partial responses
 * @property {SuccessResponse} final - Final success response (or ErrorResponse on failure)
 * @property {number} totalLatency - Elapsed time from request to final response
 * 
 * @example
 * // Flow:
 * 1. WebView sends: { messageType: "bridge:search", messageId: "uuid-789", data: { ... } }
 * 2. Bridge sends: { success: true, messageId: "uuid-789", chunk: { ... }, sequenceNumber: 0 }
 * 3. Bridge sends: { success: true, messageId: "uuid-789", chunk: { ... }, sequenceNumber: 1 }
 * 4. Bridge sends: { success: true, messageId: "uuid-789", chunk: null, isLast: true, sequenceNumber: 2 }
 * 5. WebView collects chunks and completes
 */

/**
 * ============================================================================
 * BOOTSTRAP RESPONSE (Special case: response to bootstrap request)
 * ============================================================================
 */

/**
 * Bootstrap response — bridge capabilities and handler list.
 * Response to BootstrapRequest; enables WebView to learn available functionality.
 * 
 * @typedef {Object} BootstrapResponse
 * @property {boolean} success - Always true (bootstrap always succeeds)
 * @property {string} messageId - Echoes request messageId
 * @property {Object} data
 * @property {string} data.bridgeVersion - Bridge version (e.g., "2.0.0")
 * @property {string[]} data.handlers - Array of registered handler names
 * @property {string[]} data.subscriptions - Array of available subscription types
 * @property {Object} data.capabilities
 * @property {boolean} data.capabilities.streaming - Supports streaming responses
 * @property {boolean} data.capabilities.subscriptions - Supports event subscriptions
 * @property {number} data.capabilities.maxTimeout - Maximum RPC timeout allowed
 * @property {number} data.capabilities.defaultTimeout - Default RPC timeout
 * @property {Object} data.features - Feature flags status (enabled/disabled)
 * @property {Object} [data.bridgeState] - Current bridge state info
 * 
 * @example
 * // Bridge responds to bootstrap:
 * {
 *   success: true,
 *   messageId: "uuid-v4",
 *   data: {
 *     bridgeVersion: "2.0.0",
 *     handlers: ["bridge:getEditorState", "bridge:search", "bridge:applyEdit", ...],
 *     subscriptions: ["bridge:onEditorStateChange", "bridge:onDiagnosticsChange", ...],
 *     capabilities: {
 *       streaming: true,
 *       subscriptions: true,
 *       maxTimeout: 30000,
 *       defaultTimeout: 5000
 *     },
 *     features: {
 *       terminal: true,
 *       codeActions: true,
 *       refactoring: false
 *     }
 *   }
 * }
 */

/**
 * ============================================================================
 * EXPORTS (For Documentation and Type Checking)
 * ============================================================================
 */

export const WebViewMessageTypes = {
  // Request types
  WebViewRequest: 'Object with messageType, messageId, data?, timestamp?, metadata?',
  BootstrapRequest: 'Object with messageType, messageId, data?',
  HandlerRequest: 'Object with messageType, messageId, data, timeout?',
  SubscriptionRequest: 'Object with messageType, messageId, data?',
  ConfigurationRequest: 'Object with messageType, messageId, data?',

  // Response types
  WebViewResponse: 'Object with success, messageId, data?, error?, latency?, timestamp?',
  SuccessResponse: 'Object with success=true, messageId, data, latency?',
  ErrorResponse: 'Object with success=false, messageId, error, latency?',
  PartialResponse: 'Object with success=true, messageId, chunk, isLast?, sequenceNumber',
  SubscriptionEvent: 'Object with success=true, messageId, event, timestamp?',
  SubscriptionStarted: 'Object with success=true, messageId, data',
  BootstrapResponse: 'Object with success=true, messageId, data (bridgeVersion, handlers, etc.)',

  // Error types
  BridgeError: 'Object with code, message, context?, stackTrace?',
  TimeoutError: 'BridgeError with code=TIMEOUT',
  ValidationError: 'BridgeError with code=VALIDATION_ERROR',
  HandlerError: 'BridgeError with code=HANDLER_ERROR',
  TransportError: 'BridgeError with code=TRANSPORT_ERROR',
  NotFoundError: 'BridgeError with code=NOT_FOUND',

  // Event types
  StateChangeEvent: 'Object with eventType=stateChange, data',
  TelemetryEvent: 'Object with eventType=telemetry, data',
  BridgeStateEvent: 'Object with eventType=bridgeState, data',
  DiagnosticsEvent: 'Object with eventType=diagnostics, data',
  NotificationEvent: 'Object with eventType=notification, data',
  DebugStateEvent: 'Object with eventType=debugState, data',

  // Message flow types
  RequestResponseFlow: 'Single RPC: request → response (success or error)',
  SubscriptionFlow: 'Stream: subscription request → ack → multiple events',
  StreamingFlow: 'Chunked: request → multiple partial responses → final',
};

export default WebViewMessageTypes;
