#nullable enable

using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections;

namespace ContinueVS.Services
{
    /// <summary>
    /// Interface for adapting DTE (EnvDTE) objects to testable abstractions.
    /// This allows unit tests to mock DTE behavior without loading Visual Studio assemblies.
    /// </summary>
    internal interface IDTEService
    {
        /// <summary>
        /// Gets the current solution, or null if no solution is loaded.
        /// </summary>
        ISolutionAdapter? Solution { get; }
    }

    /// <summary>
    /// Interface for adapting Solution objects.
    /// </summary>
    internal interface ISolutionAdapter
    {
        /// <summary>
        /// Gets the full path to the solution file.
        /// </summary>
        string? FullName { get; }

        /// <summary>
        /// Gets the collection of projects in the solution.
        /// </summary>
        IProjectsAdapter? Projects { get; }

        /// <summary>
        /// Gets the SolutionBuild object for build status queries.
        /// </summary>
        ISolutionBuildAdapter? SolutionBuild { get; }
    }

    /// <summary>
    /// Interface for adapting Projects collection.
    /// </summary>
    internal interface IProjectsAdapter : IEnumerable
    {
        /// <summary>
        /// Gets the number of projects in the collection.
        /// </summary>
        int Count { get; }
    }

    /// <summary>
    /// Interface for adapting individual Project objects.
    /// </summary>
    internal interface IProjectAdapter
    {
        /// <summary>
        /// Gets the project name.
        /// </summary>
        string? Name { get; }

        /// <summary>
        /// Gets the full path to the project file.
        /// </summary>
        string? FullName { get; }

        /// <summary>
        /// Gets the project kind GUID (e.g., C# project GUID).
        /// </summary>
        string? Kind { get; }

        /// <summary>
        /// Gets the project properties collection.
        /// </summary>
        IPropertiesAdapter? Properties { get; }

        /// <summary>
        /// Gets the project's configuration manager.
        /// </summary>
        IConfigurationManagerAdapter? ConfigurationManager { get; }
    }

    /// <summary>
    /// Interface for adapting Properties collection.
    /// </summary>
    internal interface IPropertiesAdapter
    {
        /// <summary>
        /// Gets a property by name.
        /// </summary>
        IPropertyAdapter? Item(string name);
    }

    /// <summary>
    /// Interface for adapting individual Property objects.
    /// </summary>
    internal interface IPropertyAdapter
    {
        /// <summary>
        /// Gets the property value.
        /// </summary>
        object? Value { get; }
    }

    /// <summary>
    /// Interface for adapting ConfigurationManager.
    /// </summary>
    internal interface IConfigurationManagerAdapter
    {
        /// <summary>
        /// Gets the active configuration.
        /// </summary>
        IConfigurationAdapter? ActiveConfiguration { get; }
    }

    /// <summary>
    /// Interface for adapting Configuration.
    /// </summary>
    internal interface IConfigurationAdapter
    {
        /// <summary>
        /// Gets the platform name.
        /// </summary>
        string? PlatformName { get; }
    }

    /// <summary>
    /// Interface for adapting SolutionBuild.
    /// </summary>
    internal interface ISolutionBuildAdapter
    {
        /// <summary>
        /// Gets the last build status (0 = succeeded, non-zero = failed).
        /// </summary>
        int LastBuildInfo { get; }

        /// <summary>
        /// Gets whether the solution is currently building.
        /// </summary>
        bool Building { get; }
    }

    /// <summary>
    /// Production implementation of IDTEService that wraps the real DTE object.
    /// </summary>
    internal sealed class DTEService : IDTEService
    {
        private readonly DTE _dte;

        public DTEService(DTE dte)
        {
            if (dte == null) throw new ArgumentNullException(nameof(dte));
            _dte = dte;
        }

        public ISolutionAdapter? Solution
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var solution = _dte.Solution;
                return solution != null ? new SolutionAdapter(solution) : null;
            }
        }
    }

    /// <summary>
    /// Production implementation of ISolutionAdapter that wraps a Solution object.
    /// </summary>
    internal sealed class SolutionAdapter : ISolutionAdapter
    {
        private readonly Solution _solution;

        public SolutionAdapter(Solution solution)
        {
            if (solution == null) throw new ArgumentNullException(nameof(solution));
            _solution = solution;
        }

        public string? FullName
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _solution.FullName;
            }
        }

        public IProjectsAdapter? Projects
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var projects = _solution.Projects;
                return projects != null ? new ProjectsAdapter(projects) : null;
            }
        }

        public ISolutionBuildAdapter? SolutionBuild
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    var sb = _solution.SolutionBuild;
                    return sb != null ? new SolutionBuildAdapter(sb) : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Production implementation of IProjectsAdapter that wraps a Projects collection.
    /// </summary>
    internal sealed class ProjectsAdapter : IProjectsAdapter
    {
        private readonly Projects _projects;

        public ProjectsAdapter(Projects projects)
        {
            if (projects == null) throw new ArgumentNullException(nameof(projects));
            _projects = projects;
        }

        public int Count
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _projects.Count;
            }
        }

        public IEnumerator GetEnumerator()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Project project in _projects)
            {
                yield return new ProjectAdapter(project);
            }
        }
    }

    /// <summary>
    /// Production implementation of IProjectAdapter that wraps a Project object.
    /// </summary>
    internal sealed class ProjectAdapter : IProjectAdapter
    {
        private readonly Project _project;

        public ProjectAdapter(Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            _project = project;
        }

        public string? Name
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _project.Name;
            }
        }

        public string? FullName
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _project.FullName;
            }
        }

        public string? Kind
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return _project.Kind;
            }
        }

        public IPropertiesAdapter? Properties
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    var props = _project.Properties;
                    return props != null ? new PropertiesAdapter(props) : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public IConfigurationManagerAdapter? ConfigurationManager
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    var cm = _project.ConfigurationManager;
                    return cm != null ? new ConfigurationManagerAdapter(cm) : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Production implementation of IPropertiesAdapter.
    /// </summary>
    internal sealed class PropertiesAdapter : IPropertiesAdapter
    {
        private readonly Properties _properties;

        public PropertiesAdapter(Properties properties)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            _properties = properties;
        }

        public IPropertyAdapter? Item(string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var prop = _properties.Item(name);
                return prop != null ? new PropertyAdapter(prop) : null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Production implementation of IPropertyAdapter.
    /// </summary>
    internal sealed class PropertyAdapter : IPropertyAdapter
    {
        private readonly Property _property;

        public PropertyAdapter(Property property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            _property = property;
        }

        public object? Value
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    return _property.Value;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Production implementation of IConfigurationManagerAdapter.
    /// </summary>
    internal sealed class ConfigurationManagerAdapter : IConfigurationManagerAdapter
    {
        private readonly ConfigurationManager _configManager;

        public ConfigurationManagerAdapter(ConfigurationManager configManager)
        {
            if (configManager == null) throw new ArgumentNullException(nameof(configManager));
            _configManager = configManager;
        }

        public IConfigurationAdapter? ActiveConfiguration
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    var config = _configManager.ActiveConfiguration;
                    return config != null ? new ConfigurationAdapter(config) : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Production implementation of IConfigurationAdapter.
    /// </summary>
    internal sealed class ConfigurationAdapter : IConfigurationAdapter
    {
        private readonly Configuration _configuration;

        public ConfigurationAdapter(Configuration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            _configuration = configuration;
        }

        public string? PlatformName
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    return _configuration.PlatformName;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Production implementation of ISolutionBuildAdapter.
    /// </summary>
    internal sealed class SolutionBuildAdapter : ISolutionBuildAdapter
    {
        private readonly SolutionBuild _solutionBuild;

        public SolutionBuildAdapter(SolutionBuild solutionBuild)
        {
            if (solutionBuild == null) throw new ArgumentNullException(nameof(solutionBuild));
            _solutionBuild = solutionBuild;
        }

        public int LastBuildInfo
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    return _solutionBuild.LastBuildInfo;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public bool Building
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                try
                {
                    // Note: SolutionBuild doesn't directly expose a "Building" property
                    // This could be extended in the future with event-based tracking
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
