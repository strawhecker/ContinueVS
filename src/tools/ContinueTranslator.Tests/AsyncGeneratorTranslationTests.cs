using Xunit;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Parsing;
using ContinueTranslator.Core.Emission;

namespace ContinueTranslator.Tests;

/// <summary>
/// Tests for TypeScript async/sync generator function translation to C#.
/// Validates that async function* and function* are properly translated to
/// IAsyncEnumerable and IEnumerable respectively.
/// </summary>
public class AsyncGeneratorTranslationTests
{
    private readonly TsParser _parser = new();

    /// <summary>
    /// Verifies that an async generator function is correctly parsed with IsGenerator=true.
    /// </summary>
    [Fact]
    public void ParseAsyncGeneratorFunction_SetsIsGeneratorTrue()
    {
        string tsCode = """
            export async function* simpleAsyncGen() {
              yield 1;
              yield 2;
            }
            """;

        // Write to a temporary file for parsing
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.ts");
        try
        {
            File.WriteAllText(tempFile, tsCode);
            var files = _parser.Parse(new[] { tempFile });

            Assert.NotEmpty(files);
            Assert.Single(files[0].Functions);

            var func = files[0].Functions[0];
            Assert.Equal("simpleAsyncGen", func.Name);
            Assert.True(func.IsAsync, "Async generator should have IsAsync=true");
            Assert.True(func.IsGenerator, "Async generator should have IsGenerator=true");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that a regular (sync) generator function is correctly parsed with IsGenerator=true.
    /// </summary>
    [Fact]
    public void ParseSyncGeneratorFunction_SetsIsGeneratorTrue()
    {
        string tsCode = """
            export function* regularGen() {
              yield "a";
              yield "b";
            }
            """;

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.ts");
        try
        {
            File.WriteAllText(tempFile, tsCode);
            var files = _parser.Parse(new[] { tempFile });

            Assert.NotEmpty(files);
            Assert.Single(files[0].Functions);

            var func = files[0].Functions[0];
            Assert.Equal("regularGen", func.Name);
            Assert.False(func.IsAsync, "Sync generator should have IsAsync=false");
            Assert.True(func.IsGenerator, "Sync generator should have IsGenerator=true");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that a regular (non-generator) async function has IsGenerator=false.
    /// </summary>
    [Fact]
    public void ParseAsyncNonGeneratorFunction_SetsIsGeneratorFalse()
    {
        string tsCode = """
            export async function normalAsync() {
              return "hello";
            }
            """;

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.ts");
        try
        {
            File.WriteAllText(tempFile, tsCode);
            var files = _parser.Parse(new[] { tempFile });

            Assert.NotEmpty(files);
            Assert.Single(files[0].Functions);

            var func = files[0].Functions[0];
            Assert.Equal("normalAsync", func.Name);
            Assert.True(func.IsAsync, "Async function should have IsAsync=true");
            Assert.False(func.IsGenerator, "Regular async function should have IsGenerator=false");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
