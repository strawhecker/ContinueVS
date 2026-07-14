#!/usr/bin/env node

/**
 * Unit Tests for Handler Registry (Step 66)
 *
 * Test Coverage:
 *   - Suite 1: Module Load & Exports (3 tests)
 *   - Suite 2: Handler Metadata Completeness (5 tests)
 *   - Suite 3: Registration Order & Dependencies (4 tests)
 *   - Suite 4: Lookup Functions (4 tests)
 *   - Suite 5: Validation & Error Handling (3 tests)
 *   - Suite 6: Extensibility (3 tests)
 *
 * Total: 22 tests
 *
 * @module src/versions/v2.0.0/tests/handler-registry.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 66 (handler registry), 71 (handler orchestration)
 */

import { strict as assert, AssertionError } from 'assert';
import {
  getAllHandlers,
  getHandlerMetadata,
  getHandlersByStabilityTier,
  getHandlersByTimeoutPolicy,
  hasHandler,
  HandlerRegistryError,
  HandlerNotFoundError
} from '../lib/handler-registry.mjs';

// ============================================================================
// TEST UTILITIES
// ============================================================================

/**
 * Test counter for reporting
 */
let testCount = 0;
let passCount = 0;
let failCount = 0;

/**
 * Simple test runner
 * @param {string} name - Test name
 * @param {Function} fn - Async test function
 */
async function test(name, fn) {
  testCount++;
  try {
    await fn();
    passCount++;
    console.log(`✓ Test ${testCount}: ${name}`);
  } catch (err) {
    failCount++;
    console.error(`✗ Test ${testCount}: ${name}`);
    console.error(`  Error: ${err.message}`);
    console.error(`  Stack: ${err.stack}`);
  }
}

/**
 * Assertion helper for expected exceptions
 */
function assertThrows(fn, ErrorClass, message) {
  try {
    fn();
    throw new AssertionError(
      `Expected ${ErrorClass.name} to be thrown, but no error was raised`
    );
  } catch (err) {
    if (!(err instanceof ErrorClass)) {
      throw new AssertionError(
        `Expected ${ErrorClass.name}, got ${err.constructor.name}: ${err.message}`
      );
    }
  }
}

// ============================================================================
// SUITE 1: MODULE LOAD & EXPORTS
// ============================================================================

console.log('\n=== Suite 1: Module Load & Exports ===');

await test('Registry loads without errors', () => {
  // If we got here, the module loaded successfully
  assert(true);
});

await test('getAllHandlers() returns array', () => {
  const handlers = getAllHandlers();
  assert(Array.isArray(handlers), 'getAllHandlers() must return array');
  assert(handlers.length > 0, 'Registry must contain at least one handler');
});

await test('getAllHandlers() returns copy (safe against mutation)', () => {
  const handlers1 = getAllHandlers();
  const handlers2 = getAllHandlers();
  assert(handlers1 !== handlers2, 'getAllHandlers() must return new array each time');
  assert.deepEqual(handlers1, handlers2, 'Registry content must be consistent');
});

// ============================================================================
// SUITE 2: HANDLER METADATA COMPLETENESS
// ============================================================================

console.log('\n=== Suite 2: Handler Metadata Completeness ===');

await test('All handlers have required fields', () => {
  const handlers = getAllHandlers();
  const requiredFields = ['messageType', 'handler', 'timeoutPolicy', 'stabilityTier', 'description'];

  for (const entry of handlers) {
    for (const field of requiredFields) {
      assert(field in entry, `Handler ${entry.messageType} missing field: ${field}`);
    }
  }
});

await test('All timeoutPolicy values are recognized', () => {
  const handlers = getAllHandlers();
  const validPolicies = ['fast', 'medium', 'slow'];

  for (const entry of handlers) {
    assert(
      validPolicies.includes(entry.timeoutPolicy),
      `Handler ${entry.messageType} has invalid timeoutPolicy: ${entry.timeoutPolicy}`
    );
  }
});

await test('All stabilityTier values are recognized', () => {
  const handlers = getAllHandlers();
  const validTiers = ['core', 'experimental', 'deprecated'];

  for (const entry of handlers) {
    assert(
      validTiers.includes(entry.stabilityTier),
      `Handler ${entry.messageType} has invalid stabilityTier: ${entry.stabilityTier}`
    );
  }
});

await test('All handlers have callable handler functions', () => {
  const handlers = getAllHandlers();

  for (const entry of handlers) {
    assert(
      typeof entry.handler === 'function',
      `Handler ${entry.messageType} is not callable: ${typeof entry.handler}`
    );
  }
});

await test('All handlers have non-empty descriptions', () => {
  const handlers = getAllHandlers();

  for (const entry of handlers) {
    assert(
      typeof entry.description === 'string' && entry.description.length > 0,
      `Handler ${entry.messageType} has invalid description`
    );
  }
});

// ============================================================================
// SUITE 3: REGISTRATION ORDER & DEPENDENCIES
// ============================================================================

console.log('\n=== Suite 3: Registration Order & Dependencies ===');

await test('Bootstrap handler is first in registry', () => {
  const handlers = getAllHandlers();
  assert(handlers.length > 0, 'Registry must have at least one handler');
  assert.equal(
    handlers[0].messageType,
    'bridge:bootstrap',
    'First handler must be bootstrap'
  );
});

await test('Editor context handlers appear before navigation handlers', () => {
  const handlers = getAllHandlers();
  const editorStateIndex = handlers.findIndex((h) => h.messageType === 'bridge:getEditorState');
  const navigationIndex = Math.max(
    handlers.findIndex((h) => h.messageType === 'bridge:goToDefinition'),
    handlers.findIndex((h) => h.messageType === 'bridge:findReferences'),
    handlers.findIndex((h) => h.messageType === 'bridge:search')
  );

  assert(
    editorStateIndex >= 0 && navigationIndex >= 0,
    'Both editor context and navigation handlers must exist'
  );
  assert(
    editorStateIndex < navigationIndex,
    `Editor context (index ${editorStateIndex}) must appear before navigation (index ${navigationIndex})`
  );
});

await test('No duplicate message types in registry', () => {
  const handlers = getAllHandlers();
  const types = new Set();

  for (const entry of handlers) {
    assert(
      !types.has(entry.messageType),
      `Duplicate message type: ${entry.messageType}`
    );
    types.add(entry.messageType);
  }
});

await test('All handlers have dependencies documented', () => {
  const handlers = getAllHandlers();

  for (const entry of handlers) {
    assert(
      'dependencies' in entry,
      `Handler ${entry.messageType} missing dependencies field`
    );
    assert(
      Array.isArray(entry.dependencies),
      `Handler ${entry.messageType} dependencies must be array`
    );
  }
});

// ============================================================================
// SUITE 4: LOOKUP FUNCTIONS
// ============================================================================

console.log('\n=== Suite 4: Lookup Functions ===');

await test('getHandlerMetadata() returns correct entry for known type', () => {
  const meta = getHandlerMetadata('bridge:getEditorState');
  assert.equal(meta.messageType, 'bridge:getEditorState');
  assert.equal(typeof meta.handler, 'function');
  assert.equal(meta.timeoutPolicy, 'fast');
  assert.equal(meta.stabilityTier, 'core');
});

await test('getHandlerMetadata() throws HandlerNotFoundError for unknown type', () => {
  assertThrows(
    () => getHandlerMetadata('bridge:unknownHandler'),
    HandlerNotFoundError,
    'Should throw HandlerNotFoundError for unknown type'
  );
});

await test('getAllHandlers() returns safe copy', () => {
  const handlers = getAllHandlers();
  const originalLength = handlers.length;

  // Attempt to mutate the returned array
  handlers.push({ messageType: 'fake', handler: () => {} });

  // Verify original registry unchanged
  const freshCopy = getAllHandlers();
  assert.equal(
    freshCopy.length,
    originalLength,
    'Mutations to returned array must not affect registry'
  );
});

await test('hasHandler() correctly identifies registered handlers', () => {
  assert(hasHandler('bridge:getEditorState'), 'Should find getEditorState handler');
  assert(hasHandler('bridge:bootstrap'), 'Should find bootstrap handler');
  assert(!hasHandler('bridge:nonExistent'), 'Should not find non-existent handler');
});

// ============================================================================
// SUITE 5: STABILITY TIER & TIMEOUT FILTERING
// ============================================================================

console.log('\n=== Suite 5: Stability Tier & Timeout Filtering ===');

await test('getHandlersByStabilityTier() returns correct handlers', () => {
  const coreHandlers = getHandlersByStabilityTier('core');
  assert(coreHandlers.length > 0, 'Should have core-tier handlers');
  assert(coreHandlers.every((h) => h.stabilityTier === 'core'), 'All must be core tier');

  // Verify specific handler
  const editorState = coreHandlers.find((h) => h.messageType === 'bridge:getEditorState');
  assert(editorState, 'Should include getEditorState in core handlers');
});

await test('getHandlersByTimeoutPolicy() returns correct handlers', () => {
  const fastHandlers = getHandlersByTimeoutPolicy('fast');
  assert(fastHandlers.length > 0, 'Should have fast-timeout handlers');
  assert(fastHandlers.every((h) => h.timeoutPolicy === 'fast'), 'All must have fast policy');

  // Verify specific handler
  const getEditorState = fastHandlers.find((h) => h.messageType === 'bridge:getEditorState');
  assert(getEditorState, 'Should include getEditorState in fast handlers');
});

await test('Filter functions maintain data integrity', () => {
  const allHandlers = getAllHandlers();
  const coreHandlers = getHandlersByStabilityTier('core');
  const mediumHandlers = getHandlersByTimeoutPolicy('medium');

  // Verify no cross-contamination
  assert(
    !coreHandlers.some((h) => h.stabilityTier !== 'core'),
    'Core filter must not include non-core handlers'
  );
  assert(
    !mediumHandlers.some((h) => h.timeoutPolicy !== 'medium'),
    'Timeout filter must not include non-matching handlers'
  );
});

// ============================================================================
// SUITE 6: EXTENSIBILITY
// ============================================================================

console.log('\n=== Suite 6: Extensibility ===');

await test('Registry structure accommodates future handlers (Steps 76–95)', () => {
  // Verify that registry structure is extensible
  const handlers = getAllHandlers();
  const firstHandler = handlers[0];

  // Check that expected fields exist for adding new handlers
  const requiredStructure = [
    'messageType',
    'handler',
    'timeoutPolicy',
    'stabilityTier',
    'description',
    'relatedSteps',
    'dependencies'
  ];

  for (const field of requiredStructure) {
    assert(
      field in firstHandler,
      `Registry entries must have ${field} field for extensibility`
    );
  }
});

await test('Can create new handler entry following existing pattern', () => {
  // Simulate creating a new handler entry (Step 76 example)
  const newHandler = {
    messageType: 'bridge:refactor',
    handler: async (message, context) => ({ success: true }),
    timeoutPolicy: 'medium',
    stabilityTier: 'experimental',
    description: 'Code refactoring operations',
    relatedSteps: [76, 71],
    dependencies: [50, 53, 54]
  };

  // Verify structure matches existing handlers
  const existingHandler = getHandlerMetadata('bridge:bootstrap');
  const newKeys = Object.keys(newHandler).sort();
  const existingKeys = Object.keys(existingHandler).sort();

  assert.deepEqual(
    newKeys.filter((k) => existingKeys.includes(k)),
    newKeys,
    'New handler entry must use consistent field structure'
  );
});

await test('Metadata schema extensible without breaking existing code', () => {
  // Add a new metadata field to verify no breakage
  const handler = getHandlerMetadata('bridge:bootstrap');

  // Simulate adding new field (this should not affect lookup)
  const extended = {
    ...handler,
    newField: 'example'
  };

  // Verify lookup still works and returns expected fields
  const fresh = getHandlerMetadata('bridge:bootstrap');
  assert.equal(fresh.messageType, 'bridge:bootstrap', 'Lookup must still work');
  assert.equal(typeof fresh.handler, 'function', 'Handler must still be callable');
});

await test('Apply-Edit handler registered and valid (Step 78)', () => {
  // Verify bridge:applyEdit handler exists
  const applyEditHandler = getHandlerMetadata('bridge:applyEdit');

  assert(applyEditHandler, 'bridge:applyEdit handler must be registered');
  assert.equal(applyEditHandler.messageType, 'bridge:applyEdit', 'Message type must match');
  assert.equal(typeof applyEditHandler.handler, 'function', 'Handler must be callable');
  assert.equal(applyEditHandler.timeoutPolicy, 'fast', 'Timeout policy must be fast');
  assert.equal(applyEditHandler.stabilityTier, 'experimental', 'Stability tier must be experimental');
  assert.equal(applyEditHandler.isFactory, true, 'Must be a factory function');
  assert(applyEditHandler.relatedSteps.includes(78), 'Must reference Step 78');
  assert(applyEditHandler.relatedSteps.includes(71), 'Must reference Step 71');
  assert(applyEditHandler.dependencies.includes(52), 'Must depend on Step 52 (DocumentProvider)');
});

// ============================================================================
// TEST SUMMARY
// ============================================================================

console.log('\n=== Test Summary ===');
console.log(`Total: ${testCount} | Passed: ${passCount} | Failed: ${failCount}`);

if (failCount === 0) {
  console.log('✓ All tests passed!');
  process.exit(0);
} else {
  console.error(`✗ ${failCount} test(s) failed`);
  process.exit(1);
}
