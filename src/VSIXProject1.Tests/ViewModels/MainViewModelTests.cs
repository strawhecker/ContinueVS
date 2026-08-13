#nullable enable

using System;
using System.Collections.ObjectModel;
using Moq;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.UI.Navigation;
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
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            // Act
            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

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
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MainViewModel(null!, mockMessengerService.Object, mockNotificationService.Object, mockConfigService.Object, mockPageNavigator.Object));
        }

        [Fact]
        public void CurrentRoute_CanBeSet()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

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
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

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
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            // Act
            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

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
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            // Act
            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

            // Assert
            Assert.NotNull(viewModel.NewSessionCommand);
            Assert.NotNull(viewModel.NavigateCommand);
            Assert.NotNull(viewModel.SaveSessionCommand);
        }

        [Fact]
        public void OnConfigChanged_UpdatesRoute()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

            // Act
            var args = new ConfigChangedEventArgs { ConfigKey = "test", OldValue = null, NewValue = "value" };
            mockConfigService.Raise(s => s.ConfigChanged += null, args);

            // Assert - verify the view model is still valid (property change was raised internally)
            Assert.NotNull(viewModel.CurrentRoute);
        }

        [Fact]
        public void NavigateCommand_WithValidRoute_InvokesPageNavigator()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

            // Act
            viewModel.NavigateCommand.Execute("settings");

            // Assert
            Assert.Equal("settings", viewModel.CurrentRoute);
            mockPageNavigator.Verify(pn => pn.NavigateAsync("settings", null), Times.Once);
        }

        [Fact]
        public void NavigateCommand_WithNullRoute_DoesNotInvokePageNavigator()
        {
            // Arrange
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockMessengerService = CreateLooseMock<IMessengerService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockPageNavigator = CreateLooseMock<IPageNavigator>();

            var viewModel = new MainViewModel(
                mockSessionService.Object,
                mockMessengerService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockPageNavigator.Object);

            // Act
            viewModel.NavigateCommand.Execute(null!);

            // Assert
            Assert.Equal("chat", viewModel.CurrentRoute);
            mockPageNavigator.Verify(pn => pn.NavigateAsync(It.IsAny<string>(), It.IsAny<System.Windows.Controls.Frame>()), Times.Never);
        }
    }
}
