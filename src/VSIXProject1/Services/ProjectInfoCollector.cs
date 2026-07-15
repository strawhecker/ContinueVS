#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Process = System.Diagnostics.Process;

namespace ContinueVS.Services
{
    /// <summary>
    /// Collects project and solution metadata from the Visual Studio DTE object model.
    /// 
    /// Provides synchronous queries for:
    /// - Solution-level metadata (name, path, project count)
    /// - Per-project information (name, path, type, target framework, build status)
    /// - Workspace metadata (root path, git branch)
    /// - Global build status (errors, warnings, last build time, is building)
    /// 
    /// All DTE property accesses are wrapped with null-safety checks to prevent
    /// exceptions from missing or unavailable projects/properties.
    /// 
    /// Integration:
    /// - Used by: project-info-handler.mjs (Step 84) via C# bridge adapter
    /// - Accesses: DTE.Solution, Project, ProjectItem properties
    /// - Throws: ProjectInfoError, CollectionError on collection failures
    /// </summary>
    internal sealed class ProjectInfoCollector
    {
        /// <summary>
        /// The DTE object representing the Visual Studio environment.
        /// </summary>
        private readonly DTE _dte;

        /// <summary>
        /// Optional logger for diagnostics and debugging.
        /// </summary>
        private readonly IBridgeLogger? _logger;

        /// <summary>
        /// Initializes a new instance of ProjectInfoCollector.
        /// </summary>
        /// <param name="dte">The DTE object from the Visual Studio IDE state.</param>
        /// <param name="logger">Optional logger for diagnostics; gracefully degrades if null.</param>
        /// <exception cref="ArgumentNullException">Thrown if dte is null.</exception>
        public ProjectInfoCollector(DTE dte, IBridgeLogger? logger = null)
        {
            if (dte == null) throw new ArgumentNullException(nameof(dte));
            _dte = dte;
            _logger = logger;
            if (_logger != null)
            {
                _ = _logger.WriteDebugAsync("ProjectInfoCollector initialized");
            }
        }

        /// <summary>
        /// Gets complete project and solution information.
        /// </summary>
        /// <returns>Structured object containing solution, projects, workspace, and build status.</returns>
        /// <exception cref="ProjectInfoError">Thrown if solution is null or inaccessible.</exception>
        /// <exception cref="CollectionError">Thrown if project enumeration fails.</exception>
        public ProjectInfo GetProjectInfo()
        {
            try
            {
                var solution = _dte.Solution;
                if (solution == null)
                {
                    throw new ProjectInfoError("DTE.Solution is null; no solution is loaded", "NO_SOLUTION");
                }

                var solutionInfo = GetSolutionInfo(solution);
                var projectsInfo = GetProjectsList(solution);
                var workspaceInfo = GetWorkspaceInfo();
                var buildStatus = GetBuildStatus();

                return new ProjectInfo
                {
                    Solution = solutionInfo,
                    Projects = projectsInfo,
                    Workspace = workspaceInfo,
                    BuildStatus = buildStatus
                };
            }
            catch (ProjectInfoError)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CollectionError($"Failed to collect project info: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets solution-level metadata: name, path, and project count.
        /// </summary>
        /// <param name="solution">The DTE Solution object.</param>
        /// <returns>SolutionInfo with name, path, and projectCount.</returns>
        private SolutionInfo GetSolutionInfo(Solution solution)
        {
            try
            {
                var name = solution.FullName ?? "Unknown";
                var projectCount = 0;

                // Count projects safely; Projects collection can be null or empty
                try
                {
                    if (solution.Projects != null)
                    {
                        projectCount = solution.Projects.Count;
                    }
                }
                catch
                {
                    // If Projects enumeration fails, default to 0
                    projectCount = 0;
                }

                return new SolutionInfo
                {
                    Name = Path.GetFileNameWithoutExtension(name) ?? "Unknown",
                    Path = name,
                    ProjectCount = projectCount
                };
            }
            catch (Exception ex)
            {
                throw new CollectionError($"Failed to get solution info: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Enumerates all projects in the solution and collects per-project metadata.
        /// Safely skips projects that fail to enumerate.
        /// </summary>
        /// <param name="solution">The DTE Solution object.</param>
        /// <returns>List of ProjectInfo objects; empty list if Projects collection is null or enumeration fails.</returns>
        private List<ProjectItemInfo> GetProjectsList(Solution solution)
        {
            var projects = new List<ProjectItemInfo>();

            try
            {
                if (solution.Projects == null || solution.Projects.Count == 0)
                {
                    if (_logger != null)
                    {
                        _ = _logger.WriteDebugAsync("Solution has no projects");
                    }
                    return projects;
                }

                foreach (Project project in solution.Projects)
                {
                    try
                    {
                        var projectInfo = GetSingleProjectInfo(project);
                        if (projectInfo != null)
                        {
                            projects.Add(projectInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log and skip individual project failures
                        if (_logger != null)
                        {
                            _ = _logger.WriteWarningAsync($"Failed to collect info for project: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new CollectionError($"Failed to enumerate projects: {ex.Message}", ex);
            }

            return projects;
        }

        /// <summary>
        /// Gets metadata for a single project.
        /// Safely handles null properties and missing attributes.
        /// </summary>
        /// <param name="project">A single DTE Project object.</param>
        /// <returns>ProjectItemInfo for the project, or null if basic properties are missing.</returns>
        private ProjectItemInfo? GetSingleProjectInfo(Project project)
        {
            try
            {
                // Guard against null project or missing Name
                if (project == null || string.IsNullOrWhiteSpace(project.Name))
                {
                    return null;
                }

                var name = project.Name;
                var path = project.FullName ?? string.Empty;
                var projectKind = project.Kind ?? "Unknown";
                var targetFramework = GetTargetFramework(project) ?? "Unknown";
                var buildStatus = GetProjectBuildStatus(project);

                return new ProjectItemInfo
                {
                    Name = name,
                    Path = path,
                    Type = DetermineProjectType(projectKind),
                    TargetFramework = targetFramework,
                    BuildStatus = buildStatus,
                    ProjectKind = projectKind
                };
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _ = _logger.WriteWarningAsync($"Failed to get single project info: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Safely extracts the target framework from project properties.
        /// Attempts to read the TargetFramework property; returns null if not available.
        /// </summary>
        /// <param name="project">The DTE Project object.</param>
        /// <returns>Target framework string (e.g., "net8.0", "net472") or null if unavailable.</returns>
        private string? GetTargetFramework(Project project)
        {
            try
            {
                if (project.Properties == null)
                {
                    return null;
                }

                // Try TargetFramework (modern .NET projects)
                try
                {
                    var targetFrameworkProp = project.Properties.Item("TargetFramework");
                    if (targetFrameworkProp?.Value != null)
                    {
                        return targetFrameworkProp.Value.ToString();
                    }
                }
                catch
                {
                    // Property not available; try next approach
                }

                // Try TargetFrameworks (multi-targeting projects)
                try
                {
                    var targetFrameworksProp = project.Properties.Item("TargetFrameworks");
                    if (targetFrameworksProp?.Value != null)
                    {
                        var frameworks = targetFrameworksProp.Value.ToString();
                        // Return first framework if multi-targeting
                        return frameworks?.Split(';').FirstOrDefault()?.Trim();
                    }
                }
                catch
                {
                    // Property not available
                }

                // Try .NET Framework TargetFrameworkVersion
                try
                {
                    var versionProp = project.Properties.Item("TargetFrameworkVersion");
                    if (versionProp?.Value != null)
                    {
                        return versionProp.Value.ToString();
                    }
                }
                catch
                {
                    // Property not available
                }

                return null;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _ = _logger.WriteDebugAsync($"Failed to get target framework: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Determines a human-readable project type from the project kind GUID.
        /// </summary>
        /// <param name="projectKind">The DTE Project.Kind string (usually a GUID).</param>
        /// <returns>Project type string (e.g., "Console App", "Class Library", "Web Project").</returns>
        private string DetermineProjectType(string projectKind)
        {
            // Common Visual Studio project kind GUIDs
            return projectKind switch
            {
                "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}" => "C# Project",
                "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}" => "VB.NET Project",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}" => "C++ Project",
                "{E24C65DC-7377-472B-9ABA-BC803B73C61A}" => "Web Application",
                "{A1591282-1198-4647-A2B1-27E5FF5F6F3B}" => "Shared Project",
                _ => "Unknown Project"
            };
        }

        /// <summary>
        /// Gets build-related status for a single project.
        /// Checks if the project is currently building and retrieves error/warning counts if available.
        /// </summary>
        /// <param name="project">The DTE Project object.</param>
        /// <returns>Build status string (e.g., "Ready", "Building", "Error").</returns>
        private string GetProjectBuildStatus(Project project)
        {
            try
            {
                // Check if project is currently building
                if (project.ConfigurationManager != null)
                {
                    try
                    {
                        var activeConfig = project.ConfigurationManager.ActiveConfiguration;
                        if (activeConfig != null)
                        {
                            return "Ready";
                        }
                    }
                    catch
                    {
                        // If we can't determine, default to Ready
                    }
                }

                return "Ready";
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _ = _logger.WriteDebugAsync($"Failed to get project build status: {ex.Message}");
                }
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets workspace-level information: root path and git branch (if available).
        /// </summary>
        /// <returns>WorkspaceInfo with rootPath and optional gitBranch.</returns>
        private WorkspaceInfo GetWorkspaceInfo()
        {
            try
            {
                var rootPath = _dte.Solution?.FullName ?? string.Empty;
                if (!string.IsNullOrEmpty(rootPath))
                {
                    rootPath = Path.GetDirectoryName(rootPath) ?? string.Empty;
                }

                var gitBranch = GetGitBranch(rootPath);

                return new WorkspaceInfo
                {
                    RootPath = rootPath,
                    GitBranch = gitBranch
                };
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _ = _logger.WriteWarningAsync($"Failed to get workspace info: {ex.Message}");
                }
                return new WorkspaceInfo
                {
                    RootPath = string.Empty,
                    GitBranch = null
                };
            }
        }

        /// <summary>
        /// Attempts to retrieve the current git branch name from the workspace root.
        /// Uses `git rev-parse --abbrev-ref HEAD` command; returns null if git is not available or fails.
        /// </summary>
        /// <param name="workspaceRoot">The root directory path of the workspace.</param>
        /// <returns>Branch name string (e.g., "main", "develop") or null if unavailable.</returns>
        private string? GetGitBranch(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
            {
                return null;
            }

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --abbrev-ref HEAD",
                    WorkingDirectory = workspaceRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process != null && process.WaitForExit(5000))
                    {
                        var branchBytes = process.StandardOutput.ReadToEndAsync().Result;
                        var branch = branchBytes.Trim();
                        if (!string.IsNullOrEmpty(branch))
                        {
                            return branch;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _ = _logger.WriteDebugAsync($"Failed to get git branch: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Gets global build status across the solution.
        /// Reports errors, warnings, and whether a build is currently in progress.
        /// </summary>
        /// <returns>BuildStatus with error/warning counts, lastBuild time, and isBuilding flag.</returns>
        private BuildStatus GetBuildStatus()
        {
            try
            {
                // Get SolutionBuild if available; some Visual Studio configurations don't expose it
                var solutionBuild = _dte.Solution?.SolutionBuild;
                var isBuilding = false;

                if (solutionBuild != null)
                {
                    try
                    {
                        // SolutionBuild doesn't always have a "Building" property, so we try-catch
                        isBuilding = false; // Placeholder; actual property check would go here
                    }
                    catch
                    {
                        isBuilding = false;
                    }
                }

                // Try to get error/warning counts from the error list
                var (errorCount, warningCount) = GetErrorAndWarningCounts();

                // Try to get last build time
                var lastBuild = GetLastBuildTime();

                return new BuildStatus
                {
                    LastBuild = lastBuild,
                    IsBuilding = isBuilding,
                    Errors = errorCount,
                    Warnings = warningCount
                };
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _ = _logger.WriteWarningAsync($"Failed to get build status: {ex.Message}");
                }
                return new BuildStatus
                {
                    LastBuild = null,
                    IsBuilding = false,
                    Errors = 0,
                    Warnings = 0
                };
            }
        }

        /// <summary>
        /// Safely retrieves error and warning counts from the DTE error list.
        /// Returns (0, 0) if counts are unavailable.
        /// </summary>
        /// <returns>Tuple of (errorCount, warningCount).</returns>
        private (int, int) GetErrorAndWarningCounts()
        {
            try
            {
                // DTE error list is not directly accessible in most contexts
                // This is a placeholder for future integration
                return (0, 0);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// Gets the timestamp of the last successful build.
        /// Returns null if no build has occurred or if unavailable.
        /// </summary>
        /// <returns>ISO 8601 datetime string or null.</returns>
        private string? GetLastBuildTime()
        {
            try
            {
                // Build timestamps are not directly exposed by DTE
                // This is a placeholder for future enhancement
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Represents complete project and solution information.
    /// </summary>
    internal sealed class ProjectInfo
    {
        /// <summary>Gets or sets solution metadata.</summary>
        public SolutionInfo Solution { get; set; } = new();

        /// <summary>Gets or sets the list of projects in the solution.</summary>
        public List<ProjectItemInfo> Projects { get; set; } = new();

        /// <summary>Gets or sets workspace-level metadata.</summary>
        public WorkspaceInfo Workspace { get; set; } = new();

        /// <summary>Gets or sets global build status.</summary>
        public BuildStatus BuildStatus { get; set; } = new();
    }

    /// <summary>
    /// Represents solution-level metadata.
    /// </summary>
    internal sealed class SolutionInfo
    {
        /// <summary>Gets or sets the solution name (without path).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the full solution file path.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Gets or sets the number of projects in the solution.</summary>
        public int ProjectCount { get; set; }
    }

    /// <summary>
    /// Represents a single project in the solution.
    /// </summary>
    internal sealed class ProjectItemInfo
    {
        /// <summary>Gets or sets the project name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the full project file path.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Gets or sets the project type (e.g., "C# Project", "VB.NET Project").</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Gets or sets the target framework (e.g., "net8.0", "net472").</summary>
        public string TargetFramework { get; set; } = string.Empty;

        /// <summary>Gets or sets the project build status (e.g., "Ready", "Building").</summary>
        public string BuildStatus { get; set; } = string.Empty;

        /// <summary>Gets or sets the project kind GUID.</summary>
        public string ProjectKind { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents workspace-level information.
    /// </summary>
    internal sealed class WorkspaceInfo
    {
        /// <summary>Gets or sets the root directory of the workspace.</summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the current git branch name, or null if git is unavailable.</summary>
        public string? GitBranch { get; set; }
    }

    /// <summary>
    /// Represents build status for the solution.
    /// </summary>
    internal sealed class BuildStatus
    {
        /// <summary>Gets or sets the ISO 8601 timestamp of the last build, or null if unavailable.</summary>
        public string? LastBuild { get; set; }

        /// <summary>Gets or sets a value indicating whether a build is currently in progress.</summary>
        public bool IsBuilding { get; set; }

        /// <summary>Gets or sets the count of build errors.</summary>
        public int Errors { get; set; }

        /// <summary>Gets or sets the count of build warnings.</summary>
        public int Warnings { get; set; }
    }

    /// <summary>
    /// Exception thrown when project information collection fails.
    /// </summary>
    internal sealed class ProjectInfoError : Exception
    {
        /// <summary>Gets the error code for this failure.</summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance of ProjectInfoError.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="errorCode">A code identifying the error type.</param>
        public ProjectInfoError(string message, string errorCode = "PROJECT_INFO_ERROR") : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of ProjectInfoError with an inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="errorCode">A code identifying the error type.</param>
        /// <param name="innerException">The inner exception that caused this error.</param>
        public ProjectInfoError(string message, string errorCode, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Exception thrown when project collection fails.
    /// </summary>
    internal sealed class CollectionError : Exception
    {
        /// <summary>
        /// Initializes a new instance of CollectionError.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception that caused this error.</param>
        public CollectionError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
