#!/usr/bin/env node

/**
 * Verification script for Step 57: Find-References Handler
 */

import {
  createFindReferencesHandler,
  ReferenceValidationError,
  ReferenceError,
} from './lib/find-references-handler.mjs';

console.log('✓ Module imports successful\n');

// Test 1: Handler instantiation
console.log('Test 1: Handler instantiation');
try {
  const mockExtractor = {
    extractSymbols: async () => ({
      success: true,
      data: {
        symbols: [
          {
            name: 'TestClass',
            kind: 'class',
            file: '/test.cs',
            line: 0,
            column: 0,
            endLine: 10,
            endColumn: 1,
            children: [],
          },
        ],
      },
    }),
  };

  const handler = createFindReferencesHandler({ symbolExtractor: mockExtractor });
  console.log('✓ Handler created successfully');
  console.log(`  Handler type: ${typeof handler}\n`);
} catch (e) {
  console.error(`✗ Handler instantiation failed: ${e.message}\n`);
  process.exit(1);
}

// Test 2: Basic message handling
console.log('Test 2: Basic message handling');
try {
  const mockExtractor = {
    extractSymbols: async () => ({
      success: true,
      data: { symbols: [] },
    }),
  };

  const handler = createFindReferencesHandler({ symbolExtractor: mockExtractor });
  const message = {
    data: {
      filepath: '/test.cs',
      line: 0,
      column: 0,
      searchScope: 'file',
    },
  };

  const result = await handler(message, {});
  console.log(`✓ Message handling successful`);
  console.log(`  Success: ${result.success}`);
  console.log(`  References: ${result.data.references.length}`);
  console.log(`  Total count: ${result.data.totalCount}\n`);
} catch (e) {
  console.error(`✗ Message handling failed: ${e.message}\n`);
  process.exit(1);
}

// Test 3: Validation error handling
console.log('Test 3: Validation error handling');
try {
  const mockExtractor = {
    extractSymbols: async () => ({
      success: true,
      data: { symbols: [] },
    }),
  };

  const handler = createFindReferencesHandler({ symbolExtractor: mockExtractor });
  const message = {
    data: {
      // Missing required filepath
      line: 0,
      column: 0,
    },
  };

  const result = await handler(message, {});
  if (!result.success && result.error.includes('Validation')) {
    console.log('✓ Validation error handling works correctly');
    console.log(`  Error: ${result.error}\n`);
  } else {
    console.error('✗ Validation error not detected\n');
    process.exit(1);
  }
} catch (e) {
  console.error(`✗ Validation test failed: ${e.message}\n`);
  process.exit(1);
}

// Test 4: Symbol extraction and reference finding
console.log('Test 4: Symbol extraction and reference finding');
try {
  const mockExtractor = {
    extractSymbols: async () => ({
      success: true,
      data: {
        symbols: [
          {
            name: 'MyMethod',
            kind: 'method',
            file: '/file.cs',
            line: 5,
            column: 2,
            endLine: 5,
            endColumn: 10,
            children: [],
          },
          {
            name: 'MyMethod',
            kind: 'reference',
            file: '/file.cs',
            line: 20,
            column: 4,
            endLine: 20,
            endColumn: 12,
            children: [],
          },
        ],
      },
    }),
  };

  const handler = createFindReferencesHandler({ symbolExtractor: mockExtractor });
  const message = {
    data: {
      filepath: '/file.cs',
      line: 5,
      column: 2,
      searchScope: 'file',
    },
  };

  const result = await handler(message, {});
  if (result.success && result.data.references.length > 0) {
    console.log('✓ Symbol extraction and reference finding works');
    console.log(`  Found ${result.data.references.length} references`);
    console.log(`  First reference: ${result.data.references[0].text} at ${result.data.references[0].file}:${result.data.references[0].line}\n`);
  } else {
    console.error('✗ No references found\n');
    process.exit(1);
  }
} catch (e) {
  console.error(`✗ Symbol extraction test failed: ${e.message}\n`);
  process.exit(1);
}

// Test 5: Error classes
console.log('Test 5: Error classes');
try {
  const validationErr = new ReferenceValidationError('filepath', 'must be non-empty');
  const refErr = new ReferenceError('Search failed', 'aggregation');

  if (
    validationErr instanceof Error &&
    validationErr.name === 'ReferenceValidationError' &&
    refErr instanceof Error &&
    refErr.name === 'ReferenceError'
  ) {
    console.log('✓ Error classes defined correctly');
    console.log(`  ReferenceValidationError: ${validationErr.message}`);
    console.log(`  ReferenceError: ${refErr.message}\n`);
  } else {
    console.error('✗ Error classes not properly defined\n');
    process.exit(1);
  }
} catch (e) {
  console.error(`✗ Error class test failed: ${e.message}\n`);
  process.exit(1);
}

console.log('═'.repeat(50));
console.log('✓ All Step 57 verification tests passed!');
console.log('═'.repeat(50));
process.exit(0);
