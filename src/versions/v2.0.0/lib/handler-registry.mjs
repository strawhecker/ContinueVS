#!/usr/bin/env node
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
import { createModelInfoHandler } from './model-info-handler.mjs';
import { createStreamingResponseHandler } from './streaming-response-handler.mjs';
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

export default {
  getAllHandlers,
  getHandlerMetadata,
  getHandlersByStabilityTier,
  getHandlersByTimeoutPolicy,
  hasHandler,
  HandlerRegistryError,
  HandlerNotFoundError,
};

