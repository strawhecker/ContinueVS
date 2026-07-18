# MJS Comment Verification Tool

A Node.js script to automatically scan `.mjs` files and verify that JSDoc comments match the actual code implementation.

## Features

- ✅ Detects exported functions, classes, and constants
- ✅ Checks for JSDoc comments on all exports
- ✅ Validates JSDoc parameter/return tags
- ✅ Reports missing documentation
- ✅ Tracks step dependencies mentioned in comments
- ✅ Colorized terminal output with emoji indicators

## Installation

No external dependencies required (uses Node.js built-ins only).

```bash
cd E:\GitRepos\ContinueVS
```

## Usage

### Basic Scan (All MJS Files)
```bash
node src/versions/v2.0.0/tools/verify-mjs-comments.mjs
```

Output shows:
- 📁 Scan directory
- 📊 Summary of issues
- ❌ Errors (critical problems)
- ⚠️ Warnings (missing docs)
- ℹ️ Info (in verbose mode)

### Verbose Mode (Show All Files)
```bash
node src/versions/v2.0.0/tools/verify-mjs-comments.mjs --verbose
```

Shows every file scanned, including those without issues.

### Filter to Specific Files
```bash
# Only scan code-lens handler
node src/versions/v2.0.0/tools/verify-mjs-comments.mjs --filter=code-lens

# Only scan handler files (*-handler.mjs)
node src/versions/v2.0.0/tools/verify-mjs-comments.mjs --filter=handler.mjs

# Only scan test files
node src/versions/v2.0.0/tools/verify-mjs-comments.mjs --filter=test.mjs
```

### Combine Options
```bash
node src/versions/v2.0.0/tools/verify-mjs-comments.mjs --verbose --filter=handler.mjs
```

## Example Output

```
🔍 MJS Comment Verification Script
📁 Scanning: E:\GitRepos\ContinueVS\src\versions\v2.0.0\lib and tests

Found 117 .mjs files

=== Verification Results ===

📄 v2.0.0\lib\code-lens-handler.mjs
  ℹ️  Info:
     Found 3 exports
     References 9 step dependencies

📄 v2.0.0\lib\apply-edit-handler.mjs
  ⚠️  Warnings:
     Function 'validateEdit' has JSDoc but no @param tags:245

📊 Summary:
  Files with issues: 2/23
  ❌ Errors: 0
  ⚠️  Warnings: 4
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | No errors (warnings OK) |
| 1 | Errors found (halt processing) |

## What It Checks

### Documentation Requirements

- ✅ All exported functions should have JSDoc with `@param` and `@returns`
- ✅ All exported classes should have JSDoc with property descriptions
- ✅ All exported constants should have JSDoc with type info
- ✅ Error classes should document error codes and properties

### Dependency Tracking

- Finds references to other steps: `Step 53: symbol-extractor`
- Counts and reports in info section
- Useful for dependency graph validation

### Current Limitations

1. **JSDoc extraction is basic** - May not detect comments separated by multiple blank lines
2. **No fix mode** - Currently read-only (--fix flag not implemented)
3. **Regex-based** - Not a full AST parser (trade-off for minimal dependencies)

## Extending the Script

To add more checks, modify the `verifyFile()` function:

```javascript
function verifyFile(filePath) {
  const result = new VerificationResult(filePath);

  try {
    const content = fs.readFileSync(filePath, 'utf8');
    const { exports, imports } = extractExports(content, filePath);

    // Add your custom checks here
    exports.forEach(exp => {
      // Example: Check for TODO comments
      if (exp.jsdoc?.includes('TODO')) {
        result.addWarning(`Incomplete documentation: ${exp.name}`, exp.line);
      }
    });

  } catch (err) {
    result.addError(`Failed to parse: ${err.message}`);
  }

  return result;
}
```

## Future Enhancements

- [ ] AST-based parsing (for more accurate JSDoc detection)
- [ ] Automatic comment generation (--fix mode)
- [ ] Integration with pre-commit hooks
- [ ] GitHub Actions workflow
- [ ] Comment drift detection (compare docs vs. behavior)
- [ ] Generate documentation from code

## Related Documentation

- [MJS Comment Verification Report](../../docs/MJS-COMMENT-VERIFICATION-REPORT.md)
- [Code Lens Handler Documentation](../lib/code-lens-handler.mjs)
- [Bridge Architecture Overview](../../docs/BRIDGE-DEVELOPER-GUIDE.md)

## License

Part of ContinueVS bridge architecture. See LICENSE for details.
