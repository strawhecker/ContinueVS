using ContinueTranslator.Cli;
using Xunit;

namespace ContinueTranslator.Tests;

/// <summary>
/// Tests for RepoScanner path filtering logic to ensure test folders are correctly excluded
/// while legitimate directories named "Test" are included.
/// </summary>
public class RepoScannerPathFilteringTests
{
    /// <summary>
    /// Verifies that paths containing ".test.ts" files are excluded.
    /// </summary>
    [Fact]
    public void IsIncluded_ExcludesTestTsFiles()
    {
        var result = RepoScanner.IsIncluded("/path/to/file.test.ts");
        Assert.False(result, "Should exclude .test.ts files");
    }

    /// <summary>
    /// Verifies that paths containing ".vitest.ts" files are excluded.
    /// </summary>
    [Fact]
    public void IsIncluded_ExcludesVitestTsFiles()
    {
        var result = RepoScanner.IsIncluded("/path/to/file.vitest.ts");
        Assert.False(result, "Should exclude .vitest.ts files");
    }

    /// <summary>
    /// Verifies that paths containing "__tests__" directory are excluded.
    /// </summary>
    [Fact]
    public void IsIncluded_ExcludesTestsUnderscoreDirectory()
    {
        var result = RepoScanner.IsIncluded("/path/__tests__/file.ts");
        Assert.False(result, "Should exclude __tests__ directories");
    }

    /// <summary>
    /// Verifies that root-level "test" or "tests" directories are excluded.
    /// These are top-level directories within core/ that are meant for testing.
    /// </summary>
    [Theory]
    [InlineData("/home/user/Continue/core/test/index.ts")]
    [InlineData("/home/user/Continue/core/tests/index.ts")]
    [InlineData(@"C:\repo\Continue\core\test\index.ts")]
    [InlineData(@"C:\repo\Continue\core\tests\index.ts")]
    public void IsIncluded_ExcludesRootLevelTestDirectories(string path)
    {
        var result = RepoScanner.IsIncluded(path);
        Assert.False(result, $"Should exclude root-level test/tests directories: {path}");
    }

    /// <summary>
    /// Verifies that directories named "Test" within legitimate paths are NOT excluded.
    /// This is the fix for the reported issue with RootPathContext/Test.
    /// </summary>
    [Theory]
    [InlineData("/core/Autocomplete/Context/RootPathContext/Test/file.ts")]
    [InlineData(@"C:\repo\core\Autocomplete\Context\RootPathContext\Test\file.ts")]
    [InlineData("/core/components/Button/Test/index.ts")]
    [InlineData("/src/utils/helpers/Test/utilities.ts")]
    public void IsIncluded_IncludesTestDirectoriesWithinPaths(string path)
    {
        var result = RepoScanner.IsIncluded(path);
        Assert.True(result, $"Should include legitimate paths with 'Test' directories: {path}");
    }

    /// <summary>
    /// Verifies that "extensions" directories are excluded.
    /// </summary>
    [Theory]
    [InlineData("/extensions/vscode/file.ts")]
    [InlineData("/core/extensions/plugin/file.ts")]
    public void IsIncluded_ExcludesExtensionsDirectories(string path)
    {
        var result = RepoScanner.IsIncluded(path);
        Assert.False(result, $"Should exclude extensions directories: {path}");
    }

    /// <summary>
    /// Verifies that "gui" directories are excluded.
    /// </summary>
    [Fact]
    public void IsIncluded_ExcludesGuiDirectories()
    {
        var result = RepoScanner.IsIncluded("/gui/components/file.ts");
        Assert.False(result, "Should exclude gui directories");
    }

    /// <summary>
    /// Verifies that "vendor" directories are excluded.
    /// </summary>
    [Fact]
    public void IsIncluded_ExcludesVendorDirectories()
    {
        var result = RepoScanner.IsIncluded("/vendor/lib/file.ts");
        Assert.False(result, "Should exclude vendor directories");
    }

    /// <summary>
    /// Verifies that legitimate source files are included.
    /// </summary>
    [Theory]
    [InlineData("/core/src/index.ts")]
    [InlineData("/core/autocomplete/provider.ts")]
    [InlineData("/core/components/Button/Button.ts")]
    public void IsIncluded_IncludesLegitimateSourceFiles(string path)
    {
        var result = RepoScanner.IsIncluded(path);
        Assert.True(result, $"Should include legitimate source files: {path}");
    }
}
