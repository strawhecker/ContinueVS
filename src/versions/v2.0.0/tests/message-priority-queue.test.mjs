/**
 * Message Priority Queue Test Suite (Step 65)
 *
 * Comprehensive tests for MessagePriorityQueue binary heap implementation.
 * Validates priority ordering, backpressure, metrics collection, and edge cases.
 *
 * Test Coverage: 23+ tests across 6 suites
 * - Initialization & Configuration (3 tests)
 * - Insertion & Priority Ordering (5 tests)
 * - Extraction & Peek (4 tests)
 * - Backpressure (Pause/Resume) (4 tests)
 * - Metrics Collection (4 tests)
 * - Edge Cases & Degradation (3+ tests)
 *
 * @module src/versions/v2.0.0/tests/message-priority-queue.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

import assert from 'assert';
import {
  MessagePriorityQueue,
  PriorityLevel,
  MessageQueueError,
  createMessagePriorityQueue
} from '../lib/message-priority-queue.mjs';

// ===== TEST UTILITIES =====

/**
 * Mock logger for testing.
 */
class MockLogger {
  constructor() {
    this.logs = [];
  }

  debug(msg, data) { this.logs.push({level: 'debug', msg, data}); }
  info(msg, data) { this.logs.push({level: 'info', msg, data}); }
  warn(msg, data) { this.logs.push({level: 'warn', msg, data}); }
  error(msg, data) { this.logs.push({level: 'error', msg, data}); }
}

/**
 * Mock metrics collector for testing.
 */
class MockMetrics {
  constructor() {
    this.tracked = [];
    this.gauged = [];
  }

  track(name, value) { this.tracked.push({name, value}); }
  gauge(name, value) { this.gauged.push({name, value}); }
}

/**
 * Create a test message.
 */
function createTestMessage(id, type = 'test') {
  return { id, type, timestamp: Date.now() };
}

/**
 * Create a test queue with optional overrides.
 */
function createTestQueue(maxSize = 100, priority = PriorityLevel.NORMAL, logger = null, metrics = null) {
  return createMessagePriorityQueue(maxSize, priority, logger, metrics);
}

/**
 * Sleep for N milliseconds.
 */
function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

// ===== TESTS =====

/**
 * SUITE 1: Initialization & Configuration
 */
console.log('\n=== SUITE 1: Initialization & Configuration ===');

// Test 1.1: Create with defaults
{
  const queue = createTestQueue();
  assert.strictEqual(queue.size(), 0);
  assert.strictEqual(queue.isFull(), false);
  assert.strictEqual(queue.isPaused(), false);
  console.log('✓ Test 1.1: Create with defaults');
}

// Test 1.2: Create with custom maxSize and priority
{
  const queue = createTestQueue(50, PriorityLevel.LOW);
  assert.strictEqual(queue.size(), 0);
  const metrics = queue.getMetrics();
  assert.strictEqual(metrics.maxSize, 50);
  console.log('✓ Test 1.2: Create with custom maxSize and priority');
}

// Test 1.3: Reject invalid maxSize
{
  let errorThrown = false;
  try {
    createMessagePriorityQueue(0, PriorityLevel.NORMAL);
  } catch (err) {
    errorThrown = true;
    assert(err instanceof MessageQueueError);
    assert.strictEqual(err.code, 'INVALID_MAX_SIZE');
  }
  assert(errorThrown);
  console.log('✓ Test 1.3: Reject invalid maxSize');
}

/**
 * SUITE 2: Insertion & Priority Ordering
 */
console.log('\n=== SUITE 2: Insertion & Priority Ordering ===');

// Test 2.1: Insert single message
{
  const queue = createTestQueue();
  const msg = createTestMessage(1);
  const inserted = queue.insert(msg);
  assert.strictEqual(inserted, true);
  assert.strictEqual(queue.size(), 1);
  console.log('✓ Test 2.1: Insert single message');
}

// Test 2.2: Insert multiple, verify extraction order (CRITICAL → NORMAL → LOW)
{
  const queue = createTestQueue();
  const msgs = [
    {id: 'normal', priority: PriorityLevel.NORMAL},
    {id: 'low', priority: PriorityLevel.LOW},
    {id: 'critical', priority: PriorityLevel.CRITICAL}
  ];

  // Insert in random order
  queue.insert(createTestMessage('normal'), PriorityLevel.NORMAL);
  queue.insert(createTestMessage('low'), PriorityLevel.LOW);
  queue.insert(createTestMessage('critical'), PriorityLevel.CRITICAL);

  // Extract in priority order
  const first = queue.extract();
  assert.strictEqual(first.id, 'critical');

  const second = queue.extract();
  assert.strictEqual(second.id, 'normal');

  const third = queue.extract();
  assert.strictEqual(third.id, 'low');

  console.log('✓ Test 2.2: Priority ordering (CRITICAL → NORMAL → LOW)');
}

// Test 2.3: Handle duplicate priorities (FIFO within level)
{
  const queue = createTestQueue();

  queue.insert(createTestMessage('msg1'), PriorityLevel.NORMAL);
  queue.insert(createTestMessage('msg2'), PriorityLevel.NORMAL);
  queue.insert(createTestMessage('msg3'), PriorityLevel.NORMAL);

  const first = queue.extract();
  assert.strictEqual(first.id, 'msg1');

  const second = queue.extract();
  assert.strictEqual(second.id, 'msg2');

  const third = queue.extract();
  assert.strictEqual(third.id, 'msg3');

  console.log('✓ Test 2.3: FIFO within same priority level');
}

// Test 2.4: Reject insertion when full (return false)
{
  const queue = createTestQueue(2);
  queue.insert(createTestMessage('msg1'));
  queue.insert(createTestMessage('msg2'));

  const inserted = queue.insert(createTestMessage('msg3'));
  assert.strictEqual(inserted, false);
  assert.strictEqual(queue.size(), 2);

  console.log('✓ Test 2.4: Reject insertion when full');
}

// Test 2.5: Reject null/invalid message
{
  const queue = createTestQueue();
  let errorThrown = false;

  try {
    queue.insert(null);
  } catch (err) {
    errorThrown = true;
    assert(err instanceof MessageQueueError);
  }

  assert(errorThrown);
  console.log('✓ Test 2.5: Reject null/invalid message');
}

/**
 * SUITE 3: Extraction & Peek
 */
console.log('\n=== SUITE 3: Extraction & Peek ===');

// Test 3.1: Extract high-priority first
{
  const queue = createTestQueue();

  queue.insert(createTestMessage('low'), PriorityLevel.LOW);
  queue.insert(createTestMessage('critical'), PriorityLevel.CRITICAL);
  queue.insert(createTestMessage('normal'), PriorityLevel.NORMAL);

  const extracted = queue.extract();
  assert.strictEqual(extracted.id, 'critical');

  console.log('✓ Test 3.1: Extract high-priority first');
}

// Test 3.2: Peek doesn't remove
{
  const queue = createTestQueue();
  const msg = createTestMessage('test');

  queue.insert(msg);
  const peeked = queue.peek();
  assert.strictEqual(peeked.id, 'test');
  assert.strictEqual(queue.size(), 1);

  const extracted = queue.extract();
  assert.strictEqual(extracted.id, 'test');
  assert.strictEqual(queue.size(), 0);

  console.log('✓ Test 3.2: Peek doesn\'t remove');
}

// Test 3.3: Extract from empty returns null
{
  const queue = createTestQueue();
  const extracted = queue.extract();
  assert.strictEqual(extracted, null);

  console.log('✓ Test 3.3: Extract from empty returns null');
}

// Test 3.4: Extract when paused returns null without removing
{
  const queue = createTestQueue();
  queue.insert(createTestMessage('test'));
  queue.pause();

  const extracted = queue.extract();
  assert.strictEqual(extracted, null);
  assert.strictEqual(queue.size(), 1);

  console.log('✓ Test 3.4: Extract when paused returns null');
}

/**
 * SUITE 4: Backpressure (Pause/Resume)
 */
console.log('\n=== SUITE 4: Backpressure (Pause/Resume) ===');

// Test 4.1: Pause stops extractions
{
  const queue = createTestQueue();
  queue.insert(createTestMessage('msg1'));
  queue.insert(createTestMessage('msg2'));

  queue.pause();
  assert.strictEqual(queue.isPaused(), true);

  const extracted = queue.extract();
  assert.strictEqual(extracted, null);
  assert.strictEqual(queue.size(), 2);

  console.log('✓ Test 4.1: Pause stops extractions');
}

// Test 4.2: Resume resumes extractions
{
  const queue = createTestQueue();
  queue.insert(createTestMessage('msg1'));

  queue.pause();
  queue.resume();
  assert.strictEqual(queue.isPaused(), false);

  const extracted = queue.extract();
  assert.strictEqual(extracted.id, 'msg1');
  assert.strictEqual(queue.size(), 0);

  console.log('✓ Test 4.2: Resume resumes extractions');
}

// Test 4.3: Insert succeeds while paused
{
  const queue = createTestQueue();
  queue.insert(createTestMessage('msg1'));
  queue.pause();

  const inserted = queue.insert(createTestMessage('msg2'));
  assert.strictEqual(inserted, true);
  assert.strictEqual(queue.size(), 2);

  console.log('✓ Test 4.3: Insert succeeds while paused');
}

// Test 4.4: Multiple pause/resume cycles
{
  const queue = createTestQueue();
  queue.insert(createTestMessage('msg1'));

  queue.pause();
  queue.resume();
  queue.pause();
  queue.resume();

  const extracted = queue.extract();
  assert.strictEqual(extracted.id, 'msg1');

  console.log('✓ Test 4.4: Multiple pause/resume cycles');
}

/**
 * SUITE 5: Metrics Collection
 */
console.log('\n=== SUITE 5: Metrics Collection ===');

// Test 5.1: Track insertion count
{
  const queue = createTestQueue(100, PriorityLevel.NORMAL, null, new MockMetrics());

  queue.insert(createTestMessage('msg1'));
  queue.insert(createTestMessage('msg2'));

  const metrics = queue.getMetrics();
  assert.strictEqual(metrics.insertCount, 2);

  console.log('✓ Test 5.1: Track insertion count');
}

// Test 5.2: Track dequeue count
{
  const queue = createTestQueue();

  queue.insert(createTestMessage('msg1'));
  queue.insert(createTestMessage('msg2'));

  queue.extract();
  queue.extract();

  const metrics = queue.getMetrics();
  assert.strictEqual(metrics.dequeueCount, 2);

  console.log('✓ Test 5.2: Track dequeue count');
}

// Test 5.3: Calculate average wait time
{
  const queue = createTestQueue();

  queue.insert(createTestMessage('msg1'));
  await sleep(50);
  const msg = queue.extract();

  const metrics = queue.getMetrics();
  assert(metrics.avgWaitTime >= 40); // Allow ±10ms tolerance

  console.log('✓ Test 5.3: Calculate average wait time');
}

// Test 5.4: Calculate p99 wait time
{
  const queue = createTestQueue();

  // Insert multiple messages with small delays
  for (let i = 0; i < 10; i++) {
    queue.insert(createTestMessage(`msg${i}`));
    if (i < 5) await sleep(5);
  }

  // Extract all
  for (let i = 0; i < 10; i++) {
    queue.extract();
  }

  const metrics = queue.getMetrics();
  assert.strictEqual(metrics.p99WaitTime >= 0, true); // Valid number

  console.log('✓ Test 5.4: Calculate p99 wait time');
}

/**
 * SUITE 6: Edge Cases & Degradation
 */
console.log('\n=== SUITE 6: Edge Cases & Degradation ===');

// Test 6.1: Handle null logger & metrics (no-op)
{
  const queue = createTestQueue(100, PriorityLevel.NORMAL, null, null);
  queue.insert(createTestMessage('test'));
  const extracted = queue.extract();
  assert.strictEqual(extracted.id, 'test');

  console.log('✓ Test 6.1: Handle null logger & metrics');
}

// Test 6.2: Queue size exactly 1
{
  const queue = createTestQueue(1);
  const msg = createTestMessage('only');
  const inserted = queue.insert(msg);
  assert.strictEqual(inserted, true);
  assert.strictEqual(queue.isFull(), true);

  const extracted = queue.extract();
  assert.strictEqual(extracted.id, 'only');
  assert.strictEqual(queue.size(), 0);

  console.log('✓ Test 6.2: Queue size exactly 1');
}

// Test 6.3: Clear removes all messages
{
  const queue = createTestQueue();
  queue.insert(createTestMessage('msg1'));
  queue.insert(createTestMessage('msg2'));
  queue.insert(createTestMessage('msg3'));

  queue.clear();
  assert.strictEqual(queue.size(), 0);
  assert.strictEqual(queue.extract(), null);

  console.log('✓ Test 6.3: Clear removes all messages');
}

// Test 6.4: Dispose cleans up resources
{
  const queue = createTestQueue();
  queue.insert(createTestMessage('msg1'));
  queue.dispose();

  assert.strictEqual(queue.size(), 0);

  console.log('✓ Test 6.4: Dispose cleans up resources');
}

// Test 6.5: Large message ID strings
{
  const queue = createTestQueue();
  const largeId = 'x'.repeat(1000);
  const msg = createTestMessage(largeId);

  const inserted = queue.insert(msg);
  assert.strictEqual(inserted, true);

  const extracted = queue.extract();
  assert.strictEqual(extracted.id, largeId);

  console.log('✓ Test 6.5: Large message ID strings');
}

// Test 6.6: Complex message objects
{
  const queue = createTestQueue();
  const complexMsg = {
    id: 'complex',
    type: 'request',
    data: {
      nested: {
        deep: {
          value: 42
        }
      }
    },
    metadata: [1, 2, 3, 4, 5]
  };

  queue.insert(complexMsg);
  const extracted = queue.extract();
  assert.deepStrictEqual(extracted, complexMsg);

  console.log('✓ Test 6.6: Complex message objects');
}

// Test 6.7: Default priority fallback
{
  const queue = createTestQueue(100, PriorityLevel.LOW);

  // Insert without specifying priority (should use NORMAL as default from factory)
  queue.insert(createTestMessage('msg1')); // Uses factory default
  queue.insert(createTestMessage('msg2'), PriorityLevel.CRITICAL);

  // Critical should come first
  const first = queue.extract();
  assert.strictEqual(first.id, 'msg2');

  console.log('✓ Test 6.7: Default priority fallback');
}

/**
 * SUITE 7: Integration Tests
 */
console.log('\n=== SUITE 7: Integration Tests ===');

// Test 7.1: Logger and metrics integration
{
  const logger = new MockLogger();
  const metrics = new MockMetrics();
  const queue = createTestQueue(100, PriorityLevel.NORMAL, logger, metrics);

  queue.insert(createTestMessage('test1'));
  queue.insert(createTestMessage('test2'));
  queue.extract();

  // Verify logger received calls
  assert(logger.logs.length > 0);

  // Verify metrics collected
  assert(metrics.tracked.length > 0);

  console.log('✓ Test 7.1: Logger and metrics integration');
}

// Test 7.2: Full workflow (insert, peek, extract, pause, metrics)
{
  const queue = createTestQueue();

  // Insert
  queue.insert(createTestMessage('msg1'), PriorityLevel.NORMAL);
  queue.insert(createTestMessage('msg2'), PriorityLevel.CRITICAL);

  // Peek
  const peeked = queue.peek();
  assert.strictEqual(peeked.id, 'msg2'); // Critical first

  // Extract
  const extracted = queue.extract();
  assert.strictEqual(extracted.id, 'msg2');

  // Pause
  queue.pause();
  assert.strictEqual(queue.extract(), null);

  // Resume
  queue.resume();
  const resumed = queue.extract();
  assert.strictEqual(resumed.id, 'msg1');

  // Metrics
  const metrics = queue.getMetrics();
  assert.strictEqual(metrics.dequeueCount, 2);
  assert.strictEqual(metrics.depth, 0);

  console.log('✓ Test 7.2: Full workflow');
}

// Test 7.3: Backpressure simulation
{
  const queue = createTestQueue(10);

  // Fill to capacity
  for (let i = 0; i < 10; i++) {
    const inserted = queue.insert(createTestMessage(`msg${i}`));
    assert.strictEqual(inserted, true);
  }

  // Next insert should fail
  const rejected = queue.insert(createTestMessage('rejected'));
  assert.strictEqual(rejected, false);

  // Pause and extract one
  queue.pause();
  assert.strictEqual(queue.extract(), null); // Paused

  queue.resume();
  queue.extract(); // Extract one

  // Now insert should succeed
  const inserted = queue.insert(createTestMessage('accepted'));
  assert.strictEqual(inserted, true);

  console.log('✓ Test 7.3: Backpressure simulation');
}

console.log('\n=== ALL TESTS PASSED ===\n');
