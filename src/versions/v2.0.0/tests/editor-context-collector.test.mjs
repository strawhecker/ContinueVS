#!/usr/bin/env node

/**
 * Editor Context Collector Tests
 *
 * Comprehensive test suite for EditorContextCollector class.
 * Tests initialization, message handler registration, state updates, getters,
 * subscription callbacks, and error handling.
 *
 * @module src/versions/v2.0.0/tests/editor-context-collector.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Test Framework: Mocha (with Chai assertions)
 * Dependencies: EditorContextCollector, error classes (editor-context-collector.js)
 *
 * Test Suites:
 *   1. Initialization (3 tests)
 *   2. Message Handler Registration (4 tests)
 *   3. State Updates (6 tests)
 *   4. Getters & Query Methods (5 tests)
 *   5. Subscription Callbacks (4 tests)
 *   6. Cleanup & Disposal (2 tests)
 *   TOTAL: 24 tests
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import {
  EditorContextCollector,
  EditorContextError,
  StateValidationError
} from '../lib/editor-context-collector.js';

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

// ============================================================================
// Test Suite 1: Initialization
// ============================================================================

describe('EditorContextCollector — Initialization', () => {
  it('should initialize with default logger and metrics', () => {
    const collector = new EditorContextCollector();
    expect(collector).to.be.instanceOf(EditorContextCollector);
    expect(collector.logger).to.exist;
    expect(collector.metrics).to.exist;
  });

  it('should initialize with custom logger and metrics', () => {
    const logger = createMockLogger();
    const metrics = createMockMetrics();
    const collector = new EditorContextCollector({ logger, metrics });

    expect(collector.logger).to.equal(logger);
    expect(collector.metrics).to.equal(metrics);
  });

  it('should throw if options is not a plain object', () => {
    expect(() => new EditorContextCollector('invalid')).to.throw(Error);
    expect(() => new EditorContextCollector([])).to.throw(Error);
    expect(() => new EditorContextCollector(123)).to.throw(Error);
  });
});

// ============================================================================
// Test Suite 2: Message Handler Registration
// ============================================================================

describe('EditorContextCollector — Message Handler Registration', () => {
  let collector;
  let mockServer;

  beforeEach(() => {
    collector = new EditorContextCollector();
    mockServer = createMockServer();
  });

  it('should register handlers for "currentFile" and "didChangeActiveTextEditor"', async () => {
    await collector.registerMessageHandlers(mockServer);

    // Verify both message types are registered
    expect(mockServer.listeners['currentFile']).to.have.lengthOf(1);
    expect(mockServer.listeners['didChangeActiveTextEditor']).to.have.lengthOf(1);
  });

  it('should throw EditorContextError if server is null', async () => {
    try {
      await collector.registerMessageHandlers(null);
      expect.fail('Should have thrown EditorContextError');
    } catch (error) {
      expect(error).to.be.instanceOf(EditorContextError);
      expect(error.operationType).to.equal('registration');
    }
  });

  it('should throw EditorContextError if server.messageHandler is missing', async () => {
    const invalidServer = { /* no messageHandler */ };
    try {
      await collector.registerMessageHandlers(invalidServer);
      expect.fail('Should have thrown EditorContextError');
    } catch (error) {
      expect(error).to.be.instanceOf(EditorContextError);
    }
  });

  it('should throw EditorContextError if server.messageHandler.on is not a function', async () => {
    const invalidServer = { messageHandler: { on: null } };
    try {
      await collector.registerMessageHandlers(invalidServer);
      expect.fail('Should have thrown EditorContextError');
    } catch (error) {
      expect(error).to.be.instanceOf(EditorContextError);
    }
  });
});

// ============================================================================
// Test Suite 3: State Updates
// ============================================================================

describe('EditorContextCollector — State Updates', () => {
  let collector;
  let mockServer;

  beforeEach(async () => {
    collector = new EditorContextCollector();
    mockServer = createMockServer();
    await collector.registerMessageHandlers(mockServer);
  });

  it('should update file context via updateFileContext()', () => {
    collector.updateFileContext('C:\\file.cs', 'contents', { line: 10, character: 5 });

    const activeFile = collector.getActiveFile();
    expect(activeFile).to.not.be.null;
    expect(activeFile.filepath).to.equal('C:\\file.cs');
    expect(activeFile.contents).to.equal('contents');
    expect(activeFile.cursorLine).to.equal(10);
    expect(activeFile.cursorColumn).to.equal(5);
  });

  it('should normalize cursor position correctly (0-based)', () => {
    collector.updateFileContext('file.cs', 'text', { line: 0, character: 0 });
    let cursor = collector.getCursorPosition();
    expect(cursor).to.deep.equal({ line: 0, character: 0 });

    collector.updateFileContext('file.cs', 'text', { line: 42, character: 100 });
    cursor = collector.getCursorPosition();
    expect(cursor).to.deep.equal({ line: 42, character: 100 });
  });

  it('should update active editor via updateActiveEditor()', () => {
    collector.updateFileContext('C:\\old.cs', 'old', { line: 0, character: 0 });
    collector.updateActiveEditor('C:\\new.cs');

    const activeFile = collector.getActiveFile();
    expect(activeFile.filepath).to.equal('C:\\new.cs');
    // Previous contents and cursor should be preserved until next updateFileContext
    expect(activeFile.contents).to.equal('old');
  });

  it('should throw StateValidationError if filepath is empty', () => {
    expect(() => {
      collector.updateFileContext('', 'contents', { line: 0, character: 0 });
    }).to.throw(StateValidationError);
  });

  it('should throw StateValidationError if cursor position is negative', () => {
    expect(() => {
      collector.updateFileContext('file.cs', 'contents', { line: -1, character: 0 });
    }).to.throw(StateValidationError);

    expect(() => {
      collector.updateFileContext('file.cs', 'contents', { line: 0, character: -5 });
    }).to.throw(StateValidationError);
  });

  it('should preserve lastUpdate timestamp on state mutation', () => {
    collector.updateFileContext('file.cs', 'v1', { line: 0, character: 0 });
    const state1 = collector.getActiveFile();
    const timestamp1 = state1 ? collector._state.lastUpdate : null;

    // Wait briefly and update again
    collector.updateFileContext('file.cs', 'v2', { line: 1, character: 0 });
    const timestamp2 = collector._state.lastUpdate;

    expect(timestamp1).to.exist;
    expect(timestamp2).to.exist;
    expect(new Date(timestamp2).getTime()).to.be.greaterThanOrEqual(new Date(timestamp1).getTime());
  });
});

// ============================================================================
// Test Suite 4: Getters & Query Methods
// ============================================================================

describe('EditorContextCollector — Getters & Query Methods', () => {
  let collector;

  beforeEach(() => {
    collector = new EditorContextCollector();
  });

  it('should return null from getActiveFile() if no file open', () => {
    expect(collector.getActiveFile()).to.be.null;
  });

  it('should return activeFile object via getActiveFile()', () => {
    collector.updateFileContext('C:\\main.cs', 'code', { line: 5, character: 10 });
    const activeFile = collector.getActiveFile();

    expect(activeFile).to.not.be.null;
    expect(activeFile.filepath).to.equal('C:\\main.cs');
    expect(activeFile.contents).to.equal('code');
  });

  it('should return null from getCursorPosition() if no file open', () => {
    expect(collector.getCursorPosition()).to.be.null;
  });

  it('should return cursor position via getCursorPosition()', () => {
    collector.updateFileContext('file.cs', 'text', { line: 42, character: 99 });
    const cursor = collector.getCursorPosition();

    expect(cursor).to.not.be.null;
    expect(cursor).to.deep.equal({ line: 42, character: 99 });
  });

  it('should return null from getSelection() if no selection', () => {
    collector.updateFileContext('file.cs', 'text', { line: 0, character: 0 });
    expect(collector.getSelection()).to.be.null;
  });
});

// ============================================================================
// Test Suite 5: Subscription Callbacks
// ============================================================================

describe('EditorContextCollector — Subscription Callbacks', () => {
  let collector;

  beforeEach(() => {
    collector = new EditorContextCollector();
  });

  it('should register a callback via onStateChange()', () => {
    const callback = () => {};
    expect(() => collector.onStateChange(callback)).to.not.throw();
  });

  it('should invoke callback when state changes', (done) => {
    let callbackInvoked = false;

    collector.onStateChange((newState, oldState) => {
      callbackInvoked = true;
      expect(newState.activeFile).to.not.be.null;
      expect(oldState.activeFile).to.be.null;
      done();
    });

    collector.updateFileContext('file.cs', 'text', { line: 0, character: 0 });
    if (!callbackInvoked) expect.fail('Callback was not invoked');
  });

  it('should invoke multiple callbacks on state change', () => {
    const calls = [];

    collector.onStateChange((newState) => calls.push('callback1'));
    collector.onStateChange((newState) => calls.push('callback2'));

    collector.updateFileContext('file.cs', 'text', { line: 0, character: 0 });

    expect(calls).to.have.lengthOf(2);
    expect(calls).to.include('callback1');
    expect(calls).to.include('callback2');
  });

  it('should pass both newState and oldState to callback', (done) => {
    collector.updateFileContext('file.cs', 'initial', { line: 0, character: 0 });

    collector.onStateChange((newState, oldState) => {
      expect(oldState.activeFile.contents).to.equal('initial');
      expect(newState.activeFile.contents).to.equal('updated');
      done();
    });

    collector.updateFileContext('file.cs', 'updated', { line: 1, character: 5 });
  });

  it('should throw TypeError if callback is not a function', () => {
    expect(() => collector.onStateChange('not a function')).to.throw(TypeError);
    expect(() => collector.onStateChange(null)).to.throw(TypeError);
    expect(() => collector.onStateChange(123)).to.throw(TypeError);
  });
});

// ============================================================================
// Test Suite 6: Cleanup & Disposal
// ============================================================================

describe('EditorContextCollector — Cleanup & Disposal', () => {
  let collector;

  beforeEach(() => {
    collector = new EditorContextCollector();
  });

  it('should remove all listeners via dispose()', () => {
    const calls = [];

    collector.onStateChange(() => calls.push('listener'));
    collector.dispose();

    collector.updateFileContext('file.cs', 'text', { line: 0, character: 0 });
    expect(calls).to.have.lengthOf(0); // Listener not invoked after dispose
  });

  it('should not invoke callbacks after dispose()', () => {
    let callbackInvoked = false;

    collector.onStateChange(() => {
      callbackInvoked = true;
    });

    collector.dispose();
    collector.updateFileContext('file.cs', 'text', { line: 0, character: 0 });

    expect(callbackInvoked).to.be.false;
  });
});

// ============================================================================
// Test Suite 7: Message Handler Integration
// ============================================================================

describe('EditorContextCollector — Message Handler Integration', () => {
  let collector;
  let mockServer;

  beforeEach(async () => {
    collector = new EditorContextCollector();
    mockServer = createMockServer();
    await collector.registerMessageHandlers(mockServer);
  });

  it('should handle "currentFile" message and update state', () => {
    mockServer.messageHandler.emit('currentFile', {
      data: {
        filepath: 'C:\\test.cs',
        contents: 'code',
        cursorPosition: { line: 5, character: 10 }
      }
    });

    const activeFile = collector.getActiveFile();
    expect(activeFile).to.not.be.null;
    expect(activeFile.filepath).to.equal('C:\\test.cs');
    expect(activeFile.cursorLine).to.equal(5);
  });

  it('should handle "didChangeActiveTextEditor" message', () => {
    collector.updateFileContext('old.cs', 'old', { line: 0, character: 0 });

    mockServer.messageHandler.emit('didChangeActiveTextEditor', {
      data: { filepath: 'C:\\new.cs' }
    });

    const activeFile = collector.getActiveFile();
    expect(activeFile.filepath).to.equal('C:\\new.cs');
  });

  it('should gracefully handle invalid "currentFile" message', () => {
    const logger = createMockLogger();
    const collector2 = new EditorContextCollector({ logger });

    // Should not throw
    collector2._handleCurrentFileMessage({ data: null });
    collector2._handleCurrentFileMessage(null);
    collector2._handleCurrentFileMessage({});

    expect(logger.getCalls().error.length).to.be.greaterThan(0);
  });

  it('should gracefully handle invalid "didChangeActiveTextEditor" message', () => {
    const logger = createMockLogger();
    const collector2 = new EditorContextCollector({ logger });

    // Should not throw
    collector2._handleDidChangeActiveTextEditorMessage({ data: {} });
    collector2._handleDidChangeActiveTextEditorMessage(null);

    expect(logger.getCalls().error.length).to.be.greaterThan(0);
  });
});

// ============================================================================
// Test Suite 8: Edge Cases & Error Handling
// ============================================================================

describe('EditorContextCollector — Edge Cases & Error Handling', () => {
  let collector;

  beforeEach(() => {
    collector = new EditorContextCollector();
  });

  it('should handle very large file contents', () => {
    const largeContents = 'x'.repeat(10_000_000); // 10MB string
    expect(() => {
      collector.updateFileContext('large.cs', largeContents, { line: 0, character: 0 });
    }).to.not.throw();

    const activeFile = collector.getActiveFile();
    expect(activeFile.contents.length).to.equal(10_000_000);
  });

  it('should handle cursor at end of file', () => {
    const contents = 'line1\nline2\nline3';
    collector.updateFileContext('file.cs', contents, { line: 2, character: 5 });

    const cursor = collector.getCursorPosition();
    expect(cursor).to.deep.equal({ line: 2, character: 5 });
  });

  it('should handle empty file contents', () => {
    expect(() => {
      collector.updateFileContext('empty.cs', '', { line: 0, character: 0 });
    }).to.not.throw();

    const activeFile = collector.getActiveFile();
    expect(activeFile.contents).to.equal('');
  });

  it('should handle state listener errors gracefully', () => {
    const logger = createMockLogger();
    const collector2 = new EditorContextCollector({ logger });

    let callCount = 0;
    collector2.onStateChange(() => {
      callCount++;
      throw new Error('Listener error');
    });

    collector2.onStateChange(() => {
      callCount++; // Second listener should still be called
    });

    expect(() => {
      collector2.updateFileContext('file.cs', 'text', { line: 0, character: 0 });
    }).to.not.throw();

    expect(callCount).to.equal(2); // Both listeners were called
    expect(logger.getCalls().error.length).to.be.greaterThan(0); // Error was logged
  });

  it('should validate contents parameter is a string', () => {
    expect(() => {
      collector.updateFileContext('file.cs', 123, { line: 0, character: 0 });
    }).to.throw(StateValidationError);

    expect(() => {
      collector.updateFileContext('file.cs', null, { line: 0, character: 0 });
    }).to.throw(StateValidationError);
  });

  it('should validate cursor position is an object', () => {
    expect(() => {
      collector.updateFileContext('file.cs', 'text', null);
    }).to.throw(StateValidationError);

    expect(() => {
      collector.updateFileContext('file.cs', 'text', 'not an object');
    }).to.throw(StateValidationError);
  });
});
