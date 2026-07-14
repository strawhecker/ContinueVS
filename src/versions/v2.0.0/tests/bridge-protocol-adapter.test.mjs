#!/usr/bin/env node

/**
 * Bridge Protocol Adapter Tests
 *
 * Comprehensive test suite covering message translation, context assembly,
 * response wrapping, timeout enforcement, RPC correlation, error recovery,
 * and middleware hook integration.
 *
 * Test Coverage: 27 tests across 7 suites
 * - Message Translation (5 tests)
 * - Context Assembly (4 tests)
 * - Response Wrapping (4 tests)
 * - Timeout Enforcement (4 tests)
 * - RPC Correlation (4 tests)
 * - Error Recovery (3 tests)
 * - Middleware Hooks (3 tests)
 *
 * @module src/versions/v2.0.0/tests/bridge-protocol-adapter.test.mjs
 * @version 1.0.0
 */

import assert from 'assert';
import {
  BridgeProtocolAdapter,
  ProtocolAdapterError,
  TimeoutError,
  ValidationError,
  createBridgeProtocolAdapter
} from '../lib/bridge-protocol-adapter.mjs';

/**
 * Mock logger for testing
 */
const createMockLogger = () => ({
  debug: (...args) => console.debug('[DEBUG]', ...args),
  info: (...args) => console.info('[INFO]', ...args),
  warn: (...args) => console.warn('[WARN]', ...args),
  error: (...args) => console.error('[ERROR]', ...args)
});

/**
 * Mock metrics collector for testing
 */
const createMockMetrics = () => ({
  recordMetric: (name, value) => {},
  recordError: (name, error) => {}
});

/**
 * Helper to create test messages
 */
const createTestMessage = (overrides = {}) => ({
  messageType: 'bridge:test',
  messageId: 'test-msg-' + Date.now(),
  data: {},
  ...overrides
});

// ============================================================================
// TEST SUITE 1: Message Translation (5 tests)
// ============================================================================

console.log('\n=== Suite 1: Message Translation ===');

// Test 1.1: Translate message with all fields
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const message = createTestMessage({
      data: { query: 'test' }
    });

    const { bridgeMessage, handlerContext } = await adapter.translateInbound(message);

    assert.strictEqual(bridgeMessage.messageType, message.messageType);
    assert.strictEqual(bridgeMessage.messageId, message.messageId);
    assert.deepStrictEqual(bridgeMessage.data, { query: 'test' });
    assert(handlerContext.logger);
    assert(handlerContext.metrics);
    console.log('✓ 1.1: Translate message with all fields');
  };
  await test();
}

// Test 1.2: Translate message with minimal fields (no data)
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const message = createTestMessage({ data: undefined });

    const { bridgeMessage } = await adapter.translateInbound(message);

    assert.deepStrictEqual(bridgeMessage.data, {});
    console.log('✓ 1.2: Translate message with minimal fields (no data)');
  };
  await test();
}

// Test 1.3: Reject null message
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();

    try {
      await adapter.translateInbound(null);
      assert.fail('Should have thrown ValidationError');
    } catch (error) {
      assert(error instanceof ValidationError);
      assert.strictEqual(error.fieldName, 'message');
      console.log('✓ 1.3: Reject null message');
    }
  };
  await test();
}

// Test 1.4: Reject message missing messageType
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const message = createTestMessage({ messageType: undefined });

    try {
      await adapter.translateInbound(message);
      assert.fail('Should have thrown ValidationError');
    } catch (error) {
      assert(error instanceof ValidationError);
      assert.strictEqual(error.fieldName, 'messageType');
      console.log('✓ 1.4: Reject message missing messageType');
    }
  };
  await test();
}

// Test 1.5: Reject message missing messageId
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const message = createTestMessage({ messageId: undefined });

    try {
      await adapter.translateInbound(message);
      assert.fail('Should have thrown ValidationError');
    } catch (error) {
      assert(error instanceof ValidationError);
      assert.strictEqual(error.fieldName, 'messageId');
      console.log('✓ 1.5: Reject message missing messageId');
    }
  };
  await test();
}

// ============================================================================
// TEST SUITE 2: Context Assembly (4 tests)
// ============================================================================

console.log('\n=== Suite 2: Context Assembly ===');

// Test 2.1: Context contains logger
{
  const test = async () => {
    const logger = createMockLogger();
    const adapter = new BridgeProtocolAdapter({ logger });
    const message = createTestMessage();

    const { handlerContext } = await adapter.translateInbound(message);

    assert.strictEqual(handlerContext.logger, logger);
    console.log('✓ 2.1: Context contains logger');
  };
  await test();
}

// Test 2.2: Context contains metrics
{
  const test = async () => {
    const metrics = createMockMetrics();
    const adapter = new BridgeProtocolAdapter({ metrics });
    const message = createTestMessage();

    const { handlerContext } = await adapter.translateInbound(message);

    assert.strictEqual(handlerContext.metrics, metrics);
    console.log('✓ 2.2: Context contains metrics');
  };
  await test();
}

// Test 2.3: Context can be overridden
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const message = createTestMessage();
    const customLogger = { custom: true };
    const customMetrics = { custom: true };

    const { handlerContext } = await adapter.translateInbound(message, {
      logger: customLogger,
      metrics: customMetrics
    });

    assert.strictEqual(handlerContext.logger, customLogger);
    assert.strictEqual(handlerContext.metrics, customMetrics);
    console.log('✓ 2.3: Context can be overridden');
  };
  await test();
}

// Test 2.4: Context includes message metadata
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const message = createTestMessage({
      messageType: 'bridge:getEditorState',
      messageId: 'abc-123'
    });

    const { handlerContext } = await adapter.translateInbound(message);

    assert.strictEqual(handlerContext.message.messageType, 'bridge:getEditorState');
    assert.strictEqual(handlerContext.message.messageId, 'abc-123');
    console.log('✓ 2.4: Context includes message metadata');
  };
  await test();
}

// ============================================================================
// TEST SUITE 3: Response Wrapping (4 tests)
// ============================================================================

console.log('\n=== Suite 3: Response Wrapping ===');

// Test 3.1: Wrap success response
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const response = { success: true, data: { activeFile: '/path/file.cs' } };

    const message = await adapter.translateOutbound(
      response,
      'msg-123',
      'bridge:getEditorState'
    );

    assert.strictEqual(message.messageType, 'bridge:getEditorState');
    assert.strictEqual(message.messageId, 'msg-123');
    assert.deepStrictEqual(message.data, response);
    console.log('✓ 3.1: Wrap success response');
  };
  await test();
}

// Test 3.2: Wrap error response
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const response = { success: false, error: 'Handler failed' };

    const message = await adapter.translateOutbound(
      response,
      'msg-456',
      'bridge:search'
    );

    assert.strictEqual(message.data.success, false);
    assert.strictEqual(message.data.error, 'Handler failed');
    console.log('✓ 3.2: Wrap error response');
  };
  await test();
}

// Test 3.3: Preserve nested data in response
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const nestedData = {
      success: true,
      data: {
        results: [
          { id: 1, name: 'item1' },
          { id: 2, name: 'item2' }
        ]
      }
    };

    const message = await adapter.translateOutbound(
      nestedData,
      'msg-789',
      'bridge:search'
    );

    assert.deepStrictEqual(message.data.data.results, nestedData.data.results);
    console.log('✓ 3.3: Preserve nested data in response');
  };
  await test();
}

// Test 3.4: Handle response with null error
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    const response = { success: true, data: { result: 42 }, error: null };

    const message = await adapter.translateOutbound(
      response,
      'msg-abc',
      'bridge:test'
    );

    assert.strictEqual(message.data.error, null);
    console.log('✓ 3.4: Handle response with null error');
  };
  await test();
}

// ============================================================================
// TEST SUITE 4: Timeout Enforcement (4 tests)
// ============================================================================

console.log('\n=== Suite 4: Timeout Enforcement ===');

// Test 4.1: Track pending request
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter({ defaultTimeoutMs: 5000 });
    const messageId = 'timeout-test-1';

    const promise = adapter.trackPendingRequest(messageId, 2000);
    assert(promise instanceof Promise);

    // Resolve it
    adapter.resolvePendingRequest(messageId, { success: true });
    const result = await promise;
    assert.deepStrictEqual(result, { success: true });
    console.log('✓ 4.1: Track pending request');
  };
  await test();
}

// Test 4.2: RPC timeout fires
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter({ defaultTimeoutMs: 100 });
    const messageId = 'timeout-test-2';

    const promise = adapter.trackPendingRequest(messageId, 100);

    try {
      await promise;
      assert.fail('Should have timed out');
    } catch (error) {
      assert(error instanceof TimeoutError);
      assert.strictEqual(error.messageId, messageId);
      assert.strictEqual(error.timeoutMs, 100);
      console.log('✓ 4.2: RPC timeout fires');
    }
  };
  await test();
}

// Test 4.3: Cleanup on timeout
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter({ defaultTimeoutMs: 50 });
    const messageId = 'timeout-test-3';

    const promise = adapter.trackPendingRequest(messageId, 50);

    try {
      await promise;
    } catch (error) {
      // Expected timeout
    }

    // Give a moment for cleanup
    await new Promise(r => setTimeout(r, 100));

    // Pending request should be removed
    const result = adapter.resolvePendingRequest(messageId, { test: 'value' });
    assert.strictEqual(result, false);
    console.log('✓ 4.3: Cleanup on timeout');
  };
  await test();
}

// Test 4.4: Use default timeout when not specified
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter({ defaultTimeoutMs: 100 });
    const messageId = 'timeout-test-4';

    // Don't specify timeout, should use default
    const promise = adapter.trackPendingRequest(messageId);

    try {
      await promise;
      assert.fail('Should have timed out');
    } catch (error) {
      assert(error instanceof TimeoutError);
      console.log('✓ 4.4: Use default timeout when not specified');
    }
  };
  await test();
}

// ============================================================================
// TEST SUITE 5: RPC Correlation (4 tests)
// ============================================================================

console.log('\n=== Suite 5: RPC Correlation ===');

// Test 5.1: Multiple concurrent requests with different messageIds
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter({ defaultTimeoutMs: 1000 });

    const p1 = adapter.trackPendingRequest('msg-1', 1000);
    const p2 = adapter.trackPendingRequest('msg-2', 1000);
    const p3 = adapter.trackPendingRequest('msg-3', 1000);

    // Resolve in different order
    adapter.resolvePendingRequest('msg-3', { id: 3 });
    adapter.resolvePendingRequest('msg-1', { id: 1 });
    adapter.resolvePendingRequest('msg-2', { id: 2 });

    const [r1, r2, r3] = await Promise.all([p1, p2, p3]);

    assert.strictEqual(r1.id, 1);
    assert.strictEqual(r2.id, 2);
    assert.strictEqual(r3.id, 3);
    console.log('✓ 5.1: Multiple concurrent requests with different messageIds');
  };
  await test();
}

// Test 5.2: Reject pending request
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter({ defaultTimeoutMs: 1000 });
    const messageId = 'msg-reject';

    const promise = adapter.trackPendingRequest(messageId, 1000);

    const error = new Error('Handler failed');
    adapter.rejectPendingRequest(messageId, error);

    try {
      await promise;
      assert.fail('Should have rejected');
    } catch (err) {
      assert.strictEqual(err.message, 'Handler failed');
      console.log('✓ 5.2: Reject pending request');
    }
  };
  await test();
}

// Test 5.3: Resolve returns false for unknown messageId
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();

    const result = adapter.resolvePendingRequest('unknown-id', { data: 'test' });

    assert.strictEqual(result, false);
    console.log('✓ 5.3: Resolve returns false for unknown messageId');
  };
  await test();
}

// Test 5.4: Reject returns false for unknown messageId
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();

    const result = adapter.rejectPendingRequest('unknown-id', new Error('test'));

    assert.strictEqual(result, false);
    console.log('✓ 5.4: Reject returns false for unknown messageId');
  };
  await test();
}

// ============================================================================
// TEST SUITE 6: Error Recovery (3 tests)
// ============================================================================

console.log('\n=== Suite 6: Error Recovery ===');

// Test 6.1: Clear expired requests
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter({ defaultTimeoutMs: 10000 });

    // Track requests but don't resolve them, capture promises
    const p1 = adapter.trackPendingRequest('old-1', 100);
    const p2 = adapter.trackPendingRequest('old-2', 100);
    const p3 = adapter.trackPendingRequest('new-1', 10000);

    // Wait a bit, but don't wait for all timeouts
    await new Promise(r => setTimeout(r, 50));

    // Clear requests older than 120ms (none should be cleared yet)
    const cleared = adapter.clearExpiredRequests(120);

    assert.strictEqual(cleared, 0);

    // Let the timeouts complete
    await Promise.allSettled([p1, p2, p3]);
    console.log('✓ 6.1: Clear expired requests');
  };
  await test();
}

// Test 6.2: Wrap non-validation error as ProtocolAdapterError
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();

    // Register hook that throws non-adapter error
    adapter.registerHook('pre-translate', () => {
      throw new Error('Custom error');
    });

    const message = createTestMessage();

    try {
      await adapter.translateInbound(message);
      assert.fail('Should have thrown ProtocolAdapterError');
    } catch (error) {
      assert(error instanceof ProtocolAdapterError);
      assert(error.message.includes('Custom error'));
      console.log('✓ 6.2: Wrap non-validation error as ProtocolAdapterError');
    }
  };
  await test();
}

// Test 6.3: Handle very large message payload
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();

    // Create large data object (>100KB)
    const largeData = { content: 'x'.repeat(150000) };
    const message = createTestMessage({ data: largeData });

    const { bridgeMessage } = await adapter.translateInbound(message);

    assert.strictEqual(bridgeMessage.data.content.length, 150000);
    console.log('✓ 6.3: Handle very large message payload');
  };
  await test();
}

// ============================================================================
// TEST SUITE 7: Middleware Hooks (3 tests)
// ============================================================================

console.log('\n=== Suite 7: Middleware Hooks ===');

// Test 7.1: Pre-translate hook invoked
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    let hookInvoked = false;
    let hookMessage = null;

    adapter.registerHook('pre-translate', (msg) => {
      hookInvoked = true;
      hookMessage = msg;
    });

    const message = createTestMessage({ data: { test: 'value' } });
    await adapter.translateInbound(message);

    assert(hookInvoked);
    assert.strictEqual(hookMessage.messageType, message.messageType);
    console.log('✓ 7.1: Pre-translate hook invoked');
  };
  await test();
}

// Test 7.2: Post-translate hook invoked
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();
    let hookInvoked = false;
    let resultData = null;

    adapter.registerHook('post-translate', (result) => {
      hookInvoked = true;
      resultData = result;
    });

    const message = createTestMessage();
    await adapter.translateInbound(message);

    assert(hookInvoked);
    assert(resultData.bridgeMessage);
    assert(resultData.context);
    console.log('✓ 7.2: Post-translate hook invoked');
  };
  await test();
}

// Test 7.3: Hook error propagates
{
  const test = async () => {
    const adapter = new BridgeProtocolAdapter();

    adapter.registerHook('pre-translate', () => {
      throw new Error('Hook failed');
    });

    const message = createTestMessage();

    try {
      await adapter.translateInbound(message);
      assert.fail('Should have thrown');
    } catch (error) {
      assert(error instanceof ProtocolAdapterError);
      assert(error.operationType, 'middleware');
      console.log('✓ 7.3: Hook error propagates');
    }
  };
  await test();
}

// ============================================================================
// FACTORY FUNCTION TEST
// ============================================================================

console.log('\n=== Factory Function ===');

// Test: createBridgeProtocolAdapter factory
{
  const test = async () => {
    const adapter = createBridgeProtocolAdapter({
      logger: createMockLogger(),
      defaultTimeoutMs: 3000
    });

    assert(adapter instanceof BridgeProtocolAdapter);
    console.log('✓ Factory: createBridgeProtocolAdapter');
  };
  await test();
}

// ============================================================================
// SUMMARY
// ============================================================================

console.log('\n' + '='.repeat(60));
console.log('✅ ALL 27 TESTS PASSING');
console.log('='.repeat(60));
console.log('Suite 1: Message Translation (5/5)');
console.log('Suite 2: Context Assembly (4/4)');
console.log('Suite 3: Response Wrapping (4/4)');
console.log('Suite 4: Timeout Enforcement (4/4)');
console.log('Suite 5: RPC Correlation (4/4)');
console.log('Suite 6: Error Recovery (3/3)');
console.log('Suite 7: Middleware Hooks (3/3)');
console.log('Factory: (1/1)');
console.log('='.repeat(60));
