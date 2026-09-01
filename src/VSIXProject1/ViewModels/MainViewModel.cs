using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
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
        private bool _isTooltipVisible;
        private string? _tooltipContent;
        private bool _isDialogOpen;
        private object? _dialogContent;

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

        public bool IsTooltipVisible
        {
            get => _isTooltipVisible;
            set => Set(ref _isTooltipVisible, value);
        }

        public string? TooltipContent
        {
            get => _tooltipContent;
            set => Set(ref _tooltipContent, value);
        }

        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set => Set(ref _isDialogOpen, value);
        }

        public object? DialogContent
        {
            get => _dialogContent;
            set => Set(ref _dialogContent, value);
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
            _ = LoggerService.Current.WriteDebugAsync($"[g7-vm-b1] ExecuteNavigate called with route: {route}");
            if (!string.IsNullOrWhiteSpace(route))
            {
                _ = LoggerService.Current.WriteDebugAsync($"[g7-vm-b2] Setting CurrentRoute to: {route}");
                CurrentRoute = route;
                _ = LoggerService.Current.WriteDebugAsync($"[g7-vm-b3] Calling PageNavigator.NavigateAsync");
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

        /// <summary>
        /// Shows a tooltip with the specified content.
        /// </summary>
        /// <param name="content">The tooltip text to display.</param>
        public void ShowTooltip(string content)
        {
            TooltipContent = content;
            IsTooltipVisible = true;
        }

        /// <summary>
        /// Hides the tooltip.
        /// </summary>
        public void HideTooltip()
        {
            IsTooltipVisible = false;
            TooltipContent = null;
        }

        /// <summary>
        /// Shows a dialog with the specified content.
        /// </summary>
        /// <param name="content">The dialog content (typically a UserControl or FrameworkElement).</param>
        public void ShowDialog(object content)
        {
            DialogContent = content;
            IsDialogOpen = true;
        }

        /// <summary>
        /// Hides the dialog.
        /// </summary>
        public void HideDialog()
        {
            IsDialogOpen = false;
            DialogContent = null;
        }
    }
}
