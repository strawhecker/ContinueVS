#!/usr/bin/env node
import { downloadWithFallback } from 'file:///E:/GitRepos/ContinueVS/src/versions/v2.0.0/lib/npm-registry-download.mjs';

const result = await downloadWithFallback(
  'v2.0.0',
  'E:/GitRepos/ContinueVS/.cache/npm-packages/v2.0.0',
  'E:/GitRepos/ContinueVS/src/versions/v2.0.0/manifest.json',
  { dryRun: true }
);

console.log(JSON.stringify(result, null, 2));
process.exit(result.success ? 0 : 2);
