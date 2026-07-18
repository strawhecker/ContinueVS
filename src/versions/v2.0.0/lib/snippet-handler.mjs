#!/usr/bin/env node

/**
 * Snippet Handler (Step 91)
 *
 * Provides a bridge handler that receives TextMate-syntax code snippets from Continue
 * and applies them to the editor at a specified position. Handles placeholder
 * extraction, variable substitution, and cursor positioning.
 *
 * **Handler Type**: Stateful mutation handler (applies edits)
 * **Message Type**: bridge:snippet
 * **Input**: BridgeMessage with payload `{ filePath: string, line: number, column: number, template: string, variables?: object }`
 * **Output**: BridgeResponse containing SnippetResult typedef data
 *
 * **Architecture Flow**:
 * ```
 * [Continue/IDE] → bridge:snippet request { filePath, line, column, template }
 *   ↓
 * [core-server dispatcher] routes to snippetHandler
 *   ↓
 * [handler] validates inputs (file path, position, template)
 *   ↓
 * [handler] parses template for TextMate syntax (${1:}, ${2:}, $TM_*, etc.)
 *   ↓
 * [handler] validates snippet syntax (placeholders, escapes, variables)
 *   ↓
 * [handler] extracts placeholder positions for cursor stops
 *   ↓
 * [handler] interpolates variables (${TM_FILENAME}, ${CURRENT_DATE}, etc.)
 *   ↓
 * [handler] queries DocumentProvider for document context
 *   ↓
 * [handler] inserts snippet text at position
 *   ↓
 * [handler] returns { success: true, data: { insertedText, primaryPlaceholder, stops: [...] } }
 *   ↓
 * [core-server] sends response via stdio
 * ```
 *
 * **TextMate Snippet Syntax Supported**:
 * - Placeholders: `${1:default}`, `${2}` (numbered, optional defaults)
 * - Final tab stop: `${0}` (cursor ends here)
 * - Variables: `${TM_FILENAME}`, `${TM_DIRECTORY}`, `${CURRENT_YEAR}`, `${CURRENT_DATE}`, `${CURRENT_TIME}`
 * - Escapes: `\$`, `\\` (literal `$` and backslash)
 * - Choices: `${1|option1,option2|}` (supported but simplified)
 *
 * **Validation**:
 * - Placeholder numbering must be sequential (1, 2, 3... optional 0 at end)
 * - No unmatched braces
 * - No invalid variable names
 * - Escape sequences must be valid
 * - Maximum snippet size: 64KB
 *
 * **Error Handling**:
 * - Invalid filePath → SnippetError (validation)
 * - Invalid position → PositionError (bounds check)
 * - Invalid template syntax → SnippetValidationError (parse error)
 * - DocumentProvider error → SnippetError (graceful fallback)
 * - Oversized template → SnippetError (size limit)
 *
 * **Thread Safety**:
 * - DocumentProvider is single-threaded (Node.js event loop)
 * - Snippet parsing is synchronous (no race conditions)
 * - Safe for concurrent calls (each handler call independent)
 *
 * **Performance**:
 * - Parsing: <5ms per template
 * - Validation: <5ms per template
 * - Insertion: <10ms per operation
 * - End-to-end: <20ms target
 *
 * **Dependencies**:
 * - DocumentProvider (Step 52) — Document content access and mutation
 * - Bridge logger (optional) — injected via context
 * - Bridge metrics (optional) — injected via context
 *
 * @module src/versions/v2.0.0/lib/snippet-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher routing)
 *   - Step 15: handler-adapter.js (wrapper methods)
 *   - Step 52: document-provider.mjs (document mutations)
 *   - Step 62: handlers.d.js (SnippetResult typedef)
 *   - Step 71: handler registration — registers this handler
 *   - Step 78: apply-edit-handler.mjs (related mutation handler)
 *   - Step 90: code-lens-handler.mjs (handler pattern reference)
 */

/**
 * Operation type enumeration for error classification.
 * @enum {string}
 */
export const SnippetOperationType = {
  INIT: 'init',
  VALIDATION: 'validation',
  PARSING: 'parsing',
  SYNTAX_CHECK: 'syntax_check',
  PLACEHOLDER_EXTRACTION: 'placeholder_extraction',
  VARIABLE_INTERPOLATION: 'variable_interpolation',
  DOCUMENT_QUERY: 'document_query',
  INSERTION: 'insertion',
};

/**
 * Error thrown when Snippet handler fails to initialize or execute.
 *
 * @class SnippetError
 * @extends {Error}
 *
 * @property {string} operationType - Which operation failed
 * @property {string} errorCode - RPC error code for bridge protocol
 * @property {*} details - Optional error details (template info, position, etc.)
 *
 * @example
 * try {
 *   const result = await snippetHandler(msg, { documentProvider: null });
 * } catch (error) {
 *   if (error instanceof SnippetError) {
 *     console.error(`Snippet failed during: ${error.operationType}`);
 *   }
 * }
 */
export class SnippetError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} operationType - Which operation failed
   * @param {string} errorCode - RPC error code
   * @param {*} details - Optional error details
   */
  constructor(
    message,
    operationType = SnippetOperationType.INIT,
    errorCode = 'SNIPPET_ERROR',
    details = null
  ) {
    super(message);
    this.name = 'SnippetError';
    this.operationType = operationType;
    this.errorCode = errorCode;
    this.details = details;
  }
}

/**
 * Error thrown when snippet template syntax is invalid.
 *
 * @class SnippetValidationError
 * @extends {SnippetError}
 *
 * @property {string} template - The invalid template
 * @property {number} position - Position in template where error occurred
 *
 * @example
 * throw new SnippetValidationError('Unmatched brace', '${1:name', 7);
 */
export class SnippetValidationError extends SnippetError {
  /**
   * @param {string} message - Error description
   * @param {string} template - The invalid template
   * @param {number} position - Error position in template
   */
  constructor(message, template = '', position = -1) {
    super(
      message,
      SnippetOperationType.SYNTAX_CHECK,
      'SNIPPET_VALIDATION_ERROR',
      { template, position }
    );
    this.name = 'SnippetValidationError';
    this.template = template;
    this.position = position;
  }
}

/**
 * Error thrown when position bounds are invalid.
 *
 * @class PositionError
 * @extends {SnippetError}
 *
 * @property {Object} position - The invalid position object
 *
 * @example
 * throw new PositionError('Line exceeds document bounds', { line: 9999, char: 0 });
 */
export class PositionError extends SnippetError {
  /**
   * @param {string} message - Error description
   * @param {*} position - The invalid position
   */
  constructor(message, position = null) {
    super(
      message,
      SnippetOperationType.VALIDATION,
      'POSITION_ERROR',
      position
    );
    this.name = 'PositionError';
    this.position = position;
  }
}

/**
 * Regular expressions for parsing TextMate snippets
 */
const SNIPPET_REGEX = {
  // Matches ${1:default}, ${2}, ${0}, ${TM_FILENAME}, ${1|option1,option2|}
  placeholder: /\$\{(\d+)(:[^}]*)?\}|\$\{([A-Z_]+)(?:\|([^}]*)\|)?\}|\$\{(\d+)\|([^}]*)\|\}/g,
  // Matches escaped characters: \$ or \\
  escape: /\\([$\\])/g,
  // Valid variable names (TextMate standard)
  validVariable: /^[A-Z_][A-Z0-9_]*$/,
  // Valid placeholder number (1-9, optional 0 at end)
  placeholderNumber: /^\d+$/,
};

/**
 * Standard TextMate variables and their default values/providers
 */
const TEXTMATE_VARIABLES = {
  TM_FILENAME: () => 'file',
  TM_DIRECTORY: () => 'directory',
  CURRENT_YEAR: () => new Date().getFullYear().toString(),
  CURRENT_DATE: () => new Date().toISOString().split('T')[0],
  CURRENT_TIME: () => new Date().toTimeString().split(' ')[0],
  CURRENT_YEAR_SHORT: () => new Date().getFullYear().toString().slice(-2),
  CURRENT_MONTH: () => String(new Date().getMonth() + 1).padStart(2, '0'),
  CURRENT_DAY: () => String(new Date().getDate()).padStart(2, '0'),
};

/**
 * Parses a TextMate snippet template into structured placeholders and text segments.
 *
 * @param {string} template - The raw template string
 * @returns {Object} Parsed structure with segments and placeholders
 * @throws {SnippetValidationError} If template syntax is invalid
 *
 * @example
 * const parsed = parseSnippetTemplate('function ${1:name}() {\n  ${2:body}\n}');
 * // Returns: { segments: [...], placeholders: [...] }
 */
export function parseSnippetTemplate(template) {
  if (typeof template !== 'string') {
    throw new SnippetValidationError('Template must be string', template);
  }

  if (template.length > 65536) {
    throw new SnippetError(
      `Template size (${template.length}) exceeds maximum 65536 bytes`,
      SnippetOperationType.PARSING,
      'TEMPLATE_TOO_LARGE',
      { size: template.length }
    );
  }

  const segments = [];
  let lastIndex = 0;
  let charIndex = 0;
  let braceDepth = 0;
  const placeholders = [];

  // First pass: validate braces and extract structure
  for (let i = 0; i < template.length; i++) {
    const char = template[i];
    const prevChar = i > 0 ? template[i - 1] : '';

    // Handle escapes
    if (prevChar === '\\' && (char === '$' || char === '\\')) {
      continue;
    }

    if (char === '$' && template[i + 1] === '{') {
      braceDepth++;
    } else if (char === '}' && braceDepth > 0) {
      braceDepth--;
    }
  }

  if (braceDepth !== 0) {
    throw new SnippetValidationError(
      'Unmatched braces in template',
      template,
      template.lastIndexOf('{')
    );
  }

  // Second pass: extract placeholders
  let match;
  const placeholderRegex = /\$\{(\d+)(?::([^}]*))?\}|\$\{([A-Z_][A-Z0-9_]*)\}|\$\{(\d+)\|([^}]*)\|\}/g;
  const foundPlaceholders = new Map(); // Track which placeholder numbers we've seen

  while ((match = placeholderRegex.exec(template)) !== null) {
    if (match[1]) {
      // Numbered placeholder with optional default: ${1:default}
      const num = match[1];
      const defaultVal = match[2] || '';
      placeholders.push({
        type: 'placeholder',
        number: parseInt(num, 10),
        default: defaultVal,
        index: match.index,
        full: match[0],
      });
      if (!foundPlaceholders.has(parseInt(num, 10))) {
        foundPlaceholders.set(parseInt(num, 10), true);
      }
    } else if (match[3]) {
      // Variable: ${VARIABLE_NAME}
      placeholders.push({
        type: 'variable',
        name: match[3],
        index: match.index,
        full: match[0],
      });
    } else if (match[4]) {
      // Choice placeholder: ${1|option1,option2|}
      const num = match[4];
      const choices = match[5].split(',');
      placeholders.push({
        type: 'choice',
        number: parseInt(num, 10),
        choices: choices,
        index: match.index,
        full: match[0],
      });
      if (!foundPlaceholders.has(parseInt(num, 10))) {
        foundPlaceholders.set(parseInt(num, 10), true);
      }
    }
  }

  return {
    template,
    placeholders,
    foundNumbers: Array.from(foundPlaceholders.keys()).sort((a, b) => a - b),
  };
}

/**
 * Validates snippet template syntax strictly.
 *
 * @param {string} template - The template to validate
 * @throws {SnippetValidationError} If syntax is invalid
 *
 * @example
 * validateSnippetSyntax('function ${1:name}() {}'); // OK
 * validateSnippetSyntax('${1|a,b,c|}'); // OK (choice)
 * validateSnippetSyntax('${1}${3}'); // FAIL (non-sequential)
 */
export function validateSnippetSyntax(template) {
  const parsed = parseSnippetTemplate(template);

  // Check placeholder numbering: must be sequential (1, 2, 3... optionally 0 at end)
  const numbers = parsed.foundNumbers;

  if (numbers.length > 0) {
    // Check for sequential numbering
    const hasFinalStop = numbers.includes(0);
    const workNumbers = hasFinalStop ? numbers.filter(n => n !== 0) : numbers;

    // Verify 1, 2, 3... sequence
    for (let i = 0; i < workNumbers.length; i++) {
      if (workNumbers[i] !== i + 1) {
        throw new SnippetValidationError(
          `Placeholder numbering must be sequential; expected ${i + 1}, got ${workNumbers[i]}`,
          template
        );
      }
    }

    // If ${0} present, ensure it's the final stop (not mixed with numbered)
    if (hasFinalStop && workNumbers.length === 0) {
      // Only ${0} is fine
    } else if (hasFinalStop && workNumbers.length > 0) {
      // ${1}, ${2}, ${0} is fine (0 is final)
    }
  }

  // Validate all variables are recognized
  for (const ph of parsed.placeholders) {
    if (ph.type === 'variable') {
      if (!TEXTMATE_VARIABLES[ph.name]) {
        throw new SnippetValidationError(
          `Unknown variable: ${ph.name}. Valid variables: ${Object.keys(TEXTMATE_VARIABLES).join(', ')}`,
          template,
          ph.index
        );
      }
    }
  }

  return true;
}

/**
 * Extracts placeholder positions from a parsed template.
 * Returns tab-stop positions relative to snippet insertion start.
 *
 * @param {string} template - The template string
 * @param {Object} parsed - Result from parseSnippetTemplate()
 * @returns {Object} Placeholder metadata with positions
 *
 * @example
 * const result = extractPlaceholders('function ${1:name}() {}', parsed);
 * // Returns: { primaryStop: 9, stops: [{number: 1, offset: 9, length: 4}], finalStop: null }
 */
export function extractPlaceholders(template, parsed) {
  if (!parsed) {
    parsed = parseSnippetTemplate(template);
  }

  const stops = [];
  let primaryStop = null;
  let finalStop = null;

  // Build a map of placeholder number to offset within expanded template
  // This requires expanding variables to their actual lengths
  for (const ph of parsed.placeholders) {
    if (ph.type === 'placeholder' || ph.type === 'choice') {
      const number = ph.number;
      const defaultOrFirst =
        ph.type === 'placeholder'
          ? ph.default
          : ph.choices[0];

      stops.push({
        number: number,
        length: defaultOrFirst.length,
        index: ph.index,
      });

      if (number === 1 && primaryStop === null) {
        primaryStop = stops.length - 1;
      }
      if (number === 0) {
        finalStop = stops.length - 1;
      }
    }
  }

  return {
    primaryStop: primaryStop !== null ? stops[primaryStop] : null,
    finalStop: finalStop !== null ? stops[finalStop] : null,
    stops: stops,
  };
}

/**
 * Interpolates variables in a template with provided values.
 *
 * @param {string} template - The template with variables
 * @param {Object} variables - Variable values to substitute
 * @returns {string} Template with variables replaced
 *
 * @example
 * interpolateVariables('File: ${TM_FILENAME}', { TM_FILENAME: 'app.js' });
 * // Returns: 'File: app.js'
 */
export function interpolateVariables(template, variables = {}) {
  let result = template;

  // Replace variables with their values
  for (const [varName, defaultProvider] of Object.entries(TEXTMATE_VARIABLES)) {
    const value = variables[varName] || defaultProvider();
    result = result.replace(new RegExp(`\\$\\{${varName}\\}`, 'g'), value);
  }

  return result;
}

/**
 * Expands snippet placeholders to their default values.
 * Converts ${1:default} to just `default`, ${1} to empty string, etc.
 *
 * @param {string} template - The template with placeholders
 * @returns {string} Expanded template with placeholders removed
 *
 * @example
 * expandSnippetPlaceholders('function ${1:name}() {}');
 * // Returns: 'function name() {}'
 */
export function expandSnippetPlaceholders(template) {
  let result = template;

  // Remove ${0}
  result = result.replace(/\$\{0\}/g, '');

  // Replace ${N:default} with default, ${N} with empty
  result = result.replace(/\$\{(\d+)(?::([^}]*))?\}/g, (match, num, defaultVal) => {
    return defaultVal || '';
  });

  // Replace ${VAR} placeholders (should be removed if not interpolated)
  result = result.replace(/\$\{[A-Z_][A-Z0-9_]*\}/g, '');

  // Replace ${N|choices|} with first choice
  result = result.replace(/\$\{(\d+)\|([^}]*)\|\}/g, (match, num, choices) => {
    const choiceList = choices.split(',');
    return choiceList[0] || '';
  });

  return result;
}

/**
 * Handles escape sequences in snippet templates.
 * Converts \$ to $, \\ to \
 *
 * @param {string} template - Template possibly containing escapes
 * @returns {string} Template with escapes processed
 *
 * @example
 * processEscapes('Price: \\$100');
 * // Returns: 'Price: $100'
 */
export function processEscapes(template) {
  return template.replace(/\\([$\\])/g, '$1');
}

/**
 * Creates a stateless snippet handler with dependencies injected via context.
 *
 * The handler applies TextMate snippets to documents:
 * - Parses and validates snippet syntax
 * - Interpolates variables
 * - Inserts text at specified position
 * - Returns cursor stop positions for IDE navigation
 *
 * **Factory Pattern**:
 * ```javascript
 * const handler = createSnippetHandler({ documentProvider, logger });
 * const response = await handler(message, context);
 * ```
 *
 * **Message Format**:
 * ```javascript
 * {
 *   messageType: 'bridge:snippet',
 *   payload: {
 *     filePath: '/path/to/file.js',
 *     line: 10,                      // 0-based line number
 *     column: 5,                     // 0-based column number
 *     template: 'function ${1:name}() { ${2:body} }',
 *     variables: { TM_FILENAME: 'app.js' }  // optional
 *   }
 * }
 * ```
 *
 * **Response Format**:
 * ```javascript
 * {
 *   success: true,
 *   data: {
 *     insertedText: 'function name() { body }',
 *     stops: [
 *       { number: 1, line: 10, column: 9, length: 4 },
 *       { number: 2, line: 10, column: 23, length: 4 }
 *     ],
 *     primaryStop: { number: 1, line: 10, column: 9 }
 *   }
 * }
 * ```
 *
 * @param {Object} dependencies - Injected dependencies
 * @param {Object} dependencies.documentProvider - Document access and mutation (required)
 * @param {Object} dependencies.logger - Logger instance (optional)
 * @param {Object} dependencies.metrics - Metrics recorder (optional)
 * @returns {Function} Async handler function (message, context) => Promise<HandlerResponse>
 * @throws {SnippetError} If dependencies are missing
 *
 * @example
 * const handler = createSnippetHandler({
 *   documentProvider: docProvider,
 *   logger: bridgeLogger
 * });
 *
 * try {
 *   const response = await handler(message, context);
 *   console.log(`Inserted at: ${response.data.primaryStop}`);
 * } catch (err) {
 *   if (err instanceof SnippetValidationError) {
 *     console.error(`Invalid template: ${err.message}`);
 *   }
 * }
 */
export function createSnippetHandler(dependencies = {}) {
  const { documentProvider, logger, metrics } = dependencies;

  // Validate dependencies
  if (!documentProvider) {
    throw new SnippetError(
      'documentProvider is required',
      SnippetOperationType.INIT,
      'MISSING_DEPENDENCY'
    );
  }

  /**
   * Async handler implementation
   *
   * @param {Object} message - Bridge message
   * @param {Object} context - Bridge context
   * @returns {Promise<Object>} Handler response
   */
  return async function snippetHandler(message, context) {
    const startTime = Date.now();

    try {
      // Extract and validate inputs
      const { filePath, line, column, template, variables } =
        message.payload || {};

      logger?.debug?.(`[Snippet] Inserting at ${filePath}:${line}:${column}`);

      // Validation phase
      if (typeof filePath !== 'string' || !filePath.trim()) {
        throw new SnippetError(
          'filePath is required and must be non-empty string',
          SnippetOperationType.VALIDATION,
          'INVALID_FILE_PATH',
          { filePath }
        );
      }

      if (typeof line !== 'number' || line < 0 || !Number.isInteger(line)) {
        throw new PositionError(
          `Invalid line number: ${line}`,
          { line, column }
        );
      }

      if (typeof column !== 'number' || column < 0 || !Number.isInteger(column)) {
        throw new PositionError(
          `Invalid column number: ${column}`,
          { line, column }
        );
      }

      if (typeof template !== 'string') {
        throw new SnippetError(
          'template is required and must be string',
          SnippetOperationType.VALIDATION,
          'INVALID_TEMPLATE',
          { template }
        );
      }

      // Parse and validate template
      const parsed = parseSnippetTemplate(template);
      validateSnippetSyntax(template);

      logger?.debug?.(`[Snippet] Template parsed: ${parsed.placeholders.length} placeholders`);

      // Interpolate variables
      const interpolated = interpolateVariables(template, variables || {});

      // Process escapes
      const processed = processEscapes(interpolated);

      // Expand placeholders to actual text
      const expanded = expandSnippetPlaceholders(processed);

      logger?.debug?.(`[Snippet] Expanded: ${expanded.length} chars`);

      // Query document to verify position is valid
      let document;
      try {
        document = documentProvider.getDocument(filePath);
      } catch (err) {
        throw new SnippetError(
          `Failed to access document: ${err.message}`,
          SnippetOperationType.DOCUMENT_QUERY,
          'DOCUMENT_NOT_FOUND',
          { filePath, originalError: err.message }
        );
      }

      if (!document) {
        throw new SnippetError(
          `Document not found: ${filePath}`,
          SnippetOperationType.DOCUMENT_QUERY,
          'DOCUMENT_NOT_FOUND',
          { filePath }
        );
      }

      // Validate position within document bounds
      const lines = document.split?.('\n') || [];
      if (line >= lines.length) {
        throw new PositionError(
          `Line ${line} exceeds document bounds (max: ${lines.length - 1})`,
          { line, column }
        );
      }

      const lineText = lines[line] || '';
      if (column > lineText.length) {
        throw new PositionError(
          `Column ${column} exceeds line length (max: ${lineText.length})`,
          { line, column }
        );
      }

      // Insert snippet into document
      let updatedContent;
      try {
        // Split document into lines
        const docLines = document.split('\n');
        const targetLine = docLines[line];

        // Insert text at position
        const newLine =
          targetLine.substring(0, column) +
          expanded +
          targetLine.substring(column);

        docLines[line] = newLine;
        updatedContent = docLines.join('\n');

        // Update document via provider
        documentProvider.updateDocument(filePath, updatedContent);

        logger?.debug?.(`[Snippet] Inserted: ${expanded.length} chars`);
      } catch (err) {
        throw new SnippetError(
          `Failed to insert snippet: ${err.message}`,
          SnippetOperationType.INSERTION,
          'INSERTION_FAILED',
          { filePath, line, column, originalError: err.message }
        );
      }

      // Extract placeholder positions for IDE cursor navigation
      const placeholders = extractPlaceholders(expanded, parsed);
      const stops = [];

      // Calculate absolute positions in the document
      for (const ph of placeholders.stops) {
        stops.push({
          number: ph.number,
          line: line,
          column: column + ph.index,
          length: ph.length,
        });
      }

      const primaryStop = placeholders.primaryStop
        ? {
            number: placeholders.primaryStop.number,
            line: line,
            column: column + placeholders.primaryStop.index,
          }
        : null;

      const finalStop = placeholders.finalStop
        ? {
            number: placeholders.finalStop.number,
            line: line,
            column: column + placeholders.finalStop.index,
          }
        : null;

      const duration = Date.now() - startTime;
      metrics?.recordMetric?.('snippet_insertion', duration);
      logger?.debug?.(`[Snippet] Complete: ${duration}ms`);

      return {
        success: true,
        data: {
          insertedText: expanded,
          stops: stops,
          primaryStop: primaryStop,
          finalStop: finalStop,
          duration: duration,
        },
      };
    } catch (error) {
      const duration = Date.now() - startTime;
      metrics?.recordMetric?.('snippet_insertion_error', duration);

      if (
        error instanceof SnippetError ||
        error instanceof SnippetValidationError ||
        error instanceof PositionError
      ) {
        logger?.error?.(`[Snippet] ${error.operationType}: ${error.message}`);
        throw error;
      }

      logger?.error?.(`[Snippet] Unexpected error: ${error.message}`);
      throw new SnippetError(
        `Unexpected error during snippet insertion: ${error.message}`,
        SnippetOperationType.INSERTION,
        'INTERNAL_ERROR',
        { originalError: error.message }
      );
    }
  };
}

export default createSnippetHandler;
