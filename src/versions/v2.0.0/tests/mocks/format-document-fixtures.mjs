#!/usr/bin/env node

/**
 * Format-Document Handler Test Fixtures (Step 79)
 *
 * Provides reusable mock data, sample documents, expected outputs,
 * and edit range expectations for format-document-handler tests.
 *
 * @module src/versions/v2.0.0/tests/mocks/format-document-fixtures.mjs
 * @version 1.0.0
 */

// ============================================================================
// SAMPLE UNFORMATTED DOCUMENTS
// ============================================================================

/**
 * JavaScript document with inconsistent indentation (tabs and spaces)
 */
export const UNFORMATTED_JS_TABS_SPACES = `function fibonacci(n) {
\tif (n <= 1) {
\t\treturn n;
\t}
  const x = fibonacci(n - 1);
    const y = fibonacci(n - 2);
  return x + y;
}`;

/**
 * JavaScript document with trailing whitespace and long lines
 */
export const UNFORMATTED_JS_TRAILING_WHITESPACE = `const config = {  
  name: "MyApp",  
  version: "1.0.0",  
  description: "This is a very long description that exceeds the typical line length and should be wrapped",  
  author: "Me"  
};`;

/**
 * JavaScript document with excessive blank lines
 */
export const UNFORMATTED_JS_BLANK_LINES = `function doSomething() {
  console.log("start");


  console.log("middle");



  console.log("end");
}`;

/**
 * JavaScript document with mixed indentation and alignment
 */
export const UNFORMATTED_JS_MIXED = `const vars = {
\tid:        123,
\tname:      "test",
\t  value:   42,
\t\tdata:    [1, 2, 3],
};`;

/**
 * CSS document with formatting issues
 */
export const UNFORMATTED_CSS = `.container   {
  display:  flex;
    justify-content: center;
    align-items: center;
}

  .item    {
\tmargin:   10px;
  }`;

/**
 * HTML document with inconsistent indentation
 */
export const UNFORMATTED_HTML = `<div>
  <header>
    <h1>Title</h1>
  </header>
\t<main>
\t\t<section>
  \t  <p>Content</p>
    </section>
  </main>
</div>`;

/**
 * Python document with tab/space mix
 */
export const UNFORMATTED_PYTHON = `def calculate(x, y):
\tif x > 0:
  \t  result = x + y
  \t  return result
\telse:
  \t  return 0`;

// ============================================================================
// EXPECTED FORMATTED OUTPUTS
// ============================================================================

/**
 * Expected output for UNFORMATTED_JS_TABS_SPACES (formatted with 2-space indent)
 */
export const FORMATTED_JS_TABS_SPACES = `function fibonacci(n) {
  if (n <= 1) {
    return n;
  }
  const x = fibonacci(n - 1);
  const y = fibonacci(n - 2);
  return x + y;
}`;

/**
 * Expected output for UNFORMATTED_JS_TRAILING_WHITESPACE (with trailing space removed)
 */
export const FORMATTED_JS_TRAILING_WHITESPACE = `const config = {
  name: "MyApp",
  version: "1.0.0",
  description: "This is a very long
description that exceeds the typical line length and should be
wrapped",
  author: "Me"
};`;

/**
 * Expected output for UNFORMATTED_JS_BLANK_LINES (max 2 consecutive blanks)
 */
export const FORMATTED_JS_BLANK_LINES = `function doSomething() {
  console.log("start");

  console.log("middle");

  console.log("end");
}`;

/**
 * Expected output for UNFORMATTED_JS_MIXED
 */
export const FORMATTED_JS_MIXED = `const vars = {
  id: 123,
  name: "test",
  value: 42,
  data: [1, 2, 3],
};`;

// ============================================================================
// EDIT RANGE EXPECTATIONS
// ============================================================================

/**
 * Expected edit ranges for formatting UNFORMATTED_JS_TABS_SPACES
 * Maps character offsets where changes occur
 *
 * Format: { range: {start, end}, text, description }
 */
export const EXPECTED_EDITS_TABS_SPACES = [
  {
    range: { start: 24, end: 25 },
    text: '  ',
    description: 'Convert first tab to 2 spaces',
  },
  {
    range: { start: 50, end: 51 },
    text: '    ',
    description: 'Convert double tab to 4 spaces',
  },
];

/**
 * Expected edit ranges for trailing whitespace removal
 */
export const EXPECTED_EDITS_TRAILING_WHITESPACE = [
  {
    range: { start: 20, end: 22 },
    text: '',
    description: 'Remove trailing spaces after opening brace',
  },
  {
    range: { start: 50, end: 52 },
    text: '',
    description: 'Remove trailing spaces after name line',
  },
];

/**
 * Expected edit ranges for blank line consolidation
 */
export const EXPECTED_EDITS_BLANK_LINES = [
  {
    range: { start: 48, end: 60 },
    text: '\n\n',
    description: 'Reduce 4 blank lines to 2',
  },
  {
    range: { start: 100, end: 110 },
    text: '\n\n',
    description: 'Reduce 5 blank lines to 2',
  },
];

// ============================================================================
// ERROR SCENARIO FIXTURES
// ============================================================================

/**
 * Invalid message payloads for validation testing
 */
export const INVALID_MESSAGES = [
  {
    payload: null,
    description: 'Null payload',
    expectedError: 'VALIDATION_ERROR',
  },
  {
    payload: { indent: 2 }, // Missing file
    description: 'Missing file property',
    expectedError: 'VALIDATION_ERROR',
  },
  {
    payload: { file: '', indent: 2 }, // Empty file
    description: 'Empty file string',
    expectedError: 'VALIDATION_ERROR',
  },
  {
    payload: { file: 'test.js', indent: -1 }, // Negative indent
    description: 'Negative indent',
    expectedError: 'VALIDATION_ERROR',
  },
  {
    payload: { file: 'test.js', indent: 2.5 }, // Float indent
    description: 'Float indent (not integer)',
    expectedError: 'VALIDATION_ERROR',
  },
  {
    payload: { file: 'test.js', lineLength: 30 }, // lineLength too short
    description: 'lineLength < 40',
    expectedError: 'VALIDATION_ERROR',
  },
  {
    payload: { file: 'test.js', lineLength: 250 }, // lineLength too long
    description: 'lineLength > 200',
    expectedError: 'VALIDATION_ERROR',
  },
];

// ============================================================================
// PERFORMANCE TEST DOCUMENTS
// ============================================================================

/**
 * Generate a document with N lines for performance testing
 *
 * @param {number} lineCount - Number of lines to generate
 * @returns {string} Generated document text
 *
 * @example
 * const doc = generatePerformanceDoc(100);
 * // "  const var0 = 0;\n  const var1 = 1;\n..."
 */
export function generatePerformanceDoc(lineCount) {
  const lines = Array.from(
    { length: lineCount },
    (_, i) => `  const variable${i} = ${i}; // Comment for line ${i}`,
  );
  return lines.join('\n');
}

/**
 * 100-line document (typical case)
 */
export const PERF_DOC_100_LINES = generatePerformanceDoc(100);

/**
 * 1000-line document (large case)
 */
export const PERF_DOC_1000_LINES = generatePerformanceDoc(1000);

/**
 * 5000-line document (extreme case)
 */
export const PERF_DOC_5000_LINES = generatePerformanceDoc(5000);

// ============================================================================
// MOCK DOCUMENT PROVIDER HELPERS
// ============================================================================

/**
 * Create a mock DocumentProvider with predefined documents
 *
 * @param {Object} documentMap - Map of {filepath: {text, language}}
 * @returns {Object} Mock DocumentProvider instance
 *
 * @example
 * const provider = createMockDocumentProvider({
 *   'src/index.js': { text: 'console.log("hi");', language: 'javascript' }
 * });
 */
export function createMockDocumentProvider(documentMap = {}) {
  return {
    getDocument: (file) => {
      if (documentMap[file]) {
        return {
          ...documentMap[file],
          filepath: file,
          isDirty: false,
          encoding: 'utf-8',
        };
      }
      return null;
    },
  };
}

/**
 * Create a DocumentProvider that throws errors (for error recovery testing)
 *
 * @returns {Object} Mock DocumentProvider that throws on getDocument
 */
export function createErrorDocumentProvider() {
  return {
    getDocument: () => {
      throw new Error('DocumentProvider error');
    },
  };
}

/**
 * Create a DocumentProvider that tracks calls (for integration testing)
 *
 * @param {Object} documentMap - Map of documents
 * @returns {Object} Mock DocumentProvider with call tracking
 */
export function createTrackedDocumentProvider(documentMap = {}) {
  const provider = createMockDocumentProvider(documentMap);
  provider._calls = [];

  const originalGetDocument = provider.getDocument.bind(provider);
  provider.getDocument = function (file) {
    provider._calls.push({ method: 'getDocument', file, timestamp: Date.now() });
    return originalGetDocument(file);
  };

  return provider;
}

// ============================================================================
// FORMATTING CONFIGURATION FIXTURES
// ============================================================================

/**
 * Common formatting configurations for testing
 */
export const FORMAT_CONFIGS = {
  COMPACT_2_SPACE: { indent: 2, lineLength: 80 },
  RELAXED_4_SPACE: { indent: 4, lineLength: 100 },
  STRICT_2_SPACE: { indent: 2, lineLength: 70 },
  WIDE_2_SPACE: { indent: 2, lineLength: 120 },
  TIGHT_4_SPACE: { indent: 4, lineLength: 60 },
};

// ============================================================================
// ASSERTION HELPERS
// ============================================================================

/**
 * Verify that edits produce the expected formatted output
 *
 * @param {string} original - Original text
 * @param {Array} edits - Array of edit objects {range, text}
 * @param {string} expected - Expected formatted text
 * @returns {boolean} True if edits transform original to expected
 *
 * @example
 * const result = verifyEditsProduceExpected(original, edits, expected);
 */
export function verifyEditsProduceExpected(original, edits, expected) {
  let result = original;

  // Sort edits in reverse order to apply from end to start (preserves offsets)
  const sortedEdits = [...edits].sort((a, b) => b.range.start - a.range.start);

  for (const edit of sortedEdits) {
    const { range, text } = edit;
    result = result.slice(0, range.start) + text + result.slice(range.end);
  }

  return result === expected;
}

/**
 * Verify that edits do not overlap
 *
 * @param {Array} edits - Array of edit objects {range, text}
 * @returns {boolean} True if no edits overlap
 *
 * @example
 * const result = verifyEditsNonOverlapping(edits);
 */
export function verifyEditsNonOverlapping(edits) {
  for (let i = 0; i < edits.length - 1; i++) {
    if (edits[i].range.end > edits[i + 1].range.start) {
      return false;
    }
  }
  return true;
}

/**
 * Verify that all lines respect maximum line length
 *
 * @param {string} text - Text to check
 * @param {number} maxLength - Maximum line length
 * @returns {boolean} True if all lines are within maxLength
 *
 * @example
 * const result = verifyLineLength(formatted, 80);
 */
export function verifyLineLength(text, maxLength) {
  const lines = text.split('\n');
  return lines.every((line) => line.length <= maxLength);
}

/**
 * Verify that text has no trailing whitespace
 *
 * @param {string} text - Text to check
 * @returns {boolean} True if no lines have trailing whitespace
 *
 * @example
 * const result = verifyNoTrailingWhitespace(formatted);
 */
export function verifyNoTrailingWhitespace(text) {
  const lines = text.split('\n');
  return lines.every((line) => !line.endsWith(' ') && !line.endsWith('\t'));
}

/**
 * Verify consistent indentation throughout text
 *
 * @param {string} text - Text to check
 * @param {string} indentChar - Expected indent character ('space' or 'tab')
 * @returns {boolean} True if indentation is consistent
 *
 * @example
 * const result = verifyConsistentIndent(formatted, 'space');
 */
export function verifyConsistentIndent(text, indentChar = 'space') {
  const lines = text.split('\n');
  const badChar = indentChar === 'space' ? '\t' : ' ';

  for (const line of lines) {
    const leadingMatch = line.match(/^(\s*)/);
    if (leadingMatch && leadingMatch[1].includes(badChar)) {
      return false;
    }
  }
  return true;
}

export default {
  UNFORMATTED_JS_TABS_SPACES,
  UNFORMATTED_JS_TRAILING_WHITESPACE,
  UNFORMATTED_JS_BLANK_LINES,
  UNFORMATTED_JS_MIXED,
  UNFORMATTED_CSS,
  UNFORMATTED_HTML,
  UNFORMATTED_PYTHON,
  FORMATTED_JS_TABS_SPACES,
  FORMATTED_JS_TRAILING_WHITESPACE,
  FORMATTED_JS_BLANK_LINES,
  FORMATTED_JS_MIXED,
  EXPECTED_EDITS_TABS_SPACES,
  EXPECTED_EDITS_TRAILING_WHITESPACE,
  EXPECTED_EDITS_BLANK_LINES,
  INVALID_MESSAGES,
  PERF_DOC_100_LINES,
  PERF_DOC_1000_LINES,
  PERF_DOC_5000_LINES,
  FORMAT_CONFIGS,
  createMockDocumentProvider,
  createErrorDocumentProvider,
  createTrackedDocumentProvider,
  generatePerformanceDoc,
  verifyEditsProduceExpected,
  verifyEditsNonOverlapping,
  verifyLineLength,
  verifyNoTrailingWhitespace,
  verifyConsistentIndent,
};
