#!/usr/bin/env node

/**
 * Test Suite: Validation Hook for Bridge Messages
 *
 * Tests validation-hook.mjs with 18 test cases covering:
 * - Envelope validation (7 tests: 3 happy path, 4 invalid)
 * - Payload validation (8 tests: 5 requests, 3 responses)
 * - Hook integration (3 tests: happy path, invalid, metrics)
 *
 * @module src/versions/v2.0.0/tests/validation-hook.test.mjs
 * @version 1.0.0
 */

import {
  ValidationError,
  validateMessageEnvelope,
  validatePayload,
  buildErrorResponse,
  createValidationHook,
} from '../lib/validation-hook.mjs';

import * as fixtures from './mocks/message-fixtures.mjs';

describe('ValidationHook', () => {
  // =========================================================================
  // SUITE 1: Message Envelope Validation — Happy Path (3 tests)
  // =========================================================================

  describe('validateMessageEnvelope - Happy Path', () => {
    test('valid request envelope with all fields', () => {
      const result = validateMessageEnvelope(fixtures.validRequestEnvelope);
      expect(result.isValid).toBe(true);
      expect(result.error).toBeUndefined();
      expect(result.code).toBeUndefined();
    });

    test('valid request envelope without params', () => {
      const result = validateMessageEnvelope(
        fixtures.validRequestEnvelopeNoParams
      );
      expect(result.isValid).toBe(true);
    });

    test('valid response envelope', () => {
      const result = validateMessageEnvelope(
        fixtures.validResponseWithResult
      );
      expect(result.isValid).toBe(true);
    });
  });

  // =========================================================================
  // SUITE 2: Message Envelope Validation — Invalid (4 tests)
  // =========================================================================

  describe('validateMessageEnvelope - Invalid', () => {
    test('null or undefined message', () => {
      const nullResult = validateMessageEnvelope(null);
      expect(nullResult.isValid).toBe(false);
      expect(nullResult.code).toBe(-32700);
      expect(nullResult.error).toContain('null');

      const undefinedResult = validateMessageEnvelope(undefined);
      expect(undefinedResult.isValid).toBe(false);
    });

    test('missing messageType field', () => {
      const message = {
        messageId: 'msg-001',
        data: { method: 'test' },
      };
      const result = validateMessageEnvelope(message);
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32600);
      expect(result.error).toContain('messageType');
    });

    test('empty messageId string', () => {
      const message = {
        messageType: 'bridge:test',
        messageId: '',
        data: { method: 'test' },
      };
      const result = validateMessageEnvelope(message);
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32600);
      expect(result.error).toContain('messageId');
    });

    test('data is not an object', () => {
      const message = {
        messageType: 'bridge:test',
        messageId: 'msg-001',
        data: 'string data',
      };
      const result = validateMessageEnvelope(message);
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32600);
      expect(result.error).toContain('data');
    });
  });

  // =========================================================================
  // SUITE 3: Request Payload Validation — Happy Path (3 tests)
  // =========================================================================

  describe('validatePayload - Request Happy Path', () => {
    test('minimal request (method only)', () => {
      const result = validatePayload({ method: 'test' }, true);
      expect(result.isValid).toBe(true);
    });

    test('request with object params', () => {
      const result = validatePayload(
        { method: 'search', params: { query: 'test' }, id: 1 },
        true
      );
      expect(result.isValid).toBe(true);
    });

    test('request with array params (notification)', () => {
      const result = validatePayload(
        { method: 'batch', params: [1, 2, 3] },
        true
      );
      expect(result.isValid).toBe(true);
    });
  });

  // =========================================================================
  // SUITE 4: Request Payload Validation — Invalid (3 tests)
  // =========================================================================

  describe('validatePayload - Request Invalid', () => {
    test('missing method field', () => {
      const result = validatePayload({ params: {}, id: 1 }, true);
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32600);
      expect(result.error).toContain('method');
    });

    test('method is not a string', () => {
      const result = validatePayload({ method: 123, id: 1 }, true);
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32600);
    });

    test('params is invalid type (string)', () => {
      const result = validatePayload(
        { method: 'test', params: 'invalid', id: 1 },
        true
      );
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32602);
      expect(result.error).toContain('params');
    });
  });

  // =========================================================================
  // SUITE 5: Response Payload Validation — Happy Path (2 tests)
  // =========================================================================

  describe('validatePayload - Response Happy Path', () => {
    test('response with result', () => {
      const result = validatePayload(
        { id: 1, result: { success: true } },
        false
      );
      expect(result.isValid).toBe(true);
    });

    test('response with error object', () => {
      const result = validatePayload(
        { id: 1, error: { code: -32601, message: 'Method not found' } },
        false
      );
      expect(result.isValid).toBe(true);
    });
  });

  // =========================================================================
  // SUITE 6: Response Payload Validation — Invalid (2 tests)
  // =========================================================================

  describe('validatePayload - Response Invalid', () => {
    test('both result and error present', () => {
      const result = validatePayload(
        {
          id: 1,
          result: 'value',
          error: { code: -1, message: 'err' },
        },
        false
      );
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32603);
      expect(result.error).toContain('not both');
    });

    test('neither result nor error present', () => {
      const result = validatePayload({ id: 1 }, false);
      expect(result.isValid).toBe(false);
      expect(result.code).toBe(-32603);
      expect(result.error).toContain('either');
    });
  });

  // =========================================================================
  // SUITE 7: Error Response Building (1 test)
  // =========================================================================

  describe('buildErrorResponse', () => {
    test('builds proper error response structure', () => {
      const original = {
        messageType: 'bridge:test',
        messageId: 'msg-001',
        data: { method: 'test' },
      };
      const errorResp = buildErrorResponse(original, -32600, 'Invalid Request');

      expect(errorResp.messageType).toBe('rpc:error');
      expect(errorResp.messageId).toBe('msg-001');
      expect(errorResp.success).toBe(false);
      expect(errorResp.data.error.code).toBe(-32600);
      expect(errorResp.data.error.message).toBe('Invalid Request');
      expect(errorResp.data.originalMessage).toEqual(original);
    });
  });

  // =========================================================================
  // SUITE 8: Validation Hook Integration (3 tests)
  // =========================================================================

  describe('createValidationHook - Integration', () => {
    test('valid message passes through to next middleware', async () => {
      const hook = createValidationHook();
      const mockNext = jest.fn(async (msg) => ({
        handled: false,
        shouldRelay: false,
        response: { messageType: 'success' },
      }));

      const message = fixtures.validRequestEnvelope;
      const result = await hook(message, mockNext, {});

      expect(mockNext).toHaveBeenCalledWith(message);
      expect(result.handled).toBe(false);
    });

    test('invalid envelope triggers error response without calling next', async () => {
      const hook = createValidationHook();
      const mockNext = jest.fn();

      const message = {
        messageId: 'msg-001',
        // Missing messageType
        data: { method: 'test' },
      };

      const result = await hook(message, mockNext, {});

      expect(mockNext).not.toHaveBeenCalled();
      expect(result.handled).toBe(true);
      expect(result.shouldRelay).toBe(true);
      expect(result.response.messageType).toBe('rpc:error');
      expect(result.response.data.error.code).toBe(-32600);
    });

    test('hook records metrics for invalid messages', async () => {
      const metrics = {
        recordValidationFailure: jest.fn(),
      };

      const hook = createValidationHook({ metrics });
      const mockNext = jest.fn();

      const message = {
        messageType: 'bridge:test',
        messageId: 'msg-001',
        data: {}, // Missing method
      };

      await hook(message, mockNext, { metrics });

      expect(metrics.recordValidationFailure).toHaveBeenCalledWith(
        'payload',
        -32600
      );
    });
  });

  // =========================================================================
  // FIXTURE-DRIVEN TESTS (Multi-variant coverage)
  // =========================================================================

  describe('validateMessageEnvelope - All Invalid Envelope Fixtures', () => {
    fixtures.invalidEnvelopes.forEach((fixture) => {
      test(`invalid: ${fixture.description}`, () => {
        const result = validateMessageEnvelope(fixture.message);
        expect(result.isValid).toBe(false);
        expect(result.code).toBe(fixture.expectedCode);
        expect(result.error.toLowerCase()).toContain(
          fixture.expectedError.toLowerCase()
        );
      });
    });
  });

  describe('validatePayload - All Invalid Request Payload Fixtures', () => {
    fixtures.invalidRequestPayloads.forEach((fixture) => {
      test(`invalid request: ${fixture.description}`, () => {
        const result = validatePayload(fixture.data, true);
        expect(result.isValid).toBe(false);
        expect(result.code).toBe(fixture.expectedCode);
        expect(result.error.toLowerCase()).toContain(
          fixture.expectedError.toLowerCase()
        );
      });
    });
  });

  describe('validatePayload - All Invalid Response Payload Fixtures', () => {
    fixtures.invalidResponsePayloads.forEach((fixture) => {
      test(`invalid response: ${fixture.description}`, () => {
        const result = validatePayload(fixture.data, false);
        expect(result.isValid).toBe(false);
        expect(result.code).toBe(fixture.expectedCode);
        expect(result.error.toLowerCase()).toContain(
          fixture.expectedError.toLowerCase()
        );
      });
    });
  });

  describe('validatePayload - All Valid Request Payload Fixtures', () => {
    fixtures.validRequestPayloads.forEach((fixture) => {
      test(`valid request: ${fixture.description}`, () => {
        const result = validatePayload(fixture.data, true);
        expect(result.isValid).toBe(true);
      });
    });
  });

  describe('validatePayload - All Valid Response Payload Fixtures', () => {
    fixtures.validResponsePayloads.forEach((fixture) => {
      test(`valid response: ${fixture.description}`, () => {
        const result = validatePayload(fixture.data, false);
        expect(result.isValid).toBe(true);
      });
    });
  });
});
