/**
 * Bridge Bootstrap State Recovery (Step 105 + Step 46)
 * 
 * Integrates with webview bootstrap handler (Step 46) to recover
 * bridge state from checkpoint file on startup.
 * 
 * Usage (early in core-server.js, before handlers are registered):
 *  import { attemptStateRecovery } from './bootstrap-state-recovery.mjs';
 *  
 *  const recoveredState = await attemptStateRecovery(logger, metrics);
 *  // Pass recoveredState to handler initialization for subscription replay
 */

import { createBridgeStatePersistence } from './bridge-state-persistence.mjs';

/**
 * Attempt to recover bridge state from checkpoint file during bootstrap.
 * This is called early, before handler initialization.
 * 
 * Results:
 * - State recovered: returns checkpoint with recovered phase, handler statuses, subscription counts
 * - State not found: returns null (start with clean state)
 * - State corrupted/stale: returns null (start with clean state)
 * 
 * @param {IBridgeLogger} logger - optional logger
 * @param {IBridgeTelemetryCollector} metrics - optional metrics
 * @returns {Promise<BridgeStateCheckpoint|null>} recovered state or null
 */
export async function attemptStateRecovery(logger = null, metrics = null) {
  const persistence = createBridgeStatePersistence({ logger, metrics });

  try {
    logger?.log?.('info', '[BootstrapStateRecovery] Attempting to recover bridge state...');

    const checkpoint = await persistence.loadAsync();

    if (!checkpoint) {
      logger?.log?.('debug', '[BootstrapStateRecovery] No valid checkpoint found, starting fresh');
      metrics?.record?.('bootstrap.state_recovery.not_found', 1);
      return null;
    }

    logger?.log?.('info', `[BootstrapStateRecovery] State checkpoint recovered (phase: ${checkpoint.phase}, handlers: ${Object.keys(checkpoint.handlers).length})`);
    metrics?.record?.('bootstrap.state_recovery.success', 1);

    // TODO: Validate recovered state
    // - Check that handler names in checkpoint match current handler registry
    // - Discard checkpoint if handlers have changed significantly
    // - If validation fails, return null and start fresh

    return checkpoint;
  } catch (error) {
    logger?.log?.('error', `[BootstrapStateRecovery] Error recovering state: ${error.message}`);
    metrics?.record?.('bootstrap.state_recovery.error', 1);
    return null; // Start fresh on error
  }
}

/**
 * Validate recovered checkpoint against current handler registry.
 * Ensures recovered state is consistent with current bridge capabilities.
 * 
 * @param {BridgeStateCheckpoint} checkpoint - recovered checkpoint
 * @param {string[]} currentHandlerNames - names of handlers now available
 * @param {IBridgeLogger} logger - optional logger
 * @returns {boolean} true if checkpoint is valid and safe to use
 */
export function validateRecoveredState(checkpoint, currentHandlerNames = [], logger = null) {
  if (!checkpoint) {
    return false;
  }

  try {
    // Basic validation
    if (!checkpoint.validate()) {
      logger?.log?.('warn', '[BootstrapStateRecovery] Checkpoint failed validation');
      return false;
    }

    // Check if handlers in checkpoint exist in current registry
    if (currentHandlerNames.length > 0) {
      const currentSet = new Set(currentHandlerNames);
      const recoveredHandlers = Object.keys(checkpoint.handlers);

      for (const handler of recoveredHandlers) {
        if (!currentSet.has(handler)) {
          logger?.log?.('warn', `[BootstrapStateRecovery] Handler ${handler} in checkpoint not in current registry`);
          return false; // Checkpoint references unknown handlers
        }
      }
    }

    logger?.log?.('debug', '[BootstrapStateRecovery] Checkpoint validation passed');
    return true;
  } catch (error) {
    logger?.log?.('error', `[BootstrapStateRecovery] Validation error: ${error.message}`);
    return false;
  }
}

/**
 * Replay subscriptions from recovered checkpoint.
 * Called during handler initialization to restore subscriptions that were active before shutdown.
 * 
 * @param {BridgeStateCheckpoint} checkpoint - recovered checkpoint
 * @param {IBridgeHandlerRegistry} handlerRegistry - Step 66 handler registry
 * @param {IBridgeLogger} logger - optional logger
 * @returns {Promise<number>} count of subscriptions replayed
 */
export async function replaySubscriptionsFromCheckpoint(checkpoint, handlerRegistry, logger = null) {
  if (!checkpoint || !handlerRegistry) {
    return 0;
  }

  try {
    const subscriptionCount = checkpoint.subscriptions?.count || 0;
    logger?.log?.('info', `[BootstrapStateRecovery] Replaying ${subscriptionCount} subscriptions from checkpoint...`);

    // TODO: Iterate through handlers and re-establish subscriptions based on checkpoint
    // This would involve:
    //  1. For each handler in checkpoint.handlers with status 'active'
    //  2. Call handler.restoreSubscription() or similar if available
    //  3. Count successful restorations

    // For now, this is a placeholder - actual replay logic depends on handler interface
    return subscriptionCount;
  } catch (error) {
    logger?.log?.('error', `[BootstrapStateRecovery] Error replaying subscriptions: ${error.message}`);
    return 0;
  }
}

/**
 * Async initialization hook for bootstrap.
 * Can be called as early as possible during server startup.
 * 
 * @param {object} config - bootstrap configuration
 * @param {IBridgeLogger} config.logger - optional logger
 * @param {IBridgeTelemetryCollector} config.metrics - optional metrics
 * @returns {Promise<object>} bootstrap context with recovered state
 */
export async function bootstrapWithStateRecovery(config = {}) {
  const { logger = null, metrics = null } = config;

  const startTime = Date.now();

  try {
    const recoveredState = await attemptStateRecovery(logger, metrics);
    const recoveryTime = Date.now() - startTime;

    logger?.log?.('info', `[BootstrapStateRecovery] Bootstrap recovery completed in ${recoveryTime}ms`);
    metrics?.record?.('bootstrap.recovery_time_ms', recoveryTime);

    return {
      success: true,
      recoveredState,
      recoveryTimeMs: recoveryTime
    };
  } catch (error) {
    const recoveryTime = Date.now() - startTime;
    logger?.log?.('error', `[BootstrapStateRecovery] Bootstrap recovery failed: ${error.message}`);
    metrics?.record?.('bootstrap.recovery_error', 1);

    return {
      success: false,
      recoveredState: null,
      recoveryTimeMs: recoveryTime,
      error: error.message
    };
  }
}

export { createBridgeStatePersistence };
