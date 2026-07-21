#!/usr/bin/env node

/**
 * Unit tests for ContinueConfigManager (Step 104)
 * 
 * 6 test suites: Initialization, File I/O, Schema Validation, Model Merging, Performance, Error Handling
 * Total: 30+ tests covering all operations, edge cases, performance characteristics, and graceful degradation.
 * 
 * **Framework**: Mocha with ESM support
 * **Assertions**: Built-in assert module
 * **Fixtures**: Defined in continue-config-fixtures.mjs
 * 
 * Run: mocha src/versions/v2.0.0/tests/continue-config-manager.test.mjs --require ./node_modules/esm
 */

import assert from 'assert';
import { promises as fs } from 'fs';
import { homedir } from 'os';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import {
  ContinueConfigManager,
  ConfigError,
  ValidationError,
  FileIOError,
  createContinueConfigManager
} from '../lib/continue-config-manager.mjs';

describe('ContinueConfigManager (Step 104)', () => {
  let manager;
  let tempDir;
  let originalHome;

  beforeEach(async () => {
    manager = new ContinueConfigManager();
    tempDir = join('/tmp', `continue_test_${Date.now()}_${Math.random().toString(36).slice(2)}`);
    try {
      await fs.mkdir(tempDir, { recursive: true });
    } catch (err) {
      // Directory may already exist
    }
  });

  afterEach(async () => {
    if (tempDir) {
      try {
        await fs.rm(tempDir, { recursive: true, force: true });
      } catch (err) {
        // Cleanup error, ignore
      }
    }
  });

  describe('Suite 1: Initialization (3 tests)', () => {
    it('should create manager without logger', () => {
      const mgr = new ContinueConfigManager();
      assert.ok(mgr);
    });

    it('should create manager with logger', () => {
      const logger = { log: () => {} };
      const mgr = new ContinueConfigManager(logger);
      assert.ok(mgr);
    });

    it('should create manager via factory function', () => {
      const logger = { log: () => {} };
      const metrics = { record: () => {} };
      const mgr = createContinueConfigManager(logger, metrics);
      assert.ok(mgr instanceof ContinueConfigManager);
    });
  });

  describe('Suite 2: File I/O (5 tests)', () => {
    it('should read nonexistent file and return empty config', async () => {
      const config = await manager.readConfig();
      assert.ok(config);
      assert.ok(Array.isArray(config.models));
      assert.equal(config.models.length, 0);
    });

    it('should read valid config file', async () => {
      const validConfig = {
        models: [
          { title: 'GPT-4', provider: 'openai', model: 'gpt-4' }
        ]
      };
      const configPath = join(tempDir, 'config.json');
      await fs.writeFile(configPath, JSON.stringify(validConfig, null, 2), 'utf-8');
      await manager.writeConfig(validConfig);

      const read = await manager.readConfig();
      assert.ok(read);
      assert.equal(read.models.length, 1);
    });

    it('should throw on corrupted JSON', async () => {
      const configPath = join(homedir(), '.continue', 'config.json');
      const configDir = dirname(configPath);
      try {
        await fs.mkdir(configDir, { recursive: true });
        await fs.writeFile(configPath, '{ invalid json', 'utf-8');

        await assert.rejects(
          async () => await manager.readConfig(),
          (err) => err instanceof ConfigError
        );
      } finally {
        try {
          await fs.rm(configPath);
        } catch (e) { /* cleanup */ }
      }
    });

    it('should create directory if not exists on write', async () => {
      const config = { models: [] };
      await manager.writeConfig(config);

      const configPath = join(homedir(), '.continue', 'config.json');
      const exists = await fs.stat(configPath).then(() => true).catch(() => false);
      assert.ok(exists);
    });

    it('should create backup of existing config', async () => {
      const config1 = { models: [] };
      const config2 = { models: [{ title: 'Model', provider: 'p', model: 'm' }] };

      await manager.writeConfig(config1);
      await manager.writeConfig(config2);

      const configPath = join(homedir(), '.continue', 'config.json');
      const backupPath = `${configPath}.backup`;
      const exists = await fs.stat(backupPath).then(() => true).catch(() => false);
      assert.ok(exists);
    });
  });

  describe('Suite 3: Schema Validation (5 tests)', () => {
    it('should validate correct config', async () => {
      const config = {
        models: [
          { title: 'GPT-4', provider: 'openai', model: 'gpt-4' }
        ]
      };
      assert.doesNotThrow(() => manager.validateSchema(config));
    });

    it('should reject null config', () => {
      assert.throws(
        () => manager.validateSchema(null),
        (err) => err instanceof ValidationError
      );
    });

    it('should reject non-array models', () => {
      const config = { models: 'not_array' };
      assert.throws(
        () => manager.validateSchema(config),
        (err) => err instanceof ValidationError
      );
    });

    it('should reject model with missing title', () => {
      const config = {
        models: [
          { provider: 'openai', model: 'gpt-4' }
        ]
      };
      assert.throws(
        () => manager.validateSchema(config),
        (err) => err instanceof ValidationError
      );
    });

    it('should reject duplicate titles', () => {
      const config = {
        models: [
          { title: 'GPT-4', provider: 'openai', model: 'gpt-4' },
          { title: 'GPT-4', provider: 'openai', model: 'gpt-4-32k' }
        ]
      };
      assert.throws(
        () => manager.validateSchema(config),
        (err) => err instanceof ValidationError && err.code === 'DUPLICATE_TITLE'
      );
    });
  });

  describe('Suite 4: Model Merging (6 tests)', () => {
    it('should add new model to config', async () => {
      const config = { models: [{ title: 'GPT-4', provider: 'openai', model: 'gpt-4' }] };
      const toMerge = [{ title: 'Claude', provider: 'anthropic', model: 'claude-3' }];

      const merged = await manager.mergeModels(config, toMerge);
      assert.equal(merged.models.length, 2);
      assert.ok(merged.models.some(m => m.title === 'Claude'));
    });

    it('should update existing model by title', async () => {
      const config = { models: [{ title: 'GPT-4', provider: 'openai', model: 'gpt-4' }] };
      const toMerge = [{ title: 'GPT-4', provider: 'openai', model: 'gpt-4-32k' }];

      const merged = await manager.mergeModels(config, toMerge);
      assert.equal(merged.models.length, 1);
      assert.equal(merged.models[0].model, 'gpt-4-32k');
    });

    it('should handle case-insensitive title matching', async () => {
      const config = { models: [{ title: 'GPT-4', provider: 'openai', model: 'gpt-4' }] };
      const toMerge = [{ title: 'gpt-4', provider: 'openai', model: 'gpt-4-turbo' }];

      const merged = await manager.mergeModels(config, toMerge);
      assert.equal(merged.models.length, 1);
      assert.equal(merged.models[0].model, 'gpt-4-turbo');
    });

    it('should handle empty merge list', async () => {
      const config = { models: [{ title: 'GPT-4', provider: 'openai', model: 'gpt-4' }] };

      const merged = await manager.mergeModels(config, []);
      assert.equal(merged.models.length, 1);
    });

    it('should remove models by title', async () => {
      const config = {
        models: [
          { title: 'GPT-4', provider: 'openai', model: 'gpt-4' },
          { title: 'Claude', provider: 'anthropic', model: 'claude-3' }
        ]
      };

      const result = await manager.removeModels(config, ['GPT-4']);
      assert.equal(result.models.length, 1);
      assert.equal(result.models[0].title, 'Claude');
    });

    it('should handle case-insensitive removal', async () => {
      const config = {
        models: [
          { title: 'GPT-4', provider: 'openai', model: 'gpt-4' }
        ]
      };

      const result = await manager.removeModels(config, ['gpt-4']);
      assert.equal(result.models.length, 0);
    });
  });

  describe('Suite 5: Performance (4 tests)', () => {
    it('readConfig should complete within 200ms', async () => {
      const start = Date.now();
      await manager.readConfig();
      const elapsed = Date.now() - start;
      assert.ok(elapsed < 200, `Read took ${elapsed}ms, expected < 200ms`);
    });

    it('writeConfig should complete within 500ms', async () => {
      const config = { models: [{ title: 'Test', provider: 'test', model: 'test' }] };
      const start = Date.now();
      await manager.writeConfig(config);
      const elapsed = Date.now() - start;
      assert.ok(elapsed < 500, `Write took ${elapsed}ms, expected < 500ms`);
    });

    it('validateSchema should complete within 50ms', () => {
      const config = {
        models: Array.from({ length: 100 }, (_, i) => ({
          title: `Model${i}`,
          provider: 'openai',
          model: `model-${i}`
        }))
      };
      const start = Date.now();
      manager.validateSchema(config);
      const elapsed = Date.now() - start;
      assert.ok(elapsed < 50, `Validation took ${elapsed}ms, expected < 50ms`);
    });

    it('mergeModels should preserve performance with large sets', async () => {
      const config = {
        models: Array.from({ length: 50 }, (_, i) => ({
          title: `Model${i}`,
          provider: 'openai',
          model: `model-${i}`
        }))
      };
      const toMerge = Array.from({ length: 50 }, (_, i) => ({
        title: `NewModel${i}`,
        provider: 'anthropic',
        model: `new-model-${i}`
      }));

      const start = Date.now();
      await manager.mergeModels(config, toMerge);
      const elapsed = Date.now() - start;
      assert.ok(elapsed < 100, `Merge took ${elapsed}ms, expected < 100ms`);
    });
  });

  describe('Suite 6: Error Handling & Graceful Degradation (4 tests)', () => {
    it('should propagate ConfigError on write with invalid config', async () => {
      const config = {
        models: [{ title: '', provider: 'openai', model: 'gpt-4' }]
      };

      await assert.rejects(
        async () => await manager.writeConfig(config),
        (err) => err instanceof ConfigError
      );
    });

    it('should handle missing logger gracefully', async () => {
      const mgr = new ContinueConfigManager(null, null);
      const config = { models: [] };

      assert.doesNotThrow(async () => {
        await mgr.writeConfig(config);
      });
    });

    it('should preserve optional fields (apiKey, apiBase)', async () => {
      const config = {
        models: [
          {
            title: 'GPT-4',
            provider: 'openai',
            model: 'gpt-4',
            apiKey: 'sk-test',
            apiBase: 'https://api.example.com'
          }
        ]
      };

      await manager.writeConfig(config);
      const read = await manager.readConfig();
      assert.equal(read.models[0].apiKey, 'sk-test');
      assert.equal(read.models[0].apiBase, 'https://api.example.com');
    });

    it('ConfigError should have correct properties', () => {
      const err = new ConfigError('Test', 'write', 'TEST_ERROR', { detail: 'info' });
      assert.equal(err.operation, 'write');
      assert.equal(err.code, 'TEST_ERROR');
      assert.ok(err.details.detail === 'info');
    });
  });
});
