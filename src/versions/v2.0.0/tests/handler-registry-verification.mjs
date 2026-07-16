#!/usr/bin/env node

/**
 * Handler Registry Verification Script
 * Ensures all handlers are properly registered including Step 87
 */

import { 
  getAllHandlers, 
  getHandlerMetadata, 
  hasHandler 
} from '../lib/handler-registry.mjs';

console.log('=== Handler Registry Verification ===\n');

try {
  const allHandlers = getAllHandlers();
  console.log(`✓ Registry loaded: ${allHandlers.length} handlers registered\n`);

  // Check for Step 87 context-window handler
  const hasContextWindow = hasHandler('bridge:getContextWindow');
  console.log(`✓ bridge:getContextWindow registered: ${hasContextWindow}`);

  if (hasContextWindow) {
    const metadata = getHandlerMetadata('bridge:getContextWindow');
    console.log(`  - Factory: ${metadata.isFactory}`);
    console.log(`  - Timeout: ${metadata.timeoutPolicy}`);
    console.log(`  - Tier: ${metadata.stabilityTier}`);
    console.log(`  - Related Steps: ${metadata.relatedSteps.join(', ')}`);
    console.log(`  - Description: ${metadata.description}`);
  }

  // Verify existing handlers still present
  const expectedHandlers = [
    'bridge:bootstrap',
    'bridge:getEditorState',
    'bridge:search',
    'bridge:goToDefinition',
    'bridge:refactor',
    'bridge:getProjectInfo',
    'bridge:getSidebarState',
    'bridge:getContextWindow'
  ];

  console.log('\n✓ Checking existing handlers:');
  let allPresent = true;
  for (const handler of expectedHandlers) {
    const present = hasHandler(handler);
    console.log(`  ${present ? '✓' : '✗'} ${handler}`);
    if (!present) allPresent = false;
  }

  if (allPresent) {
    console.log('\n✅ All handlers properly registered - no regressions detected');
    process.exit(0);
  } else {
    console.log('\n❌ Some handlers missing - regression detected');
    process.exit(1);
  }
} catch (error) {
  console.error('\n❌ Registry verification failed:');
  console.error(`  ${error.message}`);
  process.exit(1);
}
