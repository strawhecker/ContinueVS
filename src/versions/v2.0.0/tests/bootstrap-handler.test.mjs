#!/usr/bin/env node

/**
 * Bootstrap Handler Test Suite
 *
 * Comprehensive unit tests for the bootstrap handler (Step 46).
 * Tests cover happy path, feature detection, error scenarios, and telemetry.
 *
 * @module src/versions/v2.0.0/tests/bootstrap-handler.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Test Framework: Mocha + Node.js assert
 * Coverage: 10 test cases across 6 suites
 * Execution: npm test -- src/versions/v2.0.0/tests/bootstrap-handler.test.mjs
 *
 * Related Steps:
 *   - Step 46: Bootstrap handler (code under test)
 *   - Step 71: Handler registration (will use this test pattern)
 *   - Step 75: WebView integration tests (integration layer)
 */

import assert from 'assert';
import { describe, it, beforeEach } from 'mocha';
import { bootstrapHandler } from '../handlers/bootstrap-handler.js';

/**
 * Test Fixtures & Mocks
 */

/**
 * Creates a mock logger for testing.
 * Captures all log calls for assertion.
 */
function createMockLogger() {
  return {
    calls: [],
    debug: async function(message, context) {
      this.calls.push({ level: 'debug', message, context });
      return Promise.resolve();
    },
    info: async function(message, context) {
      this.calls.push({ level: 'info', message, context });
      return Promise.resolve();
    },
    warning: async function(message, context) {
      this.calls.push({ level: 'warning', message, context });
      return Promise.resolve();
    },
    error: async function(message, error) {
      this.calls.push({ level: 'error', message, error });
      return Promise.resolve();
    }
  };
}

/**
 * Creates a mock metrics collector for testing.
 * Captures all telemetry calls for assertion.
 */
function createMockMetrics() {
  return {
    calls: [],
    recordHandlerExecution: function(handlerName, success, latencyMs) {
      this.calls.push({ handlerName, success, latencyMs });
    }
  };
}

/**
 * Creates a mock server for testing.
 * Can simulate different bridge states and IDE states.
 */
function createMockServer(bridgeState = 'Ready', ideState = null) {
  return {
    getBridgeState: () => bridgeState,
    getIDEState: () => ideState || {
      activeFile: 'C:\\project\\Main.cs',
      cursorLine: 42,
      cursorColumn: 10,
      selectedText: 'foo',
      language: 'csharp',
      projectPath: 'C:\\project',
      diagnosticsCount: 3
    }
  };
}

/**
 * Creates a test message envelope.
 */
function createMessage(overrides = {}) {
  const defaults = {
    messageType: 'bridge:bootstrap',
    messageId: 'test-uuid-123',
    data: {
      ideVersion: '2026.1',
      debugMode: false,
      capabilities: {}
    }
  };
  return { ...defaults, ...overrides };
}

/**
 * Test Suites
 */

describe('bootstrapHandler', () => {
  let mockLogger;
  let mockMetrics;
  let mockServer;

  beforeEach(() => {
    mockLogger = createMockLogger();
    mockMetrics = createMockMetrics();
    mockServer = createMockServer();
  });

  /**
   * Suite 1: Happy Path
   * Verifies successful bootstrap with all services available.
   */
  describe('Suite 1: Happy Path', () => {
    it('should return success with bridge metadata on valid input', async () => {
      // Arrange
      const message = createMessage();
      const context = {
        logger: mockLogger,
        metrics: mockMetrics,
        server: mockServer
      };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert(response.data, 'response.data should exist');
      assert.strictEqual(response.data.bridgeVersion, '2.0.0', 'bridgeVersion should be 2.0.0');
      assert.strictEqual(response.data.bridgeProtocolVersion, '1.0', 'bridgeProtocolVersion should be 1.0');
      assert(response.data.features, 'features should exist');
      assert(Array.isArray(response.data.handlers), 'handlers should be an array');
      assert(response.data.handlers.length > 0, 'handlers array should not be empty');
      assert.strictEqual(response.data.handlers[0], 'bridge:bootstrap', 'first handler should be bridge:bootstrap');
      assert(response.data.editorState, 'editorState should exist');
      assert.strictEqual(response.data.editorState.activeFile, 'C:\\project\\Main.cs', 'activeFile should be captured');
    });
  });

  /**
   * Suite 2: IDE Capabilities Detection
   * Tests bootstrap with various IDE capability configurations.
   */
  describe('Suite 2: IDE Capabilities Detection', () => {
    it('should handle missing IDE capabilities and use defaults', async () => {
      // Arrange
      const message = createMessage({ data: {} });
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true);
      assert(response.data.features.editorContext, 'editorContext feature should default to enabled');
      assert(response.data.features.diagnostics, 'diagnostics feature should default to enabled');
    });

    it('should disable features based on IDE capabilities', async () => {
      // Arrange
      const message = createMessage({
        data: {
          capabilities: {
            editorContext: false,
            symbolExtraction: false,
            gitIntegration: true
          }
        }
      });
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true);
      assert.strictEqual(response.data.features.editorContext, false, 'editorContext should be disabled');
      assert.strictEqual(response.data.features.symbolExtraction, false, 'symbolExtraction should be disabled');
      assert.strictEqual(response.data.features.gitIntegration, true, 'gitIntegration should remain enabled');
    });
  });

  /**
   * Suite 3: Feature Flag Evaluation
   * Tests environment variable override and feature flag defaults.
   */
  describe('Suite 3: Feature Flag Evaluation', () => {
    it('should respect environment variable overrides for features', async () => {
      // Arrange
      const originalEnv = process.env.BRIDGE_TERMINAL;
      process.env.BRIDGE_TERMINAL = 'true';

      const message = createMessage();
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServer };

      try {
        // Act
        const response = await bootstrapHandler(message, context);

        // Assert
        assert.strictEqual(response.success, true);
        assert.strictEqual(response.data.features.terminal, true, 'terminal should be enabled via env var');
      } finally {
        // Cleanup
        if (originalEnv !== undefined) {
          process.env.BRIDGE_TERMINAL = originalEnv;
        } else {
          delete process.env.BRIDGE_TERMINAL;
        }
      }
    });

    it('should have disabled-by-default features like terminal and debugging', async () => {
      // Arrange
      const message = createMessage();
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true);
      assert.strictEqual(response.data.features.terminal, false, 'terminal should default to disabled');
      assert.strictEqual(response.data.features.debugging, false, 'debugging should default to disabled');
      assert.strictEqual(response.data.features.editorContext, true, 'editorContext should default to enabled');
    });
  });

  /**
   * Suite 4: Handler Registry Generation
   * Tests that handler list is properly constructed.
   */
  describe('Suite 4: Handler Registry Generation', () => {
    it('should return complete handler registry list', async () => {
      // Arrange
      const message = createMessage();
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true);
      const handlers = response.data.handlers;
      assert(Array.isArray(handlers), 'handlers should be an array');
      assert(handlers.length >= 15, 'should have at least 15 handlers registered');
      assert(handlers.includes('bridge:bootstrap'), 'should include bootstrap handler');
      assert(handlers.includes('bridge:getEditorState'), 'should include getEditorState handler');
      assert(handlers.includes('bridge:search'), 'should include search handler');
      assert(handlers.includes('bridge:refactor'), 'should include refactor handler');
    });
  });

  /**
   * Suite 5: Editor State Snapshot
   * Tests editor state capture from IDE.
   */
  describe('Suite 5: Editor State Snapshot', () => {
    it('should capture editor state when available', async () => {
      // Arrange
      const message = createMessage();
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true);
      const editorState = response.data.editorState;
      assert(editorState, 'editorState should be present');
      assert.strictEqual(editorState.activeFile, 'C:\\project\\Main.cs');
      assert.strictEqual(editorState.cursorLine, 42);
      assert.strictEqual(editorState.cursorColumn, 10);
      assert.strictEqual(editorState.language, 'csharp');
      assert.strictEqual(editorState.diagnosticsCount, 3);
      assert(editorState.timestamp, 'should include timestamp');
    });

    it('should return null editor state when server unavailable', async () => {
      // Arrange
      const message = createMessage();
      const context = { logger: mockLogger, metrics: mockMetrics, server: null };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true);
      assert.strictEqual(response.data.editorState, null, 'editorState should be null when server unavailable');
    });
  });

  /**
   * Suite 6: Error Scenarios & Graceful Degradation
   * Tests error handling and null-safe service access.
   */
  describe('Suite 6: Error Scenarios & Graceful Degradation', () => {
    it('should succeed with null logger', async () => {
      // Arrange
      const message = createMessage();
      const context = { logger: null, metrics: mockMetrics, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'should succeed even with null logger');
      assert(response.data, 'should still return valid data');
    });

    it('should succeed with null metrics', async () => {
      // Arrange
      const message = createMessage();
      const context = { logger: mockLogger, metrics: null, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'should succeed even with null metrics');
      assert(response.data, 'should still return valid data');
    });

    it('should succeed when bridge not ready', async () => {
      // Arrange
      const message = createMessage();
      const mockServerNotReady = createMockServer('NotInitialized');
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServerNotReady };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, false, 'should fail when bridge not ready');
      assert(response.error, 'should include error message');
      assert(response.error.includes('NotInitialized'), 'error should mention bridge state');
    });

    it('should record telemetry on success', async () => {
      // Arrange
      const message = createMessage();
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServer };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true);
      assert(mockMetrics.calls.length > 0, 'metrics should have recorded execution');
      const telemetry = mockMetrics.calls[0];
      assert.strictEqual(telemetry.handlerName, 'bridge:bootstrap');
      assert.strictEqual(telemetry.success, true);
      assert(telemetry.latencyMs >= 0, 'latencyMs should be non-negative');
      assert(telemetry.latencyMs < 5000, 'latencyMs should be reasonable (< 5s)');
    });

    it('should record telemetry on failure', async () => {
      // Arrange
      const message = createMessage();
      const mockServerNotReady = createMockServer('NotInitialized');
      const context = { logger: mockLogger, metrics: mockMetrics, server: mockServerNotReady };

      // Act
      const response = await bootstrapHandler(message, context);

      // Assert
      assert.strictEqual(response.success, false);
      assert(mockMetrics.calls.length > 0, 'metrics should record failure');
      const telemetry = mockMetrics.calls[0];
      assert.strictEqual(telemetry.handlerName, 'bridge:bootstrap');
      assert.strictEqual(telemetry.success, false);
    });
  });
});
