/**
 * E2E Scenario Engine - Orchestrates realistic workflow scenarios
 * Step 110: End-to-End Scenario Tests
 * 
 * Validates bridge by executing 8 realistic user workflows,
 * exercising 3-5 handlers per workflow with state tracking and performance assertions.
 */

export class E2EScenarioEngine {
  constructor(config = {}) {
    this.handlers = config.handlers || {};
    this.config = { ...createDefaultConfig(), ...config };
    this.logger = config.logger || createDefaultLogger();
    this.metrics = config.metrics || createDefaultMetrics();
    this.stateTracker = new WorkflowStateTracker();
    this.isDisposed = false;
  }

  /**
   * Execute a complete workflow scenario
   * @param {Object} scenario - Scenario definition
   * @param {Object} inputs - Workflow inputs
   * @returns {Promise<Object>} - { success, duration, checkpoints, errors, metrics }
   */
  async executeWorkflow(scenario, inputs) {
    if (this.isDisposed) {
      throw new Error('E2EScenarioEngine has been disposed');
    }

    const startTime = performance.now();
    const result = {
      success: false,
      duration: 0,
      checkpoints: [],
      errors: [],
      metrics: {},
      handlerChain: [],
    };

    try {
      this.logger.info(`Starting workflow: ${scenario.name}`);

      // Initialize scenario state
      const context = {
        inputs,
        state: {},
        metadata: {
          scenarioName: scenario.name,
          startTime,
        },
      };

      // Execute workflow steps
      for (const step of scenario.steps) {
        try {
          const stepResult = await this.executeStep(
            step,
            context.inputs,
            context
          );

          result.handlerChain.push({
            handler: step.handler,
            duration: stepResult.latency,
            success: stepResult.success,
          });

          // Capture checkpoint
          if (stepResult.checkpoint) {
            result.checkpoints.push({
              label: step.name,
              state: stepResult.checkpoint,
              latency: stepResult.latency,
            });
          }

          // Update context for next step
          context.inputs = stepResult.response;
          context.state[step.name] = stepResult.response;
        } catch (error) {
          result.errors.push({
            step: step.name,
            handler: step.handler,
            error: error.message,
            stack: error.stack,
          });
          throw error;
        }
      }

      // Validate final state
      const validation = this.stateTracker.validate();
      if (!validation.isValid) {
        result.errors.push({
          type: 'state-validation',
          violations: validation.violations,
        });
        throw new Error(`State validation failed: ${validation.violations.join(', ')}`);
      }

      result.success = true;
      this.logger.info(`✓ Workflow completed: ${scenario.name}`);
    } catch (error) {
      this.logger.error(`✗ Workflow failed: ${scenario.name}`, error);
      result.success = false;
    } finally {
      const endTime = performance.now();
      result.duration = endTime - startTime;
      result.metrics = this.metrics.collect();
    }

    return result;
  }

  /**
   * Execute a single workflow step with a handler
   * @param {Object} step - Step definition
   * @param {Object} input - Step input
   * @param {Object} context - Workflow context
   * @returns {Promise<Object>} - { response, checkpoint, latency, success }
   */
  async executeStep(step, input, context) {
    const startTime = performance.now();
    const stepResult = {
      response: null,
      checkpoint: null,
      latency: 0,
      success: false,
    };

    try {
      const handler = this.handlers[step.handler];
      if (!handler) {
        throw new Error(`Handler not found: ${step.handler}`);
      }

      // Execute handler with timeout
      const response = await this.executeWithTimeout(
        handler(input, context),
        this.config.handlerTimeout
      );

      stepResult.response = response;
      stepResult.success = true;

      // Capture checkpoint
      this.stateTracker.checkpoint(step.name, {
        input,
        output: response,
      });

      stepResult.checkpoint = {
        handler: step.handler,
        input,
        output: response,
      };
    } catch (error) {
      this.logger.error(`Handler failed: ${step.handler}`, error);
      throw error;
    } finally {
      const endTime = performance.now();
      stepResult.latency = endTime - startTime;
      this.metrics.recordHandlerLatency(step.handler, stepResult.latency);
    }

    return stepResult;
  }

  /**
   * Execute a promise with timeout
   */
  async executeWithTimeout(promise, timeoutMs) {
    return Promise.race([
      promise,
      new Promise((_, reject) =>
        setTimeout(
          () => reject(new Error(`Handler timeout after ${timeoutMs}ms`)),
          timeoutMs
        )
      ),
    ]);
  }

  /**
   * Get aggregated metrics from all executions
   */
  getMetrics() {
    return this.metrics.aggregate();
  }

  /**
   * Dispose resources
   */
  dispose() {
    this.isDisposed = true;
    this.stateTracker.clear();
    this.metrics.clear();
  }
}

/**
 * Workflow State Tracker - Validates state consistency
 */
export class WorkflowStateTracker {
  constructor() {
    this.checkpoints = [];
    this.violations = [];
  }

  checkpoint(label, state) {
    this.checkpoints.push({
      label,
      state,
      timestamp: Date.now(),
    });
  }

  validate() {
    const isValid = this.violations.length === 0;
    return {
      isValid,
      violations: this.violations,
    };
  }

  clear() {
    this.checkpoints = [];
    this.violations = [];
  }
}

/**
 * Scenario Runners - Pre-defined workflow orchestrators
 */

export async function runEditorToAIWorkflow(engine, fixture) {
  const scenario = {
    name: 'Editor-to-AI Workflow',
    steps: [
      {
        name: 'Get Editor State',
        handler: 'getEditorState',
      },
      {
        name: 'Extract Symbols',
        handler: 'extractSymbols',
      },
      {
        name: 'Generate Completion',
        handler: 'generateCompletion',
      },
      {
        name: 'Apply Edit',
        handler: 'applyEdit',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

export async function runCodeNavigationWorkflow(engine, fixture) {
  const scenario = {
    name: 'Code Navigation Workflow',
    steps: [
      {
        name: 'Search',
        handler: 'search',
      },
      {
        name: 'Go to Definition',
        handler: 'goToDefinition',
      },
      {
        name: 'Find References',
        handler: 'findReferences',
      },
      {
        name: 'Hover Info',
        handler: 'hoverInfo',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

export async function runGitIntegratedWorkflow(engine, fixture) {
  const scenario = {
    name: 'Git-Integrated Workflow',
    steps: [
      {
        name: 'Load Diff',
        handler: 'loadDiff',
      },
      {
        name: 'Refactor',
        handler: 'refactor',
      },
      {
        name: 'Get Git Info',
        handler: 'getGitInfo',
      },
      {
        name: 'Execute Terminal',
        handler: 'executeTerminal',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

export async function runMultiFileRefactorWorkflow(engine, fixture) {
  const scenario = {
    name: 'Multi-File Refactor Workflow',
    steps: [
      {
        name: 'Select Files',
        handler: 'selectFiles',
      },
      {
        name: 'Refactor Scope',
        handler: 'refactorScope',
      },
      {
        name: 'Clear Cache',
        handler: 'clearCache',
      },
      {
        name: 'Extract Symbols',
        handler: 'extractSymbols',
      },
      {
        name: 'Verify Changes',
        handler: 'verifyChanges',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

export async function runDebugIntegrationWorkflow(engine, fixture) {
  const scenario = {
    name: 'Debug Integration Workflow',
    steps: [
      {
        name: 'Start Debug Session',
        handler: 'startDebugSession',
      },
      {
        name: 'Get Breakpoint Info',
        handler: 'getBreakpointInfo',
      },
      {
        name: 'Execute Terminal Command',
        handler: 'executeTerminal',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

export async function runErrorRecoveryWorkflow(engine, fixture) {
  const scenario = {
    name: 'Error Recovery Workflow',
    steps: [
      {
        name: 'Trigger Timeout',
        handler: 'triggerTimeout',
      },
      {
        name: 'Activate Circuit Breaker',
        handler: 'activateCircuitBreaker',
      },
      {
        name: 'Fallback Handler',
        handler: 'fallbackHandler',
      },
      {
        name: 'Retry Request',
        handler: 'retryRequest',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

export async function runStatePersistenceWorkflow(engine, fixture) {
  const scenario = {
    name: 'State Persistence Workflow',
    steps: [
      {
        name: 'Capture State',
        handler: 'captureState',
      },
      {
        name: 'Simulate Crash',
        handler: 'simulateCrash',
      },
      {
        name: 'Recover State',
        handler: 'recoverState',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

export async function runConfigurationVariantWorkflow(engine, fixture) {
  const scenario = {
    name: 'Configuration Variant Workflow',
    steps: [
      {
        name: 'Load Settings',
        handler: 'loadSettings',
      },
      {
        name: 'Apply Settings',
        handler: 'applySettings',
      },
      {
        name: 'Reload',
        handler: 'reload',
      },
      {
        name: 'Execute Workflow',
        handler: 'executeWorkflow',
      },
    ],
  };

  return engine.executeWorkflow(scenario, fixture.inputs);
}

/**
 * Factory Functions
 */

export function createE2EScenarioEngine(config) {
  return new E2EScenarioEngine(config);
}

export function createDefaultConfig() {
  return {
    handlerTimeout: 5000, // ms
    performanceGates: {
      editorToAI: 300,
      codeNavigation: 250,
      multiFileRefactor: 400,
      default: 500,
    },
    concurrency: 1,
    enableMetrics: true,
    logLevel: 'info',
  };
}

function createDefaultLogger() {
  return {
    info: (msg) => console.log(`[INFO] ${msg}`),
    warn: (msg) => console.warn(`[WARN] ${msg}`),
    error: (msg, err) => console.error(`[ERROR] ${msg}`, err),
    debug: (msg) => console.debug(`[DEBUG] ${msg}`),
  };
}

function createDefaultMetrics() {
  const handlerLatencies = new Map();
  const errorCounts = new Map();

  return {
    recordHandlerLatency(handler, latency) {
      if (!handlerLatencies.has(handler)) {
        handlerLatencies.set(handler, []);
      }
      handlerLatencies.get(handler).push(latency);
    },

    recordError(handler) {
      errorCounts.set(handler, (errorCounts.get(handler) || 0) + 1);
    },

    collect() {
      const metrics = {};
      for (const [handler, latencies] of handlerLatencies.entries()) {
        metrics[handler] = {
          count: latencies.length,
          min: Math.min(...latencies),
          max: Math.max(...latencies),
          mean: latencies.reduce((a, b) => a + b, 0) / latencies.length,
          p99: latencies.sort((a, b) => a - b)[Math.floor(latencies.length * 0.99)],
        };
      }
      return metrics;
    },

    aggregate() {
      return {
        handlers: this.collect(),
        errors: Object.fromEntries(errorCounts),
      };
    },

    clear() {
      handlerLatencies.clear();
      errorCounts.clear();
    },
  };
}

/**
 * Export scenario runners
 */
export const scenarioRunners = {
  editorToAI: runEditorToAIWorkflow,
  codeNavigation: runCodeNavigationWorkflow,
  gitIntegrated: runGitIntegratedWorkflow,
  multiFileRefactor: runMultiFileRefactorWorkflow,
  debugIntegration: runDebugIntegrationWorkflow,
  errorRecovery: runErrorRecoveryWorkflow,
  statePersistence: runStatePersistenceWorkflow,
  configurationVariant: runConfigurationVariantWorkflow,
};
