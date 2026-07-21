/**
 * E2E Scenario Fixtures - Realistic test data for workflows
 * Step 110: End-to-End Scenario Tests
 */

/**
 * Editor-to-AI Workflow Fixtures
 */
export function getEditorToAIFixture() {
  return {
    name: 'Editor to AI',
    inputs: {
      selection: {
        file: 'app.js',
        language: 'javascript',
        startLine: 10,
        startChar: 0,
        endLine: 12,
        endChar: 20,
        text: 'function getUserName(id) {\n  return getUserData(id).name;\n}',
      },
      cursorPosition: { line: 11, character: 15 },
    },
    expectedOutput: {
      completion: 'function getUserData(id) { return cache[id]; }',
      applied: true,
    },
    expectations: {
      latency_p99: 300,
      handlers: ['getEditorState', 'extractSymbols', 'generateCompletion', 'applyEdit'],
    },
  };
}

/**
 * Code Navigation Workflow Fixtures
 */
export function getCodeNavigationFixture() {
  return {
    name: 'Code Navigation',
    inputs: {
      searchQuery: 'getUserData',
      searchScope: 'workspace',
      definitionFile: 'app.js',
      definitionLine: 5,
    },
    expectedOutput: {
      searchMatches: [
        { file: 'app.js', line: 11 },
        { file: 'cache.js', line: 3 },
        { file: 'utils.js', line: 42 },
      ],
      definition: { file: 'app.js', line: 5, column: 10 },
      references: [
        { file: 'app.js', line: 11 },
        { file: 'cache.js', line: 3 },
        { file: 'utils.js', line: 42 },
      ],
    },
    expectations: {
      latency_p99: 250,
      handlers: ['search', 'goToDefinition', 'findReferences', 'hoverInfo'],
    },
  };
}

/**
 * Git-Integrated Workflow Fixtures
 */
export function getGitIntegratedFixture() {
  return {
    name: 'Git Integrated',
    inputs: {
      diffFile: 'app.js',
      diffContent: {
        before: 'function getUserName(id) {\n  return getUserData(id).name;\n}',
        after: 'function getUserName(id) {\n  const data = getUserData(id);\n  return data?.name || "Unknown";\n}',
      },
      refactorScope: 'file',
      gitCommand: 'log --oneline -n 5',
    },
    expectedOutput: {
      refactored: true,
      gitInfo: {
        lastCommit: 'abc1234',
        author: 'Test User',
        message: 'Refactor user name retrieval',
      },
      terminalOutput: 'abc1234 Refactor user name retrieval\n...',
    },
    expectations: {
      handlers: ['loadDiff', 'refactor', 'getGitInfo', 'executeTerminal'],
    },
  };
}

/**
 * Multi-File Refactor Workflow Fixtures
 */
export function getMultiFileRefactorFixture() {
  return {
    name: 'Multi-File Refactor',
    inputs: {
      selectedFiles: ['app.js', 'cache.js', 'utils.js'],
      refactorScope: 'workspace',
      refactorType: 'rename-variable',
      oldName: 'userData',
      newName: 'userInfo',
    },
    expectedOutput: {
      filesModified: 3,
      changesPerFile: {
        'app.js': 2,
        'cache.js': 1,
        'utils.js': 3,
      },
      reloaded: true,
    },
    expectations: {
      latency_p99: 400,
      handlers: ['selectFiles', 'refactorScope', 'clearCache', 'extractSymbols', 'verifyChanges'],
    },
  };
}

/**
 * Debug Integration Workflow Fixtures
 */
export function getDebugIntegrationFixture() {
  return {
    name: 'Debug Integration',
    inputs: {
      debugAction: 'start',
      breakpoints: [
        { file: 'app.js', line: 11 },
        { file: 'cache.js', line: 3 },
      ],
      terminalCommand: 'dotnet test',
    },
    expectedOutput: {
      sessionActive: true,
      breakpointInfo: [
        { file: 'app.js', line: 11, condition: null, verified: true },
        { file: 'cache.js', line: 3, condition: null, verified: true },
      ],
      terminalOutput: 'Test run completed: 42 tests passed',
    },
    expectations: {
      handlers: ['startDebugSession', 'getBreakpointInfo', 'executeTerminal'],
    },
  };
}

/**
 * Error Recovery Workflow Fixtures
 */
export function getErrorRecoveryFixture() {
  return {
    name: 'Error Recovery',
    inputs: {
      triggerError: 'timeout',
      timeoutDuration: 100,
      fallbackHandler: 'search',
      retryAttempt: 1,
    },
    expectedOutput: {
      timeoutOccurred: true,
      circuitBreakerActive: true,
      fallbackSucceeded: true,
      retrySucceeded: true,
    },
    expectations: {
      handlers: ['triggerTimeout', 'activateCircuitBreaker', 'fallbackHandler', 'retryRequest'],
    },
  };
}

/**
 * State Persistence Workflow Fixtures
 */
export function getStatePersistenceFixture() {
  return {
    name: 'State Persistence',
    inputs: {
      editorState: {
        file: 'app.js',
        cursorLine: 11,
        cursorChar: 15,
        selection: { start: 10, end: 12 },
      },
      crashTrigger: 'simulate',
    },
    expectedOutput: {
      stateCaptureed: true,
      crashSimulated: true,
      stateRecovered: true,
      editorStateMatches: true,
    },
    expectations: {
      handlers: ['captureState', 'simulateCrash', 'recoverState'],
    },
  };
}

/**
 * Configuration Variant Workflow Fixtures
 */
export function getConfigurationVariantFixture() {
  return {
    name: 'Configuration Variant',
    inputs: {
      settings: {
        model: 'gpt-4',
        provider: 'openai',
        temperature: 0.7,
        contextWindow: 4096,
      },
      variant: 'gpt-4',
    },
    expectedOutput: {
      settingsLoaded: true,
      settingsApplied: true,
      reloaded: true,
      workflowCompleted: true,
    },
    expectations: {
      handlers: ['loadSettings', 'applySettings', 'reload', 'executeWorkflow'],
    },
  };
}

/**
 * Mock Handler Context and Dependencies
 */
export function createMockHandlerContext() {
  return {
    documentProvider: {
      get: (file) => ({ file, content: '' }),
      clearAll: () => {},
    },
    symbolExtractor: {
      extract: (file, position) => ({ symbols: [] }),
      clear: () => {},
    },
    diagnostics: {
      get: (file) => ({ diagnostics: [] }),
    },
    gitManager: {
      getDiff: (file) => ({ before: '', after: '' }),
      getInfo: () => ({ commit: 'abc1234', author: 'User', message: 'msg' }),
    },
    terminal: {
      execute: (cmd) => Promise.resolve({ output: '' }),
    },
    debugSession: {
      start: () => Promise.resolve({ active: true }),
      stop: () => Promise.resolve({ active: false }),
      getBreakpoints: () => Promise.resolve([]),
    },
    logger: {
      info: (msg) => console.log(`[INFO] ${msg}`),
      error: (msg, err) => console.error(`[ERROR] ${msg}`, err),
    },
  };
}

/**
 * Mock Document Set for Multi-File Testing
 */
export function createMockDocumentSet(fileCount = 3) {
  const documents = {};
  for (let i = 0; i < fileCount; i++) {
    const filename = `file${i}.js`;
    documents[filename] = {
      file: filename,
      language: 'javascript',
      content: `// File ${i}\nfunction func${i}() {\n  return data${i};\n}`,
      lines: 4,
    };
  }
  return documents;
}

/**
 * Realistic Completion Generation
 */
export function generateRealisticCompletion(context, model) {
  const completions = {
    'gpt-3.5-turbo': 'function getUserData(id) { return cache[id]; }',
    'gpt-4': 'function getUserData(id) { const data = cache.get(id); return data || null; }',
    'claude': 'function getUserData(id) {\n  try {\n    return cache.retrieve(id);\n  } catch (e) {\n    return null;\n  }\n}',
  };
  return completions[model] || completions['gpt-3.5-turbo'];
}

/**
 * Simulate Crash and Create Recovery Checkpoint
 */
export function simulateCrash(state) {
  return {
    checkpoint: {
      timestamp: Date.now(),
      state: JSON.parse(JSON.stringify(state)),
      version: 1,
    },
    recovered: false,
  };
}

/**
 * Validate Workflow State Before/After
 */
export function validateWorkflowState(before, after) {
  const violations = [];

  if (!after) {
    violations.push('After state is null or undefined');
  }

  // Check for state consistency rules
  if (before && after && JSON.stringify(before) === JSON.stringify(after)) {
    violations.push('State was not modified');
  }

  return {
    isValid: violations.length === 0,
    violations,
  };
}

/**
 * Create Payload Templates
 */
export const payloadTemplates = {
  validSelection: {
    file: 'app.js',
    language: 'javascript',
    text: 'function test() { return true; }',
    startLine: 1,
    endLine: 3,
  },

  validSearchQuery: {
    query: 'getUserData',
    scope: 'workspace',
    matchCase: false,
    useRegex: false,
  },

  validRefactorOperation: {
    type: 'rename',
    oldName: 'userData',
    newName: 'userInfo',
    scope: 'workspace',
  },

  validGitCommand: {
    command: 'log',
    args: ['--oneline', '-n', '5'],
  },

  validTerminalCommand: {
    command: 'npm test',
    cwd: '/project',
    timeout: 30000,
  },

  validSettings: {
    model: 'gpt-4',
    provider: 'openai',
    temperature: 0.7,
    contextWindow: 4096,
  },
};

/**
 * Baseline Expectations per Handler
 */
export const handlerBaselines = {
  getEditorState: { latency_p99: 50, throughput: 100 },
  extractSymbols: { latency_p99: 75, throughput: 50 },
  generateCompletion: { latency_p99: 200, throughput: 10 },
  applyEdit: { latency_p99: 100, throughput: 50 },
  search: { latency_p99: 150, throughput: 20 },
  goToDefinition: { latency_p99: 100, throughput: 50 },
  findReferences: { latency_p99: 150, throughput: 20 },
  hoverInfo: { latency_p99: 100, throughput: 50 },
  loadDiff: { latency_p99: 100, throughput: 50 },
  refactor: { latency_p99: 200, throughput: 10 },
  getGitInfo: { latency_p99: 100, throughput: 50 },
  executeTerminal: { latency_p99: 500, throughput: 5 },
};

/**
 * Export all fixtures as object
 */
export const allFixtures = {
  editorToAI: getEditorToAIFixture(),
  codeNavigation: getCodeNavigationFixture(),
  gitIntegrated: getGitIntegratedFixture(),
  multiFileRefactor: getMultiFileRefactorFixture(),
  debugIntegration: getDebugIntegrationFixture(),
  errorRecovery: getErrorRecoveryFixture(),
  statePersistence: getStatePersistenceFixture(),
  configurationVariant: getConfigurationVariantFixture(),
};
