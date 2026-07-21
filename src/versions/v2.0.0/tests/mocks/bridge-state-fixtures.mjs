/**
 * Bridge State Persistence Test Fixtures
 * 
 * Provides reusable fixtures, mocks, and factories for testing
 * bridge state persistence, recovery, and integration patterns.
 * 
 * Usage:
 *  import { createValidCheckpoint, mockLogger } from './bridge-state-fixtures.mjs';
 *  
 *  const checkpoint = createValidCheckpoint();
 *  const logger = mockLogger();
 */

import { BridgeStateCheckpoint } from '../lib/bridge-state-persistence.mjs';

/**
 * Create a valid checkpoint with typical bridge state.
 * Used as the baseline fixture for most tests.
 */
export function createValidCheckpoint() {
  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    phase: 'ready',
    handlers: {
      'refactor': { status: 'active', errorCount: 0, timeoutCount: 0 },
      'search': { status: 'active', errorCount: 1, timeoutCount: 0 },
      'hover': { status: 'idle', errorCount: 0, timeoutCount: 0 },
      'definition': { status: 'active', errorCount: 2, timeoutCount: 1 }
    },
    subscriptions: { 
      count: 25, 
      types: ['onEdit', 'onSave', 'onClose', 'onFocus', 'onBlur'] 
    },
    pendingRequests: { count: 3 },
    uptime: 1234,
    bridgeVersion: '2.0.0'
  });
}

/**
 * Create a minimal valid checkpoint (no handlers, no subscriptions).
 * Useful for bootstrap phase tests.
 */
export function createMinimalCheckpoint() {
  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    phase: 'bootstrap',
    handlers: {},
    subscriptions: { count: 0, types: [] },
    pendingRequests: { count: 0 },
    uptime: 0,
    bridgeVersion: '2.0.0'
  });
}

/**
 * Create a checkpoint in a degraded state.
 * Used for error recovery testing.
 */
export function createDegradedCheckpoint() {
  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    phase: 'degraded',
    handlers: {
      'refactor': { status: 'error', errorCount: 10, timeoutCount: 5 },
      'search': { status: 'error', errorCount: 8, timeoutCount: 3 }
    },
    subscriptions: { count: 5, types: ['onEdit'] },
    pendingRequests: { count: 0 },
    uptime: 300,
    bridgeVersion: '2.0.0'
  });
}

/**
 * Create a checkpoint with many handlers and high subscription count.
 * Used for performance and scale testing.
 */
export function createLargeCheckpoint(handlerCount = 50) {
  const handlers = {};
  for (let i = 0; i < handlerCount; i++) {
    handlers[`handler-${i}`] = {
      status: i % 3 === 0 ? 'error' : (i % 2 === 0 ? 'active' : 'idle'),
      errorCount: Math.floor(Math.random() * 10),
      timeoutCount: Math.floor(Math.random() * 5)
    };
  }

  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    phase: 'ready',
    handlers,
    subscriptions: { count: 500, types: Array.from({ length: 20 }, (_, i) => `type-${i}`) },
    pendingRequests: { count: 50 },
    uptime: 5000,
    bridgeVersion: '2.0.0'
  });
}

/**
 * Create various checkpoint phases for phase transition testing.
 */
export function createCheckpointByPhase(phase) {
  const phases = {
    bootstrap: {
      phase: 'bootstrap',
      handlers: {},
      subscriptions: { count: 0, types: [] },
      pendingRequests: { count: 0 },
      uptime: 0
    },
    connected: {
      phase: 'connected',
      handlers: { 'transport': { status: 'active', errorCount: 0, timeoutCount: 0 } },
      subscriptions: { count: 0, types: [] },
      pendingRequests: { count: 0 },
      uptime: 10
    },
    subscribed: {
      phase: 'subscribed',
      handlers: {
        'transport': { status: 'active', errorCount: 0, timeoutCount: 0 },
        'dispatcher': { status: 'active', errorCount: 0, timeoutCount: 0 }
      },
      subscriptions: { count: 10, types: ['onEditorState', 'onSelection'] },
      pendingRequests: { count: 0 },
      uptime: 50
    },
    ready: {
      phase: 'ready',
      handlers: {
        'refactor': { status: 'active', errorCount: 0, timeoutCount: 0 },
        'search': { status: 'active', errorCount: 0, timeoutCount: 0 },
        'hover': { status: 'active', errorCount: 0, timeoutCount: 0 }
      },
      subscriptions: { count: 30, types: ['onEdit', 'onSave', 'onClose'] },
      pendingRequests: { count: 2 },
      uptime: 200
    },
    degraded: {
      phase: 'degraded',
      handlers: {
        'refactor': { status: 'error', errorCount: 5, timeoutCount: 2 }
      },
      subscriptions: { count: 5, types: [] },
      pendingRequests: { count: 0 },
      uptime: 300
    }
  };

  const config = phases[phase] || phases.bootstrap;
  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    ...config,
    bridgeVersion: '2.0.0'
  });
}

/**
 * Create a checkpoint with an invalid phase (for validation testing).
 */
export function createInvalidPhaseCheckpoint() {
  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    phase: 'invalid-phase-xyz',
    handlers: {}
  });
}

/**
 * Create a checkpoint with negative error count (for validation testing).
 */
export function createInvalidErrorCountCheckpoint() {
  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    phase: 'ready',
    handlers: {
      'test': { status: 'active', errorCount: -1, timeoutCount: 0 }
    }
  });
}

/**
 * Create a checkpoint with missing required field (for validation testing).
 */
export function createInvalidMissingFieldCheckpoint() {
  return new BridgeStateCheckpoint({
    phase: 'ready',
    // Missing timestamp
    handlers: {}
  });
}

/**
 * Create a checkpoint that is stale (older than 7 days).
 */
export function createStaleCheckpoint() {
  const eightDaysAgo = new Date(Date.now() - 8 * 24 * 60 * 60 * 1000);
  return new BridgeStateCheckpoint({
    timestamp: eightDaysAgo.toISOString(),
    phase: 'ready',
    handlers: { 'test': { status: 'active', errorCount: 0, timeoutCount: 0 } }
  });
}

/**
 * Mock logger for capturing log calls during testing.
 */
export function mockLogger() {
  const logs = [];

  return {
    logs,
    log: (level, message) => {
      logs.push({ level, message, timestamp: new Date().toISOString() });
    },
    getLastLog: () => logs[logs.length - 1],
    getLogsByLevel: (level) => logs.filter(l => l.level === level),
    clear: () => {
      logs.length = 0;
    }
  };
}

/**
 * Mock metrics collector for capturing metric recordings during testing.
 */
export function mockMetrics() {
  const metrics = [];

  return {
    metrics,
    record: (name, value) => {
      metrics.push({ name, value, timestamp: new Date().toISOString() });
    },
    getByName: (name) => metrics.filter(m => m.name === name),
    getLastValue: (name) => {
      const filtered = metrics.filter(m => m.name === name);
      return filtered[filtered.length - 1]?.value;
    },
    clear: () => {
      metrics.length = 0;
    }
  };
}

/**
 * Mock state collector factory for C# integration testing.
 * Returns a mock that simulates various bridge states.
 */
export function mockBridgeStateCollector(initialState = {}) {
  const state = {
    handlerCount: 5,
    subscriptionCount: 20,
    pendingRequests: 0,
    currentPhase: 'ready',
    ...initialState
  };

  return {
    async createSnapshotAsync() {
      return {
        capturedAt: new Date().toISOString(),
        handlerCount: state.handlerCount,
        subscriptionCount: state.subscriptionCount,
        pendingRequestCount: state.pendingRequests,
        currentPhase: state.currentPhase,
        bridgeVersion: '2.0.0',
        validate: () => true
      };
    },
    setState: (newState) => {
      Object.assign(state, newState);
    }
  };
}

/**
 * Helper: Create a checkpoint transition sequence.
 * Simulates a full bridge lifecycle: bootstrap → connected → subscribed → ready.
 */
export function createCheckpointSequence() {
  const phases = ['bootstrap', 'connected', 'subscribed', 'ready'];
  let timestampMs = Date.now() - 1000; // Start 1 second ago

  return phases.map((phase, index) => {
    timestampMs += 100; // Each phase is 100ms apart
    return new BridgeStateCheckpoint({
      timestamp: new Date(timestampMs).toISOString(),
      phase,
      handlers: Object.fromEntries(
        Array.from({ length: Math.min(1, index) }, (_, i) => [
          `handler-${i}`,
          { status: 'active', errorCount: 0, timeoutCount: 0 }
        ])
      ),
      subscriptions: { count: Math.min(10, index * 10), types: [] },
      pendingRequests: { count: 0 },
      uptime: index * 100,
      bridgeVersion: '2.0.0'
    });
  });
}

/**
 * Helper: Create a checkpoint with many error states for regression testing.
 */
export function createErrorStateCheckpoint() {
  return new BridgeStateCheckpoint({
    timestamp: new Date().toISOString(),
    phase: 'ready',
    handlers: {
      'handler-with-errors': {
        status: 'error',
        errorCount: 15,
        timeoutCount: 8
      },
      'handler-with-timeouts': {
        status: 'error',
        errorCount: 0,
        timeoutCount: 20
      },
      'handler-mixed': {
        status: 'active',
        errorCount: 5,
        timeoutCount: 3
      }
    },
    subscriptions: { count: 5, types: [] },
    pendingRequests: { count: 2 },
    uptime: 500,
    bridgeVersion: '2.0.0'
  });
}

/**
 * Helper: Verify checkpoint properties match expected values.
 * Useful for assertion-heavy tests.
 */
export function assertCheckpointMatches(checkpoint, expected) {
  const mismatches = [];

  for (const [key, value] of Object.entries(expected)) {
    if (checkpoint[key] !== value) {
      mismatches.push(`${key}: expected ${value}, got ${checkpoint[key]}`);
    }
  }

  if (mismatches.length > 0) {
    throw new Error(`Checkpoint mismatch:\n  ${mismatches.join('\n  ')}`);
  }
}
