#!/usr/bin/env node

/**
 * Stress Test Fixtures
 *
 * Message templates, payload generators, and error injection scenarios
 * for handler stress testing.
 *
 * @module src/versions/v2.0.0/tests/mocks/stress-test-fixtures.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 99: Stress test engine (consumes these fixtures)
 *   - Step 110: E2E scenarios (uses sustained load fixtures)
 *   - Step 112: Regression suite (baseline payloads)
 */

/**
 * Generate high-concurrency message payloads.
 *
 * Produces realistic message templates for all 20 handlers.
 * Used by concurrency scenario to stress handler parallelism.
 *
 * @returns {Object} Map of messageType → payload generator function
 */
export function getConcurrencyFixtures() {
  const fixtures = {};

  // Step 76: Refactor Handler
  fixtures['bridge:refactor'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:refactor',
    data: {
      filePath: '/workspace/src/index.js',
      startLine: 10,
      endLine: 25,
      refactoringType: 'extract-function',
    },
  });

  // Step 77: Fix Suggestion Handler
  fixtures['bridge:fixSuggestion'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:fixSuggestion',
    data: {
      filePath: '/workspace/src/service.js',
      diagnosticId: 'error-001',
      diagnosticMessage: 'Unused variable',
      line: 42,
    },
  });

  // Step 78: Apply Edit Handler
  fixtures['bridge:applyEdit'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:applyEdit',
    data: {
      filePath: '/workspace/src/app.js',
      edits: [
        {
          range: { start: 0, end: 5 },
          replacement: '// Updated\n',
        },
      ],
    },
  });

  // Step 79: Format Document Handler
  fixtures['bridge:formatDocument'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:formatDocument',
    data: {
      filePath: '/workspace/src/utils.js',
      languageId: 'javascript',
    },
  });

  // Step 81: Git Integration Handler
  fixtures['bridge:gitIntegration'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:gitIntegration',
    data: {
      command: 'getStatus',
      workspaceDir: '/workspace',
    },
  });

  // Step 82: Terminal Handler
  fixtures['bridge:terminal'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:terminal',
    data: {
      command: 'echo "test"',
      cwd: '/workspace',
    },
  });

  // Step 83: File System Handler
  fixtures['bridge:fileSystem'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:fileSystem',
    data: {
      operation: 'readFile',
      filePath: '/workspace/package.json',
    },
  });

  // Step 84: Project Info Handler
  fixtures['bridge:projectInfo'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:projectInfo',
    data: {
      workspaceDir: '/workspace',
    },
  });

  // Step 85: Inline Message Handler
  fixtures['bridge:inlineMessage'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:inlineMessage',
    data: {
      filePath: '/workspace/src/component.js',
      line: 15,
      message: 'Refactor suggestion',
    },
  });

  // Step 86: Sidebar UI Handler
  fixtures['bridge:sidebarUI'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:sidebarUI',
    data: {
      action: 'updatePanel',
      panelId: 'refactor-options',
    },
  });

  // Step 87: Context Window Handler
  fixtures['bridge:contextWindow'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:contextWindow',
    data: {
      filePath: '/workspace/src/main.js',
      line: 50,
      contextSize: 10,
    },
  });

  // Additional handlers (10-20)
  fixtures['bridge:modelInfo'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:modelInfo',
    data: {
      includeDefaults: true,
    },
  });

  fixtures['bridge:streamingResponse'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:streamingResponse',
    data: {
      sessionId: 'session-' + Date.now(),
      prompt: 'Refactor this function',
    },
  });

  fixtures['bridge:codeLens'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:codeLens',
    data: {
      filePath: '/workspace/src/index.js',
    },
  });

  fixtures['bridge:diffViewer'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:diffViewer',
    data: {
      originalText: 'function foo() {}',
      newText: 'const foo = () => {}',
    },
  });

  fixtures['bridge:refactorTests'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:refactorTests',
    data: {
      testFilePath: '/workspace/test/index.test.js',
      sourcePath: '/workspace/src/index.js',
    },
  });

  fixtures['bridge:workspaceReload'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:workspaceReload',
    data: {
      workspaceDir: '/workspace',
    },
  });

  fixtures['bridge:loadSettings'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:loadSettings',
    data: {},
  });

  fixtures['bridge:applySettings'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:applySettings',
    data: {
      settings: {
        theme: 'dark',
        fontSize: 14,
      },
    },
  });

  fixtures['bridge:profiler'] = () => ({
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType: 'bridge:profiler',
    data: {
      action: 'start',
      duration: 5000,
    },
  });

  return fixtures;
}

/**
 * Generate error injection message scenarios.
 *
 * Produces payloads designed to trigger specific error conditions:
 * - Timeout: Slow processing
 * - Protocol Error: Malformed responses
 * - Missing Dependency: Unavailable service
 * - Validation Error: Invalid input
 * - Permission Error: Access denied
 *
 * @returns {Object} Error scenario definitions
 */
export function getErrorInjectionFixtures() {
  const scenarios = {
    timeout: {
      description: 'Handler should timeout after delay',
      delayMs: 100,
      expectedError: 'timeout',
      payloads: [
        {
          messageType: 'bridge:gitIntegration',
          data: {
            command: 'clone',
            repoUrl: 'https://github.com/large-repo/repo.git',
            timeout: 50, // Intentionally short timeout
          },
        },
        {
          messageType: 'bridge:terminal',
          data: {
            command: 'sleep 10 && echo "done"',
            timeout: 100,
          },
        },
      ],
    },

    protocol_error: {
      description: 'Handler returns malformed response',
      expectedError: 'protocol_error',
      payloads: [
        {
          messageType: 'bridge:modelInfo',
          data: {
            malformed: true, // Trigger protocol error
          },
        },
        {
          messageType: 'bridge:streamingResponse',
          data: {
            invalidFormat: 'should-be-json',
          },
        },
      ],
    },

    missing_dependency: {
      description: 'Required service/dependency unavailable',
      expectedError: 'missing_dependency',
      payloads: [
        {
          messageType: 'bridge:gitIntegration',
          data: {
            command: 'status',
            workspaceDir: '/nonexistent/workspace',
          },
        },
        {
          messageType: 'bridge:terminal',
          data: {
            command: 'nonexistent-command',
          },
        },
      ],
    },

    validation_error: {
      description: 'Invalid input validation fails',
      expectedError: 'validation_error',
      payloads: [
        {
          messageType: 'bridge:applyEdit',
          data: {
            filePath: '',
            edits: null, // Invalid
          },
        },
        {
          messageType: 'bridge:refactor',
          data: {
            filePath: '/valid/path',
            startLine: 100,
            endLine: 50, // startLine > endLine
            refactoringType: 'invalid-type',
          },
        },
      ],
    },

    permission_error: {
      description: 'Access denied to resource',
      expectedError: 'permission_error',
      payloads: [
        {
          messageType: 'bridge:fileSystem',
          data: {
            operation: 'write',
            filePath: '/root/protected-file.txt',
          },
        },
        {
          messageType: 'bridge:terminal',
          data: {
            command: 'sudo reboot',
          },
        },
      ],
    },
  };

  return scenarios;
}

/**
 * Generate sustained load message patterns.
 *
 * Produces realistic, high-volume message sequences for memory
 * stability and throughput testing.
 *
 * @param {number} targetMessagesPerSecond - Throughput target (default 1000)
 * @returns {Array} Array of message templates
 */
export function getSustainedLoadFixtures(targetMessagesPerSecond = 1000) {
  const allFixtures = getConcurrencyFixtures();
  const handlerNames = Object.keys(allFixtures);

  // Generate a balanced mix of messages distributed across all handlers
  const messageTemplates = [];
  const messagesPerHandler = Math.ceil(targetMessagesPerSecond / handlerNames.length);

  for (let i = 0; i < messagesPerHandler; i++) {
    handlerNames.forEach((handlerName) => {
      messageTemplates.push(allFixtures[handlerName]());
    });
  }

  return messageTemplates;
}

/**
 * Generate cascading failure scenarios.
 *
 * Produces message sequences that test handler isolation when one
 * handler begins to fail.
 *
 * @returns {Object} Cascading failure scenarios
 */
export function getCascadingFailureFixtures() {
  return {
    // Phase 1: Baseline - all handlers healthy
    baseline: {
      description: 'All handlers operating normally',
      scenarios: [
        {
          messageType: 'bridge:refactor',
          iterations: 50,
        },
        {
          messageType: 'bridge:fixSuggestion',
          iterations: 50,
        },
        {
          messageType: 'bridge:applyEdit',
          iterations: 50,
        },
        {
          messageType: 'bridge:formatDocument',
          iterations: 50,
        },
        {
          messageType: 'bridge:gitIntegration',
          iterations: 50,
        },
        {
          messageType: 'bridge:terminal',
          iterations: 50,
        },
        {
          messageType: 'bridge:fileSystem',
          iterations: 50,
        },
        {
          messageType: 'bridge:projectInfo',
          iterations: 50,
        },
        {
          messageType: 'bridge:inlineMessage',
          iterations: 50,
        },
        {
          messageType: 'bridge:sidebarUI',
          iterations: 50,
        },
        {
          messageType: 'bridge:contextWindow',
          iterations: 50,
        },
        {
          messageType: 'bridge:modelInfo',
          iterations: 50,
        },
        {
          messageType: 'bridge:streamingResponse',
          iterations: 50,
        },
        {
          messageType: 'bridge:codeLens',
          iterations: 50,
        },
        {
          messageType: 'bridge:diffViewer',
          iterations: 50,
        },
        {
          messageType: 'bridge:refactorTests',
          iterations: 50,
        },
        {
          messageType: 'bridge:workspaceReload',
          iterations: 50,
        },
        {
          messageType: 'bridge:loadSettings',
          iterations: 50,
        },
        {
          messageType: 'bridge:applySettings',
          iterations: 50,
        },
        {
          messageType: 'bridge:profiler',
          iterations: 50,
        },
      ],
    },

    // Phase 2: One handler begins to fail
    failureInjection: {
      description: 'One handler fails; others should remain unaffected',
      targetFailingHandler: 'bridge:gitIntegration', // First to fail
      scenarios: [
        {
          messageType: 'bridge:refactor',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:fixSuggestion',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:applyEdit',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:formatDocument',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:gitIntegration',
          iterations: 50,
          shouldSucceed: false, // This handler is failing
          failureReason: 'Service unavailable',
        },
        {
          messageType: 'bridge:terminal',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:fileSystem',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:projectInfo',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:inlineMessage',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:sidebarUI',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:contextWindow',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:modelInfo',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:streamingResponse',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:codeLens',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:diffViewer',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:refactorTests',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:workspaceReload',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:loadSettings',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:applySettings',
          iterations: 50,
          shouldSucceed: true,
        },
        {
          messageType: 'bridge:profiler',
          iterations: 50,
          shouldSucceed: true,
        },
      ],
    },

    // Phase 3: Cascade - multiple handlers fail
    cascadePhase: {
      description: 'Multiple handlers fail; measure degradation',
      failingHandlers: ['bridge:gitIntegration', 'bridge:terminal'],
      scenarios: [
        // All handlers defined similarly, some marked as failing
      ],
    },
  };
}

/**
 * Helper: Generate realistic message payload for a given messageType
 *
 * @param {string} messageType - Handler message type
 * @returns {Object} Complete message with headers and data
 */
export function generateMessagePayload(messageType) {
  const fixtures = getConcurrencyFixtures();
  if (fixtures[messageType]) {
    return fixtures[messageType]();
  }

  // Fallback for unknown message types
  return {
    messageId: `req-${Date.now()}-${Math.random()}`,
    messageType,
    data: {},
  };
}

/**
 * Helper: Validate message payload structure
 *
 * @param {Object} message - Message to validate
 * @returns {boolean} True if valid
 */
export function validateMessagePayload(message) {
  return (
    message &&
    typeof message === 'object' &&
    message.messageId &&
    typeof message.messageId === 'string' &&
    message.messageType &&
    typeof message.messageType === 'string' &&
    (message.data === undefined || typeof message.data === 'object')
  );
}

/**
 * Helper: Create a batch of messages for bulk testing
 *
 * @param {number} count - Number of messages to generate
 * @param {Array} messageTypes - Array of message types to cycle through
 * @returns {Array} Array of message payloads
 */
export function createMessageBatch(count, messageTypes) {
  const batch = [];
  for (let i = 0; i < count; i++) {
    const messageType = messageTypes[i % messageTypes.length];
    batch.push(generateMessagePayload(messageType));
  }
  return batch;
}
