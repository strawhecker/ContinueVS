#!/usr/bin/env node

/**
 * Handler Registry with Stress Test Integration (Step 99)
 * 
 * This registry maintains metadata for all 20 bridge handlers (Steps 76–95).
 * 
 * Step 99 Integration Notes:
 * ??????????????????????????????
 * - Stress tests (src/versions/v2.0.0/tests/handler-stress-tests.test.mjs) consume this registry
 * - The registry exports handlers for concurrency, error injection, sustained load, and cascading tests
 * - Handlers are tested under 4 stress scenarios:
 *   1. High Concurrency: 50–100 parallel requests ? p99 <500ms
 *   2. Error Injection: Timeout/protocol/dependency errors ? <5% error rate
 *   3. Sustained Load: 1000 msg/min for 30s ? memory stable
 *   4. Cascading Failures: One handler fails ? isolation >80%
 * 
 * Related Steps:
 *   - Step 97: Compliance baseline (p99 <100ms baseline)
 *   - Step 98: Performance tests (throughput baseline)
 *   - Step 99: Stress tests (THIS integration)
 *   - Step 110: E2E scenarios (uses stress fixtures)
 *   - Step 112: Regression suite (compares vs Step 99 baseline)
 *   - Step 115: Part III gate (stress report required)
 * 
 * Usage in Stress Tests:
 *   const handlers = getAllHandlers();
 *   const engine = createStressTestEngine({ handlers, logger, metrics });
 *   const results = await engine.runConcurrencyScenario(config);
 */

import { bootstrapHandler } from '../handlers/bootstrap-handler.js';
import { getEditorStateHandler } from './get-editor-state-handler.mjs';
import { searchHandler } from './search-handler.mjs';
import { createGoToDefinitionHandler } from './go-to-definition-handler.mjs';
import { createFindReferencesHandler } from './find-references-handler.mjs';
import { createCodeCompletionHandler } from './code-completion-handler.mjs';
import { createHoverInfoHandler } from './hover-info-handler.mjs';
import { createTestExplorerHandler } from './test-explorer-handler.mjs';
import { DebugSessionHandler } from './debug-session-handler.mjs';
import { refactorHandler } from './refactor-handler.mjs';
import { fixSuggestionHandler } from './fix-suggestion-handler.mjs';
import createApplyEditHandler from './apply-edit-handler.mjs';
import { createGitIntegrationHandler } from './git-integration-handler.mjs';
import { createTerminalHandler } from './terminal-handler.mjs';
import { createFileSystemHandler } from './file-system-handler.mjs';
import { createProjectInfoHandler } from './project-info-handler.mjs';
import { createInlineMessageHandler } from './inline-message-handler.mjs';
import { createSidebarUIHandler } from './sidebar-ui-handler.mjs';
import { createContextWindowHandler } from './context-window-handler.mjs';
import { createRateLimiter, createDefaultPolicy } from './rate-limiter.mjs';
import { createRateLimiterMiddleware } from './rate-limiter-middleware.mjs';
import { createCircuitBreakerManager } from './circuit-breaker-manager.mjs';
import { createCircuitBreakerMiddleware } from './circuit-breaker-middleware.mjs';
import { createModelInfoHandler } from './model-info-handler.mjs';
import { createStreamingResponseHandler } from './streaming-response-handler.mjs';
import { createCodeLensHandler } from './code-lens-handler.mjs';
import createDiffViewerHandler from './diff-viewer-handler.mjs';
import createRefactorTestsHandler from './refactor-tests-handler.mjs';
import { createWorkspaceReloadHandler } from './workspace-reload-handler.mjs';
import { createLoadSettingsHandler, createApplySettingsHandler } from './settings-sync-handler.mjs';
import { createProfilerHandler } from './profiler-integration.mjs';
import { createMetricsStreamHandler } from './metrics-stream-handler.mjs';
import { createDiagnosticPanelHandler } from './diagnostic-panel-handler.mjs';
import { createCrashRecoveryHandler } from './crash-recovery-manager.mjs';
import { TREE_SITTER_ENABLED } from './feature-flags.mjs';
import { handle as treeAnalysisHandler } from './tree-sitter-handler.mjs';
export class HandlerRegistryError extends Error {
  constructor(message, code = 'REGISTRY_ERROR', details = null) {
    super(message);
    this.name = 'HandlerRegistryError';
    this.code = code;
    this.details = details;
  }
}

export class HandlerNotFoundError extends Error {
  constructor(messageType) {
    super(`Handler not found for message type: ${messageType}`);
    this.name = 'HandlerNotFoundError';
    this.messageType = messageType;
  }
}

// Build registry with optional tree-sitter handler if feature flag enabled
const baseHandlers = [
  {
    messageType: 'bridge:bootstrap',
    handler: bootstrapHandler,
    isFactory: false,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Gateway handler',
    relatedSteps: [46, 71],
    dependencies: [45],
  },
  {
    messageType: 'bridge:getEditorState',
    handler: getEditorStateHandler,
    isFactory: false,
    timeoutPolicy: 'fast',
    stabilityTier: 'core',
    description: 'Retrieves editor state',
    relatedSteps: [50, 71],
    dependencies: [48, 49],
  },
  {
    messageType: 'bridge:onEditorStateChange',
    handler: async (m, c) => ({ success: true, data: { subscriptionId: 'sub' + Date.now() } }),
    isFactory: false,
    timeoutPolicy: 'fast',
    stabilityTier: 'core',
    description: 'Subscribes to editor changes',
    relatedSteps: [51, 71],
    dependencies: [49],
  },
  {
    messageType: 'bridge:search',
    handler: searchHandler,
    isFactory: false,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Full-text search',
    relatedSteps: [55, 71],
    dependencies: [52],
  },
  {
    messageType: 'bridge:goToDefinition',
    handler: createGoToDefinitionHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Navigate to definition',
    relatedSteps: [56, 71],
    dependencies: [53],
  },
  {
    messageType: 'bridge:findReferences',
    handler: createFindReferencesHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Find references',
    relatedSteps: [57, 71],
    dependencies: [53],
  },
  {
    messageType: 'bridge:codeCompletion',
    handler: createCodeCompletionHandler,
    isFactory: true,
    timeoutPolicy: 'fast',
    stabilityTier: 'core',
    description: 'Code completion',
    relatedSteps: [58, 71],
    dependencies: [50],
  },
  {
    messageType: 'bridge:hoverInfo',
    handler: createHoverInfoHandler,
    isFactory: true,
    timeoutPolicy: 'fast',
    stabilityTier: 'core',
    description: 'Hover information',
    relatedSteps: [59, 71],
    dependencies: [53],
  },
  {
    messageType: 'bridge:testExplorer',
    handler: createTestExplorerHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'experimental',
    description: 'Test explorer',
    relatedSteps: [60, 71],
    dependencies: [84],
  },
  {
    messageType: 'bridge:debugSession',
    handler: DebugSessionHandler,
    isFactory: false,
    timeoutPolicy: 'slow',
    stabilityTier: 'experimental',
    description: 'Debug session',
    relatedSteps: [61, 71],
    dependencies: [82],
  },
  {
    messageType: 'bridge:refactor',
    handler: refactorHandler,
    isFactory: false,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Code refactoring (rename, extract, move, simplify, inline)',
    relatedSteps: [76, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:fixSuggestion',
    handler: fixSuggestionHandler,
    isFactory: false,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Code fix suggestions for diagnostics and errors',
    relatedSteps: [77, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:applyEdit',
    handler: createApplyEditHandler,
    isFactory: true,
    timeoutPolicy: 'fast',
    stabilityTier: 'experimental',
    description: 'Apply text edits to documents',
    relatedSteps: [78, 71],
    dependencies: [52],
  },
  {
    messageType: 'bridge:gitStatus',
    handler: createGitIntegrationHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Git repository status, log, branches, diff operations',
    relatedSteps: [81, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:executeTerminalCommand',
    handler: createTerminalHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Execute terminal commands with output streaming',
    relatedSteps: [82, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:onTerminalOutput',
    handler: async (m, c) => ({ success: true, data: { subscriptionId: 'sub' + Date.now() } }),
    isFactory: false,
    timeoutPolicy: 'fast',
    stabilityTier: 'core',
    description: 'Subscribe to terminal output events',
    relatedSteps: [82, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:readFile',
    handler: createFileSystemHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Read file contents (UTF-8)',
    relatedSteps: [83, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:writeFile',
    handler: createFileSystemHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Write/create file (UTF-8)',
    relatedSteps: [83, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:deleteFile',
    handler: createFileSystemHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Delete file safely',
    relatedSteps: [83, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:listDirectory',
    handler: createFileSystemHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'List directory contents with metadata',
    relatedSteps: [83, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:getFileStats',
    handler: createFileSystemHandler,
    isFactory: true,
    timeoutPolicy: 'fast',
    stabilityTier: 'core',
    description: 'Query file/directory metadata',
    relatedSteps: [83, 71],
    dependencies: [71],
  },
  {
    messageType: 'bridge:createDirectory',
    handler: createFileSystemHandler,
    isFactory: true,
    timeoutPolicy: 'medium',
    stabilityTier: 'core',
    description: 'Create directory with optional parent creation',
    relatedSteps: [83, 71],
    dependencies: [71],
  },
    {
      messageType: 'bridge:getProjectInfo',
      handler: createProjectInfoHandler,
      isFactory: true,
      timeoutPolicy: 'slow',
      stabilityTier: 'core',
      description: 'Get project/solution metadata from IDE',
      relatedSteps: [84, 71],
      dependencies: [71],
    },
    {
      messageType: 'bridge:inlineMessage',
      handler: createInlineMessageHandler,
      isFactory: true,
      timeoutPolicy: 'fast',
      stabilityTier: 'core',
      description: 'Inline message (decorator, code lens, suggestion)',
      relatedSteps: [85, 71],
      dependencies: [71],
    },
    {
      messageType: 'bridge:getSidebarState',
      handler: createSidebarUIHandler,
      isFactory: true,
      timeoutPolicy: 'fast',
      stabilityTier: 'experimental',
      description: 'Query sidebar UI tree state (documents, symbols, diagnostics)',
      relatedSteps: [86, 71, 52, 53, 54, 83],
      dependencies: [52, 53, 54, 83],
    },
    {
      messageType: 'bridge:getContextWindow',
      handler: createContextWindowHandler,
      isFactory: true,
      timeoutPolicy: 'medium',
      stabilityTier: 'core',
      description: 'Query LLM context window token budget and utilization',
      relatedSteps: [87, 71, 88],
      dependencies: [71],
    },
    {
      messageType: 'bridge:getModelInfo',
      handler: createModelInfoHandler,
      isFactory: true,
      timeoutPolicy: 'fast',
      stabilityTier: 'core',
      description: 'Queries available LLM models and current model info',
      relatedSteps: [87, 88, 89],
      dependencies: [84, 87],
    },
    {
      messageType: 'bridge:stream',
      handler: createStreamingResponseHandler,
      isFactory: true,
      timeoutPolicy: 'slow',
      stabilityTier: 'experimental',
      description: 'Streams LLM token responses in real-time with chunk collection',
      relatedSteps: [89, 71],
      dependencies: [88],
    },
    {
      messageType: 'bridge:getCodeLenses',
      handler: createCodeLensHandler,
      isFactory: true,
      timeoutPolicy: 'medium',
      stabilityTier: 'core',
      description: 'Generate inline code lenses for symbol navigation and testing',
      relatedSteps: [90, 71],
      dependencies: [53, 52],
    },
    {
      messageType: 'bridge:getDiff',
      handler: createDiffViewerHandler,
      isFactory: true,
      timeoutPolicy: 'medium',
      stabilityTier: 'core',
      description: 'Generate unified diff between file versions with hunk grouping',
      relatedSteps: [92, 71],
      dependencies: [52],
    },
    {
      messageType: 'bridge:applyDiff',
      handler: createDiffViewerHandler,
      isFactory: true,
      timeoutPolicy: 'medium',
      stabilityTier: 'core',
      description: 'Apply selected diff hunks as edits',
      relatedSteps: [92, 71, 78],
      dependencies: [52],
    },
    {
      messageType: 'bridge:refactorTests',
      handler: createRefactorTestsHandler,
      isFactory: true,
      timeoutPolicy: 'slow',
      stabilityTier: 'experimental',
      description: 'Test generation and validation for refactored code',
      relatedSteps: [93, 71],
      dependencies: [76, 60],
    },
    {
      messageType: 'bridge:workspaceReload',
      handler: createWorkspaceReloadHandler,
      isFactory: true,
      timeoutPolicy: 'medium',
      stabilityTier: 'core',
      description: 'Scoped or full workspace reload with cache invalidation',
      relatedSteps: [94, 71],
      dependencies: [52, 53, 54],
    },
    {
      messageType: 'bridge:loadSettings',
      handler: createLoadSettingsHandler,
      isFactory: true,
      timeoutPolicy: 'medium',
      stabilityTier: 'core',
      description: 'Load LLM settings (model, provider, temperature, context window, etc.)',
      relatedSteps: [95, 71],
      dependencies: [71],
    },
    {
      messageType: 'bridge:applySettings',
      handler: createApplySettingsHandler,
      isFactory: true,
      timeoutPolicy: 'medium',
      stabilityTier: 'core',
      description: 'Apply and persist LLM settings to Continue configuration',
      relatedSteps: [95, 71],
      dependencies: [71],
    },
    {
      messageType: 'bridge:getProfilerData',
      handler: createProfilerHandler,
      isFactory: true,
      timeoutPolicy: 'fast',
      stabilityTier: 'core',
      description: 'Aggregates real-time metrics for handler health diagnostics (Step 96, optional)',
      relatedSteps: [96, 71, 97],
      dependencies: [64, 72, 74, 66],
    },
    {
      messageType: 'bridge:subscribeToMetrics',
      handler: createMetricsStreamHandler,
      isFactory: true,
      timeoutPolicy: 'fast',
      stabilityTier: 'core',
      description: 'Subscription-based real-time metrics streaming for dashboard visualization (Step 101)',
      relatedSteps: [101, 96, 72, 74, 64, 66, 71],
      dependencies: [96],
    },
    {
      messageType: 'bridge:getDiagnosticPanel',
      handler: createDiagnosticPanelHandler,
      isFactory: true,
      timeoutPolicy: 'fast',
      stabilityTier: 'utility',
      description: 'On-demand health snapshot and diagnostics aggregation (Step 102)',
      relatedSteps: [102, 101, 96, 72, 74, 24, 25, 71],
      dependencies: [24, 25],
    },
    {
      messageType: 'bridge:crashRecovery',
      handler: createCrashRecoveryHandler,
      isFactory: true,
      timeoutPolicy: 'slow',
      stabilityTier: 'core',
      description: 'Bridge crash recovery: detects failures, captures diagnostics, orchestrates recovery (Step 103)',
      relatedSteps: [103, 24, 25, 45, 74, 71],
      dependencies: [24, 25],
    },
  ];

// Add tree-sitter handler if feature flag enabled (Step 80)
if (TREE_SITTER_ENABLED) {
  baseHandlers.push({
    messageType: 'bridge:analyzeAST',
    handler: treeAnalysisHandler,
    isFactory: false,
    timeoutPolicy: 'medium',
    stabilityTier: 'experimental',
    description: 'AST analysis via tree-sitter (optional, Step 80)',
    relatedSteps: [80, 71],
    dependencies: [80],
  });
}

const HANDLER_REGISTRY = baseHandlers;

function validateRegistry() {
  const messageTypes = new Set();
  const validTimeouts = ['fast', 'medium', 'slow'];
  const validTiers = ['core', 'experimental', 'deprecated'];

  for (let i = 0; i < HANDLER_REGISTRY.length; i++) {
    const entry = HANDLER_REGISTRY[i];

    if (!entry.messageType) throw new HandlerRegistryError('missing messageType');
    if (!entry.handler) throw new HandlerRegistryError('no handler');
    if (typeof entry.handler !== 'function') throw new HandlerRegistryError('handler not callable');
    if (!validTimeouts.includes(entry.timeoutPolicy)) throw new HandlerRegistryError('bad timeout');
    if (!validTiers.includes(entry.stabilityTier)) throw new HandlerRegistryError('bad tier');
    if (messageTypes.has(entry.messageType)) throw new HandlerRegistryError('duplicate');
    messageTypes.add(entry.messageType);
  }
}

try {
  validateRegistry();
} catch (e) {
  console.error('[Registry]', e.message);
  throw e;
}

export function getAllHandlers() {
  return [...HANDLER_REGISTRY];
}

export function getHandlerMetadata(messageType) {
  const entry = HANDLER_REGISTRY.find((h) => h.messageType === messageType);
  if (!entry) throw new HandlerNotFoundError(messageType);
  return entry;
}

export function getHandlersByStabilityTier(tier) {
  return HANDLER_REGISTRY.filter((h) => h.stabilityTier === tier);
}

export function getHandlersByTimeoutPolicy(policy) {
  return HANDLER_REGISTRY.filter((h) => h.timeoutPolicy === policy);
}

export function hasHandler(messageType) {
  return HANDLER_REGISTRY.some((h) => h.messageType === messageType);
}

// Circuit Breaker Manager singleton (lazy initialized)
let _circuitBreakerManager = null;

export function getCircuitBreakerManager(config = null, deps = {}) {
  if (!_circuitBreakerManager) {
    _circuitBreakerManager = createCircuitBreakerManager(config, deps);
    _circuitBreakerManager.start();
  }
  return _circuitBreakerManager;
}

/**
 * Step 108: Circuit Breaker Integration
 * 
 * The circuit breaker provides per-handler isolation to prevent cascading failures.
 * Three-state machine (CLOSED/OPEN/HALF_OPEN) with automatic state transitions based on error rates.
 * 
 * State Transitions:
 * - CLOSED ? OPEN: errorCount ? 5 OR errorRate > 5%
 * - OPEN ? HALF_OPEN: cooldown expires (30s)
 * - HALF_OPEN ? CLOSED: 2 consecutive successes (recovery successful)
 * - HALF_OPEN ? OPEN: any failure (recovery failed)
 * 
 * Related Steps:
 *   - Step 47: MiddlewareChain (circuit breaker middleware hook)
 *   - Step 64: TimeoutManager (error rate metrics)
 *   - Step 74: ErrorRecoveryMetrics (per-request recovery)
 *   - Step 99: Stress tests (isolation validation)
 *   - Step 107: RateLimiter (complements throttling)
 *   - Step 108: CircuitBreaker (THIS integration)
 *   - Step 109: MetricsAggregator (consumes circuit state)
 *   - Step 110: E2E tests (cascading failure scenarios)
 */

export default {
  getAllHandlers,
  getHandlerMetadata,
  getHandlersByStabilityTier,
  getHandlersByTimeoutPolicy,
  hasHandler,
  HandlerRegistryError,
  HandlerNotFoundError,
  createRateLimiter,
  createRateLimiterMiddleware,
  getRateLimitPolicies,
  createCircuitBreakerManager,
  createCircuitBreakerMiddleware,
  getCircuitBreakerManager,
};

/**
 * Step 107: Rate Limiter Integration
 * 
 * The rate limiter provides per-handler throttling to prevent bridge overload.
 * Policies are registered at startup and enforced via middleware hook (Step 47).
 * 
 * Default Policies:
 * - bridge:complete: 100 tokens/second (fast, interactive completions)
 * - bridge:analyze: 50 tokens/second (medium, code analysis)
 * - bridge:refactor: 10 tokens/second (slow, complex refactors)
 * - Global ceiling: 500 RPC/second bridge-wide
 * 
 * Related Steps:
 *   - Step 47: MiddlewareChain (rate limiter middleware hook)
 *   - Step 64: TimeoutManager (complements timeout enforcement)
 *   - Step 71: HandlerRegistry (this file, policy registration)
 *   - Step 72–74: Middleware (logging, validation, error recovery)
 *   - Step 98: Performance tests (throughput baselines)
 *   - Step 99: Stress tests (load testing with rate limits)
 *   - Step 108: CircuitBreaker (complements rate limiting)
 *   - Step 109: MetricsAggregator (consumes rate limiter metrics)
 */

export function getRateLimitPolicies() {
  return {
    globalCeilingPerSecond: 500,
    handlerPolicies: new Map([
      ['bridge:complete', { tokensPerSecond: 100, burst: 5 }],
      ['bridge:analyze', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:refactor', { tokensPerSecond: 10, burst: 2 }],
      ['bridge:getEditorState', { tokensPerSecond: 100, burst: 5 }],
      ['bridge:search', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:goToDefinition', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:findReferences', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:codeCompletion', { tokensPerSecond: 100, burst: 5 }],
      ['bridge:hoverInfo', { tokensPerSecond: 100, burst: 5 }],
      ['bridge:testExplorer', { tokensPerSecond: 20, burst: 2 }],
      ['bridge:debugSession', { tokensPerSecond: 20, burst: 2 }],
      ['bridge:fixSuggestion', { tokensPerSecond: 30, burst: 2 }],
      ['bridge:applyEdit', { tokensPerSecond: 30, burst: 2 }],
      ['bridge:gitIntegration', { tokensPerSecond: 20, burst: 2 }],
      ['bridge:terminal', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:fileSystem', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:projectInfo', { tokensPerSecond: 20, burst: 2 }],
      ['bridge:inlineMessage', { tokensPerSecond: 100, burst: 5 }],
      ['bridge:sidebarUI', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:contextWindow', { tokensPerSecond: 50, burst: 3 }],
    ]),
    defaultTokensPerSecond: 20,
    defaultBurstMultiplier: 2,
    refillIntervalMs: 100,
  };
}

