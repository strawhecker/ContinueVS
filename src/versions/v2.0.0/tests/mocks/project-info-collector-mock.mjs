/**
 * Mock fixtures for ProjectInfoCollector testing.
 * Provides reusable mock responses and factories for unit/integration tests.
 */

/**
 * Returns a valid complete ProjectInfoCollector response.
 * Represents a typical multi-project solution.
 * 
 * @returns {Object} Complete project info with solution, projects, workspace, build status
 */
export function getValidProjectInfoResponse() {
  return {
    solution: {
      name: 'ContinueVS',
      path: '/home/user/projects/ContinueVS/ContinueVS.sln',
      projectCount: 3,
    },
    projects: [
      {
        name: 'VSIXProject1',
        path: '/home/user/projects/ContinueVS/src/VSIXProject1/VSIXProject1.csproj',
        type: 'C# Project',
        targetFramework: 'net472',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
      {
        name: 'VSIXProject1.Tests',
        path: '/home/user/projects/ContinueVS/src/VSIXProject1.Tests/VSIXProject1.Tests.csproj',
        type: 'C# Project',
        targetFramework: 'net10',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
      {
        name: 'ContinueTranslator.Core',
        path: '/home/user/projects/ContinueVS/src/tools/ContinueTranslator.Core/ContinueTranslator.Core.csproj',
        type: 'C# Project',
        targetFramework: 'net8.0',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
    ],
    workspace: {
      rootPath: '/home/user/projects/ContinueVS',
      gitBranch: 'main',
    },
    buildStatus: {
      lastBuild: '2024-07-15T10:30:00Z',
      isBuilding: false,
      errors: 0,
      warnings: 0,
    },
  };
}

/**
 * Returns a response for a single-project solution.
 * Minimal valid response with one project.
 * 
 * @returns {Object} Project info for a single-project workspace
 */
export function getSingleProjectResponse() {
  return {
    solution: {
      name: 'SimpleApp',
      path: '/home/user/projects/SimpleApp/SimpleApp.sln',
      projectCount: 1,
    },
    projects: [
      {
        name: 'SimpleApp',
        path: '/home/user/projects/SimpleApp/SimpleApp.csproj',
        type: 'C# Project',
        targetFramework: 'net8.0',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
    ],
    workspace: {
      rootPath: '/home/user/projects/SimpleApp',
      gitBranch: 'main',
    },
    buildStatus: {
      lastBuild: '2024-07-15T14:20:00Z',
      isBuilding: false,
      errors: 0,
      warnings: 2,
    },
  };
}

/**
 * Returns a response for an empty solution (no projects).
 * Valid but minimal response.
 * 
 * @returns {Object} Project info for a solution with zero projects
 */
export function getEmptySolutionResponse() {
  return {
    solution: {
      name: 'EmptySolution',
      path: '/home/user/projects/Empty/Empty.sln',
      projectCount: 0,
    },
    projects: [],
    workspace: {
      rootPath: '/home/user/projects/Empty',
      gitBranch: 'develop',
    },
    buildStatus: {
      lastBuild: null,
      isBuilding: false,
      errors: 0,
      warnings: 0,
    },
  };
}

/**
 * Returns a response with build errors and warnings.
 * Represents a solution with compilation issues.
 * 
 * @returns {Object} Project info with non-zero error/warning counts
 */
export function getBuildStatusWithErrorsResponse() {
  return {
    solution: {
      name: 'BrokenBuild',
      path: '/home/user/projects/BrokenBuild/BrokenBuild.sln',
      projectCount: 2,
    },
    projects: [
      {
        name: 'BrokenApp',
        path: '/home/user/projects/BrokenBuild/BrokenApp/BrokenApp.csproj',
        type: 'C# Project',
        targetFramework: 'net8.0',
        buildStatus: 'Error',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
      {
        name: 'BrokenLib',
        path: '/home/user/projects/BrokenBuild/BrokenLib/BrokenLib.csproj',
        type: 'C# Project',
        targetFramework: 'net8.0',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
    ],
    workspace: {
      rootPath: '/home/user/projects/BrokenBuild',
      gitBranch: 'bugfix/build-issue',
    },
    buildStatus: {
      lastBuild: '2024-07-15T09:15:00Z',
      isBuilding: false,
      errors: 3,
      warnings: 7,
    },
  };
}

/**
 * Returns a response for a solution that is currently building.
 * Represents in-progress build state.
 * 
 * @returns {Object} Project info with isBuilding=true
 */
export function getBuildingStatusResponse() {
  return {
    solution: {
      name: 'ActiveBuild',
      path: '/home/user/projects/ActiveBuild/ActiveBuild.sln',
      projectCount: 1,
    },
    projects: [
      {
        name: 'ActiveApp',
        path: '/home/user/projects/ActiveBuild/ActiveApp/ActiveApp.csproj',
        type: 'C# Project',
        targetFramework: 'net8.0',
        buildStatus: 'Building',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
    ],
    workspace: {
      rootPath: '/home/user/projects/ActiveBuild',
      gitBranch: 'feature/new-feature',
    },
    buildStatus: {
      lastBuild: '2024-07-15T10:25:00Z',
      isBuilding: true,
      errors: 0,
      warnings: 0,
    },
  };
}

/**
 * Returns a response without git branch info (null).
 * Represents a workspace without git or outside a git repository.
 * 
 * @returns {Object} Project info with gitBranch=null
 */
export function getNoGitBranchResponse() {
  return {
    solution: {
      name: 'NoGitProject',
      path: 'C:\\Users\\Developer\\NoGitProject\\NoGitProject.sln',
      projectCount: 1,
    },
    projects: [
      {
        name: 'NoGitApp',
        path: 'C:\\Users\\Developer\\NoGitProject\\NoGitApp\\NoGitApp.csproj',
        type: 'C# Project',
        targetFramework: 'net472',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
    ],
    workspace: {
      rootPath: 'C:\\Users\\Developer\\NoGitProject',
      gitBranch: null,
    },
    buildStatus: {
      lastBuild: '2024-07-15T11:00:00Z',
      isBuilding: false,
      errors: 0,
      warnings: 0,
    },
  };
}

/**
 * Returns a response with mixed project types.
 * Includes C#, VB.NET, and Web projects.
 * 
 * @returns {Object} Project info with diverse project types
 */
export function getMixedProjectTypesResponse() {
  return {
    solution: {
      name: 'MixedTypesSolution',
      path: '/home/user/projects/Mixed/Mixed.sln',
      projectCount: 3,
    },
    projects: [
      {
        name: 'CSharpApp',
        path: '/home/user/projects/Mixed/CSharpApp/CSharpApp.csproj',
        type: 'C# Project',
        targetFramework: 'net8.0',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
      {
        name: 'VBNetApp',
        path: '/home/user/projects/Mixed/VBNetApp/VBNetApp.vbproj',
        type: 'VB.NET Project',
        targetFramework: 'net8.0',
        buildStatus: 'Ready',
        projectKind: '{F184B08F-C81C-45F6-A57F-5ABD9991F28F}',
      },
      {
        name: 'WebApp',
        path: '/home/user/projects/Mixed/WebApp/WebApp.csproj',
        type: 'Web Application',
        targetFramework: 'net8.0',
        buildStatus: 'Ready',
        projectKind: '{E24C65DC-7377-472B-9ABA-BC803B73C61A}',
      },
    ],
    workspace: {
      rootPath: '/home/user/projects/Mixed',
      gitBranch: 'main',
    },
    buildStatus: {
      lastBuild: '2024-07-15T09:45:00Z',
      isBuilding: false,
      errors: 0,
      warnings: 1,
    },
  };
}

/**
 * Returns a response with .NET Framework targeting.
 * Represents legacy .NET Framework projects.
 * 
 * @returns {Object} Project info with .NET Framework target
 */
export function getNetFrameworkResponse() {
  return {
    solution: {
      name: 'LegacyApp',
      path: '/home/user/projects/Legacy/LegacyApp.sln',
      projectCount: 2,
    },
    projects: [
      {
        name: 'LegacyCore',
        path: '/home/user/projects/Legacy/LegacyCore/LegacyCore.csproj',
        type: 'C# Project',
        targetFramework: 'net472',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
      {
        name: 'LegacyUI',
        path: '/home/user/projects/Legacy/LegacyUI/LegacyUI.csproj',
        type: 'C# Project',
        targetFramework: 'net461',
        buildStatus: 'Ready',
        projectKind: '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}',
      },
    ],
    workspace: {
      rootPath: '/home/user/projects/Legacy',
      gitBranch: 'legacy/maintenance',
    },
    buildStatus: {
      lastBuild: '2024-07-15T08:30:00Z',
      isBuilding: false,
      errors: 0,
      warnings: 5,
    },
  };
}

/**
 * Factory function to create a mock collector instance.
 * Returns a collector-like object with getProjectInfo method.
 * 
 * @param {Object} overrides Optional response overrides
 * @returns {Object} Mock collector object
 */
export function createMockCollector(overrides = {}) {
  const defaultResponse = getValidProjectInfoResponse();
  const response = { ...defaultResponse, ...overrides };

  return {
    getProjectInfo: () => response,
    GetProjectInfo: () => response, // C# style
  };
}

/**
 * Factory function to create a mock collector that throws an error.
 * Simulates collection failures for error-handling tests.
 * 
 * @param {Error} error The error to throw
 * @returns {Object} Mock collector that throws
 */
export function createFailingCollector(error = new Error('Collection failed')) {
  return {
    getProjectInfo: () => {
      throw error;
    },
    GetProjectInfo: () => {
      throw error;
    },
  };
}

/**
 * Factory function to create a mock collector with a specific response.
 * Useful for parameterized tests with different scenarios.
 * 
 * @param {Object} response The response to return
 * @returns {Object} Mock collector with custom response
 */
export function createCustomCollector(response) {
  if (!response || typeof response !== 'object') {
    throw new Error('response must be a valid object');
  }

  return {
    getProjectInfo: () => response,
    GetProjectInfo: () => response,
  };
}

/**
 * Returns a minimal/edge-case response with missing or null fields.
 * Tests robustness of normalization logic.
 * 
 * @returns {Object} Project info with missing/null optional fields
 */
export function getMinimalResponse() {
  return {
    solution: {
      name: 'Minimal',
      path: '',
      projectCount: 0,
    },
    projects: [],
    workspace: {
      rootPath: '',
      gitBranch: null,
    },
    buildStatus: {
      lastBuild: null,
      isBuilding: false,
      errors: 0,
      warnings: 0,
    },
  };
}

/**
 * Returns all available test fixtures as an array.
 * Useful for parameterized/bulk testing scenarios.
 * 
 * @returns {Array<Object>} Array of all fixture responses
 */
export function getAllFixtures() {
  return [
    getValidProjectInfoResponse(),
    getSingleProjectResponse(),
    getEmptySolutionResponse(),
    getBuildStatusWithErrorsResponse(),
    getBuildingStatusResponse(),
    getNoGitBranchResponse(),
    getMixedProjectTypesResponse(),
    getNetFrameworkResponse(),
    getMinimalResponse(),
  ];
}

/**
 * Exports all fixtures as default for convenience.
 */
export default {
  getValidProjectInfoResponse,
  getSingleProjectResponse,
  getEmptySolutionResponse,
  getBuildStatusWithErrorsResponse,
  getBuildingStatusResponse,
  getNoGitBranchResponse,
  getMixedProjectTypesResponse,
  getNetFrameworkResponse,
  getMinimalResponse,
  createMockCollector,
  createFailingCollector,
  createCustomCollector,
  getAllFixtures,
};
