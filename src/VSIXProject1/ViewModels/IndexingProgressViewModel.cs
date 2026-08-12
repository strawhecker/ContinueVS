using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    public class IndexingProgressViewModel : ViewModelBase
    {
        private readonly IIndexingService _indexingService;

        private double _progressPercentage;
        private string? _currentFile;
        private string? _status;
        private bool _isIndexing;

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => Set(ref _progressPercentage, value);
        }

        public string? CurrentFile
        {
            get => _currentFile;
            set => Set(ref _currentFile, value);
        }

        public string? Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public bool IsIndexing
        {
            get => _isIndexing;
            set => Set(ref _isIndexing, value);
        }

        public RelayCommand PauseCommand { get; }
        public RelayCommand ResumeCommand { get; }
        public RelayCommand CancelCommand { get; }

        public IndexingProgressViewModel(IIndexingService indexingService)
        {
            if (indexingService == null) throw new ArgumentNullException(nameof(indexingService));

            _indexingService = indexingService;
            _currentFile = string.Empty;
            _status = string.Empty;

            PauseCommand = new RelayCommand(ExecutePause);
            ResumeCommand = new RelayCommand(ExecuteResume);
            CancelCommand = new RelayCommand(ExecuteCancel);

            _indexingService.ProgressChanged += OnProgressChanged;
        }

#pragma warning disable VSTHRD100
        private async void ExecutePause()
#pragma warning restore VSTHRD100
        {
            try
            {
                await _indexingService.PauseIndexingAsync();
            }
            catch (Exception)
            {
                // Handle error
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteResume()
#pragma warning restore VSTHRD100
        {
            try
            {
                await _indexingService.ResumeIndexingAsync();
            }
            catch (Exception)
            {
                // Handle error
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteCancel()
#pragma warning restore VSTHRD100
        {
            try
            {
                await _indexingService.CancelIndexingAsync();
            }
            catch (Exception)
            {
                // Handle error
            }
        }

        private void OnProgressChanged(object? sender, IndexingProgressEventArgs e)
        {
            if (e?.Progress == null)
                return;
            ProgressPercentage = e.Progress.PercentComplete;
            CurrentFile = e.Progress.CurrentFile ?? string.Empty;
            Status = e.Progress.Status.ToString();
            IsIndexing = e.Progress.Status == IndexingStatus.Indexing;
        }
    }
}
