#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class IndexingProgressViewModelTests : TestFixtureBase
    {
        [Fact]
        public void Constructor_WithValidDependencies_InitializesProperties()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();

            // Act
            var viewModel = new IndexingProgressViewModel(mockIndexingService.Object);

            // Assert
            Assert.NotNull(viewModel);
            Assert.Equal(0d, viewModel.ProgressPercentage);
            Assert.Equal(string.Empty, viewModel.CurrentFile);
            Assert.Equal(string.Empty, viewModel.Status);
            Assert.False(viewModel.IsIndexing);
        }

        [Fact]
        public void Constructor_WithNullIndexingService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new IndexingProgressViewModel(null!));
        }

        [Fact]
        public void ProgressPercentage_CanBeSet()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var viewModel = new IndexingProgressViewModel(mockIndexingService.Object);

            // Act
            viewModel.ProgressPercentage = 50.0;

            // Assert
            Assert.Equal(50.0, viewModel.ProgressPercentage);
        }

        [Fact]
        public void CurrentFile_CanBeSet()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var viewModel = new IndexingProgressViewModel(mockIndexingService.Object);

            const string testFile = "/path/to/file.cs";

            // Act
            viewModel.CurrentFile = testFile;

            // Assert
            Assert.Equal(testFile, viewModel.CurrentFile);
        }

        [Fact]
        public void Status_CanBeSet()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var viewModel = new IndexingProgressViewModel(mockIndexingService.Object);

            const string testStatus = "Indexing";

            // Act
            viewModel.Status = testStatus;

            // Assert
            Assert.Equal(testStatus, viewModel.Status);
        }

        [Fact]
        public void Commands_AreNotNull()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();

            // Act
            var viewModel = new IndexingProgressViewModel(mockIndexingService.Object);

            // Assert
            Assert.NotNull(viewModel.PauseCommand);
            Assert.NotNull(viewModel.ResumeCommand);
            Assert.NotNull(viewModel.CancelCommand);
        }

        [Fact]
        public void OnProgressChanged_UpdatesProperties()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var viewModel = new IndexingProgressViewModel(mockIndexingService.Object);

            var progressUpdate = new IndexingProgressUpdate
            {
                PercentComplete = 75.0,
                CurrentFile = "/path/to/file.cs",
                Status = IndexingStatus.Indexing
            };

            var args = new IndexingProgressEventArgs { Progress = progressUpdate };

            // Act
            mockIndexingService.Raise(s => s.ProgressChanged += null, args);

            // Assert
            Assert.Equal(75.0, viewModel.ProgressPercentage);
            Assert.Equal("/path/to/file.cs", viewModel.CurrentFile);
            Assert.Equal("Indexing", viewModel.Status);
            Assert.True(viewModel.IsIndexing);
        }

        [Fact]
        public void OnProgressChanged_WithErrorStatus_UpdatesIsIndexing()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var viewModel = new IndexingProgressViewModel(mockIndexingService.Object);

            var progressUpdate = new IndexingProgressUpdate
            {
                PercentComplete = 100.0,
                CurrentFile = "",
                Status = IndexingStatus.Error
            };

            var args = new IndexingProgressEventArgs { Progress = progressUpdate };

            // Act
            mockIndexingService.Raise(s => s.ProgressChanged += null, args);

            // Assert
            Assert.Equal(100.0, viewModel.ProgressPercentage);
            Assert.False(viewModel.IsIndexing);
        }
    }
}
