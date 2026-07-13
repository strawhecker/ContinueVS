/**
 * Debugger Mock Fixtures for Debug-Session-Handler Tests
 *
 * Provides mock debug states, stack frames, and edge cases for testing
 * the debug-session-handler.mjs without requiring a real VS IDE.
 */

/**
 * Mock debug state: Design mode (debugger stopped)
 */
export const getDesignModeState = () => ({
  state: 'stopped',
  frame: null,
  stack: [],
  sessionId: 'session-000',
});

/**
 * Mock debug state: Run mode (executing, no breakpoint)
 */
export const getRunModeState = () => ({
  state: 'running',
  frame: null,
  stack: [],
  sessionId: 'session-001',
});

/**
 * Mock debug state: Break mode with simple stack
 */
export const getBreakModeState = () => ({
  state: 'paused',
  frame: {
    file: 'C:\\Users\\dev\\project\\Program.cs',
    line: 42,
    column: 10,
    functionName: 'Main',
    locals: [
      { name: 'x', value: '5', type: 'int' },
      { name: 'message', value: '"hello"', type: 'string' },
      { name: 'list', value: '[1, 2, 3]', type: 'List<int>' },
    ],
  },
  stack: [
    {
      file: 'C:\\Users\\dev\\project\\Program.cs',
      line: 42,
      functionName: 'Main',
    },
    {
      file: 'C:\\Users\\dev\\project\\Program.cs',
      line: 28,
      functionName: 'ProcessData',
    },
    {
      file: 'C:\\Users\\dev\\project\\DataHelper.cs',
      line: 15,
      functionName: 'Load',
    },
  ],
  sessionId: 'session-002',
});

/**
 * Mock debug state: Break with many locals
 */
export const getBreakModeWithManyLocalsState = () => ({
  state: 'paused',
  frame: {
    file: 'C:\\Users\\dev\\project\\ComplexFunction.cs',
    line: 100,
    column: 5,
    functionName: 'ComplexMethod',
    locals: Array.from({ length: 50 }, (_, i) => ({
      name: `var${i}`,
      value: `value${i}`,
      type: 'object',
    })),
  },
  stack: Array.from({ length: 20 }, (_, i) => ({
    file: `C:\\Users\\dev\\project\\file${i}.cs`,
    line: 100 + i * 10,
    functionName: `Method${i}`,
  })),
  sessionId: 'session-003',
});

/**
 * Mock debug state: Break with deep stack (deeply nested calls)
 */
export const getBreakModeWithDeepStackState = () => ({
  state: 'paused',
  frame: {
    file: 'C:\\Users\\dev\\project\\Recursion.cs',
    line: 50,
    column: 0,
    functionName: 'RecursiveMethod',
    locals: [{ name: 'depth', value: '100', type: 'int' }],
  },
  stack: Array.from({ length: 100 }, (_, i) => ({
    file: 'C:\\Users\\dev\\project\\Recursion.cs',
    line: 50 + (i % 10),
    functionName: 'RecursiveMethod',
  })),
  sessionId: 'session-004',
});

/**
 * Mock debug state: Corrupted frame (malformed locals)
 */
export const getCorruptedFrameState = () => ({
  state: 'paused',
  frame: {
    file: 'C:\\Users\\dev\\project\\Program.cs',
    line: 42,
    column: 10,
    functionName: 'Main',
    locals: [
      { name: 'x', value: '5', type: 'int' },
      { name: null, value: 'bad', type: 'string' }, // Invalid: null name
      { name: 'z', value: undefined, type: 'int' }, // Invalid: undefined value
      { name: 'w', value: 'ok', type: 'bool' },
    ],
  },
  stack: [
    {
      file: 'C:\\Users\\dev\\project\\Program.cs',
      line: 42,
      functionName: 'Main',
    },
  ],
  sessionId: 'session-005',
});

/**
 * Mock debug state: Frame with null locals
 */
export const getFrameWithNullLocalsState = () => ({
  state: 'paused',
  frame: {
    file: 'C:\\Users\\dev\\project\\Program.cs',
    line: 42,
    column: 10,
    functionName: 'Main',
    locals: null,
  },
  stack: [
    {
      file: 'C:\\Users\\dev\\project\\Program.cs',
      line: 42,
      functionName: 'Main',
    },
  ],
  sessionId: 'session-006',
});

/**
 * Mock debug state: Transition sequence (stopped → running → paused)
 */
export const getTransitionSequence = () => [
  getDesignModeState(),
  { ...getRunModeState(), sessionId: 'session-seq-1' },
  { ...getBreakModeState(), sessionId: 'session-seq-1' },
  { ...getRunModeState(), sessionId: 'session-seq-1' },
  getDesignModeState(),
];

/**
 * Mock debug state: Invalid state value (for error testing)
 */
export const getInvalidStateMessage = () => ({
  state: 'invalid_state',
  frame: null,
  stack: [],
  sessionId: 'session-bad',
});

/**
 * Mock debug state: Missing sessionId
 */
export const getMissingSessionIdMessage = () => ({
  state: 'paused',
  frame: {
    file: 'test.cs',
    line: 10,
    column: 0,
    functionName: 'Foo',
    locals: [],
  },
  stack: [],
  sessionId: null,
});

/**
 * Mock debug state: Frame with special characters in file path
 */
export const getFrameWithSpecialCharsState = () => ({
  state: 'paused',
  frame: {
    file: 'C:\\Users\\dev-user\\my-project\\src\\file (1).cs',
    line: 42,
    column: 10,
    functionName: 'Method<T>',
    locals: [
      { name: 'x', value: '5', type: 'int' },
    ],
  },
  stack: [],
  sessionId: 'session-special',
});

/**
 * Mock debug state: Very large local value (edge case)
 */
export const getFrameWithLargeValueState = () => ({
  state: 'paused',
  frame: {
    file: 'C:\\Users\\dev\\project\\Program.cs',
    line: 42,
    column: 10,
    functionName: 'Main',
    locals: [
      {
        name: 'largeString',
        value: 'x'.repeat(10000),
        type: 'string',
      },
    ],
  },
  stack: [],
  sessionId: 'session-large',
});

/**
 * Mock debug state: Session ID change (new debug session starts)
 */
export const getSessionChangeSequence = () => [
  { ...getBreakModeState(), sessionId: 'session-001' },
  { ...getDesignModeState(), sessionId: 'session-001' },
  { ...getRunModeState(), sessionId: 'session-002' }, // New session ID
  { ...getBreakModeState(), sessionId: 'session-002' },
];

/**
 * Create a custom break state for flexible testing
 * @param {Object} overrides - Partial state overrides
 * @returns {Object} Merge of default break state + overrides
 */
export const createCustomBreakState = (overrides = {}) => {
  const base = getBreakModeState();
  return deepMerge(base, overrides);
};

/**
 * Deep merge utility (simple recursive merge)
 * @private
 */
function deepMerge(target, source) {
  const result = JSON.parse(JSON.stringify(target)); // Clone

  for (const key in source) {
    if (Object.prototype.hasOwnProperty.call(source, key)) {
      if (
        typeof source[key] === 'object' &&
        source[key] !== null &&
        !Array.isArray(source[key])
      ) {
        result[key] = deepMerge(result[key] || {}, source[key]);
      } else {
        result[key] = source[key];
      }
    }
  }

  return result;
}

/**
 * Get all fixture states as array (for parameterized tests)
 */
export const getAllStates = () => [
  getDesignModeState(),
  getRunModeState(),
  getBreakModeState(),
  getBreakModeWithManyLocalsState(),
  getBreakModeWithDeepStackState(),
  getCorruptedFrameState(),
  getFrameWithNullLocalsState(),
  getFrameWithSpecialCharsState(),
  getFrameWithLargeValueState(),
];

/**
 * Validator utility: Check if state matches expected structure
 */
export const validateState = (state) => {
  const errors = [];

  if (!['stopped', 'running', 'paused'].includes(state.state)) {
    errors.push(`Invalid state: ${state.state}`);
  }

  if (state.frame !== null && typeof state.frame !== 'object') {
    errors.push('frame must be null or object');
  }

  if (state.frame && typeof state.frame.file !== 'string') {
    errors.push('frame.file must be string');
  }

  if (state.frame && typeof state.frame.line !== 'number') {
    errors.push('frame.line must be number');
  }

  if (!Array.isArray(state.stack)) {
    errors.push('stack must be array');
  }

  if (typeof state.sessionId !== 'string') {
    errors.push('sessionId must be string');
  }

  return {
    valid: errors.length === 0,
    errors,
  };
};
