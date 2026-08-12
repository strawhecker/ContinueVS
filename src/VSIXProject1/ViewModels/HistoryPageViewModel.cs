using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    public class HistoryPageViewModel : ViewModelBase
    {
        private readonly ISessionService _sessionService;

        private Session? _selectedSession;

        public ObservableCollection<Session> Sessions { get; }

        public Session? SelectedSession
        {
            get => _selectedSession;
            set => Set(ref _selectedSession, value);
        }

        public RelayCommand LoadSessionCommand { get; }
        public RelayCommand DeleteSessionCommand { get; }

        public HistoryPageViewModel(ISessionService sessionService)
        {
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));

            _sessionService = sessionService;

            Sessions = new ObservableCollection<Session>();

            LoadSessionCommand = new RelayCommand(ExecuteLoadSession);
            DeleteSessionCommand = new RelayCommand(ExecuteDeleteSession);
        }

#pragma warning disable VSTHRD100
        private async void ExecuteLoadSession()
#pragma warning restore VSTHRD100
        {
            try
            {
                if (SelectedSession == null)
                    return;

                await _sessionService.LoadSessionAsync(SelectedSession.Id);
            }
            catch (Exception)
            {
                // Handle error
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteDeleteSession()
#pragma warning restore VSTHRD100
        {
            try
            {
                if (SelectedSession == null)
                    return;

                // Stub: Delete session via service
            }
            catch (Exception)
            {
                // Handle error
            }
        }
    }
}
