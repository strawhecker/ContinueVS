/**
 * Test Fixtures for Diff-Viewer Handler (Step 92)
 *
 * Provides sample documents, expected diffs, hunks, and mock dependencies
 * for comprehensive testing of diff generation and hunk application.
 *
 * @module src/versions/v2.0.0/tests/mocks/diff-viewer-fixtures.mjs
 */

// ============================================================================
// SAMPLE DOCUMENTS
// ============================================================================

/**
 * Simple single-line addition
 */
export const SIMPLE_ORIGINAL = `function greet() {
  console.log('Hello');
}`;

export const SIMPLE_MODIFIED = `function greet() {
  console.log('Hello');
  console.log('World');
}`;

/**
 * Multi-hunk complex refactoring
 */
export const COMPLEX_ORIGINAL = `class Calculator {
  add(a, b) {
    return a + b;
  }

  subtract(a, b) {
    return a - b;
  }

  multiply(a, b) {
    return a * b;
  }

  divide(a, b) {
    return a / b;
  }
}`;

export const COMPLEX_MODIFIED = `class Calculator {
  add(a, b) {
    return a + b;
  }

  subtract(a, b) {
    return a - b;
  }

  multiply(a, b) {
    if (b === 0) throw new Error('Zero multiplier');
    return a * b;
  }

  divide(a, b) {
    if (b === 0) throw new Error('Division by zero');
    return a / b;
  }

  power(a, b) {
    return Math.pow(a, b);
  }
}`;

/**
 * Identical documents (empty diff)
 */
export const IDENTICAL_ORIGINAL = `const x = 42;
const y = 24;`;

export const IDENTICAL_MODIFIED = `const x = 42;
const y = 24;`;

/**
 * Large document for performance testing
 */
export const LARGE_ORIGINAL = Array(500)
  .fill(0)
  .map((_, i) => `line ${i + 1}: const var${i} = ${i};`)
  .join('\n');

export const LARGE_MODIFIED = Array(500)
  .fill(0)
  .map((_, i) => {
    if (i === 100) return `line ${i + 1}: const var${i} = ${i}; // modified`;
    if (i === 250) return `line ${i + 1}: const var${i} = ${i}; // changed`;
    return `line ${i + 1}: const var${i} = ${i};`;
  })
  .join('\n');

/**
 * Unicode content for safety testing
 */
export const UNICODE_ORIGINAL = `function hello() {
  console.log('Hello 世界');
}`;

export const UNICODE_MODIFIED = `function hello() {
  console.log('Hello 世界');
  console.log('Привет мир');
}`;

/**
 * Binary-like content (should gracefully fail)
 */
export const BINARY_CONTENT = '\x00\x01\x02\x03\x04\xFF\xFE';

// ============================================================================
// EXPECTED DIFF RESULTS
// ============================================================================

/**
 * Expected diff result for simple modification
 */
export const SIMPLE_EXPECTED_DIFF = {
  hunksCount: 1,
  linesAdded: 1,
  linesRemoved: 0,
  hunks: 1,
};

/**
 * Expected diff result for complex modification
 */
export const COMPLEX_EXPECTED_DIFF = {
  hunksCount: 2,
  linesAdded: 5,
  linesRemoved: 0,
  hunks: 2,
};

/**
 * Expected diff result for identical documents
 */
export const IDENTICAL_EXPECTED_DIFF = {
  hunksCount: 0,
  linesAdded: 0,
  linesRemoved: 0,
  hunks: 0,
};

// ============================================================================
// MOCK DOCUMENT PROVIDER
// ============================================================================

/**
 * Create a mock DocumentProvider for testing
 *
 * @param {Object} documents - Map of filePath → { text: string }
 * @returns {Object} Mock DocumentProvider with queryDocument method
 *
 * @example
 * const provider = createMockDocumentProvider({
 *   'test.js': { text: 'const x = 1;' }
 * });
 *
 * const doc = await provider.queryDocument('test.js');
 * // → { text: 'const x = 1;' }
 */
export function createMockDocumentProvider(documents = {}) {
  return {
    async queryDocument(filePath) {
      if (filePath in documents) {
        return documents[filePath];
      }
      return null;
    },
  };
}

/**
 * Create a mock logger for testing
 *
 * @returns {Object} Mock logger with info, warn, error methods
 */
export function createMockLogger() {
  return {
    logs: [],
    info(...args) {
      this.logs.push({ level: 'info', args });
    },
    warn(...args) {
      this.logs.push({ level: 'warn', args });
    },
    error(...args) {
      this.logs.push({ level: 'error', args });
    },
  };
}

/**
 * Create a mock metrics collector for testing
 *
 * @returns {Object} Mock metrics with record method
 */
export function createMockMetrics() {
  return {
    events: [],
    record(event) {
      this.events.push(event);
    },
  };
}

// ============================================================================
// HELPER VALIDATION FUNCTIONS
// ============================================================================

/**
 * Verify that a diff has expected number of hunks
 *
 * @param {Object} diffResult - Diff result from generateUnifiedDiff
 * @param {number} expectedHunks - Expected hunk count
 * @returns {boolean} True if hunks match
 */
export function verifyHunkCount(diffResult, expectedHunks) {
  return diffResult.hunks && diffResult.hunks.length === expectedHunks;
}

/**
 * Verify that diff statistics are within expected range
 *
 * @param {Object} stats - Diff statistics
 * @param {number} maxLinesAdded - Maximum expected lines added
 * @param {number} maxLinesRemoved - Maximum expected lines removed
 * @returns {boolean} True if within range
 */
export function verifyDiffStats(stats, maxLinesAdded, maxLinesRemoved) {
  return (
    stats.linesAdded <= maxLinesAdded &&
    stats.linesRemoved <= maxLinesRemoved
  );
}

/**
 * Verify that diff text is in valid unified format
 *
 * @param {string} diffText - Diff text
 * @returns {boolean} True if valid unified diff format
 */
export function verifyUnifiedDiffFormat(diffText) {
  const lines = diffText.split('\n');
  return (
    lines[0].startsWith('---') &&
    lines[1].startsWith('+++') &&
    lines.slice(2).every(l => l === '' || l.startsWith('@') || l[0] === ' ' || l[0] === '+' || l[0] === '-')
  );
}

/**
 * Verify that all hunks have required properties
 *
 * @param {Object[]} hunks - Array of hunks
 * @returns {boolean} True if all hunks are valid
 */
export function verifyHunkStructure(hunks) {
  return hunks.every(
    h =>
      typeof h.startLine === 'number' &&
      typeof h.lineCount === 'number' &&
      typeof h.newStartLine === 'number' &&
      typeof h.newLineCount === 'number' &&
      Array.isArray(h.lines) &&
      h.lines.every(
        l =>
          ['add', 'remove', 'context'].includes(l.type) &&
          typeof l.value === 'string'
      )
  );
}

/**
 * Verify that edits have correct range structure
 *
 * @param {Object[]} edits - Array of edits
 * @returns {boolean} True if all edits are valid
 */
export function verifyEditStructure(edits) {
  return edits.every(
    e =>
      typeof e.range === 'object' &&
      typeof e.range.start === 'number' &&
      typeof e.range.end === 'number' &&
      e.range.start <= e.range.end &&
      typeof e.text === 'string'
  );
}

// ============================================================================
// ERROR CASES
// ============================================================================

/**
 * Test case: File not found
 */
export const ERROR_FILE_NOT_FOUND = {
  filePath: '/nonexistent/file.js',
  targetPath: '/other/file.js',
  expectError: true,
  errorCode: 'DIFF_GENERATION_ERROR',
};

/**
 * Test case: Invalid message structure
 */
export const ERROR_INVALID_MESSAGE = {
  messageType: 'bridge:getDiff',
  data: {
    // Missing required filePath
    targetPath: 'target.js',
  },
  expectError: true,
  errorCode: 'DIFF_VALIDATION_ERROR',
};

/**
 * Test case: Missing both targetPath and targetContent
 */
export const ERROR_MISSING_TARGET = {
  filePath: 'source.js',
  // Missing targetPath and targetContent
  expectError: true,
  errorCode: 'DIFF_VALIDATION_ERROR',
};

/**
 * Test case: Invalid hunk indices
 */
export const ERROR_INVALID_HUNKS = {
  filePath: 'test.js',
  hunks: null, // Not an array
  expectError: true,
  errorCode: 'DIFF_VALIDATION_ERROR',
};

export default {
  SIMPLE_ORIGINAL,
  SIMPLE_MODIFIED,
  COMPLEX_ORIGINAL,
  COMPLEX_MODIFIED,
  IDENTICAL_ORIGINAL,
  IDENTICAL_MODIFIED,
  LARGE_ORIGINAL,
  LARGE_MODIFIED,
  UNICODE_ORIGINAL,
  UNICODE_MODIFIED,
  BINARY_CONTENT,
  createMockDocumentProvider,
  createMockLogger,
  createMockMetrics,
  verifyHunkCount,
  verifyDiffStats,
  verifyUnifiedDiffFormat,
  verifyHunkStructure,
  verifyEditStructure,
};
