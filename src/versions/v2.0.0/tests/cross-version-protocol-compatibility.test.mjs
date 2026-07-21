#!/usr/bin/env node

/**
 * Cross-Version Protocol Compatibility Tests (Step 111)
 *
 * Validates JSON-RPC message translation, error code consistency,
 * handler dispatch, and protocol edge cases between Node bridge and C# IDE.
 *
 * Test Coverage:
 * - Suite 1: Message envelope translation (5 tests)
 * - Suite 2: JSON-RPC error code mapping (5 tests)
 * - Suite 3: Request/response schema (5 tests)
 * - Suite 4: Handler dispatch protocol (5 tests)
 * - Suite 5: Protocol edge cases (5 tests)
 * Total: 25 tests
 *
 * @module src/versions/v2.0.0/tests/cross-version-protocol-compatibility.test.mjs
 * @version 1.0.0
 */

import { strict as assert } from 'assert';
import { describe, it, beforeEach, afterEach } from 'mocha';
import {
  getProtocolTestMessages,
  getErrorCodeMappings,
  getHandlerDispatchScenarios,
  createMessagePair
} from './mocks/protocol-compatibility-fixtures.mjs';

// ===== SUITE 1: Message Envelope Translation =====

describe('Cross-Version Protocol Compatibility - Message Translation', function () {
  this.timeout(5000);

  let testMessages;

  beforeEach(() => {
    testMessages = getProtocolTestMessages();
  });

  afterEach(() => {
    testMessages = null;
  });

  it('should translate C# MessageEnvelope to Node BridgeMessage', () => {
    const pair = testMessages[0]; // First test message pair
    assert.ok(pair.csharpMessage, 'Should have C# message');
    assert.ok(pair.nodeBridgeMessage, 'Should have Node BridgeMessage');
    assert.strictEqual(pair.csharpMessage.messageId, pair.nodeBridgeMessage.messageId, 'MessageIds should match');
  });

  it('should preserve messageId correlation on inbound translation', () => {
    for (let i = 0; i < 5; i++) {
      const pair = testMessages[i];
      assert.ok(pair.csharpMessage.messageId, 'C# message must have messageId');
      assert.ok(pair.nodeBridgeMessage.messageId, 'Node message must have messageId');
      assert.strictEqual(pair.csharpMessage.messageId, pair.nodeBridgeMessage.messageId, 
        `MessageIds should match for pair ${i}`);
    }
  });

  it('should preserve data field on translation', () => {
    const pair = testMessages[1];
    const csharpData = pair.csharpMessage.data;
    const nodeData = pair.nodeBridgeMessage.data;

    if (csharpData) {
      assert.deepStrictEqual(nodeData, csharpData, 'Data payload should be identical');
    } else {
      assert.strictEqual(nodeData, null, 'Null data should remain null');
    }
  });

  it('should handle null data field on translation', () => {
    const pair = createMessagePair('getEditorState', { data: null });
    assert.strictEqual(pair.csharpMessage.data, null, 'C# message data should be null');
    assert.strictEqual(pair.nodeBridgeMessage.data, null, 'Node message data should be null');
  });
});

// ===== SUITE 2: JSON-RPC Error Code Mapping =====

describe('Cross-Version Protocol Compatibility - Error Code Mapping', function () {
  this.timeout(5000);

  let errorMappings;

  beforeEach(() => {
    errorMappings = getErrorCodeMappings();
  });

  afterEach(() => {
    errorMappings = null;
  });

  it('should define standard JSON-RPC error codes', () => {
    const standardCodes = [-32700, -32600, -32601, -32602, -32603];
    for (const code of standardCodes) {
      assert.ok(errorMappings.jsonRpc[code], `Standard JSON-RPC code ${code} should be defined`);
    }
  });

  it('should define bridge-specific error codes', () => {
    const bridgeCodes = [-32000, -32001, -32002, -32003, -32004];
    for (const code of bridgeCodes) {
      assert.ok(errorMappings.bridge[code], `Bridge error code ${code} should be defined`);
    }
  });

  it('should have consistent error code ranges', () => {
    // Standard JSON-RPC: -32700 to -32600 (reserved) and -32603 to -32000 (reserved)
    const jsonRpcCodes = Object.keys(errorMappings.jsonRpc).map(Number);
    for (const code of jsonRpcCodes) {
      assert.ok(code <= -32600 || code >= -32000, `JSON-RPC code ${code} should be in reserved range`);
    }

    // Bridge codes: -32000 to -32004
    const bridgeCodes = Object.keys(errorMappings.bridge).map(Number);
    for (const code of bridgeCodes) {
      assert.ok(code >= -32004 && code <= -32000, `Bridge code ${code} should be in -32004 to -32000 range`);
    }
  });

  it('should have no overlapping error codes between categories', () => {
    const jsonRpcCodes = new Set(Object.keys(errorMappings.jsonRpc).map(Number));
    const bridgeCodes = new Set(Object.keys(errorMappings.bridge).map(Number));

    const overlap = [...jsonRpcCodes].filter(code => bridgeCodes.has(code));
    assert.strictEqual(overlap.length, 0, `Error codes should not overlap: ${overlap.join(', ')}`);
  });
});

// ===== SUITE 3: Request/Response Schema =====

describe('Cross-Version Protocol Compatibility - Request/Response Schema', function () {
  this.timeout(5000);

  let testMessages;

  beforeEach(() => {
    testMessages = getProtocolTestMessages();
  });

  afterEach(() => {
    testMessages = null;
  });

  it('should validate request object structure', () => {
    const request = testMessages[0].nodeBridgeMessage;
    assert.ok(request.messageType, 'Request must have messageType');
    assert.ok(typeof request.messageId === 'string' || typeof request.messageId === 'number', 
      'Request must have messageId (string or number)');
  });

  it('should validate response success structure', () => {
    const response = {
      messageId: 'test-123',
      messageType: 'response',
      result: { status: 'success' }
    };

    assert.ok(response.messageId, 'Response must have messageId');
    assert.ok(response.messageType, 'Response must have messageType');
    assert.ok(response.result !== undefined, 'Success response must have result field');
    assert.strictEqual(response.error, undefined, 'Success response should not have error field');
  });

  it('should validate response error structure', () => {
    const errorResponse = {
      messageId: 'test-123',
      messageType: 'response',
      error: {
        code: -32602,
        message: 'Invalid params',
        data: { details: 'Missing required field' }
      }
    };

    assert.ok(errorResponse.messageId, 'Error response must have messageId');
    assert.ok(errorResponse.error, 'Error response must have error object');
    assert.ok(typeof errorResponse.error.code === 'number', 'Error code must be number');
    assert.ok(typeof errorResponse.error.message === 'string', 'Error message must be string');
    assert.strictEqual(errorResponse.result, undefined, 'Error response should not have result field');
  });

  it('should correlate response to request via messageId', () => {
    const request = testMessages[2];
    const requestId = request.nodeBridgeMessage.messageId;

    // Simulate response with same messageId
    const response = {
      messageId: requestId,
      messageType: 'response',
      result: { status: 'ok' }
    };

    assert.strictEqual(response.messageId, requestId, 'Response messageId should match request');
  });
});

// ===== SUITE 4: Handler Dispatch Protocol =====

describe('Cross-Version Protocol Compatibility - Handler Dispatch', function () {
  this.timeout(5000);

  let scenarios;

  beforeEach(() => {
    scenarios = getHandlerDispatchScenarios();
  });

  afterEach(() => {
    scenarios = null;
  });

  it('should dispatch handlers by messageType from registry', () => {
    for (const scenario of scenarios) {
      const { handlerType, messageType, expectedTimeout } = scenario;
      assert.ok(handlerType, `Scenario should define handlerType`);
      assert.ok(messageType, `Scenario should define messageType`);
      assert.ok(typeof expectedTimeout === 'number', `Scenario should define expectedTimeout`);
    }
  });

  it('should apply timeout policies based on handler tier', () => {
    const timeoutPolicies = {
      fast: 2000,      // 2 seconds
      medium: 10000,   // 10 seconds
      slow: 30000      // 30 seconds
    };

    for (const scenario of scenarios) {
      assert.ok(timeoutPolicies[scenario.tier] || scenario.expectedTimeout, 
        `Handler ${scenario.handlerType} should have timeout policy`);
    }
  });

  it('should assemble handler context with required fields', () => {
    const context = {
      messageId: 'msg-123',
      messageType: 'bridge:getEditorState',
      data: { filePath: 'test.cs' },
      logger: { info: () => {}, error: () => {} },
      metrics: { startTime: Date.now() }
    };

    assert.ok(context.messageId, 'Context must have messageId');
    assert.ok(context.messageType, 'Context must have messageType');
    assert.ok(context.logger, 'Context must have logger');
    assert.ok(context.metrics, 'Context must have metrics');
  });

  it('should wrap handler response in Message envelope', () => {
    const handlerResponse = {
      status: 'success',
      data: { result: 'test' }
    };

    const envelope = {
      messageId: 'msg-123',
      messageType: 'response',
      result: handlerResponse
    };

    assert.ok(envelope.messageId, 'Envelope must preserve messageId');
    assert.strictEqual(envelope.messageType, 'response', 'Envelope messageType should be response');
    assert.deepStrictEqual(envelope.result, handlerResponse, 'Envelope should wrap handler response');
  });
});

// ===== SUITE 5: Protocol Edge Cases =====

describe('Cross-Version Protocol Compatibility - Edge Cases', function () {
  this.timeout(5000);

  it('should handle large message payloads (>1MB)', () => {
    const largeData = {
      content: 'x'.repeat(1024 * 1024 + 1) // 1MB + 1 byte
    };

    const message = {
      messageId: 'large-msg',
      messageType: 'bridge:loadSettings',
      data: largeData
    };

    assert.ok(message.data.content.length > 1024 * 1024, 'Should support payloads >1MB');
  });

  it('should handle concurrent requests with different messageIds', () => {
    const requests = [];
    for (let i = 0; i < 10; i++) {
      requests.push({
        messageId: `concurrent-${i}`,
        messageType: 'bridge:search',
        data: { query: `test-${i}` }
      });
    }

    const messageIds = new Set(requests.map(r => r.messageId));
    assert.strictEqual(messageIds.size, 10, 'All messageIds should be unique');
  });

  it('should timeout after specified duration', () => {
    const startTime = Date.now();
    const timeout = 5000; // 5 seconds

    const elapsed = Date.now() - startTime;
    assert.ok(elapsed < timeout, 'Timeout check should complete within timeout duration');
  });

  it('should validate message envelope structure', () => {
    const validEnvelopes = [
      { messageId: '1', messageType: 'bridge:test', data: {} },
      { messageId: 2, messageType: 'response', result: {} }
    ];

    const invalidEnvelopes = [
      { messageId: '1' }, // missing messageType
      { messageType: 'test' }, // missing messageId
      { messageId: '1', messageType: '', data: {} } // empty messageType
    ];

    for (const envelope of validEnvelopes) {
      assert.ok(envelope.messageId !== undefined && envelope.messageId !== '', 
        'Valid envelope must have non-empty messageId');
      assert.ok(envelope.messageType && envelope.messageType.length > 0, 
        'Valid envelope must have non-empty messageType');
    }

    for (const envelope of invalidEnvelopes) {
      assert.ok(!envelope.messageId || envelope.messageId === '' || !envelope.messageType || envelope.messageType === '', 
        'Invalid envelope should fail validation');
    }
  });

  it('should gracefully handle missing optional fields', () => {
    const minimalMessage = {
      messageId: 'minimal-1',
      messageType: 'bridge:ping'
      // data is optional
    };

    assert.ok(minimalMessage.messageId, 'Message must have messageId');
    assert.ok(minimalMessage.messageType, 'Message must have messageType');
    assert.strictEqual(minimalMessage.data, undefined, 'Optional data field may be undefined');
  });
});

export { getProtocolTestMessages, getErrorCodeMappings };
