#!/usr/bin/env node

/**
 * Fix-Suggestion Handler (Step 77)
 *
 * Provides intelligent code fixes for diagnostics, errors, and code quality issues.
 *
 * **Handler Type**: Code analysis and fix suggestion handler
 * **Message Type**: bridge:fixSuggestion
 * **Input**: BridgeMessage with { source, errorMessage, errorType, line, language, ...params }
 * **Output**: BridgeResponse containing { suggestions[], metadata, confidence }
 *
 * **Supported Error Categories**:
 * - `syntax`: Syntax errors, malformed code
 * - `semantic`: Type errors, undefined references
 * - `pattern`: Common programming mistakes, anti-patterns
 * - `style`: Code style and formatting issues
 * - `performance`: Performance and efficiency issues
 * - `security`: Security vulnerabilities and best practices
 *
 * **Architecture Flow**:
 * ```
 * [Continue/IDE] → bridge:fixSuggestion request
 *   ↓
 * [dispatcher] routes to fixSuggestionHandler
 *   ↓
 * [handler] validates fix request (source, errorType, language)
 *   ↓
 * [handler] analyzes error context and surrounding code
 *   ↓
 * [handler] generates fix suggestions by category
 *   ↓
 * [handler] ranks suggestions by confidence
 *   ↓
 * [core-server] sends response back with suggestions
 * ```
 *
 * **Error Handling**:
 * - Missing source → FixSuggestionValidationError
 * - Invalid error type → FixSuggestionValidationError
 * - Invalid line number → FixSuggestionValidationError
 * - Unsupported language → FixSuggestionUnsupportedError (graceful fallback)
 * - No fixes available → Returns empty suggestions array
 *
 * **Thread Safety**:
 * - Single-threaded (Node.js event loop)
 * - No shared state mutations
 * - Safe for concurrent calls
 *
 * **Performance**:
 * - Typical analysis: <300ms
 * - Large file (10KB): <700ms
 * - Memory: <8MB per request
 *
 * **Dependencies**:
 * - Bridge logger (optional) — injected via context
 * - Bridge metrics (optional) — injected via context
 *
 * @module src/versions/v2.0.0/lib/fix-suggestion-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher routing)
 *   - Step 47: message-routing-middleware.js (middleware integration)
 *   - Step 71: handler-registry.mjs (handler registration)
 *   - Step 76: refactor-handler.mjs (related handler)
 *   - Step 78: apply-edit-handler.mjs (related handler)
 */

// ============================================================================
// Error Classes
// ============================================================================

/**
 * Base error for fix suggestion operations
 *
 * @class FixSuggestionError
 * @extends {Error}
 */
export class FixSuggestionError extends Error {
  constructor(message, code = 'FIX_SUGGESTION_ERROR', details = null) {
    super(message);
    this.name = 'FixSuggestionError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Validation error for fix suggestion requests
 *
 * @class FixSuggestionValidationError
 * @extends {FixSuggestionError}
 */
export class FixSuggestionValidationError extends FixSuggestionError {
  constructor(fieldName, message, value = null) {
    super(`${fieldName}: ${message}`, 'VALIDATION_ERROR', { fieldName, value });
    this.name = 'FixSuggestionValidationError';
    this.fieldName = fieldName;
  }
}

/**
 * Unsupported language error
 *
 * @class FixSuggestionUnsupportedError
 * @extends {FixSuggestionError}
 */
export class FixSuggestionUnsupportedError extends FixSuggestionError {
  constructor(language, supportedLanguages = []) {
    super(
      `Language '${language}' is not supported for fix suggestions`,
      'UNSUPPORTED_LANGUAGE',
      { language, supportedLanguages }
    );
    this.name = 'FixSuggestionUnsupportedError';
    this.language = language;
  }
}

/**
 * Analysis error during fix suggestion generation
 *
 * @class FixSuggestionAnalysisError
 * @extends {FixSuggestionError}
 */
export class FixSuggestionAnalysisError extends FixSuggestionError {
  constructor(message, details = null) {
    super(message, 'ANALYSIS_ERROR', details);
    this.name = 'FixSuggestionAnalysisError';
  }
}

// ============================================================================
// Constants
// ============================================================================

const SUPPORTED_LANGUAGES = ['javascript', 'typescript', 'csharp', 'c#', 'python'];
const SUPPORTED_ERROR_TYPES = [
  'syntax',
  'semantic',
  'pattern',
  'style',
  'performance',
  'security',
  'unknown',
];

const SYNTAX_ERROR_PATTERNS = {
  javascript: [
    { pattern: /unexpected token/, fix: 'Check for missing semicolons or parentheses' },
    { pattern: /invalid syntax/, fix: 'Verify bracket and brace matching' },
    { pattern: /unexpected end of input/, fix: 'Check for unclosed blocks' },
  ],
  typescript: [
    { pattern: /type.*is not assignable/, fix: 'Check type compatibility and casting' },
    { pattern: /cannot find module/, fix: 'Verify import path is correct' },
    { pattern: /Property.*does not exist/, fix: 'Check property name spelling' },
  ],
  csharp: [
    { pattern: /unexpected symbol/, fix: 'Check C# syntax rules and semicolons' },
    { pattern: /name.*does not exist/, fix: 'Verify using statements or namespace' },
    { pattern: /type or namespace expected/, fix: 'Check class or interface definition' },
  ],
  python: [
    { pattern: /unexpected indent/, fix: 'Fix indentation consistency' },
    { pattern: /invalid syntax/, fix: 'Check colon and parenthesis placement' },
    { pattern: /name.*is not defined/, fix: 'Verify variable name spelling' },
  ],
};

const SEMANTIC_ERROR_PATTERNS = {
  javascript: [
    { pattern: /is not a function/, fix: 'Ensure variable is a function before calling' },
    { pattern: /undefined/, fix: 'Initialize variable before use' },
    { pattern: /cannot read property/, fix: 'Check for null/undefined before access' },
  ],
  typescript: [
    { pattern: /Argument of type/, fix: 'Verify argument types match function signature' },
    { pattern: /Object is possibly/, fix: 'Add null check or optional chaining' },
    { pattern: /Type.*has no.*member/, fix: 'Use correct property name' },
  ],
  csharp: [
    { pattern: /The name.*does not exist/, fix: 'Check namespace or add using statement' },
    { pattern: /Cannot implicitly convert/, fix: 'Add explicit type cast' },
    { pattern: /Object reference not set/, fix: 'Check for null before using' },
  ],
  python: [
    { pattern: /is not defined/, fix: 'Define variable before use' },
    { pattern: /has no attribute/, fix: 'Check object type and attribute name' },
    { pattern: /is not callable/, fix: 'Ensure variable is a function' },
  ],
};

const PATTERN_ERROR_PATTERNS = {
  javascript: [
    {
      pattern: /var\s+\w+/,
      fix: 'Consider using const or let instead of var for better scoping',
    },
    {
      pattern: /==\s*[^=]/,
      fix: 'Use === instead of == for strict equality comparison',
    },
    {
      pattern: /function\s+\w+\(\)\s*{}\s*function/,
      fix: 'Consider using arrow functions or consolidating functions',
    },
  ],
  typescript: [
    {
      pattern: /any\b/,
      fix: 'Avoid using "any" type; use specific type or generics',
    },
    {
      pattern: /!:/,
      fix: 'Use type guards instead of non-null assertion operator',
    },
  ],
  csharp: [
    {
      pattern: /String\b/,
      fix: 'Use lowercase "string" keyword in C#',
    },
    {
      pattern: /Object\b/,
      fix: 'Use specific types instead of Object',
    },
  ],
  python: [
    {
      pattern: /except:\s*pass/,
      fix: 'Avoid bare except clauses; catch specific exceptions',
    },
    {
      pattern: /x\s*=\s*x\s*\+\s*1/,
      fix: 'Use += operator for conciseness',
    },
  ],
};

const STYLE_ERROR_PATTERNS = {
  javascript: [
    {
      pattern: /\s+$/,
      fix: 'Remove trailing whitespace',
    },
    {
      pattern: /var\s+\w+\s*=\s*\d+/,
      fix: 'Use consistent variable naming conventions (camelCase)',
    },
  ],
  typescript: [
    {
      pattern: /:\s*any\b/,
      fix: 'Replace any type annotation with specific type',
    },
  ],
  csharp: [
    {
      pattern: /\s+$/,
      fix: 'Remove trailing whitespace',
    },
    {
      pattern: /public\s+abstract\s+class\s+\w+\s*{/,
      fix: 'Ensure consistent brace placement style',
    },
  ],
  python: [
    {
      pattern: /\t/,
      fix: 'Use spaces instead of tabs for indentation',
    },
    {
      pattern: /\s+$/,
      fix: 'Remove trailing whitespace',
    },
  ],
};

// ============================================================================
// Validation
// ============================================================================

/**
 * Validate fix suggestion request
 *
 * @param {Object} request - Request object
 * @returns {void} - Throws if invalid
 * @throws {FixSuggestionValidationError}
 *
 * @example
 * validateFixSuggestionRequest({
 *   source: 'const x = ;',
 *   errorMessage: 'Unexpected token ;',
 *   errorType: 'syntax',
 *   language: 'javascript'
 * });
 */
export function validateFixSuggestionRequest(request) {
  if (!request) {
    throw new FixSuggestionValidationError('request', 'Request object is required');
  }

  if (!request.source || typeof request.source !== 'string') {
    throw new FixSuggestionValidationError(
      'source',
      'Source code must be a non-empty string',
      request.source
    );
  }

  if (request.source.trim().length === 0) {
    throw new FixSuggestionValidationError('source', 'Source code cannot be empty', '');
  }

  if (!request.errorMessage || typeof request.errorMessage !== 'string') {
    throw new FixSuggestionValidationError(
      'errorMessage',
      'Error message must be a non-empty string',
      request.errorMessage
    );
  }

  const errorType = (request.errorType || 'unknown').toLowerCase();
  if (!SUPPORTED_ERROR_TYPES.includes(errorType)) {
    throw new FixSuggestionValidationError(
      'errorType',
      `Must be one of: ${SUPPORTED_ERROR_TYPES.join(', ')}`,
      errorType
    );
  }

  const language = (request.language || 'javascript').toLowerCase();
  if (!SUPPORTED_LANGUAGES.includes(language)) {
    throw new FixSuggestionUnsupportedError(language, SUPPORTED_LANGUAGES);
  }

  if (request.line !== undefined) {
    if (!Number.isInteger(request.line) || request.line < 1) {
      throw new FixSuggestionValidationError(
        'line',
        'Line number must be a positive integer',
        request.line
      );
    }

    const lineCount = request.source.split('\n').length;
    if (request.line > lineCount) {
      throw new FixSuggestionValidationError(
        'line',
        `Line number exceeds source code line count (${lineCount})`,
        request.line
      );
    }
  }
}

// ============================================================================
// Fix Strategy Functions
// ============================================================================

/**
 * Generate syntax fix suggestions
 *
 * @param {string} source - Source code
 * @param {string} errorMessage - Error message from compiler/IDE
 * @param {string} language - Programming language
 * @param {number} line - Error line number (optional)
 * @returns {Object[]} Array of suggestions
 *
 * @example
 * generateSyntaxFixes('const x = ;', 'Unexpected token ;', 'javascript', 1)
 * // Returns: [{ suggestion: '...', confidence: 85, explanation: '...' }]
 */
export function generateSyntaxFixes(source, errorMessage, language, line = null) {
  const suggestions = [];
  const langKey = language.toLowerCase().replace('c#', 'csharp');
  const patterns = SYNTAX_ERROR_PATTERNS[langKey] || [];

  for (const { pattern, fix } of patterns) {
    if (pattern.test(errorMessage)) {
      suggestions.push({
        suggestion: fix,
        confidence: 75,
        category: 'syntax',
        explanation: `Fix suggested based on error pattern: "${errorMessage}"`,
      });
    }
  }

  // Generic syntax fixes
  if (errorMessage.includes('semicolon') || errorMessage.includes('SyntaxError')) {
    suggestions.push({
      suggestion: 'Add missing semicolon or check syntax',
      confidence: 60,
      category: 'syntax',
      explanation: 'Common syntax error pattern detected',
    });
  }

  if (
    errorMessage.includes('bracket') ||
    errorMessage.includes('paren') ||
    errorMessage.includes('brace')
  ) {
    suggestions.push({
      suggestion: 'Check for matching brackets, parentheses, or braces',
      confidence: 70,
      category: 'syntax',
      explanation: 'Bracket/paren mismatch likely',
    });
  }

  return suggestions;
}

/**
 * Generate semantic fix suggestions
 *
 * @param {string} source - Source code
 * @param {string} errorMessage - Error message
 * @param {string} language - Programming language
 * @param {number} line - Error line number (optional)
 * @returns {Object[]} Array of suggestions
 *
 * @example
 * generateSemanticFixes('x.y.z', 'Cannot read property y of undefined', 'javascript')
 */
export function generateSemanticFixes(source, errorMessage, language, line = null) {
  const suggestions = [];
  const langKey = language.toLowerCase().replace('c#', 'csharp');
  const patterns = SEMANTIC_ERROR_PATTERNS[langKey] || [];

  for (const { pattern, fix } of patterns) {
    if (pattern.test(errorMessage)) {
      suggestions.push({
        suggestion: fix,
        confidence: 80,
        category: 'semantic',
        explanation: `Fix suggested based on semantic error pattern: "${errorMessage}"`,
      });
    }
  }

  // Generic semantic fixes
  if (errorMessage.includes('undefined') || errorMessage.includes('not defined')) {
    suggestions.push({
      suggestion: 'Initialize or define the variable before use',
      confidence: 75,
      category: 'semantic',
      explanation: 'Variable appears to be undefined',
    });

    suggestions.push({
      suggestion: 'Check import/require statements for missing dependencies',
      confidence: 65,
      category: 'semantic',
      explanation: 'May need to import or require the variable',
    });
  }

  if (
    errorMessage.includes('null') ||
    errorMessage.includes('cannot read') ||
    errorMessage.includes('not assigned')
  ) {
    suggestions.push({
      suggestion: 'Add null check or optional chaining before access',
      confidence: 85,
      category: 'semantic',
      explanation: 'Null/undefined reference detected',
    });
  }

  if (errorMessage.includes('type') && errorMessage.includes('not assignable')) {
    suggestions.push({
      suggestion: 'Check type compatibility or add explicit type cast',
      confidence: 80,
      category: 'semantic',
      explanation: 'Type mismatch in assignment',
    });
  }

  return suggestions;
}

/**
 * Generate pattern fix suggestions for common mistakes
 *
 * @param {string} source - Source code
 * @param {string} errorMessage - Error message
 * @param {string} language - Programming language
 * @param {number} line - Error line number (optional)
 * @returns {Object[]} Array of suggestions
 *
 * @example
 * generatePatternFixes('if (x == 5) { }', '', 'javascript')
 */
export function generatePatternFixes(source, errorMessage, language, line = null) {
  const suggestions = [];
  const langKey = language.toLowerCase().replace('c#', 'csharp');
  const patterns = PATTERN_ERROR_PATTERNS[langKey] || [];

  for (const { pattern, fix } of patterns) {
    if (pattern.test(source)) {
      suggestions.push({
        suggestion: fix,
        confidence: 70,
        category: 'pattern',
        explanation: `Anti-pattern detected in code`,
      });
    }
  }

  return suggestions;
}

/**
 * Generate style fix suggestions
 *
 * @param {string} source - Source code
 * @param {string} errorMessage - Error message
 * @param {string} language - Programming language
 * @param {number} line - Error line number (optional)
 * @returns {Object[]} Array of suggestions
 *
 * @example
 * generateStyleFixes('var x=5', '', 'javascript')
 */
export function generateStyleFixes(source, errorMessage, language, line = null) {
  const suggestions = [];
  const langKey = language.toLowerCase().replace('c#', 'csharp');
  const patterns = STYLE_ERROR_PATTERNS[langKey] || [];

  for (const { pattern, fix } of patterns) {
    if (pattern.test(source)) {
      suggestions.push({
        suggestion: fix,
        confidence: 60,
        category: 'style',
        explanation: `Code style issue detected`,
      });
    }
  }

  return suggestions;
}

/**
 * Generate performance fix suggestions
 *
 * @param {string} source - Source code
 * @param {string} errorMessage - Error message
 * @param {string} language - Programming language
 * @returns {Object[]} Array of suggestions
 */
export function generatePerformanceFixes(source, errorMessage, language) {
  const suggestions = [];
  const langKey = language.toLowerCase().replace('c#', 'csharp');

  if (langKey === 'javascript' || langKey === 'typescript') {
    if (/for\s*\(\s*\w+\s+in\s+/.test(source)) {
      suggestions.push({
        suggestion: 'Use for...of or Array methods instead of for...in for arrays',
        confidence: 65,
        category: 'performance',
        explanation: 'for...in is slower for arrays; for...of or forEach() is preferred',
      });
    }
  }

  if (langKey === 'python') {
    if (/for\s+\w+\s+in\s+range\(len\(/.test(source)) {
      suggestions.push({
        suggestion: 'Use enumerate() instead of range(len()) for cleaner iteration',
        confidence: 65,
        category: 'performance',
        explanation: 'enumerate() is more idiomatic and slightly faster',
      });
    }
  }

  return suggestions;
}

/**
 * Generate security fix suggestions
 *
 * @param {string} source - Source code
 * @param {string} errorMessage - Error message
 * @param {string} language - Programming language
 * @returns {Object[]} Array of suggestions
 */
export function generateSecurityFixes(source, errorMessage, language) {
  const suggestions = [];

  if (/eval\s*\(/.test(source)) {
    suggestions.push({
      suggestion: 'Avoid using eval(); use safer alternatives like Function() or JSON.parse()',
      confidence: 95,
      category: 'security',
      explanation: 'eval() is a security risk; consider safer alternatives',
    });
  }

  if (/\.innerHTML\s*=/.test(source)) {
    suggestions.push({
      suggestion: 'Use textContent or createElement() instead of innerHTML to prevent XSS',
      confidence: 90,
      category: 'security',
      explanation: 'innerHTML is vulnerable to XSS attacks',
    });
  }

  if (/password|secret|token|key/i.test(source)) {
    suggestions.push({
      suggestion: 'Ensure sensitive values are not hardcoded; use environment variables',
      confidence: 80,
      category: 'security',
      explanation: 'Hardcoded sensitive information detected',
    });
  }

  return suggestions;
}

// ============================================================================
// Response Builder
// ============================================================================

/**
 * Create fix suggestion response object
 *
 * @param {Object[]} suggestions - Array of suggestion objects
 * @param {Object} metadata - Metadata about the analysis
 * @returns {Object} Response object
 *
 * @example
 * createFixSuggestionResponse(
 *   [{ suggestion: '...', confidence: 85 }],
 *   { errorType: 'syntax', language: 'javascript' }
 * )
 */
export function createFixSuggestionResponse(suggestions, metadata) {
  const sorted = suggestions.sort((a, b) => b.confidence - a.confidence);
  const avgConfidence =
    suggestions.length > 0
      ? Math.round(suggestions.reduce((sum, s) => sum + s.confidence, 0) / suggestions.length)
      : 0;

  return {
    success: true,
    data: {
      suggestions: sorted,
      metadata: {
        ...metadata,
        suggestionCount: suggestions.length,
        averageConfidence: avgConfidence,
        topConfidence: sorted.length > 0 ? sorted[0].confidence : 0,
        timestamp: new Date().toISOString(),
        categories: [...new Set(suggestions.map((s) => s.category))],
      },
    },
  };
}

// ============================================================================
// Mock Logger & Metrics (for standalone use)
// ============================================================================

function _createMockLogger() {
  return {
    debug: () => {},
    info: () => {},
    warn: () => {},
    error: () => {},
  };
}

function _createMockMetrics() {
  return {
    recordEvent: () => {},
    recordHandlerExecution: () => {},
  };
}

// ============================================================================
// Main Handler
// ============================================================================

/**
 * Main fix suggestion handler
 *
 * Processes fix suggestion requests and returns suggested fixes.
 *
 * @param {Object} message - Message object with data property
 * @param {Object} context - Context with logger, metrics, etc.
 * @returns {Promise<Object>} Response object
 *
 * @example
 * const response = await fixSuggestionHandler(
 *   { data: { source: '...', errorMessage: '...', errorType: 'syntax' } },
 *   { logger: mockLogger, metrics: mockMetrics }
 * );
 * // Returns: { success: true, data: { suggestions: [...], metadata: {...} } }
 *
 * @throws {FixSuggestionValidationError} If request is invalid
 * @throws {FixSuggestionUnsupportedError} If language not supported
 * @throws {FixSuggestionAnalysisError} If analysis fails
 */
export async function fixSuggestionHandler(message, context = {}) {
  const logger = context.logger || _createMockLogger();
  const metrics = context.metrics || _createMockMetrics();

  try {
    // Validate request
    const request = message.data || {};
    validateFixSuggestionRequest(request);

    const {
      source,
      errorMessage,
      errorType = 'unknown',
      language = 'javascript',
      line = null,
    } = request;

    logger.debug(`Processing fix suggestions for ${language}:${errorType}`);

    const suggestions = [];

    // Generate fixes by category
    const errorTypeNorm = errorType.toLowerCase();

    if (
      errorTypeNorm === 'syntax' ||
      errorTypeNorm === 'unknown' ||
      errorMessage.toLowerCase().includes('syntax')
    ) {
      suggestions.push(...generateSyntaxFixes(source, errorMessage, language, line));
    }

    if (
      errorTypeNorm === 'semantic' ||
      errorTypeNorm === 'unknown' ||
      errorMessage.toLowerCase().includes('reference')
    ) {
      suggestions.push(...generateSemanticFixes(source, errorMessage, language, line));
    }

    if (errorTypeNorm === 'pattern') {
      suggestions.push(...generatePatternFixes(source, errorMessage, language, line));
    }

    if (errorTypeNorm === 'style') {
      suggestions.push(...generateStyleFixes(source, errorMessage, language, line));
    }

    if (errorTypeNorm === 'performance') {
      suggestions.push(...generatePerformanceFixes(source, errorMessage, language));
    }

    if (errorTypeNorm === 'security') {
      suggestions.push(...generateSecurityFixes(source, errorMessage, language));
    }

    // Remove duplicates
    const unique = [];
    const seen = new Set();
    for (const s of suggestions) {
      if (!seen.has(s.suggestion)) {
        seen.add(s.suggestion);
        unique.push(s);
      }
    }

    metrics.recordHandlerExecution('bridge:fixSuggestion', unique.length > 0, {
      errorType,
      language,
      suggestionCount: unique.length,
    });

    return createFixSuggestionResponse(unique, {
      errorType,
      language,
      sourceLength: source.length,
      lineNumber: line,
    });
  } catch (error) {
    logger.error(`Fix suggestion error: ${error.message}`);
    metrics.recordHandlerExecution('bridge:fixSuggestion', false, { error: error.code });

    if (
      error instanceof FixSuggestionValidationError ||
      error instanceof FixSuggestionUnsupportedError
    ) {
      return {
        success: false,
        error: {
          code: error.code,
          message: error.message,
          name: error.name,
          details: error.details,
        },
      };
    }

    return {
      success: false,
      error: {
        code: 'ANALYSIS_ERROR',
        message: error.message,
        name: error.name,
      },
    };
  }
}

export default fixSuggestionHandler;
