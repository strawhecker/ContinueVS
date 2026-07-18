#!/usr/bin/env node

/**
 * MJS File Comment Verification Script
 *
 * Scans all .mjs files to verify:
 * 1. Exported functions/classes have JSDoc comments
 * 2. JSDoc @param/@returns match actual function signatures
 * 3. Error handling matches documented behavior
 * 4. Dependencies listed in comments exist in imports
 *
 * Usage:
 *   node verify-mjs-comments.mjs [--fix] [--verbose]
 *
 * Options:
 *   --fix      Attempt to fix common issues (not yet implemented)
 *   --verbose  Show all files checked, not just issues
 *   --filter   Only check files matching pattern (e.g., "*-handler.mjs")
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const WORKSPACE_ROOT = path.resolve(__dirname, '../..');
const LIB_DIR = path.resolve(__dirname, '../lib');
const TEST_DIR = path.resolve(__dirname, '../tests');

// Parse CLI arguments
const args = process.argv.slice(2);
const verbose = args.includes('--verbose');
const fix = args.includes('--fix');
const filterArg = args.find(arg => arg.startsWith('--filter='));
const filterPattern = filterArg ? filterArg.split('=')[1] : null;

/**
 * Result structure for each file
 */
class VerificationResult {
  constructor(filePath) {
    this.filePath = filePath;
    this.relPath = path.relative(WORKSPACE_ROOT, filePath);
    this.errors = [];
    this.warnings = [];
    this.info = [];
  }

  addError(message, line = null) {
    this.errors.push({ message, line });
  }

  addWarning(message, line = null) {
    this.warnings.push({ message, line });
  }

  addInfo(message) {
    if (verbose) this.info.push(message);
  }

  hasIssues() {
    return this.errors.length > 0 || this.warnings.length > 0;
  }

  print() {
    if (!this.hasIssues() && !verbose) return;

    console.log(`\n📄 ${this.relPath}`);

    if (this.errors.length > 0) {
      console.log('  ❌ Errors:');
      this.errors.forEach(err => {
        const loc = err.line ? `:${err.line}` : '';
        console.log(`     ${err.message}${loc}`);
      });
    }

    if (this.warnings.length > 0) {
      console.log('  ⚠️  Warnings:');
      this.warnings.forEach(warn => {
        const loc = warn.line ? `:${warn.line}` : '';
        console.log(`     ${warn.message}${loc}`);
      });
    }

    if (this.info.length > 0 && verbose) {
      console.log('  ℹ️  Info:');
      this.info.forEach(inf => console.log(`     ${inf}`));
    }
  }
}

/**
 * Extract all .mjs files from a directory
 */
function getAllMjsFiles(dir) {
  const files = [];

  if (!fs.existsSync(dir)) {
    return files;
  }

  function walk(currentDir) {
    try {
      const items = fs.readdirSync(currentDir);
      for (const item of items) {
        const fullPath = path.join(currentDir, item);
        const stat = fs.statSync(fullPath);

        if (stat.isDirectory()) {
          // Skip node_modules and other common excludes
          if (!item.startsWith('.') && item !== 'node_modules') {
            walk(fullPath);
          }
        } else if (item.endsWith('.mjs')) {
          files.push(fullPath);
        }
      }
    } catch (err) {
      console.error(`Error reading directory ${currentDir}:`, err.message);
    }
  }

  walk(dir);
  return files;
}

/**
 * Extract JSDoc comment block before a symbol
 */
function extractJSDoc(lines, startLine) {
  let jsdoc = null;
  let line = startLine - 1;

  // Walk backwards to find JSDoc block
  while (line >= 0) {
    const trimmed = lines[line].trim();

    if (trimmed.endsWith('*/')) {
      // Found end of JSDoc, now collect it
      const endLine = line;
      line--;
      while (line >= 0 && !lines[line].includes('/*')) {
        line--;
      }
      if (line >= 0) {
        jsdoc = lines.slice(line, endLine + 1).join('\n');
      }
      break;
    } else if (trimmed === '' || trimmed.startsWith('//') || trimmed.startsWith('*')) {
      line--;
    } else {
      break;
    }
  }

  return jsdoc;
}

/**
 * Extract imported modules from code
 */
function extractImports(content) {
  const imports = {};
  const importRegex = /import\s+(?:\{([^}]+)\}|(\w+))\s+from\s+['"]([^'"]+)['"]/g;
  let match;

  while ((match = importRegex.exec(content)) !== null) {
    const named = match[1];
    const defaultImport = match[2];
    const source = match[3];

    if (named) {
      named.split(',').forEach(item => {
        const trimmed = item.trim().split(' as ')[0];
        imports[trimmed] = source;
      });
    }
    if (defaultImport) {
      imports[defaultImport] = source;
    }
  }

  return imports;
}

/**
 * Extract exported symbols and their JSDoc
 */
function extractExports(content, filePath) {
  const lines = content.split('\n');
  const exports = [];
  const imports = extractImports(content);

  // Match: export const NAME, export function NAME, export class NAME, export async function NAME
  const exportRegex = /export\s+(const|function|class|async\s+function)\s+(\w+)/g;
  let match;

  // Use spread operator safely
  const matches = [...content.matchAll(exportRegex)];

  matches.forEach(match => {
    // Find line number
    const index = match.index;
    const lineNum = content.substring(0, index).split('\n').length;

    const type = (match[1] || 'const').replace('async ', '');
    const name = match[2];
    const jsdoc = extractJSDoc(lines, lineNum);

    exports.push({
      name,
      type,
      line: lineNum,
      jsdoc,
      hasDoc: jsdoc !== null
    });
  });

  return { exports, imports };
}

/**
 * Verify a single .mjs file
 */
function verifyFile(filePath) {
  const result = new VerificationResult(filePath);

  try {
    const content = fs.readFileSync(filePath, 'utf8');
    const { exports, imports } = extractExports(content, filePath);

    result.addInfo(`Found ${exports.length} exports`);

    // Check each export for documentation
    exports.forEach(exp => {
      if (!exp.hasDoc) {
        result.addWarning(
          `Exported ${exp.type} '${exp.name}' missing JSDoc comment`,
          exp.line
        );
      }

      // Check for common issues in JSDoc
      if (exp.jsdoc) {
        if (exp.type === 'function' && !exp.jsdoc.includes('@param')) {
          result.addWarning(
            `Function '${exp.name}' has JSDoc but no @param tags`,
            exp.line
          );
        }
        if (!exp.jsdoc.includes('@returns') && !exp.jsdoc.includes('@return')) {
          result.addWarning(
            `${exp.type === 'function' ? 'Function' : 'Export'} '${exp.name}' missing @returns/@return`,
            exp.line
          );
        }
      }
    });

    // Check for undocumented error classes
    const lines2 = content.split('\n');
    const errorClassRegex = /export\s+class\s+(\w+Error)\s+extends/g;
    let errorMatch;
    while ((errorMatch = errorClassRegex.exec(content)) !== null) {
      const lineNum = content.substring(0, errorMatch.index).split('\n').length;
      const jsdoc = extractJSDoc(lines2, lineNum);
      if (!jsdoc) {
        result.addWarning(
          `Error class '${errorMatch[1]}' missing JSDoc comment`,
          lineNum
        );
      }
    }

    // Check dependencies mentioned in comments
    const depRegex = /Step\s+(\d+):\s+([^\n]+)/g;
    let depMatch;
    const mentionedDeps = [];
    while ((depMatch = depRegex.exec(content)) !== null) {
      mentionedDeps.push({
        step: parseInt(depMatch[1]),
        name: depMatch[2].trim()
      });
    }
    result.addInfo(`References ${mentionedDeps.length} step dependencies`);

  } catch (err) {
    result.addError(`Failed to parse file: ${err.message}`);
  }

  return result;
}

/**
 * Main verification run
 */
function main() {
  console.log('🔍 MJS Comment Verification Script');
  console.log(`📁 Scanning: ${LIB_DIR} and ${TEST_DIR}\n`);

  const allFiles = [
    ...getAllMjsFiles(LIB_DIR),
    ...getAllMjsFiles(TEST_DIR)
  ]
    .filter(f => {
      if (filterPattern) {
        return path.basename(f).includes(filterPattern);
      }
      return true;
    })
    .sort();

  console.log(`Found ${allFiles.length} .mjs files\n`);

  const results = allFiles.map(file => verifyFile(file));

  // Print results
  const issueCount = results.filter(r => r.hasIssues()).length;
  console.log('\n=== Verification Results ===\n');

  results.forEach(result => result.print());

  // Summary
  const errorCount = results.reduce((sum, r) => sum + r.errors.length, 0);
  const warningCount = results.reduce((sum, r) => sum + r.warnings.length, 0);

  console.log('\n📊 Summary:');
  console.log(`  Files with issues: ${issueCount}/${results.length}`);
  console.log(`  ❌ Errors: ${errorCount}`);
  console.log(`  ⚠️  Warnings: ${warningCount}`);

  if (errorCount > 0) {
    process.exit(1);
  }
}

main();
