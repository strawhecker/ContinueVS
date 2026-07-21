/**
 * Bridge Lifecycle Integration for State Persistence (Step 105)
 * 
 * This module provides integration hooks for Step 45 (Bridge Lifecycle Manager)
 * to enable state persistence on graceful shutdown.
 * 
 * Usage (in Step 45 or bootstrap):
 *  import { setupBridgeStatePersistenceHooks } from './bridge-lifecycle-integration.mjs';
 *  
 *  const lifecycle = createBridgeLifecycleManager(...);
 *  setupBridgeStatePersistenceHooks(lifecycle, logger, metrics);
 */

import { createBridgeStatePersistence } from './bridge-state-persistence.mjs';

/**
 * Setup state persistence hooks in the bridge lifecycle manager.
 * Registers shutdown handler to persist state on graceful termination.
 * 
 * @param {object} lifeCycleManager - Step 45 BridgeLifecycleManager instance
 * @param {IBridgeLogger} logger - optional logger instance
 * @param {IBridgeTelemetryCollector} metrics - optional metrics instance
 * @returns {object} persistence instance for testing/inspection
 */
export function setupBridgeStatePersistenceHooks(lifeCycleManager, logger = null, metrics = null) {
  if (!lifeCycleManager) {
    throw new Error('lifeCycleManager is required');
  }

  const persistence = createBridgeStatePersistence({
    logger,
    metrics
  });

  /**
   * Register handler that persists state on graceful shutdown.
   * This is called by BridgeLifecycleManager before terminating the process.
   */
  const onGracefulShutdown = async () => {
    try {
      logger?.log?.('info', '[BridgeLifecycleIntegration] Creating state checkpoint before shutdown...');

      // TODO: Get snapshot from bridge context (Step 45 should provide this)
      // For now, this is a placeholder that Step 45 will integrate with
      const checkpoint = await captureCurrentState(lifeCycleManager);

      if (checkpoint) {
        const result = await persistence.saveAsync(checkpoint);
        if (result) {
          logger?.log?.('info', '[BridgeLifecycleIntegration] State checkpoint saved successfully');
          metrics?.record?.('bridge.lifecycle.shutdown.state_saved', 1);
        } else {
          logger?.log?.('warn', '[BridgeLifecycleIntegration] Failed to save state checkpoint');
          metrics?.record?.('bridge.lifecycle.shutdown.state_save_failed', 1);
        }
      }
    } catch (error) {
      logger?.log?.('error', `[BridgeLifecycleIntegration] Error saving state on shutdown: ${error.message}`);
      metrics?.record?.('bridge.lifecycle.shutdown.error', 1);
      // Don't throw - shutdown should proceed regardless of persistence failure
    }
  };

  /**
   * Register handler that recovers state on startup.
   * Called after bridge initialization, before handler registration.
   */
  const onStartup = async () => {
    try {
      logger?.log?.('info', '[BridgeLifecycleIntegration] Attempting to recover state from checkpoint...');

      const checkpoint = await persistence.loadAsync();
      if (checkpoint) {
        logger?.log?.('info', `[BridgeLifecycleIntegration] State checkpoint recovered (phase: ${checkpoint.phase})`);
        metrics?.record?.('bridge.lifecycle.startup.state_recovered', 1);

        // TODO: Validate checkpoint against current handler registry
        // Store in lifeCycleManager or context for handler initialization

        return checkpoint;
      } else {
        logger?.log?.('debug', '[BridgeLifecycleIntegration] No valid state checkpoint found, starting fresh');
        metrics?.record?.('bridge.lifecycle.startup.state_not_found', 1);
      }
    } catch (error) {
      logger?.log?.('error', `[BridgeLifecycleIntegration] Error recovering state: ${error.message}`);
      metrics?.record?.('bridge.lifecycle.startup.error', 1);
      // Don't throw - startup should proceed with clean state if recovery fails
    }
    return null;
  };

  /**
   * Register shutdown and startup hooks with lifecycle manager.
   * Step 45 should expose methods like:
   *  - lifeCycleManager.onGracefulShutdown(handler)
   *  - lifeCycleManager.onStartup(handler)
   */
  if (typeof lifeCycleManager.onGracefulShutdown === 'function') {
    lifeCycleManager.onGracefulShutdown(onGracefulShutdown);
  }

  if (typeof lifeCycleManager.onStartup === 'function') {
    lifeCycleManager.onStartup(onStartup);
  }

  return persistence;
}

/**
 * Capture current bridge state snapshot.
 * TODO: This should be implemented in Step 45 and exposed via lifeCycleManager.
 * 
 * @param {object} lifeCycleManager
 * @returns {Promise<BridgeStateCheckpoint|null>}
 */
async function captureCurrentState(lifeCycleManager) {
  // Placeholder - Step 45 should provide a way to capture current state
  // This could be:
  //  - lifeCycleManager.captureState()
  //  - A state collector passed to lifeCycleManager
  //  - Access to handler registry and context

  if (typeof lifeCycleManager.captureState === 'function') {
    return await lifeCycleManager.captureState();
  }

  return null;
}

/**
 * Export for testing - allows tests to inspect hooks.
 */
export { createBridgeStatePersistence };
