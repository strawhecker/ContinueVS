#!/usr/bin/env node

/**
 * Protocol Compatibility Test Fixtures (Step 111)
 *
 * Provides factory functions and test data for cross-version protocol compatibility tests.
 * Includes message pairs (C# ↔ Node), error code mappings, and handler dispatch scenarios.
 *
 * @module src/versions/v2.0.0/tests/mocks/protocol-compatibility-fixtures.mjs
 * @version 1.0.0
 */

/**
 * Get array of C# Message ↔ Node BridgeMessage pairs for testing
 * Covers all handler types from Steps 46-61
 * @returns {Array} Array of {csharpMessage, nodeBridgeMessage} pairs
 */
export function getProtocolTestMessages() {
  return [
    // 1. getEditorState handler
    createMessagePair('getEditorState', 'bridge:getEditorState', {
      filePath: 'src/Program.cs',
      position: { line: 42, character: 10 }
    }),

    // 2. onEditorStateChange subscription
    createMessagePair('onEditorStateChange', 'bridge:onEditorStateChange', {
      subscribe: true
    }),

    // 3. search handler
    createMessagePair('search', 'bridge:search', {
      query: 'FindAllReferences',
      scope: 'workspace'
    }),

    // 4. goToDefinition handler
    createMessagePair('goToDefinition', 'bridge:goToDefinition', {
      filePath: 'src/Handlers.cs',
      line: 100,
      character: 5
    }),

    // 5. findReferences handler
    createMessagePair('findReferences', 'bridge:findReferences', {
      filePath: 'src/Handler.cs',
      line: 50,
      character: 15
    }),

    // 6. codeCompletion handler
    createMessagePair('codeCompletion', 'bridge:codeCompletion', {
      filePath: 'src/Service.cs',
      line: 200,
      character: 25,
      triggerCharacter: '.'
    }),

    // 7. hoverInfo handler
    createMessagePair('hoverInfo', 'bridge:hoverInfo', {
      filePath: 'src/Main.cs',
      line: 75,
      character: 30
    }),

    // 8. refactor handler
    createMessagePair('refactor', 'bridge:refactor', {
      refactoringType: 'rename',
      oldName: 'OldClassName',
      newName: 'NewClassName',
      scope: 'project'
    }),

    // 9. fixSuggestion handler
    createMessagePair('fixSuggestion', 'bridge:fixSuggestion', {
      diagnosticCode: 'CS0246',
      filePath: 'src/Test.cs',
      line: 10,
      message: 'The type or namespace name could not be found'
    }),

    // 10. applyEdit handler
    createMessagePair('applyEdit', 'bridge:applyEdit', {
      edits: [
        {
          filePath: 'src/File1.cs',
          startLine: 5,
          startChar: 0,
          endLine: 5,
          endChar: 20,
          newText: 'var result = await GetData();'
        }
      ]
    }),

    // 11. testExplorer handler
    createMessagePair('testExplorer', 'bridge:testExplorer', {
      action: 'discover',
      projectPath: 'src/MyTests.csproj'
    }),

    // 12. debugSession handler
    createMessagePair('debugSession', 'bridge:debugSession', {
      action: 'start',
      breakpoints: [
        { filePath: 'src/Main.cs', line: 42 }
      ]
    }),

    // 13. gitIntegration handler
    createMessagePair('gitIntegration', 'bridge:gitIntegration', {
      action: 'status',
      repoPath: 'E:\\GitRepos\\ContinueVS'
    }),

    // 14. terminal handler
    createMessagePair('terminal', 'bridge:terminal', {
      command: 'dotnet build',
      workingDirectory: 'E:\\GitRepos\\ContinueVS'
    }),

    // 15. fileSystem handler
    createMessagePair('fileSystem', 'bridge:fileSystem', {
      action: 'read',
      filePath: 'src/config.json'
    }),

    // 16. projectInfo handler
    createMessagePair('projectInfo', 'bridge:projectInfo', {
      action: 'getSolution'
    }),

    // 17. inlineMessage handler
    createMessagePair('inlineMessage', 'bridge:inlineMessage', {
      message: 'Code suggestion',
      line: 50,
      filePath: 'src/Handler.cs'
    }),

    // 18. sidebarUI handler
    createMessagePair('sidebarUI', 'bridge:sidebarUI', {
      action: 'show',
      panelType: 'refactoring'
    }),

    // 19. contextWindow handler
    createMessagePair('contextWindow', 'bridge:contextWindow', {
      maxTokens: 4000
    }),

    // 20. formatDocument handler
    createMessagePair('formatDocument', 'bridge:formatDocument', {
      filePath: 'src/Program.cs'
    })
  ];
}

/**
 * Get error code mappings (C# JsonRpcProtocol ↔ Node validation)
 * @returns {Object} Error code mappings with descriptions
 */
export function getErrorCodeMappings() {
  return {
    jsonRpc: {
      '-32700': {
        name: 'PARSE_ERROR',
        message: 'Invalid JSON was received by the server',
        csharpConstant: 'JsonRpcProtocol.PARSE_ERROR'
      },
      '-32600': {
        name: 'INVALID_REQUEST',
        message: 'The JSON sent is not a valid Request object',
        csharpConstant: 'JsonRpcProtocol.INVALID_REQUEST'
      },
      '-32601': {
        name: 'METHOD_NOT_FOUND',
        message: 'The method does not exist / is not available',
        csharpConstant: 'JsonRpcProtocol.METHOD_NOT_FOUND'
      },
      '-32602': {
        name: 'INVALID_PARAMS',
        message: 'Invalid method parameter(s)',
        csharpConstant: 'JsonRpcProtocol.INVALID_PARAMS'
      },
      '-32603': {
        name: 'INTERNAL_ERROR',
        message: 'Internal JSON-RPC error',
        csharpConstant: 'JsonRpcProtocol.INTERNAL_ERROR'
      }
    },
    bridge: {
      '-32000': {
        name: 'BRIDGE_TIMEOUT',
        message: 'RPC call timed out',
        csharpConstant: 'JsonRpcProtocol.BRIDGE_TIMEOUT'
      },
      '-32001': {
        name: 'BRIDGE_PROCESS_DEAD',
        message: 'Continue process is not running or was terminated',
        csharpConstant: 'JsonRpcProtocol.BRIDGE_PROCESS_DEAD'
      },
      '-32002': {
        name: 'BRIDGE_INVALID_STATE',
        message: 'Bridge is in invalid state for the requested operation',
        csharpConstant: 'JsonRpcProtocol.BRIDGE_INVALID_STATE'
      },
      '-32003': {
        name: 'BRIDGE_HANDLER_NOT_FOUND',
        message: 'Handler not found or not registered',
        csharpConstant: 'JsonRpcProtocol.BRIDGE_HANDLER_NOT_FOUND'
      },
      '-32004': {
        name: 'BRIDGE_VALIDATION_ERROR',
        message: 'Message validation failed (malformed envelope)',
        csharpConstant: 'JsonRpcProtocol.BRIDGE_VALIDATION_ERROR'
      }
    }
  };
}

/**
 * Get handler dispatch scenarios with timeout policies
 * @returns {Array} Array of handler dispatch scenarios
 */
export function getHandlerDispatchScenarios() {
  return [
    {
      handlerType: 'getEditorState',
      messageType: 'bridge:getEditorState',
      tier: 'fast',
      expectedTimeout: 2000,
      expectedResponseSchema: {
        filePath: 'string',
        content: 'string',
        cursorPosition: 'object'
      }
    },
    {
      handlerType: 'search',
      messageType: 'bridge:search',
      tier: 'medium',
      expectedTimeout: 10000,
      expectedResponseSchema: {
        results: 'array'
      }
    },
    {
      handlerType: 'goToDefinition',
      messageType: 'bridge:goToDefinition',
      tier: 'medium',
      expectedTimeout: 10000,
      expectedResponseSchema: {
        filePath: 'string',
        line: 'number',
        character: 'number'
      }
    },
    {
      handlerType: 'codeCompletion',
      messageType: 'bridge:codeCompletion',
      tier: 'medium',
      expectedTimeout: 10000,
      expectedResponseSchema: {
        completions: 'array'
      }
    },
    {
      handlerType: 'refactor',
      messageType: 'bridge:refactor',
      tier: 'slow',
      expectedTimeout: 30000,
      expectedResponseSchema: {
        edits: 'array',
        summary: 'string'
      }
    },
    {
      handlerType: 'fixSuggestion',
      messageType: 'bridge:fixSuggestion',
      tier: 'medium',
      expectedTimeout: 10000,
      expectedResponseSchema: {
        suggestions: 'array'
      }
    },
    {
      handlerType: 'applyEdit',
      messageType: 'bridge:applyEdit',
      tier: 'medium',
      expectedTimeout: 10000,
      expectedResponseSchema: {
        success: 'boolean',
        message: 'string'
      }
    },
    {
      handlerType: 'testExplorer',
      messageType: 'bridge:testExplorer',
      tier: 'slow',
      expectedTimeout: 30000,
      expectedResponseSchema: {
        tests: 'array'
      }
    },
    {
      handlerType: 'gitIntegration',
      messageType: 'bridge:gitIntegration',
      tier: 'medium',
      expectedTimeout: 10000,
      expectedResponseSchema: {
        status: 'object'
      }
    },
    {
      handlerType: 'terminal',
      messageType: 'bridge:terminal',
      tier: 'slow',
      expectedTimeout: 30000,
      expectedResponseSchema: {
        output: 'string',
        exitCode: 'number'
      }
    }
  ];
}

/**
 * Create a message pair (C# Message + Node BridgeMessage)
 * @param {string} handlerType - Handler type name
 * @param {string} messageType - Full message type (e.g., 'bridge:getEditorState')
 * @param {object} data - Message payload data
 * @returns {Object} Object with csharpMessage and nodeBridgeMessage
 */
export function createMessagePair(handlerType, messageType, data = null) {
  const messageId = `msg-${handlerType}-${Date.now()}`;

  // C# Message envelope (as JSON)
  const csharpMessage = {
    messageId,
    messageType,
    data,
    timestamp: new Date().toISOString()
  };

  // Node BridgeMessage (same structure for this protocol)
  const nodeBridgeMessage = {
    messageId,
    messageType,
    data
  };

  return {
    csharpMessage,
    nodeBridgeMessage,
    handlerType,
    messageType,
    correlationId: messageId
  };
}

/**
 * Create an error response pair
 * @param {number} errorCode - Error code (negative integer)
 * @param {string} message - Error message
 * @param {string} messageId - Correlation messageId
 * @param {object} errorData - Optional error details
 * @returns {Object} Error response pair
 */
export function createErrorResponsePair(errorCode, message, messageId, errorData = null) {
  const response = {
    messageId,
    messageType: 'response',
    error: {
      code: errorCode,
      message: message,
      data: errorData
    }
  };

  return {
    errorCode,
    message,
    response,
    messageId,
    isError: true
  };
}

/**
 * Create a success response pair
 * @param {string} messageId - Correlation messageId
 * @param {object} result - Result payload
 * @returns {Object} Success response pair
 */
export function createSuccessResponsePair(messageId, result) {
  const response = {
    messageId,
    messageType: 'response',
    result,
    timestamp: new Date().toISOString()
  };

  return {
    response,
    messageId,
    isError: false,
    result
  };
}

export default {
  getProtocolTestMessages,
  getErrorCodeMappings,
  getHandlerDispatchScenarios,
  createMessagePair,
  createErrorResponsePair,
  createSuccessResponsePair
};
