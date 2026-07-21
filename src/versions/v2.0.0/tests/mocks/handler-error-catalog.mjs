/**
 * HANDLER-ERROR-CATALOG.mjs
 * 
 * Programmatic error index for automated diagnosis and test failure categorization.
 * Indexed by error code, message pattern, handler, and severity.
 * 
 * Used by:
 * - Troubleshooting guide (TROUBLESHOOTING-GUIDE.md) for symptom lookup
 * - Test failure categorization in Step 97–99 test runs
 * - Automated error reporting and escalation
 * 
 * Related Steps: 97–99 (compliance, performance, stress), 112 (regression gates)
 */

// ============================================================================
// COMPLIANCE ERRORS (from Step 97)
// ============================================================================

export const COMPLIANCE_ERRORS = {
  HANDLER_NOT_REGISTERED: {
    code: -32601,
    message: "Handler not found in dispatcher registry",
    rootCause: "Step 71 handler registration missing or handler disabled",
    remediation: "Verify handler entry in handler-registry.mjs; re-register if needed",
    affectedHandlers: ["all"],
    relatedSteps: [71],
    severity: "HIGH",
    category: "COMPLIANCE"
  },

  INVALID_REQUEST_ENVELOPE: {
    code: -32600,
    message: "Invalid Request: missing required field 'messageType'",
    rootCause: "Message envelope missing required field (messageId, messageType, or data)",
    remediation: "Validate message structure; check Step 73 validation rules; ensure all fields present",
    affectedHandlers: ["all"],
    relatedSteps: [73],
    severity: "HIGH",
    category: "COMPLIANCE"
  },

  INVALID_RESPONSE_SCHEMA: {
    code: -32603,
    message: "Internal Error: Response doesn't match JSON-RPC schema",
    rootCause: "Handler not wrapping response correctly (missing success field or improper error object)",
    remediation: "Check success/error object structure (Step 63); verify response wraps { success: true/false, ... }",
    affectedHandlers: ["all"],
    relatedSteps: [63, 73],
    severity: "HIGH",
    category: "COMPLIANCE"
  },

  INVALID_PARAMS_TYPE_MISMATCH: {
    code: -32602,
    message: "Invalid Params: 'timeout' must be positive integer, got 'abc'",
    rootCause: "Request parameter type mismatch or out-of-range (from Step 73 validation)",
    remediation: "Validate params against Step 95/104 schema; verify type and range; check allowed values",
    affectedHandlers: ["all"],
    relatedSteps: [73, 95, 104],
    severity: "MEDIUM",
    category: "COMPLIANCE"
  },

  INVALID_PARAMS_MISSING_REQUIRED: {
    code: -32602,
    message: "Invalid Params: missing required field 'filePath'",
    rootCause: "Request missing required parameter (from Step 73 validation)",
    remediation: "Check handler API contract (Step 71 registry); provide all required params",
    affectedHandlers: ["all"],
    relatedSteps: [71, 73],
    severity: "MEDIUM",
    category: "COMPLIANCE"
  },

  HANDLER_EXECUTION_TIMEOUT: {
    code: -32603,
    message: "Internal Error: Handler execution timeout after 10000ms",
    rootCause: "Handler timeout policy (Step 64) exceeded (timeout too short or handler slow)",
    remediation: "Increase timeout policy (Step 64) for handler tier; verify with Step 98 baseline",
    affectedHandlers: ["all"],
    relatedSteps: [64, 98],
    severity: "MEDIUM",
    category: "COMPLIANCE"
  },

  HANDLER_EXECUTION_ERROR: {
    code: -32603,
    message: "Internal Error: TypeError: cannot read property 'name' of undefined",
    rootCause: "Handler code throws exception (logic error, null reference, invalid operation)",
    remediation: "Check handler code; verify dependencies available (Step 104 config, Step 105 state); fix logic error",
    affectedHandlers: ["all"],
    relatedSteps: [76, 77, 78, 79, 81, 82, 83, 84, 85, 86, 87],
    severity: "HIGH",
    category: "COMPLIANCE"
  },

  HANDLER_MISSING_DEPENDENCY: {
    code: -32603,
    message: "Internal Error: Handler dependency not available (config file missing)",
    rootCause: "Handler missing required dependency: Step 104 config, Step 105 state, external service",
    remediation: "Create config file (Step 104), verify state persistence (Step 105), check external service availability",
    affectedHandlers: ["all"],
    relatedSteps: [104, 105],
    severity: "HIGH",
    category: "COMPLIANCE"
  },

  MESSAGE_QUEUE_FULL: {
    code: -32603,
    message: "Internal Error: Message queue full; cannot enqueue request",
    rootCause: "Priority queue (Step 65) saturated; too many concurrent requests",
    remediation: "Increase Step 65 priority queue size; reduce concurrent load; check for queue blocking",
    affectedHandlers: ["all"],
    relatedSteps: [65, 99],
    severity: "MEDIUM",
    category: "COMPLIANCE"
  },

  CASCADING_FAILURE: {
    code: -32603,
    message: "Internal Error: Handler failure cascaded from upstream handler",
    rootCause: "Shared resource corrupted (cache, state file, message queue) or handler isolation failed (Step 74)",
    remediation: "Restart bridge (clears queue); reset state file; verify Step 74 error recovery isolation",
    affectedHandlers: ["all"],
    relatedSteps: [74, 103, 105],
    severity: "HIGH",
    category: "COMPLIANCE"
  }
};

// ============================================================================
// PERFORMANCE ERRORS (from Step 98–99)
// ============================================================================

export const PERFORMANCE_ERRORS = {
  P99_LATENCY_EXCEEDED_FAST: {
    code: null, // Metric error, not RPC error
    message: "Handler p99 latency > fast tier baseline (>2000ms)",
    rootCause: "Handler slow; timeout policy mismatch; concurrent load; middleware overhead",
    remediation: "Profile with Step 96; increase timeout (Step 64); clear cache (Step 94); reduce concurrency",
    thresholds: {
      baselineFast: 2000,
      criticalFast: 3000,  // >50% regression
      highFast: 2500,      // >25% regression
      mediumFast: 2300     // >15% regression
    },
    relatedSteps: [96, 98, 112],
    severity: "HIGH",
    category: "PERFORMANCE"
  },

  P99_LATENCY_EXCEEDED_MEDIUM: {
    code: null,
    message: "Handler p99 latency > medium tier baseline (>10000ms)",
    rootCause: "Handler slow; timeout policy mismatch; concurrent load; middleware overhead",
    remediation: "Profile with Step 96; increase timeout (Step 64); clear cache (Step 94); reduce concurrency",
    thresholds: {
      baselineMedium: 10000,
      criticalMedium: 15000, // >50% regression
      highMedium: 12500,     // >25% regression
      mediumMedium: 11500    // >15% regression
    },
    relatedSteps: [96, 98, 112],
    severity: "MEDIUM",
    category: "PERFORMANCE"
  },

  P99_LATENCY_EXCEEDED_SLOW: {
    code: null,
    message: "Handler p99 latency > slow tier baseline (>30000ms)",
    rootCause: "Handler slow; external service slow; timeout policy mismatch",
    remediation: "Profile with Step 96; check external service; increase timeout (Step 64)",
    thresholds: {
      baselineSlow: 30000,
      criticalSlow: 45000,  // >50% regression
      highSlow: 37500,      // >25% regression
      mediumSlow: 34500     // >15% regression
    },
    relatedSteps: [96, 98, 112],
    severity: "MEDIUM",
    category: "PERFORMANCE"
  },

  MEMORY_SPIKE: {
    code: null,
    message: "Memory consumption increased >20MB from baseline",
    rootCause: "Potential memory leak: circular refs, uncleaned event listeners, unbounded cache",
    remediation: "Run Step 99 sustained load; attach Node.js memory profiler; review handler lifecycle",
    gates: "Step 99 memory gate: peak <50MB, avg delta <10KB/30s",
    thresholds: {
      baselineMemory: 30,     // 30 MB
      criticalMemory: 60,     // >50MB increase → critical
      highMemory: 50,         // >20MB increase → high
      mediumMemory: 40        // >10MB increase → medium
    },
    relatedSteps: [99, 105],
    severity: "MEDIUM",
    category: "PERFORMANCE"
  },

  THROUGHPUT_BELOW_BASELINE: {
    code: null,
    message: "Throughput below baseline (320 msg/sec) by >20%",
    rootCause: "Message queue saturation (Step 65), middleware overhead (Steps 72–74), validation bottleneck",
    remediation: "Increase queue size (Step 65); reduce logging (Step 72); profile middleware (Steps 72–74)",
    thresholds: {
      baselineThroughput: 320,
      criticalThroughput: 160,  // >50% drop
      highThroughput: 256,      // >20% drop
      mediumThroughput: 288     // >10% drop
    },
    relatedSteps: [98, 99],
    severity: "MEDIUM",
    category: "PERFORMANCE"
  },

  ERROR_RATE_SPIKE: {
    code: null,
    message: "Unintended error rate increased >1% from baseline",
    rootCause: "Validation failures (Step 73), timeout enforcement (Step 64), handler crashes",
    remediation: "Run Step 99 error injection baseline; identify error source; check handler compliance",
    gates: "Step 99 error injection gate: unintended errors <1% absolute increase",
    thresholds: {
      baselineErrorRate: 0.1,   // 0.1% baseline
      criticalErrorRate: 10.1,  // >10% abs increase
      highErrorRate: 5.1,       // >5% abs increase
      mediumErrorRate: 2.1      // >2% abs increase
    },
    relatedSteps: [97, 99],
    severity: "HIGH",
    category: "PERFORMANCE"
  }
};

// ============================================================================
// STRESS ERRORS (from Step 99)
// ============================================================================

export const STRESS_ERRORS = {
  TIMEOUT_DURING_LOAD: {
    code: -32603,
    message: "RPC timeout under concurrent load (100 parallel requests)",
    rootCause: "Handler slow under load; cascading requests; timeout policy too short",
    remediation: "Reduce concurrency (Step 99); increase timeout (Step 64); verify isolation (Step 74)",
    gates: "Step 99 concurrent gate: p99 <500ms @100 parallel requests",
    relatedSteps: [64, 99],
    severity: "HIGH",
    category: "STRESS"
  },

  CASCADING_FAILURE_UNDER_LOAD: {
    code: -32603,
    message: "One handler failure cascades to others under concurrent load",
    rootCause: "Shared resource corruption (cache, state, queue) or isolation failure (Step 74)",
    remediation: "Check Step 74 error recovery isolation; verify handler state cleanup; restart bridge",
    gates: "Step 99 isolation gate: >80% handler isolation (single failure doesn't cascade)",
    relatedSteps: [74, 99],
    severity: "HIGH",
    category: "STRESS"
  },

  QUEUE_SATURATION: {
    code: -32603,
    message: "Message queue full; cannot enqueue new requests",
    rootCause: "Priority queue (Step 65) size too small for concurrent load",
    remediation: "Increase Step 65 queue size; reduce concurrency; check for queue blocking handler",
    gates: "Step 99 queue gate: <1% requests dropped due to queue full",
    relatedSteps: [65, 99],
    severity: "MEDIUM",
    category: "STRESS"
  },

  MEMORY_EXHAUSTION: {
    code: null,
    message: "Memory exhaustion during sustained load test",
    rootCause: "Memory leak or unbounded growth triggered by concurrent requests",
    remediation: "Run Step 99 sustained load profile; identify leaking handler; check handler lifecycle",
    gates: "Step 99 memory gate: <10KB avg delta per 30s; peak <50MB",
    relatedSteps: [99, 105],
    severity: "CRITICAL",
    category: "STRESS"
  },

  HANDLER_CRASH_DURING_LOAD: {
    code: -32603,
    message: "Handler crashes during concurrent load test",
    rootCause: "Handler logic error triggered by concurrent access or state race condition",
    remediation: "Review handler code for race conditions; add synchronization; increase unit test concurrency",
    gates: "Step 99 crash gate: 0 handler crashes during 5 min sustained load",
    relatedSteps: [99, 103],
    severity: "CRITICAL",
    category: "STRESS"
  },

  RECOVERY_FAILURE: {
    code: -32603,
    message: "Bridge fails to recover from handler failure (crash recovery limit exceeded)",
    rootCause: "Crash recovery exponential backoff (Step 103) hit max retries; persistent crash trigger",
    remediation: "Identify crash root cause from diagnostics; fix handler; reset recovery state; restart",
    gates: "Step 99 recovery gate: bridge recovers from handler failures; max 5 consecutive crashes",
    relatedSteps: [99, 103],
    severity: "CRITICAL",
    category: "STRESS"
  }
};

// ============================================================================
// CONFIGURATION ERRORS (from Steps 104–105)
// ============================================================================

export const CONFIGURATION_ERRORS = {
  CONFIG_FILE_NOT_FOUND: {
    code: -32603,
    message: "Configuration file not found: ~/.continue/config.json",
    rootCause: "First run (file never created) or file deleted unexpectedly",
    remediation: "Create config with bridge:applySettings handler (Step 95); user provides model/API config",
    location: "~/.continue/config.json",
    relatedSteps: [95, 104],
    severity: "MEDIUM",
    category: "CONFIGURATION"
  },

  CONFIG_INVALID_JSON: {
    code: -32603,
    message: "Configuration file invalid JSON: SyntaxError at line 15, column 3",
    rootCause: "Config file corrupted or manually edited with syntax error",
    remediation: "Validate JSON (`jq . ~/.continue/config.json`); fix syntax; recreate if corrupted",
    location: "~/.continue/config.json",
    relatedSteps: [104],
    severity: "HIGH",
    category: "CONFIGURATION"
  },

  CONFIG_SCHEMA_VALIDATION_FAILED: {
    code: -32603,
    message: "Configuration schema validation failed: missing required field 'models'",
    rootCause: "Config file missing required field or wrong field type",
    remediation: "Add missing field; verify field types match schema; consult Step 104 config schema",
    location: "~/.continue/config.json",
    relatedSteps: [104],
    severity: "HIGH",
    category: "CONFIGURATION"
  },

  STATE_FILE_CORRUPTED: {
    code: -32603,
    message: "State file corrupted: invalid checkpoint data",
    rootCause: "Abnormal termination (crash, SIGKILL) or disk corruption",
    remediation: "Delete ~/.continue/bridge-state.json; restart bridge (Step 105 auto-recovery)",
    location: "~/.continue/bridge-state.json",
    relatedSteps: [105],
    severity: "MEDIUM",
    category: "CONFIGURATION"
  },

  STATE_FILE_NOT_WRITABLE: {
    code: -32603,
    message: "State persistence failed: Permission denied writing ~/.continue/bridge-state.json",
    rootCause: "File permissions issue (file not writable by bridge process)",
    remediation: "Fix permissions: `chmod 755 ~/.continue/`; `chmod 644 ~/.continue/bridge-state.json`",
    location: "~/.continue/bridge-state.json",
    relatedSteps: [105],
    severity: "HIGH",
    category: "CONFIGURATION"
  },

  DISK_FULL: {
    code: -32603,
    message: "Disk full: cannot write state file",
    rootCause: "Home directory out of disk space",
    remediation: "Free disk space; delete stale files (crash diagnostics); retry bridge restart",
    location: "~/.continue/",
    relatedSteps: [105],
    severity: "MEDIUM",
    category: "CONFIGURATION"
  }
};

// ============================================================================
// CRASH RECOVERY ERRORS (from Step 103)
// ============================================================================

export const CRASH_RECOVERY_ERRORS = {
  CRASH_DETECTED: {
    code: null,
    message: "Bridge crash detected; initiating recovery (attempt 1/5)",
    rootCause: "Handler crash, unhandled promise rejection, or process exit",
    remediation: "Review crash diagnostics; identify crashing handler; fix code; restart",
    location: "~/.continue/crash-diagnostics/",
    relatedSteps: [103],
    severity: "HIGH",
    category: "CRASH_RECOVERY"
  },

  AUTO_RESTART_LOOP: {
    code: null,
    message: "Bridge auto-restart loop detected; crash recovery limit exceeded (5 attempts)",
    rootCause: "Persistent crash trigger; handler keeps crashing after restart",
    remediation: "Disable problematic handler (Step 71); identify crash root; fix code; restart bridge",
    location: "~/.continue/crash-recovery.json",
    relatedSteps: [71, 103],
    severity: "CRITICAL",
    category: "CRASH_RECOVERY"
  },

  DEGRADED_MODE_ACTIVATED: {
    code: null,
    message: "Bridge entering degraded mode; disabling problematic handlers",
    rootCause: "2+ consecutive crashes; bridge auto-disabling failing handlers for safety",
    remediation: "Resolve crash root cause; delete ~/.continue/crash-recovery.json; restart bridge",
    location: "~/.continue/crash-recovery.json",
    relatedSteps: [103, 105],
    severity: "MEDIUM",
    category: "CRASH_RECOVERY"
  },

  GRACEFUL_SHUTDOWN_TIMEOUT: {
    code: null,
    message: "Graceful shutdown timeout exceeded (10s); force killing bridge process",
    rootCause: "Handler not responding to cancellation signal; long-running operation not cancellable",
    remediation: "Review handler lifecycle; add cancellation support; increase shutdown timeout if needed",
    relatedSteps: [103],
    severity: "MEDIUM",
    category: "CRASH_RECOVERY"
  }
};

// ============================================================================
// INTEGRATION ERRORS (from Steps 45–75)
// ============================================================================

export const INTEGRATION_ERRORS = {
  NPM_PACKAGE_MISSING: {
    code: -32603,
    message: "npm package not found: ~/.cache/npm-packages/v2.0.0/",
    rootCause: "Package not downloaded (Step 35) or cache cleared",
    remediation: "Re-download packages (Step 35); verify checksums (Step 37); run Step 12 validation",
    relatedSteps: [12, 35, 37],
    severity: "CRITICAL",
    category: "INTEGRATION"
  },

  NPM_CHECKSUM_MISMATCH: {
    code: -32603,
    message: "npm package checksum mismatch (expected abc123, got def456)",
    rootCause: "Package download corrupted or altered",
    remediation: "Re-download packages (Step 35); verify checksums (Step 37); check network connection",
    relatedSteps: [12, 35, 37],
    severity: "CRITICAL",
    category: "INTEGRATION"
  },

  CORE_SERVER_INITIALIZATION_FAILED: {
    code: -32603,
    message: "core-server.js initialization failed: SyntaxError in require",
    rootCause: "Node.js entry point (Step 13) has syntax error or missing dependency",
    remediation: "Verify core-server.js file exists; check syntax; verify npm install (Step 7)",
    relatedSteps: [13],
    severity: "CRITICAL",
    category: "INTEGRATION"
  },

  HANDLER_DISPATCHER_FAILED: {
    code: -32603,
    message: "Handler dispatcher initialization failed: handler-registry.mjs load error",
    rootCause: "Step 14 handler dispatcher or Step 71 handler registry has error",
    remediation: "Verify handler-registry.mjs syntax; check Step 71 registration; restart bridge",
    relatedSteps: [14, 71],
    severity: "CRITICAL",
    category: "INTEGRATION"
  },

  WEBVIEW_BOOTSTRAP_FAILED: {
    code: -32603,
    message: "WebView bootstrap failed: injector did not receive bootstrap message",
    rootCause: "Step 43 injector failed or Step 46 bootstrap handler not invoked",
    remediation: "Reload Continue sidebar in IDE; restart bridge; check Step 43/46 implementation",
    relatedSteps: [43, 46],
    severity: "HIGH",
    category: "INTEGRATION"
  },

  MESSAGE_ROUTING_FAILED: {
    code: -32603,
    message: "Message routing failed: handler not reached after middleware processing",
    rootCause: "Step 47 message routing middleware dropped message or Step 71 registration missing",
    remediation: "Verify handler registered (Step 71); enable Step 72 logging; trace message flow",
    relatedSteps: [47, 71, 72],
    severity: "HIGH",
    category: "INTEGRATION"
  }
};

// ============================================================================
// LOOKUP FUNCTIONS
// ============================================================================

/**
 * Find error by JSON-RPC error code
 * @param {number} code - JSON-RPC error code (e.g., -32600, -32603)
 * @returns {Object|null} - Error object or null if not found
 */
export function findErrorByCode(code) {
  const allErrors = {
    ...COMPLIANCE_ERRORS,
    ...PERFORMANCE_ERRORS,
    ...STRESS_ERRORS,
    ...CONFIGURATION_ERRORS,
    ...CRASH_RECOVERY_ERRORS,
    ...INTEGRATION_ERRORS
  };

  for (const [key, error] of Object.entries(allErrors)) {
    if (error.code === code) {
      return { key, ...error };
    }
  }
  return null;
}

/**
 * Find errors by message pattern (substring match)
 * @param {string} message - Error message to search for
 * @returns {Object[]} - Array of matching error objects
 */
export function findErrorByMessage(message) {
  const allErrors = {
    ...COMPLIANCE_ERRORS,
    ...PERFORMANCE_ERRORS,
    ...STRESS_ERRORS,
    ...CONFIGURATION_ERRORS,
    ...CRASH_RECOVERY_ERRORS,
    ...INTEGRATION_ERRORS
  };

  return Object.entries(allErrors)
    .filter(([key, error]) => error.message.toLowerCase().includes(message.toLowerCase()))
    .map(([key, error]) => ({ key, ...error }));
}

/**
 * Get all errors affecting a specific handler
 * @param {string} handlerName - Handler name (e.g., "refactor", "debug")
 * @returns {Object[]} - Array of errors affecting handler
 */
export function getHandlerErrors(handlerName) {
  const allErrors = {
    ...COMPLIANCE_ERRORS,
    ...PERFORMANCE_ERRORS,
    ...STRESS_ERRORS,
    ...CONFIGURATION_ERRORS,
    ...CRASH_RECOVERY_ERRORS,
    ...INTEGRATION_ERRORS
  };

  return Object.entries(allErrors)
    .filter(([key, error]) => 
      error.affectedHandlers && 
      (error.affectedHandlers.includes("all") || error.affectedHandlers.includes(handlerName))
    )
    .map(([key, error]) => ({ key, ...error }));
}

/**
 * Get all errors in a specific category
 * @param {string} category - Error category (e.g., "COMPLIANCE", "PERFORMANCE", "STRESS")
 * @returns {Object[]} - Array of errors in category
 */
export function getCategoryErrors(category) {
  let categoryObj = {};

  switch (category.toUpperCase()) {
    case "COMPLIANCE":
      categoryObj = COMPLIANCE_ERRORS;
      break;
    case "PERFORMANCE":
      categoryObj = PERFORMANCE_ERRORS;
      break;
    case "STRESS":
      categoryObj = STRESS_ERRORS;
      break;
    case "CONFIGURATION":
      categoryObj = CONFIGURATION_ERRORS;
      break;
    case "CRASH_RECOVERY":
      categoryObj = CRASH_RECOVERY_ERRORS;
      break;
    case "INTEGRATION":
      categoryObj = INTEGRATION_ERRORS;
      break;
    default:
      return [];
  }

  return Object.entries(categoryObj)
    .map(([key, error]) => ({ key, ...error }));
}

/**
 * Generate a diagnostic report from a list of errors
 * @param {Object[]} errors - Array of error objects
 * @returns {string} - Formatted diagnostic report
 */
export function generateDiagnosticReport(errors) {
  if (!errors || errors.length === 0) {
    return "No errors to report.";
  }

  let report = `Diagnostic Report\n`;
  report += `==================\n`;
  report += `Total Errors: ${errors.length}\n\n`;

  // Group by category
  const byCategory = {};
  errors.forEach(err => {
    const cat = err.category || "UNKNOWN";
    if (!byCategory[cat]) byCategory[cat] = [];
    byCategory[cat].push(err);
  });

  // Report by category
  for (const [category, categoryErrors] of Object.entries(byCategory)) {
    report += `${category} (${categoryErrors.length} errors)\n`;
    report += `${"=".repeat(category.length + categoryErrors.length + 10)}\n`;

    categoryErrors.forEach(err => {
      report += `\n[${err.severity}] ${err.key}\n`;
      report += `Message: ${err.message}\n`;
      report += `Root Cause: ${err.rootCause}\n`;
      report += `Remediation: ${err.remediation}\n`;

      if (err.relatedSteps && err.relatedSteps.length > 0) {
        report += `Related Steps: ${err.relatedSteps.join(", ")}\n`;
      }
    });

    report += "\n";
  }

  // Severity summary
  report += `\nSeverity Summary\n`;
  report += `================\n`;
  const bySeverity = {};
  errors.forEach(err => {
    const sev = err.severity || "UNKNOWN";
    bySeverity[sev] = (bySeverity[sev] || 0) + 1;
  });

  for (const [severity, count] of Object.entries(bySeverity)) {
    report += `${severity}: ${count}\n`;
  }

  return report;
}

/**
 * Get all errors related to a specific step
 * @param {number} stepNumber - Step number (e.g., 71, 112)
 * @returns {Object[]} - Array of errors related to step
 */
export function getStepErrors(stepNumber) {
  const allErrors = {
    ...COMPLIANCE_ERRORS,
    ...PERFORMANCE_ERRORS,
    ...STRESS_ERRORS,
    ...CONFIGURATION_ERRORS,
    ...CRASH_RECOVERY_ERRORS,
    ...INTEGRATION_ERRORS
  };

  return Object.entries(allErrors)
    .filter(([key, error]) => 
      error.relatedSteps && error.relatedSteps.includes(stepNumber)
    )
    .map(([key, error]) => ({ key, ...error }));
}

/**
 * Get all errors by severity level
 * @param {string} severity - Severity level ("CRITICAL", "HIGH", "MEDIUM", "LOW")
 * @returns {Object[]} - Array of errors with given severity
 */
export function getErrorsBySeverity(severity) {
  const allErrors = {
    ...COMPLIANCE_ERRORS,
    ...PERFORMANCE_ERRORS,
    ...STRESS_ERRORS,
    ...CONFIGURATION_ERRORS,
    ...CRASH_RECOVERY_ERRORS,
    ...INTEGRATION_ERRORS
  };

  return Object.entries(allErrors)
    .filter(([key, error]) => error.severity === severity.toUpperCase())
    .map(([key, error]) => ({ key, ...error }));
}

export default {
  COMPLIANCE_ERRORS,
  PERFORMANCE_ERRORS,
  STRESS_ERRORS,
  CONFIGURATION_ERRORS,
  CRASH_RECOVERY_ERRORS,
  INTEGRATION_ERRORS,
  findErrorByCode,
  findErrorByMessage,
  getHandlerErrors,
  getCategoryErrors,
  generateDiagnosticReport,
  getStepErrors,
  getErrorsBySeverity
};
