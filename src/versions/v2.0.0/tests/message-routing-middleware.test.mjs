#!/usr/bin/env node

/**
 * Test Suite for Message Routing Middleware
 *
 * Comprehensive tests for MiddlewareChain, hook registration, chain execution,
 * error handling, and backward compatibility.
 *
 * Test Coverage:
 *   - Suite 1: Middleware registration & composition (3 tests)
 *   - Suite 2: Chain execution order (4 tests)
 *   - Suite 3: Error handling in middleware (4 tests)
 *   - Suite 4: Hook lifecycle (4 tests)
 *   - Suite 5: Backward compatibility (3 tests)
 *   Total: 18+ tests
 *
 * @module src/versions/v2.0.0/tests/message-routing-middleware.test.mjs
 */

import assert from 'assert';
import {
  MiddlewareChain,
  MiddlewareExecutionError,
  createMiddlewareChain,
  wrapDispatcher,
} from '../lib/message-routing-middleware.mjs';

// ============================================================================
// Mock Logger & Metrics
// ============================================================================

class MockLogger {
  constructor() {
    this.logs = [];
  }

  debug(msg, meta = {}) {
    this.logs.push({ level: 'debug', msg, meta });
  }

  info(msg, meta = {}) {
    this.logs.push({ level: 'info', msg, meta });
  }

  warn(msg, meta = {}) {
    this.logs.push({ level: 'warn', msg, meta });
  }

  error(msg, meta = {}) {
    this.logs.push({ level: 'error', msg, meta });
  }

  clear() {
    this.logs = [];
  }
}

class MockMetrics {
  constructor() {
    this.records = [];
  }

  recordMiddlewareExecution(name, duration) {
    this.records.push({ name, duration });
  }

  recordError(type) {
    this.records.push({ error: type });
  }
}

// ============================================================================
// Mock Dispatcher (HandlerDispatcher)
// ============================================================================

class MockDispatcher {
  constructor(config = {}) {
    this.dispatchCalls = [];
    this.shouldFail = config.shouldFail || false;
    this.failureMessage = config.failureMessage || 'Dispatcher error';
  }

  async dispatch(message, context) {
    this.dispatchCalls.push({ message, context, timestamp: Date.now() });

    if (this.shouldFail) {
      throw new Error(this.failureMessage);
    }

    return {
      handled: message.messageType.startsWith('bridge:'),
      shouldRelay: !message.messageType.startsWith('bridge:'),
      response: {
        messageType: message.messageType,
        messageId: message.messageId,
        success: true,
        data: { processed: true },
      },
    };
  }
}

// ============================================================================
// SUITE 1: Middleware Registration & Composition (3 tests)
// ============================================================================

console.log('\n📋 SUITE 1: Middleware Registration & Composition');

// Test 1.1: Register valid middleware
{
  const chain = new MiddlewareChain();
  const middleware = async (msg, next) => next();

  chain.use(middleware);
  assert.strictEqual(chain.middlewares.length, 1, 'Middleware should be registered');
  console.log('  ✓ Test 1.1: Register valid middleware');
}

// Test 1.2: Prevent duplicate middleware registration (no-op, but tracked)
{
  const chain = new MiddlewareChain();
  const mw1 = async (msg, next) => next();
  const mw2 = async (msg, next) => next();

  chain.use(mw1);
  chain.use(mw2);

  assert.strictEqual(chain.middlewares.length, 2, 'Both middleware should be registered');
  console.log('  ✓ Test 1.2: Register multiple middleware in order');
}

// Test 1.3: Reject invalid middleware (not a function)
{
  const chain = new MiddlewareChain();
  let errorThrown = false;

  try {
    chain.use('not a function');
  } catch (err) {
    errorThrown = true;
    assert.ok(
      err.message.includes('Invalid middleware'),
      'Should throw descriptive error'
    );
  }

  assert.ok(errorThrown, 'Should throw error for non-function middleware');
  console.log('  ✓ Test 1.3: Reject invalid middleware');
}

// ============================================================================
// SUITE 2: Chain Execution Order (4 tests)
// ============================================================================

console.log('\n📋 SUITE 2: Chain Execution Order');

// Test 2.1: Execute middleware in FIFO order
await (async () => {
  const chain = new MiddlewareChain();
  const execution = [];

  const mw1 = async (msg, next) => {
    execution.push('mw1-pre');
    const result = await next();
    execution.push('mw1-post');
    return result;
  };

  const mw2 = async (msg, next) => {
    execution.push('mw2-pre');
    const result = await next();
    execution.push('mw2-post');
    return result;
  };

  chain.use(mw1);
  chain.use(mw2);

  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-1', data: {} };

  await chain.execute(message, dispatcher);
  assert.deepStrictEqual(
    execution,
    ['mw1-pre', 'mw2-pre', 'mw2-post', 'mw1-post'],
    'Middleware should execute in FIFO order (pre-phase), then LIFO unwinding (post-phase)'
  );
  console.log('  ✓ Test 2.1: Execute middleware in FIFO order (with post-phase unwinding)');
})();

// Test 2.2: Dispatcher called after all middleware
await (async () => {
  const chain = new MiddlewareChain();
  const mw1 = async (msg, next) => next();

  chain.use(mw1);

  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-2', data: {} };

  await chain.execute(message, dispatcher);
  assert.strictEqual(dispatcher.dispatchCalls.length, 1, 'Dispatcher should be called once');
  console.log('  ✓ Test 2.2: Dispatcher called after all middleware');
})();

// Test 2.3: Empty middleware chain still calls dispatcher
await (async () => {
  const chain = new MiddlewareChain();
  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-3', data: {} };

  const result = await chain.execute(message, dispatcher);
  assert.strictEqual(dispatcher.dispatchCalls.length, 1, 'Dispatcher should be called');
  assert.ok(result.response, 'Response should exist');
  console.log('  ✓ Test 2.3: Empty middleware chain calls dispatcher');
})();

// Test 2.4: Middleware can modify message before dispatch
await (async () => {
  const chain = new MiddlewareChain();
  const modifyingMw = async (msg, next) => {
    msg.modified = true;
    return next();
  };

  chain.use(modifyingMw);

  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-4', data: {} };

  await chain.execute(message, dispatcher);
  const dispatchedMsg = dispatcher.dispatchCalls[0].message;
  assert.ok(dispatchedMsg.modified, 'Message modification should propagate to dispatcher');
  console.log('  ✓ Test 2.4: Middleware can modify message before dispatch');
})();

// ============================================================================
// SUITE 3: Error Handling in Middleware (4 tests)
// ============================================================================

console.log('\n📋 SUITE 3: Error Handling in Middleware');

// Test 3.1: Middleware error is wrapped in MiddlewareExecutionError
await (async () => {
  const chain = new MiddlewareChain();
  const errorMw = async (msg, next) => {
    throw new Error('Middleware error');
  };

  chain.use(errorMw);

  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-5', data: {} };

  try {
    await chain.execute(message, dispatcher);
    throw new Error('Should have thrown');
  } catch (err) {
    assert.ok(err instanceof MiddlewareExecutionError, 'Should throw MiddlewareExecutionError');
    assert.ok(err.message.includes('Middleware error'), 'Should contain original message');
    console.log('  ✓ Test 3.1: Middleware error is wrapped correctly');
  }
})();

// Test 3.2: Dispatcher error propagates through middleware
await (async () => {
  const chain = new MiddlewareChain();
  const passthruMw = async (msg, next) => next();

  chain.use(passthruMw);

  const dispatcher = new MockDispatcher({ shouldFail: true, failureMessage: 'Dispatcher failed' });
  const message = { messageType: 'bridge:test', messageId: 'msg-6', data: {} };

  try {
    await chain.execute(message, dispatcher);
    throw new Error('Should have thrown');
  } catch (err) {
    assert.ok(err instanceof MiddlewareExecutionError, 'Should wrap dispatcher error');
    console.log('  ✓ Test 3.2: Dispatcher error propagates');
  }
})();

// Test 3.3: Middleware can handle errors from next()
await (async () => {
  const chain = new MiddlewareChain();
  const errorHandlingMw = async (msg, next) => {
    try {
      return await next();
    } catch (err) {
      // Handle error and return safe response
      return {
        handled: true,
        shouldRelay: false,
        response: {
          messageType: msg.messageType,
          messageId: msg.messageId,
          success: false,
          error: 'Handled by middleware',
        },
      };
    }
  };

  chain.use(errorHandlingMw);

  const dispatcher = new MockDispatcher({ shouldFail: true });
  const message = { messageType: 'bridge:test', messageId: 'msg-7', data: {} };

  const result = await chain.execute(message, dispatcher);
  assert.ok(!result.response.success, 'Response should be failure');
  assert.strictEqual(result.response.error, 'Handled by middleware', 'Error should be handled');
  console.log('  ✓ Test 3.3: Middleware can handle errors from next()');
})();

// Test 3.4: Multiple middleware can handle errors sequentially
await (async () => {
  const chain = new MiddlewareChain();
  const outer = async (msg, next) => {
    try {
      return await next();
    } catch (err) {
      return { recovered: true };
    }
  };

  const inner = async (msg, next) => {
    throw new Error('Inner error');
  };

  chain.use(outer);
  chain.use(inner);

  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-8', data: {} };

  const result = await chain.execute(message, dispatcher);
  assert.ok(result.recovered, 'Outer middleware should catch and recover');
  console.log('  ✓ Test 3.4: Multiple middleware can handle errors');
})();

// ============================================================================
// SUITE 4: Hook Lifecycle (4 tests)
// ============================================================================

console.log('\n📋 SUITE 4: Hook Lifecycle');

// Test 4.1: Register validation hook
{
  const chain = new MiddlewareChain();
  const validationHook = async (msg, next) => {
    msg.validated = true;
    return next();
  };

  chain.registerHook('validationHook', validationHook);
  const hooks = chain.listHooks();

  assert.ok(hooks.validationHook, 'validationHook should be registered');
  console.log('  ✓ Test 4.1: Register validation hook');
}

// Test 4.2: Validation hook executes before user middleware
await (async () => {
  const execution = [];
  const chain = new MiddlewareChain();

  const validationHook = async (msg, next) => {
    execution.push('validation');
    return next();
  };

  const userMw = async (msg, next) => {
    execution.push('user');
    return next();
  };

  chain.registerHook('validationHook', validationHook);
  chain.use(userMw);

  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-9', data: {} };

  await chain.execute(message, dispatcher);
  assert.deepStrictEqual(
    execution,
    ['validation', 'user'],
    'Validation should execute before user middleware'
  );
  console.log('  ✓ Test 4.2: Validation hook executes before user middleware');
})();

// Test 4.3: Logging hook executes after user middleware
await (async () => {
  const execution = [];
  const chain = new MiddlewareChain();

  const userMw = async (msg, next) => {
    execution.push('user');
    return next();
  };

  const loggingHook = async (msg, next) => {
    execution.push('logging');
    return next();
  };

  chain.use(userMw);
  chain.registerHook('loggingHook', loggingHook);

  const dispatcher = new MockDispatcher();
  const message = { messageType: 'bridge:test', messageId: 'msg-10', data: {} };

  await chain.execute(message, dispatcher);
  assert.deepStrictEqual(
    execution,
    ['user', 'logging'],
    'User middleware should execute before logging'
  );
  console.log('  ✓ Test 4.3: Logging hook executes in correct order');
})();

// Test 4.4: Reject unknown hook name
{
  const chain = new MiddlewareChain();
  const unknownHook = async () => {};

  let errorThrown = false;
  try {
    chain.registerHook('unknownHook', unknownHook);
  } catch (err) {
    errorThrown = true;
    assert.ok(err.message.includes('Unknown hook'), 'Should reject unknown hook');
  }

  assert.ok(errorThrown, 'Should throw error for unknown hook');
  console.log('  ✓ Test 4.4: Reject unknown hook names');
}

// ============================================================================
// SUITE 5: Backward Compatibility (3 tests)
// ============================================================================

console.log('\n📋 SUITE 5: Backward Compatibility');

// Test 5.1: wrapDispatcher() creates compatible interface
{
  const chain = new MiddlewareChain();
  const dispatcher = new MockDispatcher();

  const wrapped = wrapDispatcher(chain, dispatcher);

  assert.ok(wrapped.dispatch, 'Wrapped dispatcher should have dispatch method');
  assert.ok(wrapped.chain, 'Should expose chain');
  assert.ok(wrapped.originalDispatcher, 'Should expose original dispatcher');
  console.log('  ✓ Test 5.1: wrapDispatcher creates compatible interface');
}

// Test 5.2: createMiddlewareChain() factory works
{
  const logger = new MockLogger();
  const metrics = new MockMetrics();

  const chain = createMiddlewareChain({ logger, metrics });

  assert.ok(chain instanceof MiddlewareChain, 'Factory should create MiddlewareChain');
  console.log('  ✓ Test 5.2: createMiddlewareChain() factory works');
}

// Test 5.3: Wrapped dispatcher response shape unchanged
await (async () => {
  const chain = new MiddlewareChain();
  const dispatcher = new MockDispatcher();
  const wrapped = wrapDispatcher(chain, dispatcher);

  const message = { messageType: 'bridge:test', messageId: 'msg-11', data: {} };

  const result = await wrapped.dispatch(message);
  assert.ok(result.response, 'Response should exist');
  assert.strictEqual(result.response.messageType, 'bridge:test', 'messageType should match');
  assert.strictEqual(result.response.messageId, 'msg-11', 'messageId should match');
  assert.ok(result.response.success, 'success flag should exist');
  console.log('  ✓ Test 5.3: Response shape unchanged');
})();

// ============================================================================
// Summary
// ============================================================================

console.log('\n✅ All 18+ tests completed!');
console.log('\nTest Suites:');
console.log('  Suite 1: Middleware Registration & Composition .......... 3/3 ✓');
console.log('  Suite 2: Chain Execution Order ......................... 4/4 ✓');
console.log('  Suite 3: Error Handling in Middleware .................. 4/4 ✓');
console.log('  Suite 4: Hook Lifecycle ................................ 4/4 ✓');
console.log('  Suite 5: Backward Compatibility ........................ 3/3 ✓');
console.log('\nTotal: 18/18 tests passing');
