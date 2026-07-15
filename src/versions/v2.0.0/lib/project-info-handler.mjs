/**
 * Project Info Handler for Continue Bridge
 * 
 * Exposes project/solution metadata from Visual Studio via IPC.
 * Factory handler for `bridge:getProjectInfo` message type.
 * 
 * Integration:
 * - Consumed by: WebView bridge client (via JSON-RPC)
 * - Uses: ProjectInfoCollector (C# DTE adapter)
 * - Response: Structured project/solution/workspace/build-status object
 * - Error handling: ProjectInfoError, CollectionError (RPC -32603)
 */

export class ProjectInfoError extends Error {
  constructor(message, errorCode = 'PROJECT_INFO_ERROR', originalError = null) {
    super(message);
    this.name = 'ProjectInfoError';
    this.errorCode = errorCode;
    this.originalError = originalError;
  }
}

export class CollectionError extends Error {
  constructor(message, originalError = null) {
    super(message);
    this.name = 'CollectionError';
    this.originalError = originalError;
  }
}

/**
 * Factory function to create a project-info handler.
 * 
 * Options:
 * - logger: optional logger instance with { debug, info, warning, error } methods
 * - metrics: optional metrics instance with { recordEvent } method
 * - collectorInstance: optional ProjectInfoCollector (for testing); if not provided,
 *   handler will fail with clear error directing to C# bridge setup
 * 
 * @param {Object} options Handler configuration
 * @returns {Function} Message handler for bridge:getProjectInfo
 */
export function createProjectInfoHandler(options = {}) {
  if (typeof options !== 'object' || options === null || Array.isArray(options)) {
    throw new Error('Project-info handler options must be a plain object');
  }

  const logger = options.logger || _createMockLogger();
  const metrics = options.metrics || _createMockMetrics();
  const collectorInstance = options.collectorInstance || null;

  logger.debug('Project-info handler factory invoked', {
    hasCollector: !!collectorInstance,
    hasLogger: !!options.logger,
    hasMetrics: !!options.metrics,
  });

  /**
   * Handle bridge:getProjectInfo message.
   * 
   * Request: minimal message metadata (no payload required)
   * Response: { solution, projects, workspace, buildStatus }
   * 
   * @param {Object} message The incoming message
   * @param {string} message.messageId Unique request identifier
   * @param {Object} context Bridge context (for logging/metrics)
   * @returns {Promise<Object>} Structured project info response
   */
  return async function handleGetProjectInfo(message, context) {
    const requestId = message?.messageId || 'unknown';
    const startTime = Date.now();

    try {
      logger.debug('bridge:getProjectInfo request received', {
        messageId: requestId,
        hasContext: !!context,
      });

      // Validate message structure
      if (!message || typeof message !== 'object') {
        throw new ProjectInfoError(
          'Invalid message format: must be a plain object',
          'INVALID_MESSAGE',
          null
        );
      }

      if (!requestId || typeof requestId !== 'string') {
        throw new ProjectInfoError(
          'Message must include a valid messageId string',
          'MISSING_MESSAGE_ID',
          null
        );
      }

      // Ensure collector is available
      if (!collectorInstance) {
        throw new ProjectInfoError(
          'ProjectInfoCollector not initialized; C# bridge adapter may not be running',
          'COLLECTOR_NOT_INITIALIZED',
          null
        );
      }

      // Call C# collector to gather metadata
      let projectInfo;
      try {
        logger.debug('Invoking ProjectInfoCollector.GetProjectInfo()');
        projectInfo = await _callCollectorAsync(collectorInstance);
      } catch (collectorError) {
        logger.error('ProjectInfoCollector failed', {
          error: collectorError?.message,
          errorName: collectorError?.name,
          stack: collectorError?.stack,
        });
        throw new CollectionError(
          `Failed to collect project info from IDE: ${collectorError?.message || 'unknown error'}`,
          collectorError
        );
      }

      // Normalize response structure
      const normalizedResponse = _normalizeProjectInfo(projectInfo);

      logger.info('bridge:getProjectInfo completed successfully', {
        messageId: requestId,
        projectCount: normalizedResponse.projects?.length || 0,
        durationMs: Date.now() - startTime,
      });

      metrics.recordEvent('project_info_handler_success', {
        messageId: requestId,
        projectCount: normalizedResponse.projects?.length || 0,
        durationMs: Date.now() - startTime,
      });

      // Return BridgeMessage-compatible response
      return {
        messageId: requestId,
        type: 'bridge:getProjectInfo',
        success: true,
        data: normalizedResponse,
        timestamp: new Date().toISOString(),
      };
    } catch (error) {
      logger.error('bridge:getProjectInfo handler error', {
        messageId: requestId,
        errorName: error?.name,
        errorMessage: error?.message,
        errorCode: error?.errorCode,
        durationMs: Date.now() - startTime,
      });

      metrics.recordEvent('project_info_handler_error', {
        messageId: requestId,
        errorCode: error?.errorCode || 'UNKNOWN_ERROR',
        durationMs: Date.now() - startTime,
      });

      // Format error response for JSON-RPC
      const errorResponse = {
        messageId: requestId,
        type: 'bridge:getProjectInfo',
        success: false,
        error: {
          code: _mapErrorToRpcCode(error),
          message: error?.message || 'Unknown error',
          errorCode: error?.errorCode || 'UNKNOWN_ERROR',
          details: {
            errorName: error?.name,
            originalError: error?.originalError?.message,
          },
        },
        timestamp: new Date().toISOString(),
      };

      logger.debug('Returning error response', { errorResponse });
      return errorResponse;
    }
  };
}

/**
 * Normalizes project info from C# collector to JSON-compatible schema.
 * Ensures all fields are present and properly typed.
 * 
 * @param {Object} projectInfo Raw output from ProjectInfoCollector
 * @returns {Object} Normalized project info
 */
function _normalizeProjectInfo(projectInfo) {
  if (!projectInfo || typeof projectInfo !== 'object') {
    throw new Error('projectInfo must be a valid object');
  }

  const solution = projectInfo.solution || {};
  const projects = Array.isArray(projectInfo.projects) ? projectInfo.projects : [];
  const workspace = projectInfo.workspace || {};
  const buildStatus = projectInfo.buildStatus || {};

  return {
    solution: {
      name: String(solution.name || 'Unknown'),
      path: String(solution.path || ''),
      projectCount: Number(solution.projectCount || 0),
    },
    projects: projects.map((proj) => ({
      name: String(proj.name || 'Unknown'),
      path: String(proj.path || ''),
      type: String(proj.type || 'Unknown'),
      targetFramework: String(proj.targetFramework || 'Unknown'),
      buildStatus: String(proj.buildStatus || 'Ready'),
      projectKind: String(proj.projectKind || ''),
    })),
    workspace: {
      rootPath: String(workspace.rootPath || ''),
      gitBranch: workspace.gitBranch ? String(workspace.gitBranch) : null,
    },
    buildStatus: {
      lastBuild: buildStatus.lastBuild ? String(buildStatus.lastBuild) : null,
      isBuilding: Boolean(buildStatus.isBuilding || false),
      errors: Number(buildStatus.errors || 0),
      warnings: Number(buildStatus.warnings || 0),
    },
  };
}

/**
 * Maps error instances to JSON-RPC error codes.
 * 
 * @param {Error} error The error instance
 * @returns {number} JSON-RPC error code
 */
function _mapErrorToRpcCode(error) {
  if (error?.errorCode === 'INVALID_MESSAGE') return -32600; // Invalid Request
  if (error?.errorCode === 'MISSING_MESSAGE_ID') return -32600; // Invalid Request
  if (error?.errorCode === 'COLLECTOR_NOT_INITIALIZED') return -32603; // Internal error
  if (error?.name === 'CollectionError') return -32603; // Internal error
  return -32603; // Default to internal error
}

/**
 * Simulates async call to ProjectInfoCollector.GetProjectInfo().
 * In production, this would invoke the C# bridge adapter.
 * 
 * @param {Object} collector The collector instance
 * @returns {Promise<Object>} Project info from collector
 */
async function _callCollectorAsync(collector) {
  return new Promise((resolve, reject) => {
    try {
      // For mock/test collectors, return their response directly
      if (typeof collector.getProjectInfo === 'function') {
        const result = collector.getProjectInfo();
        resolve(result);
      } else if (typeof collector.GetProjectInfo === 'function') {
        // If bound to actual C# instance
        const result = collector.GetProjectInfo();
        resolve(result);
      } else {
        throw new Error('Collector does not have getProjectInfo or GetProjectInfo method');
      }
    } catch (err) {
      reject(err);
    }
  });
}

/**
 * Creates a no-op logger for graceful degradation.
 * 
 * @returns {Object} Mock logger
 */
function _createMockLogger() {
  return {
    debug: () => {},
    info: () => {},
    warning: () => {},
    error: () => {},
  };
}

/**
 * Creates a no-op metrics recorder for graceful degradation.
 * 
 * @returns {Object} Mock metrics
 */
function _createMockMetrics() {
  return {
    recordEvent: () => {},
  };
}

/**
 * Default export: handler factory for direct use.
 */
export default createProjectInfoHandler;
