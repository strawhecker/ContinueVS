#nullable enable

using System;
using System.Collections.ObjectModel;
using Moq;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class MainViewModelTests : TestFixtureBase
    {
        [Fact]
        public void Constructor_WithValidDependencies_InitializesProperties()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();

            // Act
            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            // Assert
            Assert.NotNull(viewModel);
            Assert.NotNull(viewModel.CurrentMessages);
            Assert.IsType<ObservableCollection<ChatMessage>>(viewModel.CurrentMessages);
            Assert.Equal("chat", viewModel.CurrentRoute);
            Assert.Null(viewModel.CurrentSession);
            Assert.False(viewModel.IsLoading);
        }

        [Fact]
        public void Constructor_WithNullSessionService_ThrowsArgumentNullException()
        {
            // Arrange
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainViewModel(null!, mockMessengerService.Object, mockNotificationService.Object, mockConfigService.Object));
        }

        [Fact]
        public void CurrentRoute_CanBeSet()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();

            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            // Act
            viewModel.CurrentRoute = "settings";

            // Assert
            Assert.Equal("settings", viewModel.CurrentRoute);
        }

        [Fact]
        public void IsLoading_CanBeSet()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();

            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            // Act
            viewModel.IsLoading = true;

            // Assert
            Assert.True(viewModel.IsLoading);
        }

        [Fact]
        public void CurrentMessages_InitializedAsEmptyCollection()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();

            // Act
            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            // Assert
            Assert.NotNull(viewModel.CurrentMessages);
            Assert.Empty(viewModel.CurrentMessages);
        }

        [Fact]
        public void Commands_AreNotNull()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();

            // Act
            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            // Assert
            Assert.NotNull(viewModel.NewSessionCommand);
            Assert.NotNull(viewModel.NavigateCommand);
            Assert.NotNull(viewModel.SaveSessionCommand);
        }
    }
}
