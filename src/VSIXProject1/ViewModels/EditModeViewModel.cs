using System;
using System.Threading.Tasks;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    public class EditModeViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;

        private string? _originalCode;
        private string? _newCode;
        private string? _diff;
        private bool _showAcceptPrompt;

        public string? OriginalCode
        {
            get => _originalCode;
            set => Set(ref _originalCode, value);
        }

        public string? NewCode
        {
            get => _newCode;
            set => Set(ref _newCode, value);
        }

        public string? Diff
        {
            get => _diff;
            set => Set(ref _diff, value);
        }

        public bool ShowAcceptPrompt
        {
            get => _showAcceptPrompt;
            set => Set(ref _showAcceptPrompt, value);
        }

        public RelayCommand AcceptCommand { get; }
        public RelayCommand RejectCommand { get; }

        public EditModeViewModel(INotificationService notificationService)
        {
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));

            _notificationService = notificationService;
            _originalCode = string.Empty;
            _newCode = string.Empty;
            _diff = string.Empty;
            _showAcceptPrompt = false;

            AcceptCommand = new RelayCommand(ExecuteAccept);
            RejectCommand = new RelayCommand(ExecuteReject);
        }

#pragma warning disable VSTHRD100
        private async void ExecuteAccept()
#pragma warning restore VSTHRD100
        {
            try
            {
                ShowAcceptPrompt = false;
                await _notificationService.ShowNotificationAsync("Success", "Changes applied.", NotificationType.Success);
            }
            catch (Exception ex)
            {
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteReject()
#pragma warning restore VSTHRD100
        {
            try
            {
                ShowAcceptPrompt = false;
                await _notificationService.ShowNotificationAsync("Info", "Changes discarded.", NotificationType.Information);
            }
            catch (Exception ex)
            {
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
        }
    }
}
