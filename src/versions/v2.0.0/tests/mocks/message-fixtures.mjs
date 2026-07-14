#!/usr/bin/env node

/**
 * Message Fixtures for Validation Hook Tests
 *
 * Provides reusable test data for validation-hook.test.mjs:
 * - Valid envelope and payload examples
 * - Invalid message variants with error signatures
 * - Edge cases for boundary testing
 *
 * @module src/versions/v2.0.0/tests/mocks/message-fixtures.mjs
 * @version 1.0.0
 */

/**
 * Valid request message envelope with all fields.
 */
export const validRequestEnvelope = {
  messageType: 'bridge:getEditorState',
  messageId: 'msg-001',
  data: {
    method: 'getEditorState',
    params: { includeStack: true },
    id: 1,
  },
};

/**
 * Valid request message envelope without params.
 */
export const validRequestEnvelopeNoParams = {
  messageType: 'bridge:search',
  messageId: 'msg-002',
  data: {
    method: 'search',
    id: 2,
  },
};

/**
 * Valid notification (request without id).
 */
export const validNotification = {
  messageType: 'bridge:onEditorStateChange',
  messageId: 'msg-003',
  data: {
    method: 'onEditorStateChange',
    params: { state: 'paused' },
  },
};

/**
 * Valid response message with result.
 */
export const validResponseWithResult = {
  messageType: 'bridge:response',
  messageId: 'msg-004',
  data: {
    id: 1,
    result: { success: true, data: { file: 'test.js', line: 42 } },
  },
};

/**
 * Valid response message with error.
 */
export const validResponseWithError = {
  messageType: 'bridge:response',
  messageId: 'msg-005',
  data: {
    id: 2,
    error: { code: -32601, message: 'Method not found' },
  },
};

/**
 * Valid response with result as array.
 */
export const validResponseWithArrayResult = {
  messageType: 'bridge:response',
  messageId: 'msg-006',
  data: {
    id: 3,
    result: [{ name: 'item1' }, { name: 'item2' }],
  },
};

/**
 * Valid response with result as null.
 */
export const validResponseWithNullResult = {
  messageType: 'bridge:response',
  messageId: 'msg-007',
  data: {
    id: 4,
    result: null,
  },
};

/**
 * Collection of invalid envelope examples.
 * Each has: { description, message, expectedError, expectedCode }
 */
export const invalidEnvelopes = [
  {
    description: 'null message',
    message: null,
    expectedError: 'null or undefined',
    expectedCode: -32700,
  },
  {
    description: 'undefined message',
    message: undefined,
    expectedError: 'null or undefined',
    expectedCode: -32700,
  },
  {
    description: 'message is string',
    message: 'not an object',
    expectedError: 'must be an object',
    expectedCode: -32700,
  },
  {
    description: 'missing messageType',
    message: {
      messageId: 'msg-008',
      data: { method: 'test' },
    },
    expectedError: 'messageType',
    expectedCode: -32600,
  },
  {
    description: 'empty messageType',
    message: {
      messageType: '',
      messageId: 'msg-009',
      data: { method: 'test' },
    },
    expectedError: 'messageType',
    expectedCode: -32600,
  },
  {
    description: 'null messageType',
    message: {
      messageType: null,
      messageId: 'msg-010',
      data: { method: 'test' },
    },
    expectedError: 'messageType',
    expectedCode: -32600,
  },
  {
    description: 'missing messageId',
    message: {
      messageType: 'bridge:test',
      data: { method: 'test' },
    },
    expectedError: 'messageId',
    expectedCode: -32600,
  },
  {
    description: 'empty messageId',
    message: {
      messageType: 'bridge:test',
      messageId: '',
      data: { method: 'test' },
    },
    expectedError: 'messageId',
    expectedCode: -32600,
  },
  {
    description: 'missing data',
    message: {
      messageType: 'bridge:test',
      messageId: 'msg-011',
    },
    expectedError: 'data',
    expectedCode: -32600,
  },
  {
    description: 'data is null',
    message: {
      messageType: 'bridge:test',
      messageId: 'msg-012',
      data: null,
    },
    expectedError: 'data',
    expectedCode: -32600,
  },
  {
    description: 'data is string',
    message: {
      messageType: 'bridge:test',
      messageId: 'msg-013',
      data: 'not an object',
    },
    expectedError: 'data',
    expectedCode: -32600,
  },
];

/**
 * Collection of invalid request payload examples.
 * Each has: { description, data, expectedError, expectedCode }
 */
export const invalidRequestPayloads = [
  {
    description: 'missing method',
    data: { params: {}, id: 1 },
    expectedError: 'method',
    expectedCode: -32600,
  },
  {
    description: 'null method',
    data: { method: null, id: 1 },
    expectedError: 'method',
    expectedCode: -32600,
  },
  {
    description: 'empty method string',
    data: { method: '', id: 1 },
    expectedError: 'method',
    expectedCode: -32600,
  },
  {
    description: 'method is number',
    data: { method: 123, id: 1 },
    expectedError: 'method',
    expectedCode: -32600,
  },
  {
    description: 'params is string',
    data: { method: 'test', params: 'invalid', id: 1 },
    expectedError: 'params',
    expectedCode: -32602,
  },
  {
    description: 'params is boolean',
    data: { method: 'test', params: true, id: 1 },
    expectedError: 'params',
    expectedCode: -32602,
  },
  {
    description: 'params is number',
    data: { method: 'test', params: 42, id: 1 },
    expectedError: 'params',
    expectedCode: -32602,
  },
  {
    description: 'id is boolean',
    data: { method: 'test', id: true },
    expectedError: 'id',
    expectedCode: -32600,
  },
  {
    description: 'id is object',
    data: { method: 'test', id: {} },
    expectedError: 'id',
    expectedCode: -32600,
  },
];

/**
 * Collection of invalid response payload examples.
 * Each has: { description, data, expectedError, expectedCode }
 */
export const invalidResponsePayloads = [
  {
    description: 'neither result nor error',
    data: { id: 1 },
    expectedError: 'result or error',
    expectedCode: -32603,
  },
  {
    description: 'both result and error',
    data: { id: 1, result: 'value', error: { code: -1, message: 'err' } },
    expectedError: 'result or error, not both',
    expectedCode: -32603,
  },
  {
    description: 'error is not object',
    data: { id: 1, error: 'string error' },
    expectedError: 'error must be an object',
    expectedCode: -32603,
  },
  {
    description: 'error missing code',
    data: { id: 1, error: { message: 'err' } },
    expectedError: 'code',
    expectedCode: -32603,
  },
  {
    description: 'error code is string',
    data: { id: 1, error: { code: '123', message: 'err' } },
    expectedError: 'code',
    expectedCode: -32603,
  },
  {
    description: 'error missing message',
    data: { id: 1, error: { code: -1 } },
    expectedError: 'message',
    expectedCode: -32603,
  },
  {
    description: 'error message is number',
    data: { id: 1, error: { code: -1, message: 123 } },
    expectedError: 'message',
    expectedCode: -32603,
  },
];

/**
 * Valid request payloads.
 * Each has: { description, data }
 */
export const validRequestPayloads = [
  {
    description: 'minimal request (method only)',
    data: { method: 'test' },
  },
  {
    description: 'request with object params',
    data: { method: 'search', params: { query: 'hello' } },
  },
  {
    description: 'request with array params',
    data: { method: 'batch', params: [1, 2, 3] },
  },
  {
    description: 'request with empty object params',
    data: { method: 'init', params: {} },
  },
  {
    description: 'request with id',
    data: { method: 'query', id: 42 },
  },
  {
    description: 'request with string id',
    data: { method: 'query', id: 'request-123' },
  },
  {
    description: 'request with params and id',
    data: { method: 'save', params: { data: 'value' }, id: 99 },
  },
];

/**
 * Valid response payloads.
 * Each has: { description, data }
 */
export const validResponsePayloads = [
  {
    description: 'response with object result',
    data: { id: 1, result: { success: true } },
  },
  {
    description: 'response with array result',
    data: { id: 2, result: [1, 2, 3] },
  },
  {
    description: 'response with string result',
    data: { id: 3, result: 'done' },
  },
  {
    description: 'response with null result',
    data: { id: 4, result: null },
  },
  {
    description: 'response with number result',
    data: { id: 5, result: 42 },
  },
  {
    description: 'response with boolean result',
    data: { id: 6, result: false },
  },
  {
    description: 'response with standard error',
    data: {
      id: 7,
      error: { code: -32601, message: 'Method not found' },
    },
  },
  {
    description: 'response with custom error code',
    data: {
      id: 8,
      error: { code: -32100, message: 'Custom application error' },
    },
  },
];

/**
 * Export all fixtures as default object.
 */
export default {
  // Valid envelopes
  validRequestEnvelope,
  validRequestEnvelopeNoParams,
  validNotification,
  validResponseWithResult,
  validResponseWithError,
  validResponseWithArrayResult,
  validResponseWithNullResult,

  // Invalid envelopes
  invalidEnvelopes,

  // Invalid payloads
  invalidRequestPayloads,
  invalidResponsePayloads,

  // Valid payloads
  validRequestPayloads,
  validResponsePayloads,
};
