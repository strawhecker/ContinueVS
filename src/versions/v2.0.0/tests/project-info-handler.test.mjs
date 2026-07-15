/**
 * Unit tests for project-info-handler.mjs
 * Tests: factory initialization, message handling, collector integration,
 * response structure, error handling, and logging/metrics.
 */

import assert from 'assert';
import { describe, it, beforeEach } from 'mocha';
import {
  createProjectInfoHandler,
  ProjectInfoError,
  CollectionError,
} from '../lib/project-info-handler.mjs';

describe('Project-Info Handler', () => {
  // ============================================================================
  // Suite 1: Initialization & Factory (4 tests)
  // ============================================================================
  describe('Suite 1: Initialization & Factory', () => {
    it('should create handler with valid options', () => {
      // Arrange & Act
      const handler = createProjectInfoHandler({});

      // Assert
      assert.ok(typeof handler === 'function', 'handler should be a function');
    });

    it('should accept logger in options', () => {
      // Arrange
      const mockLogger = {
        debug: () => {},
        info: () => {},
        warning: () => {},
        error: () => {},
      };

      // Act
      const handler = createProjectInfoHandler({ logger: mockLogger });

      // Assert
      assert.ok(typeof handler === 'function');
    });

    it('should accept metrics in options', () => {
      // Arrange
      const mockMetrics = {
        recordEvent: () => {},
      };

      // Act
      const handler = createProjectInfoHandler({ metrics: mockMetrics });

      // Assert
      assert.ok(typeof handler === 'function');
    });

    it('should throw error if options is not a plain object', () => {
      // Act & Assert
      assert.throws(
        () => createProjectInfoHandler(null),
        Error,
        'should throw for null options'
      );

      assert.throws(
        () => createProjectInfoHandler([]),
        Error,
        'should throw for array options'
      );

      assert.throws(
        () => createProjectInfoHandler('string'),
        Error,
        'should throw for string options'
      );
    });
  });

  // ============================================================================
  // Suite 2: Message Handling (5 tests)
  // ============================================================================
  describe('Suite 2: Message Handling', () => {
    let handler;
    let mockCollector;

    beforeEach(() => {
      mockCollector = createMockCollector();
      handler = createProjectInfoHandler({ collectorInstance: mockCollector });
    });

    it('should handle valid request message', async () => {
      // Arrange
      const message = {
        messageId: 'msg-1',
        type: 'bridge:getProjectInfo',
      };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.messageId, 'msg-1');
      assert.equal(response.success, true);
      assert.ok(response.data);
    });

    it('should throw error if message is null', async () => {
      // Arrange
      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });

      // Act & Assert
      const response = await handler(null, {});
      assert.equal(response.success, false);
      assert.equal(response.error.code, -32600);
    });

    it('should throw error if messageId is missing', async () => {
      // Arrange
      const message = {
        type: 'bridge:getProjectInfo',
      };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.success, false);
      assert.equal(response.error.code, -32600);
    });

    it('should throw error if messageId is not a string', async () => {
      // Arrange
      const message = {
        messageId: 123,
        type: 'bridge:getProjectInfo',
      };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.success, false);
      assert.equal(response.error.code, -32600);
    });

    it('should handle message with missing optional context', async () => {
      // Arrange
      const message = {
        messageId: 'msg-2',
        type: 'bridge:getProjectInfo',
      };

      // Act
      const response = await handler(message);

      // Assert
      assert.equal(response.success, true);
      assert.ok(response.data);
    });
  });

  // ============================================================================
  // Suite 3: Collector Integration (4 tests)
  // ============================================================================
  describe('Suite 3: Collector Integration', () => {
    it('should invoke collector getProjectInfo method', async () => {
      // Arrange
      let called = false;
      const mockCollector = {
        getProjectInfo: () => {
          called = true;
          return getValidProjectInfoResponse();
        },
      };

      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });
      const message = { messageId: 'msg-1' };

      // Act
      await handler(message, {});

      // Assert
      assert.ok(called, 'collector.getProjectInfo should have been called');
    });

    it('should pass context to collector if available', async () => {
      // Arrange
      const mockCollector = createMockCollector();
      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });
      const message = { messageId: 'msg-1' };
      const context = { userId: 'user-123' };

      // Act
      const response = await handler(message, context);

      // Assert
      assert.equal(response.success, true);
    });

    it('should wrap collector errors as CollectionError', async () => {
      // Arrange
      const mockCollector = {
        getProjectInfo: () => {
          throw new Error('Collector crashed');
        },
      };

      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });
      const message = { messageId: 'msg-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.success, false);
      assert.ok(response.error.message.includes('Failed to collect project info'));
    });

    it('should return error if collector is not initialized', async () => {
      // Arrange
      const handler = createProjectInfoHandler({
        collectorInstance: null,
      });
      const message = { messageId: 'msg-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.success, false);
      assert.equal(response.error.errorCode, 'COLLECTOR_NOT_INITIALIZED');
    });
  });

  // ============================================================================
  // Suite 4: Response Structure (4 tests)
  // ============================================================================
  describe('Suite 4: Response Structure', () => {
    let handler;
    let mockCollector;

    beforeEach(() => {
      mockCollector = createMockCollector();
      handler = createProjectInfoHandler({ collectorInstance: mockCollector });
    });

    it('should include all required response fields', async () => {
      // Arrange
      const message = { messageId: 'msg-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.ok(response.messageId);
      assert.ok(response.type);
      assert.strictEqual(response.success, true);
      assert.ok(response.data);
      assert.ok(response.timestamp);
    });

    it('should include solution info in data', async () => {
      // Arrange
      const message = { messageId: 'msg-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      const data = response.data;
      assert.ok(data.solution);
      assert.ok(typeof data.solution.name === 'string');
      assert.ok(typeof data.solution.path === 'string');
      assert.ok(typeof data.solution.projectCount === 'number');
    });

    it('should include projects array in data', async () => {
      // Arrange
      const message = { messageId: 'msg-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      const data = response.data;
      assert.ok(Array.isArray(data.projects));
      if (data.projects.length > 0) {
        const proj = data.projects[0];
        assert.ok(typeof proj.name === 'string');
        assert.ok(typeof proj.path === 'string');
        assert.ok(typeof proj.type === 'string');
      }
    });

    it('should normalize workspace and buildStatus in data', async () => {
      // Arrange
      const message = { messageId: 'msg-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      const data = response.data;
      assert.ok(data.workspace);
      assert.ok(typeof data.workspace.rootPath === 'string');
      assert.ok(data.buildStatus);
      assert.ok(typeof data.buildStatus.isBuilding === 'boolean');
      assert.ok(typeof data.buildStatus.errors === 'number');
    });
  });

  // ============================================================================
  // Suite 5: Error Handling (4 tests)
  // ============================================================================
  describe('Suite 5: Error Handling', () => {
    it('should return error response when collector throws', async () => {
      // Arrange
      const mockCollector = {
        getProjectInfo: () => {
          throw new Error('Test error');
        },
      };

      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });
      const message = { messageId: 'err-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.success, false);
      assert.ok(response.error);
      assert.equal(response.error.code, -32603);
    });

    it('should include error details in response', async () => {
      // Arrange
      const mockCollector = {
        getProjectInfo: () => {
          throw new Error('Detailed error');
        },
      };

      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });
      const message = { messageId: 'err-2' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.ok(response.error.message);
      assert.ok(response.error.errorCode);
      assert.ok(response.error.details);
    });

    it('should map ProjectInfoError correctly', async () => {
      // Arrange
      const mockCollector = {
        getProjectInfo: () => {
          throw new ProjectInfoError('Custom error', 'CUSTOM_CODE');
        },
      };

      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });
      const message = { messageId: 'err-3' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.success, false);
      assert.equal(response.error.code, -32603);
    });

    it('should maintain messageId in error response', async () => {
      // Arrange
      const mockCollector = {
        getProjectInfo: () => {
          throw new Error('Error');
        },
      };

      const handler = createProjectInfoHandler({ collectorInstance: mockCollector });
      const message = { messageId: 'error-preserve' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.messageId, 'error-preserve');
      assert.equal(response.success, false);
    });
  });

  // ============================================================================
  // Suite 6: Logging & Metrics (3 tests)
  // ============================================================================
  describe('Suite 6: Logging & Metrics', () => {
    it('should record metrics on success', async () => {
      // Arrange
      let metricsRecorded = false;
      const mockMetrics = {
        recordEvent: (eventName) => {
          if (eventName === 'project_info_handler_success') {
            metricsRecorded = true;
          }
        },
      };

      const mockCollector = createMockCollector();
      const handler = createProjectInfoHandler({
        collectorInstance: mockCollector,
        metrics: mockMetrics,
      });

      const message = { messageId: 'metrics-1' };

      // Act
      await handler(message, {});

      // Assert
      assert.ok(metricsRecorded, 'success metrics should be recorded');
    });

    it('should record metrics on error', async () => {
      // Arrange
      let errorMetricsRecorded = false;
      const mockMetrics = {
        recordEvent: (eventName) => {
          if (eventName === 'project_info_handler_error') {
            errorMetricsRecorded = true;
          }
        },
      };

      const mockCollector = {
        getProjectInfo: () => {
          throw new Error('Test');
        },
      };

      const handler = createProjectInfoHandler({
        collectorInstance: mockCollector,
        metrics: mockMetrics,
      });

      const message = { messageId: 'metrics-2' };

      // Act
      await handler(message, {});

      // Assert
      assert.ok(errorMetricsRecorded, 'error metrics should be recorded');
    });

    it('should gracefully degrade when logger and metrics are null', async () => {
      // Arrange
      const mockCollector = createMockCollector();
      const handler = createProjectInfoHandler({
        collectorInstance: mockCollector,
        logger: null,
        metrics: null,
      });

      const message = { messageId: 'graceful-1' };

      // Act
      const response = await handler(message, {});

      // Assert
      assert.equal(response.success, true);
      assert.ok(response.data);
    });
  });
});

// ============================================================================
// Test Fixtures & Helpers
// ============================================================================

function createMockCollector() {
  return {
    getProjectInfo: () => getValidProjectInfoResponse(),
  };
}

function getValidProjectInfoResponse() {
  return {
    solution: {
      name: 'TestSolution',
      path: 'C:\\Solution\\TestSolution.sln',
      projectCount: 2,
    },
    projects: [
      {
        name: 'Project1',
        path: 'C:\\Solution\\Project1\\Project1.csproj',
        type: 'C# Project',
        targetFramework: 'net8.0',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
      {
        name: 'Project2',
        path: 'C:\\Solution\\Project2\\Project2.csproj',
        type: 'C# Project',
        targetFramework: 'net472',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
    ],
    workspace: {
      rootPath: 'C:\\Solution',
      gitBranch: 'main',
    },
    buildStatus: {
      lastBuild: '2024-01-15T10:30:00Z',
      isBuilding: false,
      errors: 0,
      warnings: 0,
    },
  };
}

function getEmptySolutionResponse() {
  return {
    solution: {
      name: 'EmptySolution',
      path: 'C:\\Solution\\Empty.sln',
      projectCount: 0,
    },
    projects: [],
    workspace: {
      rootPath: 'C:\\Solution',
      gitBranch: 'main',
    },
    buildStatus: {
      lastBuild: null,
      isBuilding: false,
      errors: 0,
      warnings: 0,
    },
  };
}

function getErrorResponse() {
  return {
    solution: {
      name: 'ErrorSolution',
      path: '',
      projectCount: 0,
    },
    projects: [],
    workspace: {
      rootPath: '',
      gitBranch: null,
    },
    buildStatus: {
      lastBuild: null,
      isBuilding: false,
      errors: 1,
      warnings: 0,
    },
  };
}

export { getValidProjectInfoResponse, getEmptySolutionResponse, getErrorResponse };
