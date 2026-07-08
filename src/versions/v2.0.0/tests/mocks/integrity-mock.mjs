/**
 * Mock Adapter for Integrity Utility (Test Support)
 *
 * Provides mocked validatePackageIntegrity() function with
 * configurable scenarios for testing npm-validate.mjs
 *
 * @module src/versions/v2.0.0/tests/mocks/integrity-mock.mjs
 * @version 1.0.0
 */

export class MockAdapter {
  constructor() {
    this.cacheDir = '.cache/npm-packages/v2.0.0';
    this.scenario = 'healthy';
    this.throwError = null;
    this.slowDelay = 0;
  }

  /**
   * Reset to healthy state
   */
  reset() {
    this.scenario = 'healthy';
    this.throwError = null;
    this.slowDelay = 0;
  }

  /**
   * Set up package-not-found scenario
   */
  setPackageNotFound() {
    this.scenario = 'packageNotFound';
  }

  /**
   * Set up checksum-mismatch scenario
   */
  setChecksumMismatch() {
    this.scenario = 'checksumMismatch';
  }

  /**
   * Set up manifest-invalid scenario
   */
  setManifestInvalid() {
    this.scenario = 'manifestInvalid';
  }

  /**
   * Set up checksum-missing scenario
   */
  setChecksumMissing() {
    this.scenario = 'checksumMissing';
  }

  /**
   * Set up cache-inaccessible scenario
   */
  setCacheInaccessible() {
    this.scenario = 'cacheInaccessible';
  }

  /**
   * Set up slow validation for timeout testing
   */
  setSlowValidation(delayMs) {
    this.slowDelay = delayMs;
  }

  /**
   * Set up error-throwing scenario
   */
  setThrowError(error) {
    this.throwError = error;
  }

  /**
   * Get mocked validation result based on current scenario
   */
  async getValidationResult(versionDir, version) {
    // Add delay if configured
    if (this.slowDelay > 0) {
      await new Promise(resolve => setTimeout(resolve, this.slowDelay));
    }

    // Throw error if configured
    if (this.throwError) {
      throw this.throwError;
    }

    const versionNum = version.toString().replace(/^v/, '');

    switch (this.scenario) {
      case 'healthy':
        return {
          valid: true,
          version: versionNum,
          versionDir,
          packagePath: `${versionDir}/continue-v${versionNum}.tgz`,
          manifestPath: `${versionDir}/manifest-v${versionNum}.json`,
          checksumValid: true,
          manifestValid: true,
          metadata: {
            version: versionNum,
            continueVersion: '0.4.x',
            releaseDate: '2024-01-15T10:30:00Z',
            status: 'stable'
          },
          errors: []
        };

      case 'packageNotFound':
        return {
          valid: false,
          version: versionNum,
          versionDir,
          packagePath: `${versionDir}/continue-v${versionNum}.tgz`,
          manifestPath: `${versionDir}/manifest-v${versionNum}.json`,
          checksumValid: false,
          manifestValid: true,
          metadata: null,
          errors: [
            `Failed to compute SHA256 for ${versionDir}/continue-v${versionNum}.tgz: ENOENT: no such file or directory`
          ]
        };

      case 'checksumMismatch':
        return {
          valid: false,
          version: versionNum,
          versionDir,
          packagePath: `${versionDir}/continue-v${versionNum}.tgz`,
          manifestPath: `${versionDir}/manifest-v${versionNum}.json`,
          checksumValid: false,
          manifestValid: true,
          metadata: {
            version: versionNum,
            continueVersion: '0.4.x',
            releaseDate: '2024-01-15T10:30:00Z',
            status: 'stable'
          },
          errors: [
            'Checksum mismatch: expected abc123def456..., computed 789def012abc... (file may be corrupted)'
          ]
        };

      case 'manifestInvalid':
        return {
          valid: false,
          version: versionNum,
          versionDir,
          packagePath: `${versionDir}/continue-v${versionNum}.tgz`,
          manifestPath: `${versionDir}/manifest-v${versionNum}.json`,
          checksumValid: true,
          manifestValid: false,
          metadata: null,
          errors: [
            'Manifest missing required field: "continueVersion"'
          ]
        };

      case 'checksumMissing':
        return {
          valid: false,
          version: versionNum,
          versionDir,
          packagePath: `${versionDir}/continue-v${versionNum}.tgz`,
          manifestPath: `${versionDir}/manifest-v${versionNum}.json`,
          checksumValid: false,
          manifestValid: true,
          metadata: null,
          errors: [
            `Checksum file not found: ${versionDir}/continue-v${versionNum}.tgz.sha256`
          ]
        };

      case 'cacheInaccessible':
        return {
          valid: false,
          version: versionNum,
          versionDir,
          packagePath: `${versionDir}/continue-v${versionNum}.tgz`,
          manifestPath: `${versionDir}/manifest-v${versionNum}.json`,
          checksumValid: false,
          manifestValid: false,
          metadata: null,
          errors: [
            `EACCES: permission denied, access '${versionDir}'`
          ]
        };

      default:
        return {
          valid: false,
          version: versionNum,
          versionDir,
          packagePath: `${versionDir}/continue-v${versionNum}.tgz`,
          manifestPath: `${versionDir}/manifest-v${versionNum}.json`,
          checksumValid: false,
          manifestValid: false,
          metadata: null,
          errors: ['Unknown scenario']
        };
    }
  }
}

/**
 * Patch validatePackageIntegrity in tests
 * This would be called during test setup to replace the real function
 */
export function createMockValidatePackageIntegrity(adapter) {
  return async (versionDir, version) => {
    return await adapter.getValidationResult(versionDir, version);
  };
}
