#!/usr/bin/env node

/**
 * Bootstrap Handler for Bridge Initialization
 *
 * This is the **first handler** called by the WebView after continueVS bridge injection.
 * It negotiates capabilities, returns bridge metadata, and establishes readiness for
 * subsequent handlers (Steps 50–61).
 *
 * Message Type: bridge:bootstrap
 * Input: IDE capabilities (optional)
 * Output: Bridge metadata, feature flags, handler registry, editor state
 *
 * @module src/versions/v2.0.0/handlers/bootstrap-handler.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 45: BridgeLifecycleManager (prerequisite)
 *   - Step 50: getEditorState handler (depends on bootstrap success)
 *   - Step 71: Handler registration (registers this handler)
 *   - Step 75: WebView integration tests (tests bootstrap)
 *
 * Design Principles:
 *   - Pure async function; returns response envelope always
 *   - Graceful degradation: null logger/telemetry don't cause failures
 *   - No external npm dependencies; Node.js built-ins only
 *   - Telemetry recorded for success and failure paths
 *   - Error messages propagate to WebView for diagnostics
 */

/**
 * Bootstrap Handler — Gateway for Part II handlers
 *
 * Called immediately after WebviewInjector injects the continueVS bridge.
 * Performs capability negotiation and returns bridge readiness state.
 *
 * @async
 * @param {Object} message - Message envelope from IDE
 * @param {string} message.messageType - Should be "bridge:bootstrap"
 * @param {string} message.messageId - Unique correlation UUID
 * @param {Object} [message.data={}] - Optional IDE capabilities
 * @param {string} [message.data.ideVersion] - IDE version (e.g., "2026.1")
 * @param {boolean} [message.data.debugMode] - Whether debug logging enabled
 * @param {Object} [message.data.capabilities] - IDE capabilities bitmap
 * @param {Object} context - Handler context (shared services)
 * @param {Object} [context.logger] - Logger instance (null-safe)
 * @param {Object} [context.metrics] - Telemetry collector (null-safe)
 * @param {Object} [context.server] - CoreServer instance (null-safe)
 *
 * @returns {Promise<Object>} Handler response envelope
 * @returns {Promise<Object>} .success - Whether bootstrap succeeded
 * @returns {Promise<Object>} .data - Bridge metadata (if success=true)
 * @returns {Promise<Object>} .data.bridgeVersion - Bridge version (e.g., "2.0.0")
 * @returns {Promise<Object>} .data.bridgeProtocolVersion - Protocol version (e.g., "1.0")
 * @returns {Promise<Object>} .data.features - Enabled feature flags
 * @returns {Promise<Object>} .data.handlers - List of available handler message types
 * @returns {Promise<Object>} .data.editorState - Current editor state snapshot (or null)
 * @returns {Promise<Object>} .error - Error message (if success=false)
 *
 * @throws Never throws; always returns response envelope
 *
 * Example Usage:
 * ```javascript
 * const response = await bootstrapHandler({
 *   messageType: 'bridge:bootstrap',
 *   messageId: 'uuid-here',
 *   data: { ideVersion: '2026.1', debugMode: false }
 * }, {
 *   logger: myLogger,
 *   metrics: myTelemetry,
 *   server: coreServer
 * });
 *
 * if (response.success) {
 *   console.log('Bridge version:', response.data.bridgeVersion);
 *   console.log('Available handlers:', response.data.handlers);
 * } else {
 *   console.error('Bootstrap failed:', response.error);
 * }
 * ```
 */
export async function bootstrapHandler(message, context) {
  const startTime = Date.now();
  const messageId = message?.messageId || 'unknown';

  // Safe service access (null-safe)
  const logger = context?.logger || null;
  const metrics = context?.metrics || null;
  const server = context?.server || null;

  // Safe data extraction
  const data = message?.data || {};
  const ideVersion = data.ideVersion || 'unknown';
  const debugMode = data.debugMode || false;
  const ideCapabilities = data.capabilities || {};

  try {
    // Log bootstrap start
    if (logger) {
      await logger.debug?.(
        `[bootstrap] Starting bootstrap for IDE ${ideVersion}`,
        { messageId, debugMode }
      ) ?? Promise.resolve();
    }

    // Validate bridge readiness
    const bridgeReady = validateBridgeReadiness(server, logger);
    if (!bridgeReady.ready) {
      const latencyMs = Date.now() - startTime;
      if (metrics) {
        metrics.recordHandlerExecution?.('bridge:bootstrap', false, latencyMs);
      }

      if (logger) {
        await logger.warning?.(`[bootstrap] Bridge not ready: ${bridgeReady.reason}`) ?? Promise.resolve();
      }

      return {
        success: false,
        error: bridgeReady.reason
      };
    }

    // Evaluate feature flags based on IDE capabilities and environment
    const features = evaluateFeatureFlags(ideCapabilities, logger);

    // Build handler registry (placeholder; filled by Step 71)
    // In production, this would be queried from the dispatcher
    const handlers = buildHandlerRegistry();

    // Capture editor state snapshot if available
    const editorState = captureEditorState(server, logger);

    // Construct response
    const response = {
      success: true,
      data: {
        bridgeVersion: '2.0.0',
        bridgeProtocolVersion: '1.0',
        timestamp: new Date().toISOString(),
        features,
        handlers,
        editorState
      }
    };

    // Log success
    const latencyMs = Date.now() - startTime;
    if (logger) {
      await logger.info?.(
        `[bootstrap] Bootstrap complete (${latencyMs}ms)`,
        { messageId, handlerCount: handlers.length }
      ) ?? Promise.resolve();
    }

    // Record telemetry
    if (metrics) {
      metrics.recordHandlerExecution?.('bridge:bootstrap', true, latencyMs);
    }

    return response;
  } catch (error) {
    // Graceful error handling; never throw
    const latencyMs = Date.now() - startTime;
    const errorMessage = error instanceof Error ? error.message : String(error);

    if (logger) {
      await logger.error?.(
        `[bootstrap] Unexpected error during bootstrap: ${errorMessage}`,
        error instanceof Error ? error : new Error(errorMessage)
      ) ?? Promise.resolve();
    }

    if (metrics) {
      metrics.recordHandlerExecution?.('bridge:bootstrap', false, latencyMs);
    }

    return {
      success: false,
      error: `Bootstrap failed: ${errorMessage}`
    };
  }
}

/**
 * Validates that the bridge is in a ready state.
 *
 * @private
 * @param {Object} [server] - CoreServer instance (null-safe)
 * @param {Object} [logger] - Logger instance (null-safe)
 * @returns {Object} Validation result
 * @returns {boolean} .ready - Whether bridge is ready
 * @returns {string} .reason - Reason if not ready
 */
function validateBridgeReadiness(server, logger) {
  if (!server) {
    return {
      ready: true, // Degraded mode; continue anyway
      reason: null
    };
  }

  try {
    // Query bridge state from server
    const bridgeState = server.getBridgeState?.();

    if (!bridgeState) {
      return {
        ready: true, // Server doesn't expose state; assume ready
        reason: null
      };
    }

    const isReady = bridgeState === 'Ready' || bridgeState === 'Degraded';

    if (!isReady) {
      return {
        ready: false,
        reason: `Bridge is in ${bridgeState} state; not ready for handlers`
      };
    }

    return {
      ready: true,
      reason: null
    };
  } catch (ex) {
    if (logger) {
      logger.warning?.(`[bootstrap] Failed to validate bridge readiness: ${ex.message}`) ?? Promise.resolve();
    }

    return {
      ready: true, // Assume ready on error
      reason: null
    };
  }
}

/**
 * Evaluates which features are enabled based on IDE capabilities and environment.
 *
 * @private
 * @param {Object} [ideCapabilities={}] - IDE capability flags
 * @param {Object} [logger] - Logger instance (null-safe)
 * @returns {Object} Enabled features (true = enabled, false = disabled)
 *
 * Feature flags are determined by:
 *   1. IDE capabilities reported by C# bridge
 *   2. Environment variables (BRIDGE_* flags)
 *   3. Defaults (most features enabled by default)
 */
function evaluateFeatureFlags(ideCapabilities = {}, logger) {
  const env = typeof process !== 'undefined' ? process.env : {};

  // Check feature flags from environment (overrides IDE capabilities)
  const getFlag = (featureName, defaultValue) => {
    const envKey = `BRIDGE_${featureName.toUpperCase()}`;
    const envValue = env[envKey];

    if (envValue !== undefined) {
      return envValue === 'true' || envValue === '1';
    }

    return ideCapabilities[featureName] !== false && defaultValue;
  };

  const features = {
    // Core editor features
    editorContext: getFlag('editorContext', true),
    symbolExtraction: getFlag('symbolExtraction', true),
    diagnostics: getFlag('diagnostics', true),
    search: getFlag('search', true),

    // Navigation features
    goToDefinition: getFlag('goToDefinition', true),
    findReferences: getFlag('findReferences', true),

    // Code intelligence
    codeCompletion: getFlag('codeCompletion', true),
    hoverInfo: getFlag('hoverInfo', true),

    // Editing features
    refactoring: getFlag('refactoring', true),
    fixSuggestions: getFlag('fixSuggestions', true),
    formatting: getFlag('formatting', true),

    // Integration features
    gitIntegration: getFlag('gitIntegration', true),
    terminal: getFlag('terminal', false), // Disabled by default
    fileSystem: getFlag('fileSystem', false), // Disabled by default
    debugging: getFlag('debugging', false) // Disabled by default
  };

  if (logger) {
    const enabledFeatures = Object.entries(features)
      .filter(([, enabled]) => enabled)
      .map(([name]) => name);

    logger.debug?.(
      `[bootstrap] Enabled features: ${enabledFeatures.join(', ')}`,
      { count: enabledFeatures.length }
    ) ?? Promise.resolve();
  }

  return features;
}

/**
 * Builds the handler registry (message types available).
 *
 * @private
 * @returns {string[]} Array of available handler message types
 *
 * Note: In Step 46 (now), bootstrap is the only handler registered.
 * Step 71 will import this handler and register it with the dispatcher,
 * then other handlers (Steps 50–61) will be added to this list.
 *
 * For now, we return a placeholder list with the expected handlers.
 * In production (after Step 71), this would query the dispatcher.
 */
function buildHandlerRegistry() {
  // Hard-coded handler list (placeholder for Step 71 integration)
  // Once Step 71 registers handlers, this would query the dispatcher
  const handlers = [
    // Bridge lifecycle
    'bridge:bootstrap',

    // Editor context (Steps 50–51)
    'bridge:getEditorState',
    'bridge:onEditorStateChange',

    // Navigation (Steps 55–57)
    'bridge:search',
    'bridge:goToDefinition',
    'bridge:findReferences',

    // Code intelligence (Steps 58–59)
    'bridge:codeCompletion',
    'bridge:hoverInfo',

    // Editing (Steps 76–79)
    'bridge:refactor',
    'bridge:fixSuggestion',
    'bridge:applyEdit',
    'bridge:formatDocument',

    // Infrastructure (Steps 81–87)
    'bridge:gitIntegration',
    'bridge:terminal',
    'bridge:fileSystem',
    'bridge:projectInfo',

    // UI (Steps 85–92)
    'bridge:inlineMessage',
    'bridge:sidebar',
    'bridge:codeLens',
    'bridge:diffViewer'
  ];

  return handlers;
}

/**
 * Captures a snapshot of the current editor state.
 *
 * @private
 * @param {Object} [server] - CoreServer instance (null-safe)
 * @param {Object} [logger] - Logger instance (null-safe)
 * @returns {Object|null} Editor state snapshot, or null if unavailable
 *
 * Returns: { activeFile, cursorLine, cursorColumn, selectedText, ... }
 * If server is unavailable or doesn't expose editor state, returns null.
 */
function captureEditorState(server, logger) {
  if (!server) {
    return null;
  }

  try {
    // Attempt to get IDE state from server
    const ideState = server.getIDEState?.();

    if (!ideState) {
      return null; // Server doesn't expose IDE state
    }

    // Build editor state snapshot
    const editorState = {
      activeFile: ideState.activeFile || null,
      cursorLine: ideState.cursorLine ?? -1,
      cursorColumn: ideState.cursorColumn ?? -1,
      selectedText: ideState.selectedText || '',
      language: ideState.language || 'unknown',
      projectPath: ideState.projectPath || null,
      diagnosticsCount: ideState.diagnosticsCount ?? 0,
      timestamp: new Date().toISOString()
    };

    if (logger) {
      logger.debug?.(
        `[bootstrap] Captured editor state: ${editorState.activeFile} @ ${editorState.cursorLine}:${editorState.cursorColumn}`,
        { language: editorState.language }
      ) ?? Promise.resolve();
    }

    return editorState;
  } catch (error) {
    if (logger) {
      logger.warning?.(
        `[bootstrap] Failed to capture editor state: ${error instanceof Error ? error.message : String(error)}`
      ) ?? Promise.resolve();
    }

    return null;
  }
}

export default bootstrapHandler;
