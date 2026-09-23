using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

#nullable enable

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Reproduction test: get_workspace_files / grep / glob must return real files.
    /// Exercises the REAL VsIdeService.GetWorkspaceFiles path (not a mock).
    /// </summary>
    public class GetWorkspaceFilesReproTests
    {
        private class StubDteProvider : IDteProvider
        {
            public string ActiveFilepath { get; set; } = string.Empty;
            public string? SolutionDir { get; set; }

            public string GetActiveFilepath() => ActiveFilepath;
            public string GetSolutionDirectory() => SolutionDir ?? string.Empty;
            public string GetSelectedText() => string.Empty;
            public string GetActiveDocumentContent() => string.Empty;
            public List<string> GetRecentFiles(int maxCount) => new List<string>();
            public Selection? GetCursorSelection() => null;
        }

        [Fact]
        public void GetWorkspaceFiles_ReturnsFiles_FromRealTree()
        {
            var root = Path.Combine(Path.GetTempPath(), "wsrepro_" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "src", "ProjA"));
            File.WriteAllText(Path.Combine(root, "src", "ProjA", "Class1.cs"), "public class Class1 { }");
            File.WriteAllText(Path.Combine(root, "README.md"), "readme");

            try
            {
                // Simulate the repo being reported as the workspace root via git root.
                // The stub's ActiveFilepath drives ResolveGitWorkingDir -> git rev-parse.
                var stub = new StubDteProvider { ActiveFilepath = root };
                var sut = new VsIdeService(stub);

                // Force RootDir resolution to our temp tree by pointing CWD at it too.
                var previous = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(root);
                try
                {
                    // Also stub the solution dir so ResolveWorkspaceRoot reliably uses our tree.
                    var stub2 = new StubDteProvider { SolutionDir = root };
                    var sut2 = new VsIdeService(stub2);

                    var files = sut2.GetWorkspaceFiles("*.cs").ToList();

                    Assert.NotEmpty(files);
                    Assert.Contains(files, f => f.EndsWith("Class1.cs", StringComparison.OrdinalIgnoreCase));

                    // Regression: recursive "**/" globs must also find files. Before the fix,
                    // the raw glob (containing a path separator) was passed to
                    // Directory.EnumerateFiles, which rejects it, so these returned empty.
                    var recursive = sut2.GetWorkspaceFiles("**/*.cs").ToList();
                    Assert.NotEmpty(recursive);
                    Assert.Contains(recursive, f => f.EndsWith("Class1.cs", StringComparison.OrdinalIgnoreCase));

                    var deep = sut2.GetWorkspaceFiles("**/src/**/*.cs").ToList();
                    Assert.NotEmpty(deep);
                    Assert.Contains(deep, f => f.EndsWith("Class1.cs", StringComparison.OrdinalIgnoreCase));
                }
                finally
                {
                    Directory.SetCurrentDirectory(previous);
                }
            }
            finally
            {
                DeleteDirectoryWithRetry(root);
            }
        }

        /// <summary>
        /// Recursively deletes a directory, retrying a few times on transient Windows
        /// file-lock errors. Handles used by scanning/AV/indexing can be released moments
        /// after a test finishes, so a single blind delete is flaky.
        /// </summary>
        private static void DeleteDirectoryWithRetry(string path)
        {
            const int maxAttempts = 5;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= maxAttempts - 1)
                        throw;
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt >= maxAttempts - 1)
                        throw;
                }

                System.Threading.Thread.Sleep(100 * (attempt + 1));
            }
        }
    }
}
