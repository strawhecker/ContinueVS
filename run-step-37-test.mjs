import { orchestrateChecksumGeneration } from './src/versions/v2.0.0/lib/generate-checksums.mjs';

const result = await orchestrateChecksumGeneration({
  packagePath: './.cache/npm-packages/v2.0.0/continue-2.0.0.tgz',
  checksumsOutputPath: './.cache/npm-packages/v2.0.0/CHECKSUMS.txt',
  manifestPath: './src/versions/v2.0.0/manifest.json',
  updateManifest: true,
  validate: true
});

console.log(JSON.stringify(result, null, 2));
process.exit(result.success ? 0 : 1);
