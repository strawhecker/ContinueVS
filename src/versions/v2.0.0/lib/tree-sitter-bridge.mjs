#!/usr/bin/env node

/**
 * tree-sitter-bridge.mjs
 *
 * Language-agnostic wrapper around the npm tree-sitter package (Step 80).
 *
 * **Purpose**: Provides unified AST parsing and querying interface for multiple programming languages
 * without requiring tree-sitter as a hard dependency. If tree-sitter is unavailable, all methods
 * degrade gracefully without throwing errors.
 *
 * **Architecture**:
 * - Lazy language loader: Parsers loaded on first use per language
 * - Graceful degradation: Missing tree-sitter or language → return null (not throw)
 * - Single-threaded: Compatible with Node.js event loop
 * - Stateless queries: AST trees can be queried multiple times
 * - Metrics collection: Parse time, query latency tracked optionally
 *
 * **Supported Languages**: C#, JavaScript, TypeScript, Python, Java, Go, Rust, C, C++
 * (Parser availability depends on tree-sitter npm packages installed)
 *
 * **Error Handling**:
 * - TreeSitterInitializationError: Thrown during initialize() if tree-sitter load fails
 * - ParseError: Thrown during parseFile() if syntax is invalid (errors logged, not re-thrown to callers)
 * - QueryError: Thrown during query methods if position or query invalid
 * - All other errors: Logged at WARN, returns null (graceful degradation)
 *
 * @module src/versions/v2.0.0/lib/tree-sitter-bridge.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 53: symbol-extractor (optional enhancement)
 *   - Step 56: go-to-definition-handler (optional enhancement)
 *   - Step 58: code-completion-handler (optional enhancement)
 *   - Step 76: refactor-handler (optional enhancement)
 *   - Step 80: tree-sitter-handler (consumer)
 */

/**
 * Error thrown when tree-sitter initialization fails.
 * Indicates tree-sitter npm package is unavailable or corrupted.
 *
 * @class TreeSitterInitializationError
 * @extends {Error}
 */
export class TreeSitterInitializationError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [language] - Language that failed to load (if applicable)
   * @param {Error} [originalError] - Original error (if wrapping)
   */
  constructor(message, language = null, originalError = null) {
    super(message);
    this.name = 'TreeSitterInitializationError';
    this.language = language;
    this.originalError = originalError;
  }
}

/**
 * Error thrown when parse operation fails.
 * Indicates code has invalid syntax or tree-sitter encountered internal error.
 *
 * @class ParseError
 * @extends {Error}
 */
export class ParseError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [language] - Language being parsed
   * @param {string} [filepath] - File being parsed
   * @param {Error} [originalError] - Original error (if wrapping)
   */
  constructor(message, language = null, filepath = null, originalError = null) {
    super(message);
    this.name = 'ParseError';
    this.language = language;
    this.filepath = filepath;
    this.originalError = originalError;
  }
}

/**
 * Error thrown when query operation fails.
 * Indicates position is out of bounds or query format invalid.
 *
 * @class QueryError
 * @extends {Error}
 */
export class QueryError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [queryType] - Query type that failed
   * @param {Object} [position] - Position that failed { line, column }
   */
  constructor(message, queryType = null, position = null) {
    super(message);
    this.name = 'QueryError';
    this.queryType = queryType;
    this.position = position;
  }
}

/**
 * Language configuration for tree-sitter.
 * Maps language ID to npm package name.
 *
 * @type {Object.<string, {package: string, language: string}>}
 */
const LANGUAGE_MAP = {
  'csharp': { package: 'tree-sitter-c-sharp', language: 'c_sharp' },
  'cs': { package: 'tree-sitter-c-sharp', language: 'c_sharp' },
  'javascript': { package: 'tree-sitter-javascript', language: 'javascript' },
  'js': { package: 'tree-sitter-javascript', language: 'javascript' },
  'typescript': { package: 'tree-sitter-typescript', language: 'typescript' },
  'ts': { package: 'tree-sitter-typescript', language: 'typescript' },
  'python': { package: 'tree-sitter-python', language: 'python' },
  'py': { package: 'tree-sitter-python', language: 'python' },
  'java': { package: 'tree-sitter-java', language: 'java' },
  'go': { package: 'tree-sitter-go', language: 'go' },
  'rust': { package: 'tree-sitter-rust', language: 'rust' },
  'rs': { package: 'tree-sitter-rust', language: 'rust' },
  'c': { package: 'tree-sitter-c', language: 'c' },
  'cpp': { package: 'tree-sitter-cpp', language: 'cpp' },
};

/**
 * TreeSitterBridge class.
 *
 * Wraps tree-sitter npm package with graceful degradation and lazy loading.
 * All methods are non-blocking; errors are logged, not thrown (except during initialize).
 *
 * @class TreeSitterBridge
 */
export class TreeSitterBridge {
  /**
   * @param {Object} options - Configuration
   * @param {Object} [options.logger] - Logger instance (optional)
   * @param {Object} [options.metrics] - Metrics collector (optional)
   * @param {string[]} [options.enabledLanguages] - List of languages to support (default: all)
   */
  constructor(options = {}) {
    this.logger = options.logger || null;
    this.metrics = options.metrics || null;
    this.enabledLanguages = options.enabledLanguages || Object.keys(LANGUAGE_MAP);

    /** @type {Object} tree-sitter module (null if unavailable) */
    this.treeSitter = null;

    /** @type {Map<string, Object>} Cached language parsers */
    this.languageParsers = new Map();

    /** @type {boolean} Initialization status */
    this.initialized = false;

    /** @type {boolean} tree-sitter availability flag */
    this.available = false;

    this._logInfo('TreeSitterBridge initialized with constructor options');
  }

  /**
   * Initialize tree-sitter and load language parsers.
   * This is a blocking operation; call once before using parseFile or query methods.
   *
   * @returns {Promise<void>}
   * @throws {TreeSitterInitializationError} If tree-sitter cannot be loaded
   */
  async initialize() {
    if (this.initialized) {
      return;
    }

    try {
      this.treeSitter = await import('tree-sitter');
      this.available = true;
      this.initialized = true;
      this._logInfo('tree-sitter module loaded successfully');
    } catch (error) {
      this._logWarn(`tree-sitter module not available: ${error.message}`);
      this.available = false;
      this.initialized = true;
      throw new TreeSitterInitializationError(
        'tree-sitter npm package is not available. Install with: npm install tree-sitter',
        null,
        error
      );
    }
  }

  /**
   * Parse source code and return AST.
   * If tree-sitter unavailable, returns null and logs warning.
   *
   * @param {string} filepath - File path (for logging/metadata)
   * @param {string} code - Source code to parse
   * @param {string} language - Language ID (e.g., 'csharp', 'javascript')
   * @returns {Promise<Object|null>} AST tree or null if unavailable
   * @throws {QueryError} If code or language invalid
   */
  async parseFile(filepath, code, language) {
    const startTime = performance.now();

    if (!this.available || !this.treeSitter) {
      this._logWarn(`tree-sitter unavailable for ${filepath} (${language})`);
      return null;
    }

    if (!code || typeof code !== 'string') {
      throw new QueryError('code must be a non-empty string', 'parseFile', null);
    }

    language = language.toLowerCase();
    if (!LANGUAGE_MAP[language]) {
      this._logWarn(`Unsupported language: ${language}`);
      return null;
    }

    try {
      const parser = await this._loadLanguageParser(language);
      if (!parser) {
        return null;
      }

      const tree = parser.parse(code);
      const parseTime = performance.now() - startTime;

      this._recordMetric('tree_sitter.parse_time_ms', parseTime);
      this._logInfo(`Parsed ${filepath} (${language}) in ${parseTime.toFixed(2)}ms`);

      return tree;
    } catch (error) {
      this._logWarn(`Parse error for ${filepath} (${language}): ${error.message}`);
      this._recordMetric('tree_sitter.parse_error', 1);
      return null;
    }
  }

  /**
   * Extract a function definition at the given position.
   *
   * @param {Object} tree - AST tree from parseFile()
   * @param {number} line - Line number (0-based)
   * @param {number} column - Column number (0-based)
   * @returns {Object|null} Function node or null if not found
   */
  extractFunctionAtPosition(tree, line, column) {
    if (!tree) {
      return null;
    }

    try {
      const node = this._nodeAtPosition(tree, line, column);
      if (!node) {
        return null;
      }

      return this._findAncestorByType(node, ['function_declaration', 'method_declaration', 'function', 'method']);
    } catch (error) {
      this._logWarn(`extractFunctionAtPosition error: ${error.message}`);
      return null;
    }
  }

  /**
   * Extract a class definition at the given position.
   *
   * @param {Object} tree - AST tree from parseFile()
   * @param {number} line - Line number (0-based)
   * @param {number} column - Column number (0-based)
   * @returns {Object|null} Class node or null if not found
   */
  extractClassAtPosition(tree, line, column) {
    if (!tree) {
      return null;
    }

    try {
      const node = this._nodeAtPosition(tree, line, column);
      if (!node) {
        return null;
      }

      return this._findAncestorByType(node, ['class_declaration', 'interface_declaration', 'class', 'interface']);
    } catch (error) {
      this._logWarn(`extractClassAtPosition error: ${error.message}`);
      return null;
    }
  }

  /**
   * Determine scope type at position (local, member, module).
   *
   * @param {Object} tree - AST tree from parseFile()
   * @param {number} line - Line number (0-based)
   * @param {number} column - Column number (0-based)
   * @returns {string|null} Scope type: 'local' | 'member' | 'module' or null
   */
  extractScope(tree, line, column) {
    if (!tree) {
      return null;
    }

    try {
      const node = this._nodeAtPosition(tree, line, column);
      if (!node) {
        return null;
      }

      // Check if inside function/method
      if (this._findAncestorByType(node, ['function_declaration', 'method_declaration', 'function', 'method'])) {
        return 'local';
      }

      // Check if inside class/interface
      if (this._findAncestorByType(node, ['class_declaration', 'interface_declaration', 'class', 'interface'])) {
        return 'member';
      }

      // Otherwise module-level
      return 'module';
    } catch (error) {
      this._logWarn(`extractScope error: ${error.message}`);
      return null;
    }
  }

  /**
   * Query AST for all symbols of a given type.
   *
   * @param {Object} tree - AST tree from parseFile()
   * @param {string} symbolType - Symbol type to find (e.g., 'function', 'class', 'variable')
   * @returns {Object[]} Array of matching nodes or empty array if none found
   */
  queryBySymbolType(tree, symbolType) {
    if (!tree) {
      return [];
    }

    try {
      const results = [];
      this._walkTree(tree.rootNode, (node) => {
        if (node.type === symbolType || node.type === `${symbolType}_declaration`) {
          results.push(node);
        }
      });
      return results;
    } catch (error) {
      this._logWarn(`queryBySymbolType error: ${error.message}`);
      return [];
    }
  }

  /**
   * Dispose of resources and clean up cached parsers.
   *
   * @returns {void}
   */
  dispose() {
    this.languageParsers.clear();
    this.treeSitter = null;
    this.available = false;
    this._logInfo('TreeSitterBridge disposed');
  }

  // =========================================================================
  // Private helper methods
  // =========================================================================

  /**
   * Load a language parser, using cache if available.
   *
   * @private
   * @param {string} language - Language ID (normalized to lowercase)
   * @returns {Promise<Object|null>} Parser instance or null if unavailable
   */
  async _loadLanguageParser(language) {
    if (this.languageParsers.has(language)) {
      return this.languageParsers.get(language);
    }

    const langConfig = LANGUAGE_MAP[language];
    if (!langConfig) {
      this._logWarn(`No language config for: ${language}`);
      return null;
    }

    try {
      const langModule = await import(langConfig.package);
      const parser = new this.treeSitter.Parser();
      const language = await langModule.default();
      parser.setLanguage(language);
      this.languageParsers.set(language, parser);
      this._logInfo(`Loaded parser for language: ${language}`);
      return parser;
    } catch (error) {
      this._logWarn(`Failed to load parser for ${language}: ${error.message}`);
      this._recordMetric('tree_sitter.parser_load_error', 1);
      return null;
    }
  }

  /**
   * Find node at given position (line, column).
   *
   * @private
   * @param {Object} tree - AST tree
   * @param {number} line - Line number (0-based)
   * @param {number} column - Column number (0-based)
   * @returns {Object|null} Node at position or null
   */
  _nodeAtPosition(tree, line, column) {
    if (!tree || !tree.rootNode) {
      return null;
    }

    try {
      return tree.rootNode.descendantForPosition({ row: line, column });
    } catch (error) {
      return null;
    }
  }

  /**
   * Find ancestor node of given type(s).
   *
   * @private
   * @param {Object} node - Start node
   * @param {string[]} types - Target types to find
   * @returns {Object|null} Matching ancestor or null
   */
  _findAncestorByType(node, types) {
    let current = node;
    while (current) {
      if (types.includes(current.type)) {
        return current;
      }
      current = current.parent;
    }
    return null;
  }

  /**
   * Walk AST tree and invoke callback for each node.
   *
   * @private
   * @param {Object} node - Start node
   * @param {Function} callback - Callback(node)
   * @returns {void}
   */
  _walkTree(node, callback) {
    callback(node);
    for (const child of node.children || []) {
      this._walkTree(child, callback);
    }
  }

  /**
   * Log info message if logger available.
   *
   * @private
   * @param {string} message - Message text
   */
  _logInfo(message) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log(`[TreeSitterBridge] ${message}`);
    }
  }

  /**
   * Log warning message if logger available.
   *
   * @private
   * @param {string} message - Message text
   */
  _logWarn(message) {
    if (this.logger && typeof this.logger.warn === 'function') {
      this.logger.warn(`[TreeSitterBridge] ${message}`);
    }
  }

  /**
   * Record metric if metrics collector available.
   *
   * @private
   * @param {string} metricName - Metric name
   * @param {number} value - Metric value
   */
  _recordMetric(metricName, value) {
    if (this.metrics && typeof this.metrics.record === 'function') {
      this.metrics.record(metricName, value);
    }
  }
}

/**
 * Factory function to create and initialize TreeSitterBridge.
 *
 * @param {Object} options - Configuration (see TreeSitterBridge constructor)
 * @returns {Promise<TreeSitterBridge>} Initialized bridge instance
 * @throws {TreeSitterInitializationError} If tree-sitter cannot be loaded
 */
export async function createTreeSitterBridge(options = {}) {
  const bridge = new TreeSitterBridge(options);
  try {
    await bridge.initialize();
  } catch (error) {
    // Re-throw initialization errors; caller can handle unavailability
    throw error;
  }
  return bridge;
}

/**
 * Factory function to create TreeSitterBridge without requiring initialization.
 * Bridge can be used, but will return null for all queries until initialize() is called.
 *
 * @param {Object} options - Configuration (see TreeSitterBridge constructor)
 * @returns {TreeSitterBridge} Uninitialized bridge instance
 */
export function createTreeSitterBridgeLazy(options = {}) {
  return new TreeSitterBridge(options);
}
