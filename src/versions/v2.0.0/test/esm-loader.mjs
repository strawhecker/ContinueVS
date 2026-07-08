/**
 * ESM Loader for Mocha Tests
 * 
 * Provides experimental module loading support for Node.js ESM in Mocha.
 * This is a minimal loader that enables ES module support.
 * 
 * @module test/esm-loader.mjs
 */

export async function resolve(specifier, context, nextResolve) {
  return nextResolve(specifier);
}

export async function getFormat(url, context, nextGetFormat) {
  return nextGetFormat(url);
}

export async function getSource(url, context, nextGetSource) {
  return nextGetSource(url);
}

export async function getGlobalPreloadCode() {
  return '';
}
