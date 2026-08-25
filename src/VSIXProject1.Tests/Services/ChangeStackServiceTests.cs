using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Tests.Services
{
    public class ChangeStackServiceTests
    {
        private static string GetTempFilePath()
        {
            return Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        }

        private static void Cleanup(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public async Task ApplyChange_CreatesBaselineAndWritesFile()
        {
            // Arrange
            var service = new ChangeStackService();
            var stackId = service.CreateChangeStack();
            var filePath = GetTempFilePath();

            try
            {
                var originalContent = "original content";
                File.WriteAllText(filePath, originalContent);

                var change = new CodeChange
                {
                    ChangeId = Guid.NewGuid().ToString(),
                    FilePath = filePath,
                    OldContent = originalContent,
                    NewContent = "new content",
                    Description = "Test change"
                };

                // Act
                await service.ApplyChangeAsync(stackId, change, filePath);

                // Assert
                var stack = service.GetChangeStack(stackId);
                Assert.NotNull(stack);
                Assert.Single(stack.GetChangeHistory());
                Assert.Single(stack.GetAppliedChanges());

                var appliedChange = stack.GetChangeHistory()[0];
                Assert.NotNull(appliedChange.Baseline);
                Assert.Equal(filePath, appliedChange.Baseline.FilePath);
                Assert.Equal(originalContent, appliedChange.Baseline.BaselineContent);
                Assert.Equal("new content", File.ReadAllText(filePath));
            }
            finally
            {
                Cleanup(filePath);
            }
        }

        [Fact]
        public async Task RollbackChange_RestoresFileToBaseline()
        {
            // Arrange
            var service = new ChangeStackService();
            var stackId = service.CreateChangeStack();
            var filePath = GetTempFilePath();

            try
            {
                var originalContent = "original content";
                File.WriteAllText(filePath, originalContent);

                var change = new CodeChange
                {
                    ChangeId = Guid.NewGuid().ToString(),
                    FilePath = filePath,
                    OldContent = originalContent,
                    NewContent = "modified content",
                    Description = "Test change"
                };

                await service.ApplyChangeAsync(stackId, change, filePath);

                // Act
                await service.RollbackChangeAsync(stackId, change.ChangeId);

                // Assert
                var fileContent = File.ReadAllText(filePath);
                Assert.Equal(originalContent, fileContent);

                var stack = service.GetChangeStack(stackId);
                Assert.Empty(stack.GetAppliedChanges());
            }
            finally
            {
                Cleanup(filePath);
            }
        }

        [Fact]
        public async Task RollbackToChange_RevertsOnlyChangesAfter()
        {
            // Arrange
            var service = new ChangeStackService();
            var stackId = service.CreateChangeStack();
            var filePath = GetTempFilePath();

            try
            {
                File.WriteAllText(filePath, "initial content");

                var change1 = new CodeChange
                {
                    ChangeId = "change-1",
                    FilePath = filePath,
                    OldContent = "initial content",
                    NewContent = "after change 1",
                    Description = "Change 1"
                };

                await service.ApplyChangeAsync(stackId, change1, filePath);
                Assert.Equal("after change 1", File.ReadAllText(filePath));

                var change2 = new CodeChange
                {
                    ChangeId = "change-2",
                    FilePath = filePath,
                    OldContent = "after change 1",
                    NewContent = "after change 2",
                    Description = "Change 2"
                };

                await service.ApplyChangeAsync(stackId, change2, filePath);
                Assert.Equal("after change 2", File.ReadAllText(filePath));

                var change3 = new CodeChange
                {
                    ChangeId = "change-3",
                    FilePath = filePath,
                    OldContent = "after change 2",
                    NewContent = "after change 3",
                    Description = "Change 3"
                };

                await service.ApplyChangeAsync(stackId, change3, filePath);
                Assert.Equal("after change 3", File.ReadAllText(filePath));

                var stack = service.GetChangeStack(stackId);
                Assert.Equal(new[] { "change-1", "change-2", "change-3" }, stack.GetAppliedChanges());

                // Act: Rollback to change-1 (keeps change-1, removes 2 and 3)
                await service.RollbackToChangeAsync(stackId, "change-1");

                // Assert
                Assert.Equal("after change 1", File.ReadAllText(filePath));
                var updatedStack = service.GetChangeStack(stackId);
                Assert.Equal(new[] { "change-1" }, updatedStack.GetAppliedChanges());
            }
            finally
            {
                Cleanup(filePath);
            }
        }

        [Fact]
        public async Task EarlierChangesSurviveRollbackOfLaterChange()
        {
            // Arrange
            var service = new ChangeStackService();
            var stackId = service.CreateChangeStack();
            var filePath = GetTempFilePath();

            try
            {
                File.WriteAllText(filePath, "start");

                var changeA = new CodeChange
                {
                    ChangeId = "change-a",
                    FilePath = filePath,
                    OldContent = "start",
                    NewContent = "after A",
                    Description = "Change A"
                };

                await service.ApplyChangeAsync(stackId, changeA, filePath);

                var changeB = new CodeChange
                {
                    ChangeId = "change-b",
                    FilePath = filePath,
                    OldContent = "after A",
                    NewContent = "after B",
                    Description = "Change B"
                };

                await service.ApplyChangeAsync(stackId, changeB, filePath);

                // Act: Rollback only change B
                await service.RollbackChangeAsync(stackId, "change-b");

                // Assert
                var fileContent = File.ReadAllText(filePath);
                Assert.Equal("after A", fileContent);

                var stack = service.GetChangeStack(stackId);
                Assert.Single(stack.GetAppliedChanges());
                Assert.Contains("change-a", stack.GetAppliedChanges());
                Assert.DoesNotContain("change-b", stack.GetAppliedChanges());
            }
            finally
            {
                Cleanup(filePath);
            }
        }

        [Fact]
        public async Task BaselinePreserved_AcrossOperations()
        {
            // Arrange
            var service = new ChangeStackService();
            var stackId = service.CreateChangeStack();
            var filePath = GetTempFilePath();

            try
            {
                var originalContent = "original";
                File.WriteAllText(filePath, originalContent);

                var change = new CodeChange
                {
                    ChangeId = Guid.NewGuid().ToString(),
                    FilePath = filePath,
                    OldContent = originalContent,
                    NewContent = "modified",
                    Description = "Test"
                };

                // Act
                await service.ApplyChangeAsync(stackId, change, filePath);

                var stack = service.GetChangeStack(stackId);
                var appliedChange = stack.FindChangeById(change.ChangeId);

                // Assert
                Assert.NotNull(appliedChange.Baseline);
                Assert.Equal(originalContent, appliedChange.Baseline.BaselineContent);

                // Baseline should be immutable (dates should be close to creation time)
                Assert.True(appliedChange.Baseline.CreatedAt <= DateTime.UtcNow);
                Assert.True((DateTime.UtcNow - appliedChange.Baseline.CreatedAt).TotalSeconds < 5);
            }
            finally
            {
                Cleanup(filePath);
            }
        }

        [Fact]
        public void CreateChangeStack_ReturnsUniqueIds()
        {
            // Arrange
            var service = new ChangeStackService();

            // Act
            var stackId1 = service.CreateChangeStack();
            var stackId2 = service.CreateChangeStack();

            // Assert
            Assert.NotNull(stackId1);
            Assert.NotNull(stackId2);
            Assert.NotEqual(stackId1, stackId2);

            Assert.NotNull(service.GetChangeStack(stackId1));
            Assert.NotNull(service.GetChangeStack(stackId2));
        }

        [Fact]
        public void RemoveChangeStack_CleansUpInstance()
        {
            // Arrange
            var service = new ChangeStackService();
            var stackId = service.CreateChangeStack();

            Assert.NotNull(service.GetChangeStack(stackId));

            // Act
            service.RemoveChangeStack(stackId);

            // Assert
            Assert.Null(service.GetChangeStack(stackId));
        }
    }
}
