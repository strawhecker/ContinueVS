#!/usr/bin/env node

/**
 * Integration Test: Validation Hook in Middleware Flow (Mocha version)
 *
 * Tests validation-hook.mjs integration with bridge message flow.
 * Verifies end-to-end validation behavior when used in context of middleware chain.
 *
 * @module src/versions/v2.0.0/tests/validation-integration-mocha.test.mjs
 * @version 1.0.0
 */

import { strict as assert } from 'assert';
import { createValidationHook } from '../lib/validation-hook.mjs';
import * as fixtures from './mocks/message-fixtures.mjs';

describe('ValidationHook Integration - Middleware Flow', () => {
  /**
   * Mock logger for testing.
   */
  const createMockLogger = () => ({
    debug: () => {},
    info: () => {},
    warn: () => {},
    error: () => {},
  });

  /**
   * Mock metrics collector for testing.
   */
  const createMockMetrics = () => ({
    recordValidationFailure: () => {},
    recordMessageProcessed: () => {},
  });

  describe('Valid Messages Pass Through', () => {
    it('valid request passes to dispatcher', async () => {
      const message = fixtures.validRequestEnvelope;
      let dispatcherCalled = false;

      const mockDispatcher = async (msg) => {
        dispatcherCalled = true;
        return {
          handled: true,
          shouldRelay: false,
          response: { messageType: 'success' },
        };
      };

      const hook = createValidationHook();
      await hook(message, mockDispatcher, {});

      assert.strictEqual(dispatcherCalled, true);
    });

    it('valid notification passes through', async () => {
      const message = fixtures.validNotification;
      let dispatcherCalled = false;

      const mockDispatcher = async () => {
        dispatcherCalled = true;
        return { handled: true, shouldRelay: false, response: { success: true } };
      };

      const hook = createValidationHook();
      await hook(message, mockDispatcher, {});

      assert.strictEqual(dispatcherCalled, true);
    });

    it('valid response passes through', async () => {
      const message = fixtures.validResponseWithResult;
      let dispatcherCalled = false;

      const mockDispatcher = async () => {
        dispatcherCalled = true;
        return { handled: true, shouldRelay: false, response: { success: true } };
      };

      const hook = createValidationHook();
      await hook(message, mockDispatcher, {});

      assert.strictEqual(dispatcherCalled, true);
    });
  });

  describe('Invalid Messages Trigger Errors', () => {
    it('invalid envelope blocks dispatch', async () => {
      const message = {
        messageId: 'msg-invalid',
        // Missing messageType
        data: { method: 'test' },
      };

      let dispatcherCalled = false;
      const mockDispatcher = async () => {
        dispatcherCalled = true;
        return {};
      };

      const hook = createValidationHook();
      const result = await hook(message, mockDispatcher, {});

      assert.strictEqual(dispatcherCalled, false);
      assert.strictEqual(result.handled, true);
      assert.strictEqual(result.response.messageType, 'rpc:error');
    });

    it('invalid request payload returns error', async () => {
      const message = {
        messageType: 'bridge:test',
        messageId: 'msg-invalid',
        data: {
          method: 'test',
          params: 'invalid_string', // Invalid params type
          id: 1,
        },
      };

      const mockDispatcher = async () => {};
      const hook = createValidationHook();
      const result = await hook(message, mockDispatcher, {});

      assert.strictEqual(result.response.messageType, 'rpc:error');
      assert.strictEqual(result.response.data.error.code, -32602); // Invalid params
    });

    it('invalid response payload returns error', async () => {
      const message = {
        messageType: 'bridge:response',
        messageId: 'msg-invalid',
        data: {
          id: 1,
          // Neither result nor error
        },
      };

      const mockDispatcher = async () => {};
      const hook = createValidationHook();
      const result = await hook(message, mockDispatcher, {});

      assert.strictEqual(result.response.messageType, 'rpc:error');
      assert.strictEqual(result.response.data.error.code, -32603);
    });
  });

  describe('Metrics and Logging', () => {
    it('metrics recorded for validation failures', async () => {
      const failures = [];
      const metrics = {
        recordValidationFailure: (type, code) => {
          failures.push({ type, code });
        },
      };

      const message = {
        messageType: 'bridge:test',
        messageId: 'msg-001',
        data: {
          method: 'test',
          params: 123, // Invalid params type (should be object or array)
          id: 1,
        },
      };

      const hook = createValidationHook({ metrics });
      await hook(message, async () => {}, { metrics });

      assert.strictEqual(failures.length, 1);
      assert.strictEqual(failures[0].type, 'payload');
      assert.strictEqual(failures[0].code, -32602); // Invalid params
    });

    it('envelope validation failure recorded', async () => {
      const failures = [];
      const metrics = {
        recordValidationFailure: (type, code) => {
          failures.push({ type, code });
        },
      };

      const message = {
        messageId: 'msg-001',
        // Missing messageType
        data: {},
      };

      const hook = createValidationHook({ metrics });
      await hook(message, async () => {}, { metrics });

      assert.strictEqual(failures.length, 1);
      assert.strictEqual(failures[0].type, 'envelope');
      assert.strictEqual(failures[0].code, -32600);
    });

    it('logger records validation warnings', async () => {
      const warnings = [];
      const logger = {
        warn: (msg) => {
          warnings.push(msg);
        },
      };

      const message = {
        messageType: 'bridge:test',
        messageId: 'msg-001',
        data: {
          // Missing method
          id: 1,
        },
      };

      const hook = createValidationHook({ logger });
      await hook(message, async () => {}, { logger });

      assert.strictEqual(warnings.length, 1);
      assert(warnings[0].includes('Validation failed'));
      assert(warnings[0].includes('msg-001'));
    });
  });

  describe('All Valid Fixtures Pass', () => {
    it('all valid fixtures reach dispatcher', async () => {
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

      const mockDispatcher = async () => {
        passCount++;
        return { handled: true, shouldRelay: false, response: { success: true } };
      };

      for (const msg of validMessages) {
        await hook(msg, mockDispatcher, {});
      }

      assert.strictEqual(passCount, validMessages.length);
    });
  });

  describe('Error Response Correlation', () => {
    it('error response preserves original messageId', async () => {
      const message = {
        messageType: 'bridge:test',
        messageId: 'correlation-trace-xyz-123',
        data: {
          // Missing method
        },
      };

      const hook = createValidationHook();
      const result = await hook(message, async () => {}, {});

      assert.strictEqual(result.response.messageId, 'correlation-trace-xyz-123');
      assert.deepStrictEqual(result.response.data.originalMessage, message);
    });
  });
});

console.log('✓ All integration tests completed successfully');
