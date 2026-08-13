#nullable enable

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.UI.Navigation;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.UI
{
    public class MainViewModelBindingTests : DataBindingTestBase
    {
        [Fact]
        public void CurrentRoute_PropertyChanged_FiresNotification()
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

            using var tracker = new PropertyChangedTracker(viewModel);

            // Act
            viewModel.CurrentRoute = "ConfigPage";

            // Assert
            AssertPropertyChanged(tracker, nameof(MainViewModel.CurrentRoute));
            Assert.Equal("ConfigPage", viewModel.CurrentRoute);
        }

        [Fact]
        public void IsLoading_PropertyChanged_FiresNotification()
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

            using var tracker = new PropertyChangedTracker(viewModel);

            // Act
            viewModel.IsLoading = true;

            // Assert
            AssertPropertyChanged(tracker, nameof(MainViewModel.IsLoading));
            Assert.True(viewModel.IsLoading);
        }

        [Fact]
        public void CurrentMessages_CollectionChanged_FiresNotificationOnAdd()
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

            using var collectionTracker = new CollectionChangeTracker(viewModel.CurrentMessages);

            var message = new ChatMessage { Role = ChatMessageRole.User, Content = "Test" };

            // Act
            viewModel.CurrentMessages.Add(message);

            // Assert
            AssertCollectionAdded(collectionTracker, count: 1);
            Assert.Single(viewModel.CurrentMessages);
            Assert.Contains(message, viewModel.CurrentMessages);
        }

        [Fact]
        public void CurrentMessages_CollectionChanged_FiresNotificationOnRemove()
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

            var message = new ChatMessage { Role = ChatMessageRole.User, Content = "Test" };
            viewModel.CurrentMessages.Add(message);

            using var collectionTracker = new CollectionChangeTracker(viewModel.CurrentMessages);

            // Act
            viewModel.CurrentMessages.Remove(message);

            // Assert
            AssertCollectionRemoved(collectionTracker, count: 1);
            Assert.Empty(viewModel.CurrentMessages);
        }

        [Fact]
        public void NewSessionCommand_CanBeExecuted()
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

            // Act & Assert
            Assert.NotNull(viewModel.NewSessionCommand);
            Assert.True(viewModel.NewSessionCommand.CanExecute(null));
        }

        [Fact]
        public void NavigateCommand_CanBeExecuted()
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

            // Act & Assert
            Assert.NotNull(viewModel.NavigateCommand);
            Assert.True(viewModel.NavigateCommand.CanExecute("ChatPage"));
        }

        [Fact]
        public void SaveSessionCommand_CanBeExecuted()
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

            // Act & Assert
            Assert.NotNull(viewModel.SaveSessionCommand);
            Assert.True(viewModel.SaveSessionCommand.CanExecute(null));
        }

        [Fact]
        public void MultiplePropertyChanges_AllFireNotifications()
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

            using var tracker = new PropertyChangedTracker(viewModel);

            // Act
            viewModel.CurrentRoute = "ChatPage";
            viewModel.IsLoading = true;
            viewModel.IsLoading = false;
            viewModel.CurrentRoute = "ConfigPage";

            // Assert
            AssertPropertyChanged(tracker, nameof(MainViewModel.CurrentRoute));
            AssertPropertyChanged(tracker, nameof(MainViewModel.IsLoading));
            Assert.Equal("ConfigPage", viewModel.CurrentRoute);
            Assert.False(viewModel.IsLoading);
        }

        [Fact]
        public void CurrentSession_PropertyChanged_FiresNotification()
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

            using var tracker = new PropertyChangedTracker(viewModel);

            var session = new Session { Id = "session-1", Title = "Test Session" };

            // Act
            viewModel.CurrentSession = session;

            // Assert
            AssertPropertyChanged(tracker, nameof(MainViewModel.CurrentSession));
            Assert.Equal(session, viewModel.CurrentSession);
        }

        [Fact]
        public void LargeMessageCollection_PerformsExpected()
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

            using var collectionTracker = new CollectionChangeTracker(viewModel.CurrentMessages);

            // Act
            for (int i = 0; i < 10; i++)
            {
                var message = new ChatMessage 
                { 
                    Role = i % 2 == 0 ? ChatMessageRole.User : ChatMessageRole.Assistant, 
                    Content = $"Message {i}" 
                };
                viewModel.CurrentMessages.Add(message);
            }

            // Assert
            Assert.Equal(10, viewModel.CurrentMessages.Count);
            var addCount = collectionTracker.Changes.Count(c => c.Action == NotifyCollectionChangedAction.Add);
            Assert.Equal(10, addCount);
        }
    }
}
