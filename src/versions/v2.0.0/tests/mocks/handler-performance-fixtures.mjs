/**
 * handler-performance-fixtures.mjs
 * Step 98: Performance Test Fixtures & Payloads
 * 
 * Provides 4 payload sizes × 20 handlers = 80 test scenarios.
 * Integrates with Step 97 compliance fixtures.
 */

/**
 * Payload size definitions
 */
export const PAYLOAD_SIZES = {
  SMALL: { label: 'small', sizeKB: 5, description: 'Minimal fields (single file)' },
  MEDIUM: { label: 'medium', sizeKB: 50, description: 'Normal workflow (10 files)' },
  LARGE: { label: 'large', sizeKB: 250, description: 'Large workspace (50+ files)' },
  BATCH: { label: 'batch', sizeKB: 500, description: '10× repeated messages' }
};

/**
 * Handler tier assignment (explicit mapping)
 */
export const HANDLER_TIER_MAP = {
  fast: ['search', 'code-lens', 'model-info', 'profiler', 'go-to-def'],
  medium: ['refactor', 'completion', 'hover', 'apply-edit', 'format', 'git',
    'terminal', 'settings', 'snippet', 'workspace-reload'],
  slow: ['diff-viewer', 'test-explorer', 'debug-session', 'streaming',
    'refactor-tests', 'project-info', 'sidebar', 'context-window', 'inline-msg', 'find-ref']
};

/**
 * Per-tier memory SLAs (in bytes)
 */
export const MEMORY_SLA = {
  fast: 10 * 1024 * 1024,    // 10MB
  medium: 25 * 1024 * 1024,  // 25MB
  slow: 50 * 1024 * 1024     // 50MB
};

/**
 * Expected scaling factors (latency vs. payload size)
 */
export const SCALING_EXPECTATIONS = {
  'code-completion': { small: 1, medium: 1.2, large: 1.5 },  // Sublinear
  'git-integration': { small: 1, medium: 1.1, large: 1.3 },
  'refactor': { small: 1, medium: 1.4, large: 2.0 },         // Near-linear
  'snippet': { small: 1, medium: 1.05, large: 1.08 },        // Near-flat
  'streaming-response': { small: 1, medium: 1, large: 1 },   // Throughput-based
};

/**
 * Handler-specific performance configuration
 */
export const HANDLER_PERFORMANCE_EXPECTATIONS = {
  'streaming-response': {
    measureThroughput: true,
    skipMemory: false,
    concurrencyModel: 'streaming',
    note: 'Measure output streaming rate, not single-message latency'
  },
  'git-integration': {
    networkDependent: true,
    skipIfOffline: true,
    note: 'Latency may vary due to network; test locally cached repos'
  },
  'terminal-handler': {
    processDependent: true,
    skipIfUnavailable: true,
    note: 'Latency depends on spawned process; ensure test harness available'
  },
  'code-completion': {
    cacheWarmup: true,
    measureCacheHitRate: true,
    note: 'First call slower; measure with symbol cache primed'
  }
};

/**
 * Get handler tier
 */
export function getHandlerTier(handlerName) {
  for (const [tier, handlers] of Object.entries(HANDLER_TIER_MAP)) {
    if (handlers.includes(handlerName)) {
      return tier;
    }
  }
  return 'unknown';
}

/**
 * Get scaling expectation for handler
 */
export function getScalingExpectation(handlerName, fromSize, toSize) {
  const expectations = SCALING_EXPECTATIONS[handlerName];
  if (!expectations) {
    return 1.5; // Default: expect 1.5x scaling
  }

  const fromFactor = expectations[fromSize] || 1;
  const toFactor = expectations[toSize] || 1;

  return toFactor / fromFactor;
}

/**
 * Standard fixtures for all 20 handlers
 */
const HANDLER_FIXTURES = {
  // Fast tier: search
  search: {
    small: {
      valid: [
        {
          label: 'search-small-query',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:search',
            data: {
              query: 'function',
              scope: 'file',
              caseSensitive: false
            }
          },
          expectedLatencyMs: 3,
          category: 'valid',
          sizeKB: 2,
          description: 'Simple search query'
        }
      ],
      invalid: [
        {
          label: 'search-small-missing-query',
          payload: { messageId: 'msg-2', messageType: 'bridge:search', data: { scope: 'file' } },
          expectedErrorCode: -32602,
          category: 'invalid',
          description: 'Missing query field'
        }
      ]
    },
    large: {
      valid: [
        {
          label: 'search-large-complex',
          payload: {
            messageId: 'msg-3',
            messageType: 'bridge:search',
            data: {
              query: 'async function.*{.*return.*}',
              scope: 'workspace',
              caseSensitive: false,
              regex: true,
              searchContext: '/* Large C# codebase */'
            }
          },
          expectedLatencyMs: 15,
          category: 'valid',
          sizeKB: 250,
          description: 'Complex workspace search'
        }
      ]
    }
  },

  // Fast tier: code-lens
  'code-lens': {
    small: {
      valid: [
        {
          label: 'code-lens-small-minimal',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getCodeLens',
            data: { filePath: '/test.cs', lineStart: 0, lineEnd: 10 }
          },
          expectedLatencyMs: 2,
          category: 'valid',
          sizeKB: 3,
          description: 'Minimal code lens request'
        }
      ]
    }
  },

  // Fast tier: model-info
  'model-info': {
    small: {
      valid: [
        {
          label: 'model-info-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getModelInfo',
            data: {}
          },
          expectedLatencyMs: 1,
          category: 'valid',
          sizeKB: 1,
          description: 'Model info query'
        }
      ]
    }
  },

  // Fast tier: profiler
  profiler: {
    small: {
      valid: [
        {
          label: 'profiler-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getProfilerData',
            data: { handler: 'completion' }
          },
          expectedLatencyMs: 3,
          category: 'valid',
          sizeKB: 2,
          description: 'Profiler data request'
        }
      ]
    }
  },

  // Fast tier: go-to-def
  'go-to-def': {
    small: {
      valid: [
        {
          label: 'go-to-def-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:goToDefinition',
            data: {
              filePath: '/test.cs',
              position: { line: 10, character: 5 }
            }
          },
          expectedLatencyMs: 5,
          category: 'valid',
          sizeKB: 4,
          description: 'Go to definition query'
        }
      ]
    }
  },

  // Medium tier: refactor
  refactor: {
    small: {
      valid: [
        {
          label: 'refactor-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:refactor',
            data: {
              filePath: '/test.cs',
              refactoringType: 'rename',
              oldName: 'foo',
              newName: 'bar'
            }
          },
          expectedLatencyMs: 10,
          category: 'valid',
          sizeKB: 5,
          description: 'Simple refactor'
        }
      ]
    }
  },

  // Medium tier: completion
  completion: {
    small: {
      valid: [
        {
          label: 'completion-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getCodeCompletion',
            data: {
              filePath: '/test.cs',
              position: { line: 5, character: 10 },
              context: { document: 'class Foo { }' }
            }
          },
          expectedLatencyMs: 15,
          category: 'valid',
          sizeKB: 8,
          description: 'Code completion request'
        }
      ]
    },
    large: {
      valid: [
        {
          label: 'completion-large',
          payload: {
            messageId: 'msg-2',
            messageType: 'bridge:getCodeCompletion',
            data: {
              filePath: '/large.cs',
              position: { line: 500, character: 50 },
              context: {
                document: '/* 100KB C# source */',
                symbols: Array(500).fill({ name: 'symbol' }),
                diagnostics: Array(50).fill({ message: 'diag' })
              }
            }
          },
          expectedLatencyMs: 35,
          category: 'valid',
          sizeKB: 250,
          description: 'Large workspace completion'
        }
      ]
    }
  },

  // Medium tier: hover
  hover: {
    small: {
      valid: [
        {
          label: 'hover-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getHoverInfo',
            data: {
              filePath: '/test.cs',
              position: { line: 10, character: 5 }
            }
          },
          expectedLatencyMs: 8,
          category: 'valid',
          sizeKB: 4,
          description: 'Hover info request'
        }
      ]
    }
  },

  // Medium tier: apply-edit
  'apply-edit': {
    small: {
      valid: [
        {
          label: 'apply-edit-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:applyEdit',
            data: {
              edits: [{
                filePath: '/test.cs',
                range: { start: { line: 0, character: 0 }, end: { line: 1, character: 0 } },
                newText: 'new code'
              }]
            }
          },
          expectedLatencyMs: 12,
          category: 'valid',
          sizeKB: 6,
          description: 'Apply single edit'
        }
      ]
    }
  },

  // Medium tier: format
  format: {
    small: {
      valid: [
        {
          label: 'format-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:formatDocument',
            data: {
              filePath: '/test.cs',
              options: { tabSize: 2 }
            }
          },
          expectedLatencyMs: 20,
          category: 'valid',
          sizeKB: 5,
          description: 'Format document'
        }
      ]
    }
  },

  // Medium tier: git
  git: {
    small: {
      valid: [
        {
          label: 'git-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getGitStatus',
            data: { filePath: '/test.cs' }
          },
          expectedLatencyMs: 10,
          category: 'valid',
          sizeKB: 3,
          description: 'Git status query'
        }
      ]
    }
  },

  // Medium tier: terminal
  terminal: {
    small: {
      valid: [
        {
          label: 'terminal-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:runTerminal',
            data: { command: 'echo "test"' }
          },
          expectedLatencyMs: 25,
          category: 'valid',
          sizeKB: 4,
          description: 'Terminal command'
        }
      ]
    }
  },

  // Medium tier: settings
  settings: {
    small: {
      valid: [
        {
          label: 'settings-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getSettings',
            data: { key: 'performance' }
          },
          expectedLatencyMs: 5,
          category: 'valid',
          sizeKB: 2,
          description: 'Settings retrieval'
        }
      ]
    }
  },

  // Medium tier: snippet
  snippet: {
    small: {
      valid: [
        {
          label: 'snippet-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:insertSnippet',
            data: { snippet: 'class ${1:Name} { }' }
          },
          expectedLatencyMs: 8,
          category: 'valid',
          sizeKB: 3,
          description: 'Snippet insertion'
        }
      ]
    }
  },

  // Medium tier: workspace-reload
  'workspace-reload': {
    small: {
      valid: [
        {
          label: 'workspace-reload-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:reloadWorkspace',
            data: {}
          },
          expectedLatencyMs: 40,
          category: 'valid',
          sizeKB: 2,
          description: 'Workspace reload'
        }
      ]
    }
  },

  // Slow tier: diff-viewer
  'diff-viewer': {
    small: {
      valid: [
        {
          label: 'diff-viewer-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getDiff',
            data: {
              filePath: '/test.cs',
              revisions: ['HEAD', 'HEAD~1']
            }
          },
          expectedLatencyMs: 100,
          category: 'valid',
          sizeKB: 5,
          description: 'Diff viewer request'
        }
      ]
    }
  },

  // Slow tier: test-explorer
  'test-explorer': {
    small: {
      valid: [
        {
          label: 'test-explorer-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getTests',
            data: { project: '/project' }
          },
          expectedLatencyMs: 150,
          category: 'valid',
          sizeKB: 8,
          description: 'Test explorer query'
        }
      ]
    }
  },

  // Slow tier: debug-session
  'debug-session': {
    small: {
      valid: [
        {
          label: 'debug-session-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:startDebug',
            data: { configuration: 'launch' }
          },
          expectedLatencyMs: 200,
          category: 'valid',
          sizeKB: 6,
          description: 'Debug session start'
        }
      ]
    }
  },

  // Slow tier: streaming
  streaming: {
    small: {
      valid: [
        {
          label: 'streaming-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:streamResponse',
            data: { streamId: 'stream-1' }
          },
          expectedLatencyMs: 50,
          category: 'valid',
          sizeKB: 2,
          description: 'Stream response'
        }
      ]
    }
  },

  // Slow tier: refactor-tests
  'refactor-tests': {
    small: {
      valid: [
        {
          label: 'refactor-tests-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:runRefactorTests',
            data: { refactoringId: 'ref-1' }
          },
          expectedLatencyMs: 250,
          category: 'valid',
          sizeKB: 5,
          description: 'Refactor tests'
        }
      ]
    }
  },

  // Slow tier: project-info
  'project-info': {
    small: {
      valid: [
        {
          label: 'project-info-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getProjectInfo',
            data: {}
          },
          expectedLatencyMs: 80,
          category: 'valid',
          sizeKB: 10,
          description: 'Project info'
        }
      ]
    }
  },

  // Slow tier: sidebar
  sidebar: {
    small: {
      valid: [
        {
          label: 'sidebar-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:updateSidebar',
            data: { content: 'sidebar data' }
          },
          expectedLatencyMs: 100,
          category: 'valid',
          sizeKB: 4,
          description: 'Sidebar update'
        }
      ]
    }
  },

  // Slow tier: context-window
  'context-window': {
    small: {
      valid: [
        {
          label: 'context-window-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:getContextWindow',
            data: { position: { line: 10, character: 5 } }
          },
          expectedLatencyMs: 120,
          category: 'valid',
          sizeKB: 8,
          description: 'Context window query'
        }
      ]
    }
  },

  // Slow tier: inline-msg
  'inline-msg': {
    small: {
      valid: [
        {
          label: 'inline-msg-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:showInlineMessage',
            data: { message: 'info' }
          },
          expectedLatencyMs: 50,
          category: 'valid',
          sizeKB: 3,
          description: 'Inline message'
        }
      ]
    }
  },

  // Slow tier: find-ref
  'find-ref': {
    small: {
      valid: [
        {
          label: 'find-ref-small',
          payload: {
            messageId: 'msg-1',
            messageType: 'bridge:findReferences',
            data: {
              filePath: '/test.cs',
              position: { line: 10, character: 5 }
            }
          },
          expectedLatencyMs: 180,
          category: 'valid',
          sizeKB: 6,
          description: 'Find references'
        }
      ]
    }
  }
};

/**
 * Get fixtures for a handler and payload size
 */
export function getHandlerFixtures(handlerName, payloadSize) {
  const handlerFixtures = HANDLER_FIXTURES[handlerName] || {};
  const sizeLabel = typeof payloadSize === 'string'
    ? payloadSize
    : payloadSize.label;

  const fixtures = handlerFixtures[sizeLabel] || { valid: [], invalid: [] };

  return {
    valid: fixtures.valid || [],
    invalid: fixtures.invalid || []
  };
}

/**
 * Get all handler fixtures
 */
export function getAllHandlerFixtures() {
  const all = new Map();

  for (const handlerName of Object.keys(HANDLER_FIXTURES)) {
    all.set(handlerName, new Map());

    for (const size of Object.values(PAYLOAD_SIZES)) {
      const fixtures = getHandlerFixtures(handlerName, size);
      all.get(handlerName).set(size.label, fixtures);
    }
  }

  return all;
}

/**
 * Get all handler names
 */
export function getAllHandlerNames() {
  return Object.keys(HANDLER_FIXTURES);
}
