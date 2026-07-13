#!/usr/bin/env node

/**
 * Selection Tracker Tests
 *
 * Comprehensive test suite for SelectionTracker class.
 * Tests initialization, message handler registration, state updates, getters,
 * subscription callbacks, and error handling.
 *
 * @module src/versions/v2.0.0/tests/selection-tracker.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Test Framework: Mocha (with Chai assertions)
 * Dependencies: SelectionTracker, error classes (selection-tracker.mjs)
 *
 * Test Suites:
 *   1. Initialization (3 tests)
 *   2. Message Handler Registration (3 tests)
 *   3. Selection Updates (4 tests)
 *   4. Query Methods (4 tests)
 *   5. Listener Subscriptions (3 tests)
 *   6. Cleanup & Disposal (2 tests)
 *   TOTAL: 19 tests
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import {
  SelectionTracker,
  SelectionTrackerError,
  StateValidationError
} from '../lib/selection-tracker.mjs';

// ============================================================================
// Test Helpers
// ============================================================================

/**
 * Create a mock server with messageHandler
 */
function createMockServer() {
  const listeners = {};
  return {
    messageHandler: {
      on: (messageType, callback) => {
        if (!listeners[messageType]) {
          listeners[messageType] = [];
        }
        listeners[messageType].push(callback);
      },
      emit: (messageType, message) => {
        if (listeners[messageType]) {
          for (const callback of listeners[messageType]) {
            callback(message);
          }
        }
      },
      off: (messageType, callback) => {
        if (listeners[messageType]) {
          listeners[messageType] = listeners[messageType].filter(cb => cb !== callback);
        }
      }
    },
    listeners // Expose for test inspection
  };
}

/**
 * Create a mock logger
 */
function createMockLogger() {
  const calls = { debug: [], error: [], info: [] };
  return {
    debug: (...args) => calls.debug.push(args),
    error: (...args) => calls.error.push(args),
    info: (...args) => calls.info.push(args),
    getCalls: () => calls
  };
}

/**
 * Create a mock metrics collector
 */
function createMockMetrics() {
  const events = [];
  return {
    recordEvent: (eventName, data) => events.push({ eventName, data, timestamp: Date.now() }),
    getEvents: () => events
  };
}

/**
 * Create a cursor position object
 */
function createCursorPosition(line, character) {
  return { line, character };
}

/**
 * Create a selection object for testing
 */
function createSelection(startLine, startChar, endLine, endChar, text) {
  return {
    start: createCursorPosition(startLine, startChar),
    end: createCursorPosition(endLine, endChar),
    text,
    isMultiline: startLine !== endLine
  };
}

// ============================================================================
// Test Suite 1: Initialization
// ============================================================================

describe('SelectionTracker — Initialization', () => {
  it('should initialize with default logger and metrics', () => {
    const tracker = new SelectionTracker();
    expect(tracker).to.be.instanceOf(SelectionTracker);
    expect(tracker.getSelection()).to.be.null;
    expect(tracker.hasSelection()).to.be.false;
  });

  it('should initialize with provided logger and metrics', () => {
    const logger = createMockLogger();
    const metrics = createMockMetrics();

    const tracker = new SelectionTracker({ logger, metrics });
    expect(tracker).to.be.instanceOf(SelectionTracker);

    const metricEvents = metrics.getEvents();
    expect(metricEvents).to.have.length(1);
    expect(metricEvents[0].eventName).to.equal('selection_tracker_initialized');
  });

  it('should initialize with null selection state', () => {
    const tracker = new SelectionTracker();
    expect(tracker.getSelection()).to.be.null;
    expect(tracker.hasSelection()).to.be.false;
    expect(tracker.isMultilineSelection()).to.be.false;
    expect(tracker.getSelectionLength()).to.equal(0);
  });
});

// ============================================================================
// Test Suite 2: Message Handler Registration
// ============================================================================

describe('SelectionTracker — Message Handler Registration', () => {
  let tracker;
  let server;

  beforeEach(() => {
    tracker = new SelectionTracker();
    server = createMockServer();
  });

  afterEach(() => {
    tracker.dispose();
  });

  it('should register successfully with valid server', async () => {
    await tracker.registerMessageHandlers(server);
    expect(server.listeners['currentFile']).to.exist;
    expect(server.listeners['currentFile']).to.be.an('array');
  });

  it('should throw SelectionTrackerError if server is null', async () => {
    try {
      await tracker.registerMessageHandlers(null);
      expect.fail('Should have thrown SelectionTrackerError');
    } catch (error) {
      expect(error).to.be.instanceOf(SelectionTrackerError);
      expect(error.operationType).to.equal('registration');
    }
  });

  it('should throw SelectionTrackerError if messageHandler.on is unavailable', async () => {
    const invalidServer = { messageHandler: {} };
    try {
      await tracker.registerMessageHandlers(invalidServer);
      expect.fail('Should have thrown SelectionTrackerError');
    } catch (error) {
      expect(error).to.be.instanceOf(SelectionTrackerError);
      expect(error.message).to.include('messageHandler.on');
    }
  });
});

// ============================================================================
// Test Suite 3: Selection Updates
// ============================================================================

describe('SelectionTracker — Selection Updates', () => {
  let tracker;

  beforeEach(() => {
    tracker = new SelectionTracker();
  });

  afterEach(() => {
    tracker.dispose();
  });

  it('should update selection with valid start and end positions', () => {
    const start = createCursorPosition(0, 0);
    const end = createCursorPosition(0, 5);
    const text = 'hello';

    tracker.updateSelection(start, end, text);

    const selection = tracker.getSelection();
    expect(selection).to.exist;
    expect(selection.text).to.equal('hello');
    expect(selection.isMultiline).to.be.false;
    expect(selection.start.line).to.equal(0);
    expect(selection.end.character).to.equal(5);
  });

  it('should detect multiline selections correctly', () => {
    const start = createCursorPosition(0, 10);
    const end = createCursorPosition(3, 15);
    const text = 'multi\nline\ntext';

    tracker.updateSelection(start, end, text);

    expect(tracker.isMultilineSelection()).to.be.true;
  });

  it('should not emit change event if selection is identical', () => {
    const start = createCursorPosition(0, 0);
    const end = createCursorPosition(0, 5);
    const text = 'hello';

    let changeCount = 0;
    tracker.onSelectionChange(() => {
      changeCount++;
    });

    tracker.updateSelection(start, end, text);
    tracker.updateSelection(start, end, text); // Same selection again

    expect(changeCount).to.equal(1); // Should only fire once
  });

  it('should throw StateValidationError if start position is invalid', () => {
    try {
      tracker.updateSelection(
        { line: 'invalid', character: 0 },
        createCursorPosition(0, 5),
        'text'
      );
      expect.fail('Should have thrown StateValidationError');
    } catch (error) {
      expect(error).to.be.instanceOf(StateValidationError);
      expect(error.fieldName).to.equal('start');
    }
  });

  it('should throw StateValidationError if end position is invalid', () => {
    try {
      tracker.updateSelection(
        createCursorPosition(0, 0),
        { line: 0, character: -1 },
        'text'
      );
      expect.fail('Should have thrown StateValidationError');
    } catch (error) {
      expect(error).to.be.instanceOf(StateValidationError);
      expect(error.fieldName).to.equal('end');
    }
  });

  it('should throw StateValidationError if text is not a string', () => {
    try {
      tracker.updateSelection(
        createCursorPosition(0, 0),
        createCursorPosition(0, 5),
        12345 // Not a string
      );
      expect.fail('Should have thrown StateValidationError');
    } catch (error) {
      expect(error).to.be.instanceOf(StateValidationError);
      expect(error.fieldName).to.equal('text');
    }
  });
});

// ============================================================================
// Test Suite 4: Query Methods
// ============================================================================

describe('SelectionTracker — Query Methods', () => {
  let tracker;

  beforeEach(() => {
    tracker = new SelectionTracker();
  });

  afterEach(() => {
    tracker.dispose();
  });

  it('should return null for getSelection() when no selection', () => {
    expect(tracker.getSelection()).to.be.null;
  });

  it('should return true for hasSelection() after update', () => {
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );
    expect(tracker.hasSelection()).to.be.true;
  });

  it('should return false for isMultilineSelection() for single-line selection', () => {
    tracker.updateSelection(
      createCursorPosition(5, 0),
      createCursorPosition(5, 10),
      'single line'
    );
    expect(tracker.isMultilineSelection()).to.be.false;
  });

  it('should return structured range from getSelectedRange()', () => {
    tracker.updateSelection(
      createCursorPosition(2, 5),
      createCursorPosition(4, 10),
      'multi\nline'
    );

    const range = tracker.getSelectedRange();
    expect(range).to.exist;
    expect(range.startLine).to.equal(2);
    expect(range.startChar).to.equal(5);
    expect(range.endLine).to.equal(4);
    expect(range.endChar).to.equal(10);
  });

  it('should return correct selection length', () => {
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 11),
      'hello world'
    );
    expect(tracker.getSelectionLength()).to.equal(11);
  });

  it('should return 0 length when no selection', () => {
    expect(tracker.getSelectionLength()).to.equal(0);
  });
});

// ============================================================================
// Test Suite 5: Listener Subscriptions
// ============================================================================

describe('SelectionTracker — Listener Subscriptions', () => {
  let tracker;

  beforeEach(() => {
    tracker = new SelectionTracker();
  });

  afterEach(() => {
    tracker.dispose();
  });

  it('should invoke listener on selection change', () => {
    let called = false;
    let newSel;
    let oldSel;

    tracker.onSelectionChange((newSelection, oldSelection) => {
      called = true;
      newSel = newSelection;
      oldSel = oldSelection;
    });

    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );

    expect(called).to.be.true;
    expect(newSel).to.exist;
    expect(newSel.text).to.equal('hello');
    expect(oldSel).to.be.null;
  });

  it('should support multiple listeners', () => {
    let callCount1 = 0;
    let callCount2 = 0;

    tracker.onSelectionChange(() => {
      callCount1++;
    });

    tracker.onSelectionChange(() => {
      callCount2++;
    });

    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );

    expect(callCount1).to.equal(1);
    expect(callCount2).to.equal(1);
  });

  it('should throw TypeError if callback is not a function', () => {
    try {
      tracker.onSelectionChange('not a function');
      expect.fail('Should have thrown TypeError');
    } catch (error) {
      expect(error).to.be.instanceOf(TypeError);
      expect(error.message).to.include('callback must be a function');
    }
  });

  it('should handle listener errors gracefully', () => {
    let secondListenerCalled = false;

    tracker.onSelectionChange(() => {
      throw new Error('Listener error');
    });

    tracker.onSelectionChange(() => {
      secondListenerCalled = true;
    });

    // Should not throw, and second listener should still be called
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );

    expect(secondListenerCalled).to.be.true;
  });
});

// ============================================================================
// Test Suite 6: Cleanup & Disposal
// ============================================================================

describe('SelectionTracker — Cleanup & Disposal', () => {
  let tracker;

  beforeEach(() => {
    tracker = new SelectionTracker();
  });

  it('should clear all listeners on dispose', () => {
    let callCount = 0;

    tracker.onSelectionChange(() => {
      callCount++;
    });

    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );

    expect(callCount).to.equal(1);

    tracker.dispose();

    // Update after disposal should not call listener
    tracker.updateSelection(
      createCursorPosition(0, 5),
      createCursorPosition(0, 10),
      'world'
    );

    expect(callCount).to.equal(1); // Still 1, not incremented
  });

  it('should reset selection state on dispose', () => {
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );

    expect(tracker.hasSelection()).to.be.true;

    tracker.dispose();

    expect(tracker.getSelection()).to.be.null;
    expect(tracker.hasSelection()).to.be.false;
  });
});

// ============================================================================
// Test Suite 7: Message Handler Integration
// ============================================================================

describe('SelectionTracker — Message Handler Integration', () => {
  let tracker;
  let server;

  beforeEach(async () => {
    tracker = new SelectionTracker();
    server = createMockServer();
    await tracker.registerMessageHandlers(server);
  });

  afterEach(() => {
    tracker.dispose();
  });

  it('should handle currentFile message with selection data', () => {
    const message = {
      data: {
        selection: {
          start: createCursorPosition(0, 0),
          end: createCursorPosition(0, 5),
          text: 'hello'
        }
      }
    };

    server.messageHandler.emit('currentFile', message);

    expect(tracker.hasSelection()).to.be.true;
    expect(tracker.getSelection().text).to.equal('hello');
  });

  it('should clear selection when message has no selection data', () => {
    // First set a selection
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );
    expect(tracker.hasSelection()).to.be.true;

    // Then emit message with no selection
    const message = { data: {} };
    server.messageHandler.emit('currentFile', message);

    expect(tracker.hasSelection()).to.be.false;
  });

  it('should handle invalid message gracefully', () => {
    // Should not throw
    const message = { data: null };
    expect(() => {
      server.messageHandler.emit('currentFile', message);
    }).to.not.throw();
  });

  it('should handle malformed selection in message', () => {
    const message = {
      data: {
        selection: {
          start: { line: 'invalid' },
          end: createCursorPosition(0, 5),
          text: 'hello'
        }
      }
    };

    // Should not throw (graceful error handling)
    expect(() => {
      server.messageHandler.emit('currentFile', message);
    }).to.not.throw();

    // Selection should remain unchanged from any previous state
    expect(tracker.hasSelection()).to.be.false;
  });
});

// ============================================================================
// Test Suite 8: Multiline Selection Edge Cases
// ============================================================================

describe('SelectionTracker — Multiline Selection Edge Cases', () => {
  let tracker;

  beforeEach(() => {
    tracker = new SelectionTracker();
  });

  afterEach(() => {
    tracker.dispose();
  });

  it('should correctly identify 2-line selection', () => {
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(1, 0),
      'line1\n'
    );

    expect(tracker.isMultilineSelection()).to.be.true;

    const range = tracker.getSelectedRange();
    expect(range.startLine).to.equal(0);
    expect(range.endLine).to.equal(1);
  });

  it('should handle empty selection (same start and end)', () => {
    tracker.updateSelection(
      createCursorPosition(5, 10),
      createCursorPosition(5, 10),
      ''
    );

    expect(tracker.hasSelection()).to.be.true; // Empty but still a selection
    expect(tracker.isMultilineSelection()).to.be.false;
    expect(tracker.getSelectionLength()).to.equal(0);
  });

  it('should track selection changes from single to multiline', () => {
    let changeCount = 0;

    tracker.onSelectionChange(() => {
      changeCount++;
    });

    // Single line
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(0, 5),
      'hello'
    );
    expect(changeCount).to.equal(1);
    expect(tracker.isMultilineSelection()).to.be.false;

    // Multiline
    tracker.updateSelection(
      createCursorPosition(0, 0),
      createCursorPosition(2, 5),
      'hello\nworld\nfoo'
    );
    expect(changeCount).to.equal(2);
    expect(tracker.isMultilineSelection()).to.be.true;
  });
});
