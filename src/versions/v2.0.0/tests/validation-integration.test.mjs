#!/usr/bin/env node

/**
 * Integration Test: Validation Hook in MiddlewareChain
 *
 * Tests validation-hook.mjs integration with MiddlewareChain (Step 47).
 * Verifies end-to-end message validation flow:
 * - Valid messages reach dispatcher
 * - Invalid messages trigger error responses
 * - Metrics recorded correctly
 *
 * @module src/versions/v2.0.0/tests/validation-integration.test.mjs
 * @version 1.0.0
 */

import { createValidationHook } from '../lib/validation-hook.mjs';
import * as fixtures from './mocks/message-fixtures.mjs';

describe('ValidationHook Integration with MiddlewareChain', () => {
  /**
   * Mock logger for testing.
   */
  const createMockLogger = () => ({
    debug: jest.fn(),
    info: jest.fn(),
    warn: jest.fn(),
    error: jest.fn(),
  });

  /**
   * Mock metrics collector for testing.
   */
  const createMockMetrics = () => ({
    recordValidationFailure: jest.fn(),
    recordMessageProcessed: jest.fn(),
  });

  /**
   * Simulate MiddlewareChain execution.
   * Builds middleware chain with validation hook and measures results.
   */
  const executeWithValidationHook = async (message, options = {}) => {
    const logger = options.logger || createMockLogger();
    const metrics = options.metrics || createMockMetrics();

    // Create validation hook
    const validationHook = createValidationHook({ logger, metrics });

    // Mock dispatcher (next middleware)
    const mockDispatcher = async (msg) => ({
      handled: true,
      shouldRelay: false,
      response: {
        messageType: 'bridge:response',
        messageId: msg.messageId,
        success: true,
        data: { result: 'processed' },
      },
    });

    // Execute validation hook with dispatcher as next
    return validationHook(message, mockDispatcher, { logger, metrics });
  };

  // =========================================================================
  // SCENARIO 1: Valid Messages Pass Through
  // =========================================================================

  test('Scenario 1: Valid request passes to dispatcher', async () => {
    // Arrange
    const message = fixtures.validRequestEnvelope;
    let dispatcherCalled = false;

    const mockDispatcher = jest.fn(async (msg) => {
      dispatcherCalled = true;
      return {
        handled: true,
        shouldRelay: false,
        response: { messageType: 'success' },
      };
    });

    const hook = createValidationHook();

    // Act
    const result = await hook(message, mockDispatcher, {});

    // Assert
    expect(dispatcherCalled).toBe(true);
    expect(mockDispatcher).toHaveBeenCalledWith(message);
    expect(result.messageType).toBe('success');
  });

  test('Scenario 2: Valid notification passes through', async () => {
    // Arrange
    const message = fixtures.validNotification;
    const mockDispatcher = jest.fn(async () => ({
      handled: true,
      shouldRelay: false,
      response: { success: true },
    }));

    const hook = createValidationHook();

    // Act
    await hook(message, mockDispatcher, {});

    // Assert
    expect(mockDispatcher).toHaveBeenCalled();
  });

  test('Scenario 3: Valid response passes through', async () => {
    // Arrange
    const message = fixtures.validResponseWithResult;
    const mockDispatcher = jest.fn(async () => ({
      handled: true,
      shouldRelay: false,
      response: { success: true },
    }));

    const hook = createValidationHook();

    // Act
    await hook(message, mockDispatcher, {});

    // Assert
    expect(mockDispatcher).toHaveBeenCalled();
  });

  // =========================================================================
  // SCENARIO 4: Invalid Messages Trigger Error Responses
  // =========================================================================

  test('Scenario 4: Invalid envelope blocks dispatch and returns error', async () => {
    // Arrange
    const message = {
      messageId: 'msg-invalid',
      // Missing messageType
      data: { method: 'test' },
    };

    let dispatcherCalled = false;
    const mockDispatcher = jest.fn(() => {
      dispatcherCalled = true;
      return {};
    });

    const hook = createValidationHook();

    // Act
    const result = await hook(message, mockDispatcher, {});

    // Assert
    expect(dispatcherCalled).toBe(false);
    expect(mockDispatcher).not.toHaveBeenCalled();
    expect(result.handled).toBe(true);
    expect(result.response.messageType).toBe('rpc:error');
    expect(result.response.data.error.code).toBe(-32600);
  });

  test('Scenario 5: Invalid request payload returns error', async () => {
    // Arrange
    const message = {
      messageType: 'bridge:test',
      messageId: 'msg-invalid',
      data: {
        // Missing method
        params: {},
        id: 1,
      },
    };

    const mockDispatcher = jest.fn();
    const hook = createValidationHook();

    // Act
    const result = await hook(message, mockDispatcher, {});

    // Assert
    expect(result.response.messageType).toBe('rpc:error');
    expect(result.response.data.error.code).toBe(-32600);
    expect(result.response.data.error.message).toContain('method');
  });

  test('Scenario 6: Invalid response payload returns error', async () => {
    // Arrange
    const message = {
      messageType: 'bridge:response',
      messageId: 'msg-invalid',
      data: {
        id: 1,
        // Neither result nor error
      },
    };

    const mockDispatcher = jest.fn();
    const hook = createValidationHook();

    // Act
    const result = await hook(message, mockDispatcher, {});

    // Assert
    expect(result.response.messageType).toBe('rpc:error');
    expect(result.response.data.error.code).toBe(-32603);
  });

  // =========================================================================
  // SCENARIO 7: Metrics Recording
  // =========================================================================

  test('Scenario 7: Metrics recorded for validation failures', async () => {
    // Arrange
    const metrics = createMockMetrics();
    const message = {
      messageType: 'bridge:test',
      messageId: 'msg-001',
      data: {
        // Missing method
        id: 1,
      },
    };

    const hook = createValidationHook({ metrics });

    // Act
    await hook(message, jest.fn(), { metrics });

    // Assert
    expect(metrics.recordValidationFailure).toHaveBeenCalledWith('payload', -32600);
  });

  test('Scenario 8: Envelope validation failure recorded', async () => {
    // Arrange
    const metrics = createMockMetrics();
    const message = {
      messageId: 'msg-001',
      // Missing messageType
      data: {},
    };

    const hook = createValidationHook({ metrics });

    // Act
    await hook(message, jest.fn(), { metrics });

    // Assert
    expect(metrics.recordValidationFailure).toHaveBeenCalledWith('envelope', -32600);
  });

  // =========================================================================
  // SCENARIO 9: Logging
  // =========================================================================

  test('Scenario 9: Logger records validation warnings', async () => {
    // Arrange
    const logger = createMockLogger();
    const message = {
      messageType: 'bridge:test',
      messageId: 'msg-001',
      data: {
        // Missing method
        id: 1,
      },
    };

    const hook = createValidationHook({ logger });

    // Act
    await hook(message, jest.fn(), { logger });

    // Assert
    expect(logger.warn).toHaveBeenCalled();
    const warnCall = logger.warn.mock.calls[0][0];
    expect(warnCall).toContain('Validation failed');
    expect(warnCall).toContain('msg-001');
  });

  // =========================================================================
  // SCENARIO 10: Batch Processing (Mixed Valid/Invalid)
  // =========================================================================

  test('Scenario 10: Batch of 10 mixed messages (5 valid, 5 invalid)', async () => {
    // Arrange
    const messages = [
      fixtures.validRequestEnvelope,
      { messageId: 'msg-invalid-1', data: {} }, // Missing messageType
      fixtures.validNotification,
      { messageType: 'bridge:test', messageId: 'msg-invalid-2', data: { id: 1 } }, // Missing method
      fixtures.validResponseWithResult,
      { messageType: 'bridge:test', messageId: 'msg-invalid-3', data: { method: null } }, // Null method
      fixtures.validResponseWithError,
      { messageType: 'bridge:test', messageId: 'msg-invalid-4', data: { method: 'test', params: 'string' } }, // Invalid params
      fixtures.validRequestEnvelopeNoParams,
      { messageType: 'bridge:test', messageId: 'msg-invalid-5', data: { id: 1 } }, // Neither result nor error
    ];

    const metrics = createMockMetrics();
    const logger = createMockLogger();
    const hook = createValidationHook({ metrics, logger });

    let dispatchedCount = 0;
    const mockDispatcher = jest.fn(async () => {
      dispatchedCount++;
      return { handled: true, shouldRelay: false, response: { success: true } };
    });

    // Act
    const results = [];
    for (const msg of messages) {
      const result = await hook(msg, mockDispatcher, { metrics, logger });
      results.push(result);
    }

    // Assert
    expect(dispatchedCount).toBe(5); // Only valid messages reach dispatcher
    expect(mockDispatcher).toHaveBeenCalledTimes(5);
    expect(metrics.recordValidationFailure).toHaveBeenCalledTimes(5); // 5 failures
    expect(logger.warn).toHaveBeenCalledTimes(5); // 5 warnings

    // Verify error responses for invalid messages
    const invalidResults = results.filter((r) => r.handled && r.response.messageType === 'rpc:error');
    expect(invalidResults).toHaveLength(5);
  });

  // =========================================================================
  // SCENARIO 11: Error Response Correlation
  // =========================================================================

  test('Scenario 11: Error response preserves original messageId for correlation', async () => {
    // Arrange
    const message = {
      messageType: 'bridge:test',
      messageId: 'correlation-trace-xyz-123',
      data: {
        // Missing method
      },
    };

    const hook = createValidationHook();

    // Act
    const result = await hook(message, jest.fn(), {});

    // Assert
    expect(result.response.messageId).toBe('correlation-trace-xyz-123');
    expect(result.response.data.originalMessage).toEqual(message);
  });

  // =========================================================================
  // SCENARIO 12: All Fixture Types
  // =========================================================================

  test('Scenario 12: All valid fixtures pass through', async () => {
    // Arrange
    const validMessages = [
      fixtures.validRequestEnvelope,
      fixtures.validRequestEnvelopeNoParams,
      fixtures.validNotification,
      fixtures.validResponseWithResult,
      fixtures.validResponseWithError,
      fixtures.validResponseWithArrayResult,
      fixtures.validResponseWithNullResult,
    ];

    const hook = createValidationHook();
    let passCount = 0;

    const mockDispatcher = jest.fn(async () => {
      passCount++;
      return { handled: true, shouldRelay: false, response: { success: true } };
    });

    // Act
    for (const msg of validMessages) {
      await hook(msg, mockDispatcher, {});
    }

    // Assert
    expect(passCount).toBe(validMessages.length);
    expect(mockDispatcher).toHaveBeenCalledTimes(validMessages.length);
  });
});
