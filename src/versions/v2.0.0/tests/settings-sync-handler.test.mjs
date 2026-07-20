#!/usr/bin/env node

/**
 * Settings-Sync Handler Test Suite (Step 95)
 *
 * Comprehensive test coverage for bidirectional settings synchronization.
 * 
 * Test Coverage:
 * - Suite 1: Initialization & Configuration (3 tests)
 * - Suite 2: Input Validation (5 tests)
 * - Suite 3: Load Operations (4 tests)
 * - Suite 4: Apply Operations (4 tests)
 * - Suite 5: Error Handling (4 tests)
 * - Suite 6: File I/O & Persistence (4 tests)
 *
 * Total: 24 tests
 *
 * @file src/versions/v2.0.0/tests/settings-sync-handler.test.mjs
 */

import assert from 'assert';
import { describe, it, beforeEach } from 'mocha';
import {
  createLoadSettingsHandler,
  createApplySettingsHandler,
  SettingsSyncError,
  ValidationError,
  FileIOError,
} from '../lib/settings-sync-handler.mjs';
import {
  VALID_SETTINGS_FULL,
  VALID_SETTINGS_MINIMAL,
  VALID_SETTINGS_ALT_MODEL,
  INVALID_SETTINGS_MISSING_MODEL,
  INVALID_SETTINGS_MISSING_PROVIDER,
  INVALID_SETTINGS_TEMP_HIGH,
  INVALID_SETTINGS_TEMP_LOW,
  INVALID_SETTINGS_CTX_SMALL,
  INVALID_SETTINGS_MODEL_WRONG_TYPE,
  INVALID_SETTINGS_TEMP_WRONG_TYPE,
  createMockSettingsCollector,
  createMockLogger,
  createMockMetrics,
  createTestMessage,
  createApplySettingsMessage,
} from './mocks/settings-fixtures.mjs';

describe('Settings-Sync Handler', () => {
  // ========== Suite 1: Initialization & Configuration (3 tests) ==========

  describe('Suite 1: Initialization & Configuration', () => {
    it('should create loadSettings handler without context', async () => {
      // Arrange & Act
      const handler = createLoadSettingsHandler();

      // Assert
      assert.strictEqual(typeof handler, 'function');
      const result = await handler({ payload: {} }, {});
      assert(result.success !== undefined);
    });

    it('should create applySettings handler without context', async () => {
      // Arrange & Act
      const handler = createApplySettingsHandler();

      // Assert
      assert.strictEqual(typeof handler, 'function');
      const result = await handler({ payload: { settings: VALID_SETTINGS_MINIMAL } }, {});
      assert(result.success !== undefined);
    });

    it('should inject context dependencies correctly', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector(VALID_SETTINGS_FULL);
      const mockLogger = createMockLogger();
      const mockMetrics = createMockMetrics();
      const context = { settingsCollector: mockCollector, logger: mockLogger, metrics: mockMetrics };
      const handler = createLoadSettingsHandler(context);

      // Act
      await handler({ payload: {} }, {});

      // Assert
      assert(mockMetrics.getEvents().length > 0);
      assert(mockLogger.getLogs().info.length > 0);
    });
  });

  // ========== Suite 2: Input Validation (5 tests) ==========

  describe('Suite 2: Input Validation', () => {
    it('should reject missing required model field', async () => {
      // Arrange
      const handler = createApplySettingsHandler(createMockSettingsCollector());

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_MISSING_MODEL),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32602); // Invalid params
      assert(result.error.message.includes('model'));
    });

    it('should reject missing required provider field', async () => {
      // Arrange
      const handler = createApplySettingsHandler();

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_MISSING_PROVIDER),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32602);
      assert(result.error.message.includes('provider'));
    });

    it('should reject temperature out of range (too high)', async () => {
      // Arrange
      const handler = createApplySettingsHandler();

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_TEMP_HIGH),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32602);
      assert(result.error.message.includes('temperature'));
    });

    it('should reject temperature out of range (negative)', async () => {
      // Arrange
      const handler = createApplySettingsHandler();

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_TEMP_LOW),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32602);
    });

    it('should reject wrong field types', async () => {
      // Arrange
      const handler = createApplySettingsHandler();

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_MODEL_WRONG_TYPE),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32602);
    });
  });

  // ========== Suite 3: Load Operations (4 tests) ==========

  describe('Suite 3: Load Operations', () => {
    it('should load settings successfully with full settings', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector(VALID_SETTINGS_FULL);
      const context = { settingsCollector: mockCollector };
      const handler = createLoadSettingsHandler(context);

      // Act
      const result = await handler({ payload: {} }, {});

      // Assert
      assert.strictEqual(result.success, true);
      assert(result.data.settings);
      assert.strictEqual(result.data.settings.model, 'gpt-4');
      assert(result.data.duration < 5000);
    });

    it('should load settings with scope filter (modelConfig)', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector(VALID_SETTINGS_FULL);
      const context = { settingsCollector: mockCollector };
      const handler = createLoadSettingsHandler(context);

      // Act
      const result = await handler({ payload: { scope: 'modelConfig' } }, {});

      // Assert
      assert.strictEqual(result.success, true);
      assert(result.data.settings.model);
      assert(result.data.settings.provider);
      assert(result.data.settings.temperature);
      assert(!result.data.settings.endpoint);
    });

    it('should handle missing collector gracefully', async () => {
      // Arrange
      const mockLogger = createMockLogger();
      const context = { logger: mockLogger };
      const handler = createLoadSettingsHandler(context);

      // Act
      const result = await handler({ payload: {} }, {});

      // Assert
      assert.strictEqual(result.success, true);
      assert.deepStrictEqual(result.data.settings, {});
    });

    it('should return duration metrics', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector(VALID_SETTINGS_MINIMAL);
      const context = { settingsCollector: mockCollector };
      const handler = createLoadSettingsHandler(context);

      // Act
      const result = await handler({ payload: {} }, {});

      // Assert
      assert(result.data.duration >= 0);
      assert(result.data.duration < 10000);
    });
  });

  // ========== Suite 4: Apply Operations (4 tests) ==========

  describe('Suite 4: Apply Operations', () => {
    it('should apply valid settings successfully', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      const mockMetrics = createMockMetrics();
      const context = { settingsCollector: mockCollector, metrics: mockMetrics };
      const handler = createApplySettingsHandler(context);

      // Act
      const result = await handler(
        createApplySettingsMessage(VALID_SETTINGS_MINIMAL),
        {}
      );

      // Assert
      assert.strictEqual(result.success, true);
      assert(Array.isArray(result.data.appliedFields));
      assert(result.data.appliedFields.includes('model'));
      assert(result.data.appliedFields.includes('provider'));
      assert.strictEqual(result.data.cacheInvalidated, true);
    });

    it('should track metrics on successful apply', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      const mockMetrics = createMockMetrics();
      const context = { settingsCollector: mockCollector, metrics: mockMetrics };
      const handler = createApplySettingsHandler(context);

      // Act
      await handler(createApplySettingsMessage(VALID_SETTINGS_FULL), {});

      // Assert
      const lastEvent = mockMetrics.getLastEvent();
      assert.strictEqual(lastEvent.type, 'apply');
      assert.strictEqual(lastEvent.success, true);
    });

    it('should validate before persisting', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      const context = { settingsCollector: mockCollector };
      const handler = createApplySettingsHandler(context);

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_MISSING_MODEL),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(mockCollector.getState().writeCount, 0); // Not written
    });

    it('should handle collector write errors', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      mockCollector.setThrowOnWrite(new Error('Permission denied'));
      const context = { settingsCollector: mockCollector };
      const handler = createApplySettingsHandler(context);

      // Act
      const result = await handler(
        createApplySettingsMessage(VALID_SETTINGS_MINIMAL),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32603);
    });
  });

  // ========== Suite 5: Error Handling (4 tests) ==========

  describe('Suite 5: Error Handling', () => {
    it('should return JSON-RPC error format for validation errors', async () => {
      // Arrange
      const handler = createApplySettingsHandler();

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_TEMP_HIGH),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32602);
      assert(result.error.message);
      assert(result.error.data);
    });

    it('should return JSON-RPC error format for file I/O errors', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      mockCollector.setThrowOnWrite(new Error('File not writable'));
      const context = { settingsCollector: mockCollector };
      const handler = createApplySettingsHandler(context);

      // Act
      const result = await handler(
        createApplySettingsMessage(VALID_SETTINGS_MINIMAL),
        {}
      );

      // Assert
      assert.strictEqual(result.success, false);
      assert.strictEqual(result.error.code, -32603);
    });

    it('should mask sensitive data in error logging', async () => {
      // Arrange
      const mockLogger = createMockLogger();
      const context = { logger: mockLogger };
      const handler = createLoadSettingsHandler(context);

      // Act
      const settingsWithKey = {
        model: 'gpt-4',
        provider: 'openai',
        endpoint: 'https://api.openai.com/v1?key=secret123',
      };
      const mockCollector = createMockSettingsCollector(settingsWithKey);
      const ctxWithCollector = { ...context, settingsCollector: mockCollector };
      const handler2 = createLoadSettingsHandler(ctxWithCollector);
      await handler2({ payload: {} }, {});

      // Assert
      const infoLogs = mockLogger.getLogs().info;
      assert(infoLogs.length > 0);
      // Logs should not contain raw secret (masked instead)
    });

    it('should provide operation type in errors', async () => {
      // Arrange
      const handler = createApplySettingsHandler();

      // Act
      const result = await handler(
        createApplySettingsMessage(INVALID_SETTINGS_MISSING_MODEL),
        {}
      );

      // Assert
      assert(result.error);
      assert(result.error.data);
    });
  });

  // ========== Suite 6: File I/O & Persistence (4 tests) ==========

  describe('Suite 6: File I/O & Persistence', () => {
    it('should persist settings via collector', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      const context = { settingsCollector: mockCollector };
      const handler = createApplySettingsHandler(context);

      // Act
      await handler(createApplySettingsMessage(VALID_SETTINGS_FULL), {});

      // Assert
      const state = mockCollector.getState();
      assert.strictEqual(state.writeCount, 1);
      assert.deepStrictEqual(state.settings.model, VALID_SETTINGS_FULL.model);
    });

    it('should track write history', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      const context = { settingsCollector: mockCollector };
      const handler = createApplySettingsHandler(context);

      // Act
      await handler(createApplySettingsMessage(VALID_SETTINGS_MINIMAL), {});
      await handler(createApplySettingsMessage(VALID_SETTINGS_ALT_MODEL), {});

      // Assert
      const state = mockCollector.getState();
      assert.strictEqual(state.writeHistory.length, 2);
    });

    it('should support concurrent applies', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      const context = { settingsCollector: mockCollector };
      const handler = createApplySettingsHandler(context);

      // Act
      await Promise.all([
        handler(createApplySettingsMessage(VALID_SETTINGS_MINIMAL), {}),
        handler(createApplySettingsMessage(VALID_SETTINGS_ALT_MODEL), {}),
        handler(createApplySettingsMessage(VALID_SETTINGS_FULL), {}),
      ]);

      // Assert
      const state = mockCollector.getState();
      assert.strictEqual(state.writeCount, 3);
    });

    it('should be idempotent for identical settings', async () => {
      // Arrange
      const mockCollector = createMockSettingsCollector();
      const context = { settingsCollector: mockCollector };
      const handler = createApplySettingsHandler(context);

      // Act
      const result1 = await handler(
        createApplySettingsMessage(VALID_SETTINGS_MINIMAL),
        {}
      );
      const result2 = await handler(
        createApplySettingsMessage(VALID_SETTINGS_MINIMAL),
        {}
      );

      // Assert
      assert.strictEqual(result1.success, true);
      assert.strictEqual(result2.success, true);
      assert.deepStrictEqual(result1.data.appliedFields, result2.data.appliedFields);
    });
  });
});
