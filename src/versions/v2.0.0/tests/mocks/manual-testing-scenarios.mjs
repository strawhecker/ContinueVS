/**
 * manual-testing-scenarios.mjs
 * Reusable message templates and fixtures for all 20 bridge handlers
 * Purpose: Copy-paste message fixtures for reproducible manual testing
 * 
 * Usage:
 *   import { getHandlerScenarios, getWorkflowScenarios } from './manual-testing-scenarios.mjs'
 *   const scenarios = getHandlerScenarios('bridge:refactor')
 *   console.log(JSON.stringify(scenarios.successMessage, null, 2))
 */

// ============================================================================
// HANDLER SCENARIOS (Category 1: Factory - 6 handlers)
// ============================================================================

const refactorHandlerScenarios = {
  name: 'bridge:refactor',
  description: 'Apply automated refactoring to selected code',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:refactor',
    params: {
      filePath: 'C:/project/src/Service.cs',
      range: { start: 42, end: 65 },
      refactoringType: 'extractMethod',
      newName: 'ValidateInput'
    },
    id: 1
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      success: true,
      newCode: 'private void ValidateInput() { /* impl */ }',
      affectedLines: [42, 65],
      previewDiff: '- old code\n+ new code'
    },
    id: 1
  },
  errorMessage: {
    jsonrpc: '2.0',
    method: 'bridge:refactor',
    params: {
      filePath: 'C:/project/src/Service.cs',
      range: { start: 9999, end: 10000 },
      refactoringType: 'extractMethod'
    },
    id: 2
  },
  expectedErrorCode: -32602,
  edgeCases: [
    {
      description: 'Large method (500+ lines)',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:refactor',
        params: {
          filePath: 'C:/project/src/LargeService.cs',
          range: { start: 1, end: 500 },
          refactoringType: 'extractMethod'
        },
        id: 10
      },
      expectedLatency: 150,
      expectedResult: 'success'
    },
    {
      description: 'Concurrent requests',
      expectedBehavior: 'non-blocking, queued'
    },
    {
      description: 'Missing file',
      expectedErrorCode: -32603
    }
  ]
};

const fixSuggestionHandlerScenarios = {
  name: 'bridge:fixSuggestion',
  description: 'Generate fix suggestion for diagnostic',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:fixSuggestion',
    params: {
      filePath: 'C:/project/src/app.cs',
      line: 15,
      diagnosticCode: 'CS0168',
      diagnosticMessage: 'Variable assigned but never used'
    },
    id: 3
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      fix: 'Remove variable declaration',
      newCode: '// Variable removed',
      severity: 'warning',
      applicableRanges: [15]
    },
    id: 3
  },
  errorMessage: {
    jsonrpc: '2.0',
    method: 'bridge:fixSuggestion',
    params: {
      filePath: 'C:/project/src/app.cs',
      line: 15,
      diagnosticCode: 'UNKNOWN',
      diagnosticMessage: 'Unknown error'
    },
    id: 4
  },
  expectedErrorCode: -32602,
  edgeCases: [
    {
      description: 'Unknown diagnostic code',
      expectedErrorCode: -32602
    },
    {
      description: 'File modified since parse',
      expectedBehavior: 'cache miss, re-parse'
    }
  ]
};

const applyEditHandlerScenarios = {
  name: 'bridge:applyEdit',
  description: 'Apply code edit to document',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:applyEdit',
    params: {
      filePath: 'C:/project/src/Service.cs',
      range: { start: 42, end: 65 },
      newText: 'private void ValidateInput() { /* new impl */ }'
    },
    id: 4
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      success: true,
      newLineCount: 10,
      savedToFile: true
    },
    id: 4
  },
  errorMessage: {
    jsonrpc: '2.0',
    method: 'bridge:applyEdit',
    params: {
      filePath: '/nonexistent/file.cs',
      range: { start: 1, end: 10 },
      newText: 'test'
    },
    id: 5
  },
  expectedErrorCode: -32603,
  edgeCases: [
    {
      description: 'File locked',
      expectedErrorCode: -32603
    },
    {
      description: 'Out-of-order edits',
      expectedBehavior: 'reject with conflict message'
    }
  ]
};

const formatDocumentHandlerScenarios = {
  name: 'bridge:formatDocument',
  description: 'Format entire document',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:formatDocument',
    params: {
      filePath: 'C:/project/src/Service.cs',
      style: 'microsoft'
    },
    id: 5
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      success: true,
      linesFormatted: 150,
      indentationFixed: true
    },
    id: 5
  },
  errorMessage: {
    jsonrpc: '2.0',
    method: 'bridge:formatDocument',
    params: {
      filePath: 'C:/project/src/Service.txt',
      style: 'microsoft'
    },
    id: 6
  },
  expectedErrorCode: -32602,
  edgeCases: [
    {
      description: 'Unsupported language',
      expectedErrorCode: -32602
    },
    {
      description: 'No changes needed',
      expectedResult: { linesFormatted: 0 }
    }
  ]
};

const snippetHandlerScenarios = {
  name: 'bridge:snippet',
  description: 'Insert code snippet at cursor',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:snippet',
    params: {
      filePath: 'C:/project/src/app.cs',
      line: 20,
      snippetName: 'tryForEach',
      language: 'csharp'
    },
    id: 6
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      snippet: 'try { foreach(var item in collection) { } } catch { }',
      placeholders: [{ name: 'collection', line: 21 }]
    },
    id: 6
  },
  errorMessage: {
    jsonrpc: '2.0',
    method: 'bridge:snippet',
    params: {
      filePath: 'C:/project/src/app.cs',
      line: 20,
      snippetName: 'unknownSnippet',
      language: 'csharp'
    },
    id: 7
  },
  expectedErrorCode: -32602
};

const diffViewerHandlerScenarios = {
  name: 'bridge:diffViewer',
  description: 'Generate diff preview',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:diffViewer',
    params: {
      originalCode: 'var x = 5;',
      newCode: 'const x = 5;'
    },
    id: 7
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      diff: '- var x = 5;\n+ const x = 5;',
      linesDiff: 1,
      linesAdded: 1,
      linesRemoved: 1
    },
    id: 7
  },
  edgeCases: [
    {
      description: 'Identical code',
      expectedResult: { diff: '', linesDiff: 0 }
    }
  ]
};

// ============================================================================
// HANDLER SCENARIOS (Category 2: Subscriptions - 4 handlers)
// ============================================================================

const onEditorStateChangeScenarios = {
  name: 'bridge:onEditorStateChange',
  description: 'Subscribe to editor state changes',
  performanceGate: { p99ms: 2000, errorRateLimit: 0.01 },
  subscriptionMessage: {
    jsonrpc: '2.0',
    method: 'bridge:onEditorStateChange',
    params: {},
    id: 100
  },
  expectedFirstEvent: {
    jsonrpc: '2.0',
    method: 'bridge:onEditorStateChange',
    params: {
      filePath: 'C:/project/src/Service.cs',
      selection: { start: 42, end: 42 },
      line: 42,
      column: 15,
      language: 'csharp',
      symbolAtCursor: 'ValidateInput'
    }
  },
  edgeCases: [
    {
      description: 'Multiple files open',
      expectedBehavior: 'Emit per active file'
    },
    {
      description: 'Selection change',
      expectedBehavior: 'Immediate event, no debounce'
    }
  ]
};

const onTerminalOutputScenarios = {
  name: 'bridge:onTerminalOutput',
  description: 'Subscribe to terminal output',
  performanceGate: { p99ms: 500, errorRateLimit: 0.01 },
  subscriptionMessage: {
    jsonrpc: '2.0',
    method: 'bridge:onTerminalOutput',
    params: { terminalId: 'build' },
    id: 101
  },
  expectedEvent: {
    jsonrpc: '2.0',
    method: 'bridge:onTerminalOutput',
    params: {
      terminalId: 'build',
      output: 'Building project...\n',
      timestamp: 1705328400
    }
  }
};

const gitStatusSubscriptionScenarios = {
  name: 'bridge:gitStatus',
  description: 'Subscribe to Git status changes',
  performanceGate: { p99ms: 1000, errorRateLimit: 0.01 },
  subscriptionMessage: {
    jsonrpc: '2.0',
    method: 'bridge:gitStatus',
    params: { repoPath: 'C:/project' },
    id: 102
  },
  expectedEvent: {
    jsonrpc: '2.0',
    method: 'bridge:gitStatus',
    params: {
      filePath: 'C:/project/src/Service.cs',
      status: 'modified',
      staged: false
    }
  }
};

const debugSessionSubscriptionScenarios = {
  name: 'bridge:debugSession',
  description: 'Subscribe to debug session events',
  performanceGate: { p99ms: 1000, errorRateLimit: 0.01 },
  subscriptionMessage: {
    jsonrpc: '2.0',
    method: 'bridge:debugSession',
    params: {},
    id: 103
  },
  expectedEvent: {
    jsonrpc: '2.0',
    method: 'bridge:debugSession',
    params: {
      event: 'breakpoint',
      filePath: 'C:/project/src/Service.cs',
      line: 42,
      variables: {
        x: { value: '5', type: 'int' }
      }
    }
  }
};

// ============================================================================
// HANDLER SCENARIOS (Category 3: Bidirectional - 3 handlers)
// ============================================================================

const searchHandlerScenarios = {
  name: 'bridge:search',
  description: 'Search code',
  performanceGate: { p99ms: 500, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:search',
    params: {
      query: 'ValidateInput',
      scope: 'workspace',
      matchCase: false
    },
    id: 200
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      results: [
        {
          filePath: 'C:/project/src/Service.cs',
          line: 42,
          column: 15,
          context: 'private void ValidateInput() {'
        }
      ],
      totalResults: 1
    },
    id: 200
  },
  edgeCases: [
    {
      description: 'Empty results',
      expectedResult: { results: [], totalResults: 0 }
    },
    {
      description: 'Large workspace',
      expectedBehavior: 'Timeout after 30s, return partial results'
    }
  ]
};

const goToDefinitionHandlerScenarios = {
  name: 'bridge:goToDefinition',
  description: 'Navigate to symbol definition',
  performanceGate: { p99ms: 500, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:goToDefinition',
    params: {
      filePath: 'C:/project/src/Service.cs',
      line: 42,
      column: 15,
      symbol: 'ValidateInput'
    },
    id: 201
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      filePath: 'C:/project/src/Service.cs',
      line: 10,
      column: 15,
      snippet: 'private void ValidateInput() { ... }'
    },
    id: 201
  },
  edgeCases: [
    {
      description: 'Cross-file navigation',
      expectedBehavior: 'Verify file opened'
    },
    {
      description: 'External library',
      expectedResult: null
    }
  ]
};

const findReferencesHandlerScenarios = {
  name: 'bridge:findReferences',
  description: 'Find all references to symbol',
  performanceGate: { p99ms: 500, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:findReferences',
    params: {
      filePath: 'C:/project/src/Service.cs',
      line: 10,
      column: 15,
      symbol: 'ValidateInput'
    },
    id: 202
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      references: [
        { filePath: 'C:/project/src/app.cs', line: 99 },
        { filePath: 'C:/project/src/Service.cs', line: 42 }
      ],
      totalReferences: 2
    },
    id: 202
  }
};

// ============================================================================
// HANDLER SCENARIOS (Category 4: Analysis & UI - 4 handlers)
// ============================================================================

const codeCompletionHandlerScenarios = {
  name: 'bridge:codeCompletion',
  description: 'Generate code completions',
  performanceGate: { p99ms: 200, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:codeCompletion',
    params: {
      filePath: 'C:/project/src/Service.cs',
      line: 50,
      column: 10,
      prefix: 'Valid'
    },
    id: 300
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      completions: [
        {
          label: 'ValidateInput',
          detail: 'method',
          documentation: 'Validates input parameters',
          sortText: 'ValidateInput'
        }
      ]
    },
    id: 300
  },
  edgeCases: [
    {
      description: 'Large file',
      expectedLatency: 250
    },
    {
      description: 'No matches',
      expectedResult: { completions: [] }
    }
  ]
};

const hoverInfoHandlerScenarios = {
  name: 'bridge:hoverInfo',
  description: 'Generate hover information',
  performanceGate: { p99ms: 200, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:hoverInfo',
    params: {
      filePath: 'C:/project/src/Service.cs',
      line: 42,
      column: 15
    },
    id: 301
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      contents: 'private void ValidateInput()',
      documentation: 'Validates input parameters against business rules',
      signature: 'ValidateInput(): void'
    },
    id: 301
  }
};

const testExplorerHandlerScenarios = {
  name: 'bridge:testExplorer',
  description: 'List tests in workspace',
  performanceGate: { p99ms: 200, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:testExplorer',
    params: {
      filePath: 'C:/project/tests/ServiceTests.cs'
    },
    id: 302
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      tests: [
        {
          name: 'ValidateInput_WithValidData_Passes',
          line: 10,
          testFramework: 'xUnit'
        }
      ]
    },
    id: 302
  }
};

const inlineMessageHandlerScenarios = {
  name: 'bridge:inlineMessage',
  description: 'Display inline message at line',
  performanceGate: { p99ms: 200, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:inlineMessage',
    params: {
      filePath: 'C:/project/src/Service.cs',
      line: 50,
      message: 'Consider renaming this variable for clarity',
      severity: 'info'
    },
    id: 303
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      messageId: 'msg_12345',
      displayed: true
    },
    id: 303
  }
};

// ============================================================================
// HANDLER SCENARIOS (Category 5: Metadata & Config - 3 handlers)
// ============================================================================

const loadSettingsHandlerScenarios = {
  name: 'bridge:loadSettings',
  description: 'Load bridge configuration',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:loadSettings',
    params: {},
    id: 400
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      model: 'gpt-4',
      apiKey: '***',
      enableTelemetry: true,
      logLevel: 'info'
    },
    id: 400
  }
};

const applySettingsHandlerScenarios = {
  name: 'bridge:applySettings',
  description: 'Apply new settings',
  performanceGate: { p99ms: 100, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:applySettings',
    params: {
      model: 'gpt-4-turbo',
      enableTelemetry: false
    },
    id: 401
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      applied: true,
      requiresRestart: false
    },
    id: 401
  },
  errorMessage: {
    jsonrpc: '2.0',
    method: 'bridge:applySettings',
    params: {
      model: 'invalid-model'
    },
    id: 402
  },
  expectedErrorCode: -32602,
  edgeCases: [
    {
      description: 'Requires restart',
      expectedResult: { applied: true, requiresRestart: true }
    }
  ]
};

const workspaceReloadHandlerScenarios = {
  name: 'bridge:workspaceReload',
  description: 'Reload workspace',
  performanceGate: { p99ms: 2000, errorRateLimit: 0.01 },
  successMessage: {
    jsonrpc: '2.0',
    method: 'bridge:workspaceReload',
    params: {},
    id: 402
  },
  successResponse: {
    jsonrpc: '2.0',
    result: {
      reloaded: true,
      filesScanned: 150
    },
    id: 402
  },
  edgeCases: [
    {
      description: 'Large workspace',
      expectedLatency: 5000,
      expectedBehavior: 'May take 3-5s (acceptable)'
    },
    {
      description: 'Error during reload',
      expectedBehavior: 'Rollback to previous state'
    }
  ]
};

// ============================================================================
// WORKFLOW SCENARIOS (Multi-handler integration)
// ============================================================================

const contextCompletionWorkflow = {
  name: 'contextToCompletion',
  description: 'User opens file, types partial symbol, selects completion',
  steps: [
    {
      stepNumber: 1,
      handler: 'bridge:onEditorStateChange',
      message: {
        method: 'bridge:onEditorStateChange',
        params: { filePath: 'C:/project/src/app.cs', line: 50 }
      },
      expectedResponse: {
        method: 'bridge:onEditorStateChange',
        params: { filePath: 'C:/project/src/app.cs', line: 50, symbolAtCursor: 'Valid' }
      },
      validationNote: 'Verify editor context loaded'
    },
    {
      stepNumber: 2,
      handler: 'bridge:codeCompletion',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:codeCompletion',
        params: { filePath: 'C:/project/src/app.cs', line: 50, prefix: 'Valid' },
        id: 1
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: {
          completions: [{ label: 'ValidateInput' }]
        },
        id: 1
      },
      validationNote: 'Verify completions returned'
    },
    {
      stepNumber: 3,
      handler: 'bridge:hoverInfo',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:hoverInfo',
        params: { filePath: 'C:/project/src/app.cs', line: 50 },
        id: 2
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { documentation: 'Validates input parameters' },
        id: 2
      },
      validationNote: 'Verify documentation available'
    }
  ],
  acceptanceCriteria: 'All events fire in <2s total. State consistent across handlers.'
};

const searchNavEditWorkflow = {
  name: 'searchToNavigation',
  description: 'Find symbol, navigate to definition, find references, edit',
  steps: [
    {
      stepNumber: 1,
      handler: 'bridge:search',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:search',
        params: { query: 'ValidateInput', scope: 'workspace' },
        id: 10
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { results: [{ filePath: 'C:/project/src/Service.cs', line: 42 }] },
        id: 10
      },
      validationNote: 'Verify search results'
    },
    {
      stepNumber: 2,
      handler: 'bridge:goToDefinition',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:goToDefinition',
        params: { symbol: 'ValidateInput', filePath: 'C:/project/src/Service.cs', line: 42 },
        id: 11
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { filePath: 'C:/project/src/Service.cs', line: 10 },
        id: 11
      },
      validationNote: 'Verify navigation to definition'
    },
    {
      stepNumber: 3,
      handler: 'bridge:findReferences',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:findReferences',
        params: { symbol: 'ValidateInput', filePath: 'C:/project/src/Service.cs', line: 10 },
        id: 12
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { references: [{ filePath: 'C:/project/src/app.cs', line: 99 }] },
        id: 12
      },
      validationNote: 'Verify all references found'
    },
    {
      stepNumber: 4,
      handler: 'bridge:refactor',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:refactor',
        params: { refactoringType: 'rename', newName: 'CheckInput', filePath: 'C:/project/src/Service.cs' },
        id: 13
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { success: true },
        id: 13
      },
      validationNote: 'Verify refactor applied'
    }
  ],
  acceptanceCriteria: 'Multi-file navigation consistent. No cross-handler conflicts.'
};

const refactorFormatDiffWorkflow = {
  name: 'refactorFormatDiff',
  description: 'Suggest refactor, format, preview, user accepts',
  steps: [
    {
      stepNumber: 1,
      handler: 'bridge:refactor',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:refactor',
        params: { refactoringType: 'extractMethod', filePath: 'C:/project/src/Service.cs' },
        id: 20
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { success: true, newCode: '...' },
        id: 20
      },
      validationNote: 'Verify refactor generated'
    },
    {
      stepNumber: 2,
      handler: 'bridge:formatDocument',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:formatDocument',
        params: { filePath: 'C:/project/src/Service.cs', style: 'microsoft' },
        id: 21
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { success: true, linesFormatted: 10 },
        id: 21
      },
      validationNote: 'Verify formatting applied'
    },
    {
      stepNumber: 3,
      handler: 'bridge:diffViewer',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:diffViewer',
        params: { originalCode: '...', newCode: '...' },
        id: 22
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { diff: '- old\n+ new' },
        id: 22
      },
      validationNote: 'Verify diff generated'
    },
    {
      stepNumber: 4,
      handler: 'bridge:applyEdit',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:applyEdit',
        params: { filePath: 'C:/project/src/Service.cs', newText: '...' },
        id: 23
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { success: true, savedToFile: true },
        id: 23
      },
      validationNote: 'Verify edit saved'
    }
  ],
  acceptanceCriteria: 'State preserved across handlers. No data loss.'
};

const settingsReloadWorkflow = {
  name: 'settingsReload',
  description: 'Load settings, change model, reload workspace, verify persistence',
  steps: [
    {
      stepNumber: 1,
      handler: 'bridge:loadSettings',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:loadSettings',
        params: {},
        id: 30
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { model: 'gpt-4' },
        id: 30
      },
      validationNote: 'Verify initial settings'
    },
    {
      stepNumber: 2,
      handler: 'bridge:applySettings',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:applySettings',
        params: { model: 'gpt-4-turbo' },
        id: 31
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { applied: true },
        id: 31
      },
      validationNote: 'Verify settings applied'
    },
    {
      stepNumber: 3,
      handler: 'bridge:workspaceReload',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:workspaceReload',
        params: {},
        id: 32
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { reloaded: true },
        id: 32
      },
      validationNote: 'Verify workspace reloaded'
    },
    {
      stepNumber: 4,
      handler: 'bridge:loadSettings',
      message: {
        jsonrpc: '2.0',
        method: 'bridge:loadSettings',
        params: {},
        id: 33
      },
      expectedResponse: {
        jsonrpc: '2.0',
        result: { model: 'gpt-4-turbo' },
        id: 33
      },
      validationNote: 'Verify new model persisted'
    }
  ],
  acceptanceCriteria: 'Settings persisted. Reload completes successfully. New model active.'
};

// ============================================================================
// EXPORT FUNCTIONS
// ============================================================================

/**
 * Get scenario object for a specific handler
 * @param {string} handlerName - Handler name (e.g., 'bridge:refactor')
 * @returns {object} Scenario with success/error messages and performance gates
 */
export const getHandlerScenarios = (handlerName) => {
  const handlers = {
    'bridge:refactor': refactorHandlerScenarios,
    'bridge:fixSuggestion': fixSuggestionHandlerScenarios,
    'bridge:applyEdit': applyEditHandlerScenarios,
    'bridge:formatDocument': formatDocumentHandlerScenarios,
    'bridge:snippet': snippetHandlerScenarios,
    'bridge:diffViewer': diffViewerHandlerScenarios,
    'bridge:onEditorStateChange': onEditorStateChangeScenarios,
    'bridge:onTerminalOutput': onTerminalOutputScenarios,
    'bridge:gitStatus': gitStatusSubscriptionScenarios,
    'bridge:debugSession': debugSessionSubscriptionScenarios,
    'bridge:search': searchHandlerScenarios,
    'bridge:goToDefinition': goToDefinitionHandlerScenarios,
    'bridge:findReferences': findReferencesHandlerScenarios,
    'bridge:codeCompletion': codeCompletionHandlerScenarios,
    'bridge:hoverInfo': hoverInfoHandlerScenarios,
    'bridge:testExplorer': testExplorerHandlerScenarios,
    'bridge:inlineMessage': inlineMessageHandlerScenarios,
    'bridge:loadSettings': loadSettingsHandlerScenarios,
    'bridge:applySettings': applySettingsHandlerScenarios,
    'bridge:workspaceReload': workspaceReloadHandlerScenarios
  };
  return handlers[handlerName] || null;
};

/**
 * Get workflow scenario by name
 * @param {string} workflowName - Workflow name (e.g., 'contextToCompletion')
 * @returns {object} Workflow with chained steps and acceptance criteria
 */
export const getWorkflowScenarios = (workflowName) => {
  const workflows = {
    contextToCompletion: contextCompletionWorkflow,
    searchNavEdit: searchNavEditWorkflow,
    refactorFormatDiff: refactorFormatDiffWorkflow,
    settingsReload: settingsReloadWorkflow
  };
  return workflows[workflowName] || null;
};

/**
 * Get all handler fixtures
 * @returns {array} All 20 handler scenario objects
 */
export const getAllHandlerFixtures = () => {
  return [
    refactorHandlerScenarios,
    fixSuggestionHandlerScenarios,
    applyEditHandlerScenarios,
    formatDocumentHandlerScenarios,
    snippetHandlerScenarios,
    diffViewerHandlerScenarios,
    onEditorStateChangeScenarios,
    onTerminalOutputScenarios,
    gitStatusSubscriptionScenarios,
    debugSessionSubscriptionScenarios,
    searchHandlerScenarios,
    goToDefinitionHandlerScenarios,
    findReferencesHandlerScenarios,
    codeCompletionHandlerScenarios,
    hoverInfoHandlerScenarios,
    testExplorerHandlerScenarios,
    inlineMessageHandlerScenarios,
    loadSettingsHandlerScenarios,
    applySettingsHandlerScenarios,
    workspaceReloadHandlerScenarios
  ];
};

/**
 * Get all workflow fixtures
 * @returns {array} All 4 workflow scenario objects
 */
export const getAllWorkflowFixtures = () => {
  return [
    contextCompletionWorkflow,
    searchNavEditWorkflow,
    refactorFormatDiffWorkflow,
    settingsReloadWorkflow
  ];
};

/**
 * Format scenario as terminal command (curl + jq)
 * @param {object} scenario - Handler scenario object
 * @returns {string} Shell command ready to copy-paste
 */
export const formatAsTerminalCommand = (scenario) => {
  const message = scenario.successMessage || scenario.subscriptionMessage;
  const jsonStr = JSON.stringify(message).replace(/"/g, '\\"');
  return `curl -X POST http://localhost:5173/rpc \\
  -H "Content-Type: application/json" \\
  -d '${JSON.stringify(message)}' | jq '.'`;
};

/**
 * Get handler count and summary
 * @returns {object} Summary statistics
 */
export const getSummary = () => {
  return {
    totalHandlers: 20,
    factoryHandlers: 6,
    subscriptionHandlers: 4,
    bidirectionalHandlers: 3,
    analysisHandlers: 4,
    metadataHandlers: 3,
    totalWorkflows: 4,
    allHandlers: getAllHandlerFixtures().map(h => h.name),
    allWorkflows: getAllWorkflowFixtures().map(w => w.name)
  };
};

export default {
  getHandlerScenarios,
  getWorkflowScenarios,
  getAllHandlerFixtures,
  getAllWorkflowFixtures,
  formatAsTerminalCommand,
  getSummary
};
