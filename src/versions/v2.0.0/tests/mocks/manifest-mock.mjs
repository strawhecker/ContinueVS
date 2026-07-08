/**
 * Manifest Mock Fixtures for Version Upgrade Tests (Step 32)
 *
 * Provides realistic v1.9.5 and v2.0.0 manifest objects for upgrade path validation.
 * Used by version-upgrade.test.mjs to simulate version transitions and breaking changes.
 *
 * @module src/versions/v2.0.0/tests/mocks/manifest-mock.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 32 (version upgrade test), 4 (manifest schema), 37 (checksums)
 */

/**
 * Returns a mock v1.9.5 manifest representing a legacy bridge version.
 * Includes stable features, some deprecated entries, and backward compatibility markers.
 */
export function getManifestV195() {
  return {
    version: '1.9.5',
    releaseDate: '2023-12-01T08:00:00Z',
    npmPackage: {
      name: 'continue',
      version: '1.9.5',
      tarballUrl: 'https://registry.npmjs.org/continue/-/continue-1.9.5.tgz',
      registry: 'https://registry.npmjs.org'
    },
    checksums: {
      sha256: 'b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2',
      sha512: '2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f2'
    },
    compatibility: {
      vsCodeVersions: ['1.75.0', '1.76.0', '1.77.0', '1.78.0', '1.79.0'],
      nodeVersions: ['16.0.0', '18.0.0'],
      platforms: ['win32']
    },
    features: {
      stable: [
        'coreEditorIntegration',
        'diagnosticsCollection',
        'goToDefinition',
        'findReferences',
        'codeCompletion',
        'search',
        'basicHoverInfo',
        'selectionTracking'
      ],
      experimental: [
        'advancedSymbolSearch'
      ],
      deprecated: [
        'legacyStdioTransport',
        'legacyWebviewInjection'
      ]
    },
    dependencies: {
      minBridgeVersion: '0.8.0',
      previousVersions: ['1.9.4', '1.9.3', '1.9.0']
    }
  };
}

/**
 * Returns a mock v2.0.0 manifest representing the current bridge version.
 * Includes new features, improvements over v1.9.5, and removal of deprecated features.
 */
export function getManifestV200() {
  return {
    version: '2.0.0',
    releaseDate: '2024-01-15T10:30:00Z',
    npmPackage: {
      name: 'continue',
      version: '2.0.0',
      tarballUrl: 'https://registry.npmjs.org/continue/-/continue-2.0.0.tgz',
      registry: 'https://registry.npmjs.org'
    },
    checksums: {
      sha256: 'a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6',
      sha512: '1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f'
    },
    compatibility: {
      vsCodeVersions: ['1.80.0', '1.81.0', '1.82.0', '1.83.0', '1.84.0'],
      nodeVersions: ['18.0.0', '20.0.0'],
      platforms: ['win32']
    },
    features: {
      stable: [
        'coreEditorIntegration',
        'diagnosticsCollection',
        'goToDefinition',
        'findReferences',
        'codeCompletion',
        'search',
        'enhancedHoverInfo',
        'selectionTracking',
        'modernStdioTransport',
        'improvedWebviewMessaging'
      ],
      experimental: [
        'advancedSymbolSearch',
        'webviewMessaging',
        'streamingResponses'
      ],
      deprecated: []
    },
    dependencies: {
      minBridgeVersion: '1.0.0',
      previousVersions: ['1.9.5', '1.9.0']
    },
    breakingChanges: [
      {
        feature: 'basicHoverInfo',
        replacement: 'enhancedHoverInfo',
        description: 'basicHoverInfo renamed to enhancedHoverInfo with new API'
      },
      {
        feature: 'legacyStdioTransport',
        replacement: 'modernStdioTransport',
        description: 'Legacy stdio transport removed; migrate to modernStdioTransport'
      }
    ]
  };
}

/**
 * Returns a manifest for v2.1.0 (future version for upgrade chain testing).
 * Used to test multi-version upgrade paths and transitive compatibility.
 */
export function getManifestV210() {
  return {
    version: '2.1.0',
    releaseDate: '2024-02-20T14:00:00Z',
    npmPackage: {
      name: 'continue',
      version: '2.1.0',
      tarballUrl: 'https://registry.npmjs.org/continue/-/continue-2.1.0.tgz',
      registry: 'https://registry.npmjs.org'
    },
    checksums: {
      sha256: 'c2d3e4f5a6b1c2d3e4f5a6b1c2d3e4f5a6b1c2d3e4f5a6b1c2d3e4f5a6b1c2d3',
      sha512: '3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f3'
    },
    compatibility: {
      vsCodeVersions: ['1.85.0', '1.86.0'],
      nodeVersions: ['18.0.0', '20.0.0'],
      platforms: ['win32']
    },
    features: {
      stable: [
        'coreEditorIntegration',
        'diagnosticsCollection',
        'goToDefinition',
        'findReferences',
        'codeCompletion',
        'search',
        'enhancedHoverInfo',
        'selectionTracking',
        'modernStdioTransport',
        'improvedWebviewMessaging',
        'refactoringSupport'
      ],
      experimental: [
        'advancedSymbolSearch',
        'webviewMessaging',
        'streamingResponses',
        'aiPoweredCompletion'
      ],
      deprecated: []
    },
    dependencies: {
      minBridgeVersion: '1.0.0',
      previousVersions: ['2.0.0', '1.9.5']
    }
  };
}

/**
 * Returns a corrupted manifest for negative testing.
 * Missing critical fields to test error handling.
 */
export function getCorruptedManifest() {
  return {
    // Missing version field
    releaseDate: '2024-01-15T10:30:00Z',
    // Missing checksums
    features: {
      stable: ['someFeature'],
      experimental: [],
      deprecated: []
    }
    // Missing dependencies
  };
}

export default {
  getManifestV195,
  getManifestV200,
  getManifestV210,
  getCorruptedManifest
};
