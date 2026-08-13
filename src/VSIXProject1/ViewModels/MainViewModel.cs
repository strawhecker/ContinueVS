using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.UI.Navigation;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISessionService _sessionService;
        private readonly IMessengerService _messengerService;
        private readonly INotificationService _notificationService;
        private readonly IConfigService _configService;
        private readonly IPageNavigator _pageNavigator;

        private Session? _currentSession;
        private string? _currentRoute;
        private bool _isLoading;

        public ObservableCollection<ChatMessage> CurrentMessages { get; }

        public Session? CurrentSession
        {
            get => _currentSession;
            set => Set(ref _currentSession, value);
        }

        public string? CurrentRoute
        {
            get => _currentRoute;
            set => Set(ref _currentRoute, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public RelayCommand NewSessionCommand { get; }
        public RelayCommand<string> NavigateCommand { get; }
        public RelayCommand SaveSessionCommand { get; }

        public event EventHandler<SessionChangedEventArgs>? SessionChanged;

        public MainViewModel(
            ISessionService sessionService,
            IMessengerService messengerService,
            INotificationService notificationService,
            IConfigService configService,
            IPageNavigator pageNavigator)
        {
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));
            if (messengerService == null) throw new ArgumentNullException(nameof(messengerService));
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (pageNavigator == null) throw new ArgumentNullException(nameof(pageNavigator));

            _sessionService = sessionService;
            _messengerService = messengerService;
            _notificationService = notificationService;
            _configService = configService;
            _pageNavigator = pageNavigator;

            CurrentMessages = new ObservableCollection<ChatMessage>();
            _currentRoute = "chat";
            _currentSession = null;

            _sessionService.SessionChanged += OnSessionChanged;
            _configService.ConfigChanged += OnConfigChanged;

            NewSessionCommand = new RelayCommand(ExecuteNewSession);
            NavigateCommand = new RelayCommand<string>(ExecuteNavigate);
            SaveSessionCommand = new RelayCommand(ExecuteSaveSession);
        }

#pragma warning disable VSTHRD100
        private async void ExecuteNewSession()
#pragma warning restore VSTHRD100
        {
            try
            {
                IsLoading = true;
                await _sessionService.CreateNewSessionAsync();
            }
            catch (Exception ex)
            {
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #pragma warning disable VSTHRD100
        private async void ExecuteNavigate(string route)
#pragma warning restore VSTHRD100
        {
            if (!string.IsNullOrWhiteSpace(route))
            {
                CurrentRoute = route;
                await _pageNavigator.NavigateAsync(route, null);
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteSaveSession()
#pragma warning restore VSTHRD100
        {
            try
            {
                IsLoading = true;
                await _sessionService.SaveCurrentSessionAsync();
                await _notificationService.ShowNotificationAsync("Success", "Session saved.", NotificationType.Success);
            }
            catch (Exception ex)
            {
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
        {
            CurrentSession = e.Session;
            CurrentMessages.Clear();
            if (e.Session?.Messages != null)
            {
                foreach (var msg in e.Session.Messages)
                {
                    CurrentMessages.Add(msg);
                }
            }
            SessionChanged?.Invoke(this, e);
        }

        private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(CurrentRoute));
            RaisePropertyChanged(nameof(IsLoading));
        }
    }
}
