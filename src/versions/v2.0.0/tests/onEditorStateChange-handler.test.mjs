#!/usr/bin/env node

/**
 * onEditorStateChange Handler Tests
 *
 * Comprehensive test suite for the onEditorStateChange handler factory and class.
 * Tests initialization, subscription lifecycle, message emission, and error handling.
 *
 * @module src/versions/v2.0.0/tests/onEditorStateChange-handler.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Test Framework: Mocha (with Chai assertions)
 * Dependencies: onEditorStateChangeHandler, EditorStateChangeError (onEditorStateChange-handler.mjs)
 *
 * Test Suites:
 *   1. Factory Function Validation (2 tests)
 *   2. Subscription Lifecycle (3 tests)
 *   3. Message Emission (4 tests)
 *   4. Error Handling & Resilience (2 tests)
 *   5. Integration with SelectionTracker (1 test)
 *   TOTAL: 12 tests
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import {
  onEditorStateChangeHandler,
  EditorStateChangeError
} from '../lib/onEditorStateChange-handler.mjs';

// ============================================================================
// Test Helpers
// ============================================================================

/**
 * Create a mock dispatcher with sendMessage tracking.
 */
function createMockDispatcher() {
  return {
    messages: [],
    sendMessage(message) {
      this.messages.push({ ...message, sentAt: Date.now() });
    },
    getMessages() {
      return this.messages;
    },
    clear() {
      this.messages = [];
    }
  };
}

/**
 * Create a mock SelectionTracker with subscription capability.
 */
function createMockTracker() {
  const listeners = [];
  return {
    listeners,
    onSelectionChange(callback) {
      this.listeners.push(callback);
    },
    emit(newSelection, oldSelection) {
      for (const callback of this.listeners) {
        callback(newSelection, oldSelection);
      }
    },
    getListenerCount() {
      return this.listeners.length;
    }
  };
}

/**
 * Create a mock selection object.
 */
function createSelection(text = 'hello', startLine = 0, startChar = 0, endLine = 0, endChar = 5) {
  return {
    start: { line: startLine, character: startChar },
    end: { line: endLine, character: endChar },
    text,
    isMultiline: startLine !== endLine
  };
}

// ============================================================================
// Test Suite 1: Factory Function Validation
// ============================================================================

describe('onEditorStateChangeHandler — Factory Function Validation', () => {
  it('should initialize handler with valid dispatcher and tracker', () => {
    const dispatcher = createMockDispatcher();
    const tracker = createMockTracker();

    expect(() => {
      onEditorStateChangeHandler(dispatcher, tracker);
    }).to.not.throw();
  });

  it('should throw EditorStateChangeError for invalid dispatcher', () => {
    const tracker = createMockTracker();

    expect(() => {
      onEditorStateChangeHandler(null, tracker);
    }).to.throw(EditorStateChangeError);

    expect(() => {
      onEditorStateChangeHandler({ sendMessage: undefined }, tracker);
    }).to.throw(EditorStateChangeError);
  });

  it('should throw EditorStateChangeError for invalid tracker', () => {
    const dispatcher = createMockDispatcher();

    expect(() => {
      onEditorStateChangeHandler(dispatcher, null);
    }).to.throw(EditorStateChangeError);

    expect(() => {
      onEditorStateChangeHandler(dispatcher, { onSelectionChange: undefined });
    }).to.throw(EditorStateChangeError);
  });
});

// ============================================================================
// Test Suite 2: Subscription Lifecycle
// ============================================================================

describe('onEditorStateChangeHandler — Subscription Lifecycle', () => {
  let dispatcher;
  let tracker;
  let handler;

  beforeEach(() => {
    dispatcher = createMockDispatcher();
    tracker = createMockTracker();
    handler = onEditorStateChangeHandler(dispatcher, tracker);
  });

  afterEach(() => {
    if (handler) {
      handler.dispose();
    }
  });

  it('should establish subscription to tracker on creation', () => {
    const tracker2 = createMockTracker();
    const dispatcher2 = createMockDispatcher();

    expect(tracker2.getListenerCount()).to.equal(0);

    onEditorStateChangeHandler(dispatcher2, tracker2);

    expect(tracker2.getListenerCount()).to.equal(1);
  });

  it('should report isActive() as true after successful initialization', () => {
    expect(handler.isActive()).to.be.true;
  });

  it('should report isActive() as false after dispose()', () => {
    expect(handler.isActive()).to.be.true;
    handler.dispose();
    expect(handler.isActive()).to.be.false;
  });

  it('should handle double-dispose gracefully (idempotent)', () => {
    expect(() => {
      handler.dispose();
      handler.dispose();
    }).to.not.throw();

    expect(handler.isActive()).to.be.false;
  });
});

// ============================================================================
// Test Suite 3: Message Emission
// ============================================================================

describe('onEditorStateChangeHandler — Message Emission', () => {
  let dispatcher;
  let tracker;
  let handler;

  beforeEach(() => {
    dispatcher = createMockDispatcher();
    tracker = createMockTracker();
    handler = onEditorStateChangeHandler(dispatcher, tracker);
  });

  afterEach(() => {
    if (handler) {
      handler.dispose();
    }
  });

  it('should emit selection change message on tracker event', () => {
    const selection = createSelection('hello');

    tracker.emit(selection, null);

    expect(dispatcher.getMessages()).to.have.lengthOf(1);
    const message = dispatcher.getMessages()[0];
    expect(message.method).to.equal('onEditorStateChange');
    expect(message.params.type).to.equal('selection');
  });

  it('should include newSelection and oldSelection in message', () => {
    const oldSelection = createSelection('old');
    const newSelection = createSelection('new');

    tracker.emit(newSelection, oldSelection);

    const message = dispatcher.getMessages()[0];
    expect(message.params.newSelection).to.deep.include({
      text: 'new',
      isMultiline: false
    });
    expect(message.params.oldSelection).to.deep.include({
      text: 'old',
      isMultiline: false
    });
  });

  it('should handle null oldSelection (first change)', () => {
    const newSelection = createSelection('initial');

    tracker.emit(newSelection, null);

    const message = dispatcher.getMessages()[0];
    expect(message.params.newSelection).to.not.be.null;
    expect(message.params.oldSelection).to.be.null;
  });

  it('should handle null newSelection (selection cleared)', () => {
    const oldSelection = createSelection('cleared');

    tracker.emit(null, oldSelection);

    const message = dispatcher.getMessages()[0];
    expect(message.params.newSelection).to.be.null;
    expect(message.params.oldSelection).to.not.be.null;
  });

  it('should include timestamp in message params', () => {
    const selection = createSelection('test');

    const beforeTime = new Date().toISOString();
    tracker.emit(selection, null);
    const afterTime = new Date().toISOString();

    const message = dispatcher.getMessages()[0];
    const msgTimestamp = message.params.timestamp;

    expect(msgTimestamp).to.exist;
    expect(msgTimestamp).to.be.at.least(beforeTime);
    expect(msgTimestamp).to.be.at.most(afterTime);
  });

  it('should track change count', () => {
    expect(handler.getChangeCount()).to.equal(0);

    tracker.emit(createSelection('first'), null);
    expect(handler.getChangeCount()).to.equal(1);

    tracker.emit(createSelection('second'), createSelection('first'));
    expect(handler.getChangeCount()).to.equal(2);

    tracker.emit(null, createSelection('second'));
    expect(handler.getChangeCount()).to.equal(3);
  });

  it('should include changeCount in message params', () => {
    tracker.emit(createSelection('a'), null);
    tracker.emit(createSelection('b'), createSelection('a'));

    const messages = dispatcher.getMessages();
    expect(messages[0].params.changeCount).to.equal(1);
    expect(messages[1].params.changeCount).to.equal(2);
  });

  it('should handle multiline selection in message', () => {
    const multilineSelection = createSelection('line1\nline2', 0, 0, 1, 0);

    tracker.emit(multilineSelection, null);

    const message = dispatcher.getMessages()[0];
    expect(message.params.newSelection.isMultiline).to.be.true;
  });
});

// ============================================================================
// Test Suite 4: Error Handling & Resilience
// ============================================================================

describe('onEditorStateChangeHandler — Error Handling & Resilience', () => {
  it('should log and store dispatch errors without throwing', () => {
    const dispatcher = {
      sendMessage() {
        throw new Error('Dispatcher unavailable');
      }
    };
    const tracker = createMockTracker();

    const handler = onEditorStateChangeHandler(dispatcher, tracker);

    expect(() => {
      tracker.emit(createSelection('test'), null);
    }).to.not.throw();

    expect(handler.getLastError()).to.exist;
    expect(handler.getLastError().message).to.include('Dispatcher unavailable');

    handler.dispose();
  });

  it('should ignore events after disposal', () => {
    const dispatcher = createMockDispatcher();
    const tracker = createMockTracker();
    const handler = onEditorStateChangeHandler(dispatcher, tracker);

    handler.dispose();

    // Emit event after disposal
    tracker.emit(createSelection('test'), null);

    // Message should NOT be dispatched
    expect(dispatcher.getMessages()).to.have.lengthOf(0);
    expect(handler.getChangeCount()).to.equal(0);
  });

  it('should track consecutive errors', () => {
    let callCount = 0;
    const dispatcher = {
      sendMessage() {
        callCount++;
        throw new Error(`Error ${callCount}`);
      }
    };
    const tracker = createMockTracker();
    const handler = onEditorStateChangeHandler(dispatcher, tracker);

    tracker.emit(createSelection('first'), null);
    const firstError = handler.getLastError();

    tracker.emit(createSelection('second'), createSelection('first'));
    const secondError = handler.getLastError();

    expect(firstError.message).to.include('Error 1');
    expect(secondError.message).to.include('Error 2');

    handler.dispose();
  });
});

// ============================================================================
// Test Suite 5: Integration with SelectionTracker
// ============================================================================

describe('onEditorStateChangeHandler — Integration with SelectionTracker', () => {
  it('should correctly handle realistic SelectionTracker emission pattern', () => {
    const dispatcher = createMockDispatcher();
    const tracker = createMockTracker();
    const handler = onEditorStateChangeHandler(dispatcher, tracker);

    // Simulate: User selects "hello"
    const hello = createSelection('hello', 0, 0, 0, 5);
    tracker.emit(hello, null);
    expect(dispatcher.getMessages()).to.have.lengthOf(1);
    expect(handler.getChangeCount()).to.equal(1);

    // Simulate: User extends selection to next line
    const multiline = createSelection('hello\nworld', 0, 0, 1, 5);
    tracker.emit(multiline, hello);
    expect(dispatcher.getMessages()).to.have.lengthOf(2);
    expect(handler.getChangeCount()).to.equal(2);

    const message2 = dispatcher.getMessages()[1];
    expect(message2.params.newSelection.isMultiline).to.be.true;
    expect(message2.params.oldSelection.text).to.equal('hello');

    // Simulate: User clears selection
    tracker.emit(null, multiline);
    expect(dispatcher.getMessages()).to.have.lengthOf(3);
    expect(handler.getChangeCount()).to.equal(3);

    const message3 = dispatcher.getMessages()[2];
    expect(message3.params.newSelection).to.be.null;
    expect(message3.params.oldSelection.isMultiline).to.be.true;

    handler.dispose();
  });
});

// ============================================================================
// Test Suite 6: Handler Interface Compliance
// ============================================================================

describe('onEditorStateChangeHandler — Handler Interface Compliance', () => {
  it('should have handle() method that returns Promise', async () => {
    const dispatcher = createMockDispatcher();
    const tracker = createMockTracker();
    const handler = onEditorStateChangeHandler(dispatcher, tracker);

    const result = handler.handle();
    expect(result).to.be.instanceOf(Promise);

    await result; // Should not throw

    handler.dispose();
  });

  it('should support handler registry pattern', () => {
    const dispatcher = createMockDispatcher();
    const tracker = createMockTracker();
    const handler = onEditorStateChangeHandler(dispatcher, tracker);

    // Verify interface expected by handler registry
    expect(handler).to.have.property('handle');
    expect(handler).to.have.property('dispose');
    expect(handler).to.have.property('isActive');
    expect(handler).to.have.property('getChangeCount');
    expect(handler).to.have.property('getLastError');

    expect(typeof handler.handle).to.equal('function');
    expect(typeof handler.dispose).to.equal('function');
    expect(typeof handler.isActive).to.equal('function');
    expect(typeof handler.getChangeCount).to.equal('function');
    expect(typeof handler.getLastError).to.equal('function');

    handler.dispose();
  });
});
