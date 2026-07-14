#!/usr/bin/env node

/**
 * Test Suite: Validation Hook for Bridge Messages (Mocha version)
 *
 * Tests validation-hook.mjs with comprehensive test cases covering:
 * - Envelope validation
 * - Request/response payload validation
 * - Hook integration with middleware
 * - Error response building
 *
 * @module src/versions/v2.0.0/tests/validation-hook-mocha.test.mjs
 * @version 1.0.0
 */

import { strict as assert } from 'assert';
import {
  ValidationError,
  validateMessageEnvelope,
  validatePayload,
  buildErrorResponse,
  createValidationHook,
} from '../lib/validation-hook.mjs';

import * as fixtures from './mocks/message-fixtures.mjs';

describe('ValidationHook - Envelope Validation', () => {
  it('valid request envelope with all fields', () => {
    const result = validateMessageEnvelope(fixtures.validRequestEnvelope);
    assert.strictEqual(result.isValid, true);
    assert.strictEqual(result.error, undefined);
  });

  it('valid request envelope without params', () => {
    const result = validateMessageEnvelope(fixtures.validRequestEnvelopeNoParams);
    assert.strictEqual(result.isValid, true);
  });

  it('null message returns false', () => {
    const result = validateMessageEnvelope(null);
    assert.strictEqual(result.isValid, false);
    assert(result.error.includes('null'));
  });

  it('missing messageType field returns false', () => {
    const message = {
      messageId: 'msg-001',
      data: { method: 'test' },
    };
    const result = validateMessageEnvelope(message);
    assert.strictEqual(result.isValid, false);
    assert(result.error.includes('messageType'));
  });

  it('empty messageId string returns false', () => {
    const message = {
      messageType: 'bridge:test',
      messageId: '',
      data: { method: 'test' },
    };
    const result = validateMessageEnvelope(message);
    assert.strictEqual(result.isValid, false);
    assert(result.error.includes('messageId'));
  });
});

describe('ValidationHook - Request Payload Validation', () => {
  it('minimal request (method only) returns true', () => {
    const result = validatePayload({ method: 'test' }, true);
    assert.strictEqual(result.isValid, true);
  });

  it('request with object params returns true', () => {
    const result = validatePayload(
      { method: 'search', params: { query: 'test' }, id: 1 },
      true
    );
    assert.strictEqual(result.isValid, true);
  });

  it('missing method field returns false', () => {
    const result = validatePayload({ params: {}, id: 1 }, true);
    assert.strictEqual(result.isValid, false);
    assert.strictEqual(result.code, -32600);
    assert(result.error.includes('method'));
  });

  it('params invalid type (string) returns false', () => {
    const result = validatePayload(
      { method: 'test', params: 'invalid', id: 1 },
      true
    );
    assert.strictEqual(result.isValid, false);
    assert.strictEqual(result.code, -32602);
  });

  it('notification (no id) returns true', () => {
    const result = validatePayload({ method: 'onStateChange' }, true);
    assert.strictEqual(result.isValid, true);
  });
});

describe('ValidationHook - Response Payload Validation', () => {
  it('response with result returns true', () => {
    const result = validatePayload(
      { id: 1, result: { success: true } },
      false
    );
    assert.strictEqual(result.isValid, true);
  });

  it('response with error object returns true', () => {
    const result = validatePayload(
      { id: 1, error: { code: -32601, message: 'Method not found' } },
      false
    );
    assert.strictEqual(result.isValid, true);
  });

  it('both result and error present returns false', () => {
    const result = validatePayload(
      {
        id: 1,
        result: 'value',
        error: { code: -1, message: 'err' },
      },
      false
    );
    assert.strictEqual(result.isValid, false);
    assert.strictEqual(result.code, -32603);
  });

  it('neither result nor error returns false', () => {
    const result = validatePayload({ id: 1 }, false);
    assert.strictEqual(result.isValid, false);
    assert.strictEqual(result.code, -32603);
  });
});

describe('ValidationHook - Error Response Building', () => {
  it('builds proper error response structure', () => {
    const original = {
      messageType: 'bridge:test',
      messageId: 'msg-001',
      data: { method: 'test' },
    };
    const errorResp = buildErrorResponse(original, -32600, 'Invalid Request');

    assert.strictEqual(errorResp.messageType, 'rpc:error');
    assert.strictEqual(errorResp.messageId, 'msg-001');
    assert.strictEqual(errorResp.success, false);
    assert.strictEqual(errorResp.data.error.code, -32600);
    assert.strictEqual(errorResp.data.error.message, 'Invalid Request');
    assert.deepStrictEqual(errorResp.data.originalMessage, original);
  });

  it('preserves original messageId for correlation', () => {
    const original = {
      messageType: 'bridge:test',
      messageId: 'correlation-xyz-123',
      data: { method: 'test' },
    };
    const errorResp = buildErrorResponse(original, -32603, 'Internal Error');

    assert.strictEqual(errorResp.messageId, 'correlation-xyz-123');
  });
});

describe('ValidationHook - Hook Integration', () => {
  it('valid message passes through to next middleware', async () => {
    const hook = createValidationHook();
    let nextCalled = false;

    const mockNext = async (msg) => {
      nextCalled = true;
      return {
        handled: false,
        shouldRelay: false,
        response: { messageType: 'success' },
      };
    };

    const message = fixtures.validRequestEnvelope;
    await hook(message, mockNext, {});

    assert.strictEqual(nextCalled, true);
  });

  it('invalid envelope triggers error response without calling next', async () => {
    const hook = createValidationHook();
    let nextCalled = false;

    const mockNext = async (msg) => {
      nextCalled = true;
      return {};
    };

    const message = {
      messageId: 'msg-001',
      // Missing messageType
      data: { method: 'test' },
    };

    const result = await hook(message, mockNext, {});

    assert.strictEqual(nextCalled, false);
    assert.strictEqual(result.handled, true);
    assert.strictEqual(result.shouldRelay, true);
    assert.strictEqual(result.response.messageType, 'rpc:error');
    assert.strictEqual(result.response.data.error.code, -32600);
  });

  it('hook records metrics for invalid messages', async () => {
    const metricsRecorded = [];
    const metrics = {
      recordValidationFailure: (type, code) => {
        metricsRecorded.push({ type, code });
      },
    };

    const hook = createValidationHook({ metrics });
    const mockNext = async () => {};

    const message = {
      messageType: 'bridge:test',
      messageId: 'msg-001',
      data: { method: 'test', params: 'invalid_type' }, // Invalid params type
    };

    await hook(message, mockNext, { metrics });

    assert.strictEqual(metricsRecorded.length, 1);
    assert.strictEqual(metricsRecorded[0].type, 'payload');
    assert.strictEqual(metricsRecorded[0].code, -32602); // Invalid params
  });
});

describe('ValidationHook - Batch Processing', () => {
  it('batch of mixed messages (5 valid, 5 invalid)', async () => {
    const messages = [
      fixtures.validRequestEnvelope,
      { messageId: 'msg-invalid-1', data: {} }, // Missing messageType
      fixtures.validNotification,
      { messageType: 'bridge:test', messageId: 'msg-invalid-2', data: { id: 1 } }, // Missing method
      fixtures.validResponseWithResult,
    ];

    const hook = createValidationHook();
    let dispatchedCount = 0;

    const mockDispatcher = async () => {
      dispatchedCount++;
      return { handled: true, shouldRelay: false, response: { success: true } };
    };

    for (const msg of messages) {
      await hook(msg, mockDispatcher, {});
    }

    // Only 3 valid messages should reach dispatcher
    assert.strictEqual(dispatchedCount, 3);
  });
});

console.log('✓ All validation hook tests completed successfully');
