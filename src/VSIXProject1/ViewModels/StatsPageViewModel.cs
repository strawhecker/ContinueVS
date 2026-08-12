using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Services.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    public class StatsPageViewModel : ViewModelBase
    {
        private readonly ILlmService _llmService;

        private long _tokensUsed;
        private string? _modelsUsed;
        private decimal _costEstimate;

        public long TokensUsed
        {
            get => _tokensUsed;
            set => Set(ref _tokensUsed, value);
        }

        public string? ModelsUsed
        {
            get => _modelsUsed;
            set => Set(ref _modelsUsed, value);
        }

        public decimal CostEstimate
        {
            get => _costEstimate;
            set => Set(ref _costEstimate, value);
        }

        public RelayCommand ExportStatsCommand { get; }

        public StatsPageViewModel(ILlmService llmService)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));

            _llmService = llmService;
            _tokensUsed = 0;
            _modelsUsed = string.Empty;
            _costEstimate = 0m;

            ExportStatsCommand = new RelayCommand(ExecuteExportStats);
        }

#pragma warning disable VSTHRD100
        private async void ExecuteExportStats()
#pragma warning restore VSTHRD100
        {
            try
            {
                // Stub: Export statistics to file
            }
            catch (Exception)
            {
                // Handle error
            }
        }
    }
}
