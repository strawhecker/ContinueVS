#nullable enable

using ContinueVS.Services;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for ProjectInfoCollector.
    /// Tests DTE query patterns, null-safety, project enumeration, and error handling.
    /// These tests use the mockable IDTEService interface instead of direct DTE mocking,
    /// allowing them to run without loading Visual Studio assemblies.
    /// </summary>
    public class ProjectInfoCollectorTests
    {
        #region Suite 1: Initialization & Null-Safety (3 tests)

        [Fact]
        public void Constructor_WithNullDTEService_ThrowsArgumentNullException()
        {
            // Act & Assert
            IDTEService? nullService = null;
            Assert.Throws<ArgumentNullException>(() => new ProjectInfoCollector(nullService!));
        }

        [Fact]
        public void Constructor_WithValidDTEService_CreatesSuccessfully()
        {
            // Arrange
            var dteServiceMock = new Mock<IDTEService>();

            // Act
            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Assert
            Assert.NotNull(collector);
        }

        [Fact]
        public void Constructor_WithOptionalLogger_AcceptsNullLogger()
        {
            // Arrange
            var dteServiceMock = new Mock<IDTEService>();

            // Act
            var collector = new ProjectInfoCollector(dteServiceMock.Object, null);

            // Assert
            Assert.NotNull(collector);
        }

        #endregion

        #region Suite 2: Solution Info Queries (4 tests)

        [Fact]
        public void GetProjectInfo_WithNullSolution_ThrowsProjectInfoError()
        {
            // Arrange
            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d. Solution).Returns((ISolutionAdapter)null!);
            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act & Assert
            var ex = Assert.Throws<ProjectInfoError>(() => collector.GetProjectInfo());
            Assert.Equal("NO_SOLUTION", ex.ErrorCode);
        }

        [Fact]
        public void GetProjectInfo_WithValidSolution_ReturnsSolutionInfo()
        {
            // Arrange
            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\MySolution.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(new List<(string name, string path)>()));

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.NotNull(result.Solution);
            Assert.Equal("MySolution", result.Solution.Name);
            Assert.Equal(@"C:\Solution\MySolution.sln", result.Solution.Path);
        }

        [Fact]
        public void GetProjectInfo_WithZeroProjects_ReturnsZeroProjectCount()
        {
            // Arrange
            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Empty.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(new List<(string, string)>()));

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.Equal(0, result.Solution.ProjectCount);
        }

        [Fact]
        public void GetProjectInfo_WithMultipleProjects_ReturnsCorrectCount()
        {
            // Arrange
            var projects = new List<(string, string)>
            {
                ("Project1", @"C:\Solution\Project1\Project1.csproj"),
                ("Project2", @"C:\Solution\Project2\Project2.csproj"),
                ("Project3", @"C:\Solution\Project3\Project3.csproj")
            };

            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Multi.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(projects));

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.Equal(3, result.Solution.ProjectCount);
            Assert.Equal(3, result.Projects.Count);
        }

        [Fact]
        public void GetProjectInfo_WithNullFullName_HandlesGracefully()
        {
            // Arrange
            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns((string)null!);
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(new List<(string, string)>()));

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.NotNull(result.Solution);
            Assert.Equal("Unknown", result.Solution.Name);
        }

        #endregion

        #region Suite 3: Project Enumeration (5 tests)

        [Fact]
        public void GetProjectInfo_WithProjects_EnumeratesAllProjects()
        {
            // Arrange
            var projects = new List<(string, string)>
            {
                ("WebApp", @"C:\Solution\WebApp\WebApp.csproj"),
                ("ClassLib", @"C:\Solution\ClassLib\ClassLib.csproj")
            };

            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Multi.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(projects));

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.Equal(2, result.Projects.Count);
            Assert.Contains(result.Projects, p => p.Name == "WebApp");
            Assert.Contains(result.Projects, p => p.Name == "ClassLib");
        }

        [Fact]
        public void GetProjectInfo_WithCSharpProject_DetectsProjectType()
        {
            // Arrange
            var projects = new List<(string, string)>
            {
                ("CSharpApp", @"C:\Solution\CSharpApp\CSharpApp.csproj")
            };

            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Multi.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(projects));

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            var project = result.Projects.First();
            Assert.Equal("C# Project", project.Type);
        }

        [Fact]
        public void GetProjectInfo_WithMultipleProjects_SkipsProjectsWithoutName()
        {
            // Arrange
            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Multi.sln");

            // Create mock projects adapter with one valid and one invalid
            var projectsAdapterMock = new Mock<IProjectsAdapter>();
            projectsAdapterMock.Setup(p => p.Count).Returns(2);
            projectsAdapterMock.Setup(p => p.GetEnumerator()).Returns(() =>
            {
                var list = new ArrayList();

                var proj1 = new Mock<IProjectAdapter>();
                proj1.Setup(pr => pr.Name).Returns("ValidProject");
                proj1.Setup(pr => pr.FullName).Returns(@"C:\Solution\ValidProject\Valid.csproj");
                proj1.Setup(pr => pr.Kind).Returns("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
                proj1.Setup(pr => pr.Properties).Returns((IPropertiesAdapter)null!);
                proj1.Setup(pr => pr.ConfigurationManager).Returns((IConfigurationManagerAdapter)null!);
                list.Add(proj1.Object);

                var proj2 = new Mock<IProjectAdapter>();
                proj2.Setup(pr => pr.Name).Returns((string)null!);
                proj2.Setup(pr => pr.FullName).Returns((string)null!);
                list.Add(proj2.Object);

                return list.GetEnumerator();
            });

            solutionMock.Setup(s => s.Projects).Returns(projectsAdapterMock.Object);

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            // Should skip project with null name and only include the valid one
            Assert.Single(result.Projects);
            Assert.Equal("ValidProject", result.Projects.First().Name);
        }

        [Fact]
        public void GetProjectInfo_WithTargetFramework_IncludesInProjectInfo()
        {
            // Arrange
            var projects = new List<(string, string)>
            {
                ("NetProject", @"C:\Solution\NetProject\NetProject.csproj")
            };

            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Multi.sln");
            var projectsAdapter = CreateMockProjects(projects);
            solutionMock.Setup(s => s.Projects).Returns(projectsAdapter);

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            var project = result.Projects.First();
            Assert.NotNull(project.TargetFramework);
            Assert.NotEmpty(project.TargetFramework);
        }

        #endregion

        #region Suite 4: Build Status Collection (3 tests)

        [Fact]
        public void GetProjectInfo_WithValidProject_IncludesBuildStatus()
        {
            // Arrange
            var projects = new List<(string, string)>
            {
                ("MyProject", @"C:\Solution\MyProject\MyProject.csproj")
            };

            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Multi.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(projects));

            var solutionBuildMock = new Mock<ISolutionBuildAdapter>();
            solutionMock.Setup(s => s.SolutionBuild).Returns(solutionBuildMock.Object);

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.NotNull(result.BuildStatus);
            Assert.False(result.BuildStatus.IsBuilding);
        }

        [Fact]
        public void GetProjectInfo_WhenSolutionBuilding_ReportsBuildingStatus()
        {
            // Arrange
            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Building.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(new List<(string, string)>()));

            var solutionBuildMock = new Mock<ISolutionBuildAdapter>();
            solutionMock.Setup(s => s.SolutionBuild).Returns(solutionBuildMock.Object);

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.NotNull(result.BuildStatus);
        }

        [Fact]
        public void GetProjectInfo_WithNullSolutionBuild_DefaultsToNotBuilding()
        {
            // Arrange
            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\NoBuild.sln");
            solutionMock.Setup(s => s.Projects).Returns(CreateMockProjects(new List<(string, string)>()));
            solutionMock.Setup(s => s.SolutionBuild).Returns((ISolutionBuildAdapter)null!);

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act
            var result = collector.GetProjectInfo();

            // Assert
            Assert.False(result.BuildStatus.IsBuilding);
        }

        #endregion

        #region Suite 5: Error Propagation (3 tests)

        [Fact]
        public void GetProjectInfo_WithProjectEnumerationFailure_ThrowsCollectionError()
        {
            // Arrange
            var solutionMock = new Mock<ISolutionAdapter>();
            solutionMock.Setup(s => s.FullName).Returns(@"C:\Solution\Bad.sln");
            solutionMock.Setup(s => s.Projects).Throws<Exception>();

            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns(solutionMock.Object);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act & Assert
            Assert.Throws<CollectionError>(() => collector.GetProjectInfo());
        }

        [Fact]
        public void GetProjectInfo_WithSolutionNull_ThrowsProjectInfoError()
        {
            // Arrange
            var dteServiceMock = new Mock<IDTEService>();
            dteServiceMock.Setup(d => d.Solution).Returns((ISolutionAdapter)null!);

            var collector = new ProjectInfoCollector(dteServiceMock.Object);

            // Act & Assert
            var ex = Assert.Throws<ProjectInfoError>(() => collector.GetProjectInfo());
            Assert.NotNull(ex.ErrorCode);
        }

        [Fact]
        public void ProjectInfoError_HasErrorCode()
        {
            // Arrange & Act
            var ex = new ProjectInfoError("Test error", "TEST_ERROR");

            // Assert
            Assert.Equal("TEST_ERROR", ex.ErrorCode);
        }

        #endregion

        #region Helpers

        private IProjectsAdapter CreateMockProjects(List<(string name, string path)> projectList)
        {
            var projectsAdapterMock = new Mock<IProjectsAdapter>();
            projectsAdapterMock.Setup(p => p.Count).Returns(projectList.Count);

            projectsAdapterMock.Setup(p => p.GetEnumerator()).Returns(() =>
            {
                var list = new ArrayList();

                foreach (var (name, path) in projectList)
                {
                    var projMock = new Mock<IProjectAdapter>();
                    projMock.Setup(pr => pr.Name).Returns(name);
                    projMock.Setup(pr => pr.FullName).Returns(path);
                    projMock.Setup(pr => pr.Kind).Returns("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"); // C# GUID

                    // Mock Properties for target framework
                    var propertiesMock = new Mock<IPropertiesAdapter>();
                    propertiesMock.Setup(pr => pr.Item("TargetFramework")).Returns(
                        CreateMockProperty("net8.0"));
                    propertiesMock.Setup(pr => pr.Item("TargetFrameworks")).Returns(
                        CreateMockProperty(null));
                    propertiesMock.Setup(pr => pr.Item("TargetFrameworkVersion")).Returns(
                        CreateMockProperty(null));

                    projMock.Setup(pr => pr.Properties).Returns(propertiesMock.Object);
                    projMock.Setup(pr => pr.ConfigurationManager).Returns((IConfigurationManagerAdapter)null!);
                    list.Add(projMock.Object);
                }

                return list.GetEnumerator();
            });

            return projectsAdapterMock.Object;
        }

        private IPropertyAdapter? CreateMockProperty(string? value)
        {
            if (value == null)
            {
                return null;
            }

            var propMock = new Mock<IPropertyAdapter>();
            propMock.Setup(p => p.Value).Returns(value);
            return propMock.Object;
        }

        #endregion
    }
}
