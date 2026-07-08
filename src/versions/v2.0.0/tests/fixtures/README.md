# Test Fixtures for npm-registry-download

Directory containing test data for the npm registry download unit tests.

## Contents

### Expected Files (to be created by tests)

- `test.tgz` - Minimal valid tarball (created during tests)
- `test-corrupted.tgz` - Corrupted tarball for negative tests
- `manifest-valid.json` - Valid manifest with correct checksums
- `manifest-invalid.json` - Invalid manifest for error scenarios

## Notes

- Fixtures are dynamically generated during test execution
- No committed binary files (tarballs) to minimize repo size
- Checksums are computed at test time using `crypto.createHash('sha256')`

## See Also

- `npm-registry-download.test.mjs` - Test suite that uses these fixtures
- `../lib/npm-registry-download.mjs` - Module under test
