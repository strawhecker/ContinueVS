using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Utilities;
using ContinueVS.ViewModels.Models;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    /// <summary>
    /// System message prompts for each operational mode.
    /// </summary>
    internal static class ChatModeSystemPrompts
    {
        /// <summary>
        /// System prompt for Ask mode: guidance for basic Q&A interaction.
        /// </summary>
        public const string DEFAULT_ASK_SYSTEM_MESSAGE = "You are a helpful coding assistant in Ask mode. Provide code suggestions and explanations. Use the Apply button or switch to Agent Mode for automatic edits.";

        /// <summary>
        /// System prompt for Agent mode: guidance for autonomous tool calling.
        /// </summary>
        public const string DEFAULT_AGENT_SYSTEM_MESSAGE = "You are an autonomous coding agent in Agent mode. Call read-only tools to analyze code. Use edit tools when the user approves changes. Always confirm before applying edits.";

        /// <summary>
        /// System prompt for Plan mode: guidance for read-only plan generation.
        /// </summary>
        public const string DEFAULT_PLAN_SYSTEM_MESSAGE = "You are a planning assistant in Plan mode. Generate detailed implementation plans and analysis in read-only mode. Suggest Agent Mode for executing code changes.";
    }

    public class ChatPageViewModel : ViewModelBase
        {
            private readonly ILlmService _llmService;
                private readonly IContextService _contextService;
                private readonly IToolService _toolService;
                private readonly ISessionService _sessionService;
                private readonly INotificationService _notificationService;
                private readonly IConfigService _configService;
                private readonly ISystemPromptService _systemPromptService;
                private readonly IUIStateService _uiStateService;
                private readonly IDebugSessionService _debugSessionService;
                private IModeService? _modeService;
                private IWorkflowService? _workflowService;
                private readonly IIdeService? _ideService;
            private UIState? _cachedUIState;

        private string? _inputText;
        private bool _isStreaming;
        private string? _streamingResponse;
        private CancellationTokenSource? _streamingCts;
        private ChatMode _currentMode = ChatMode.Ask;
        private ModelInfo? _selectedModel;
        private List<ToolCall> _pendingToolCalls = new List<ToolCall>();
        private const int MaxToolCallIterations = 10;
        private int _toolCallIterationCount = 0;

        /// <summary>
        /// Tracks cumulative tool failures in current iteration.
        /// Gap23_3: If 2+ tools fail in same iteration, loop terminates.
        /// Reset each iteration; single failures continue loop.
        /// </summary>
        private int _toolFailureCount = 0;

        /// <summary>
        /// Flag to track if tool call limit has been reached (gap23_4_3).
        /// Set when InvalidOperationException thrown, resets on new session.
        /// </summary>
        private bool _limitReachedFlag = false;

        /// <summary>
        /// Flag to show the 80% warning banner (gap23_4_4).
        /// Auto-dismisses after 5 seconds.
        /// </summary>
        private bool _showWarningBanner = false;

        /// <summary>
        /// Flag to show the 100% error banner (gap23_4_4).
        /// Persists until user closes it.
        /// </summary>
        private bool _showErrorBanner = false;

        /// <summary>
        /// Timer to auto-dismiss warning banner after 5 seconds.
        /// </summary>
        private DispatcherTimer? _warningDismissTimer;

        /// <summary>
        /// Formatted string display of tool call counter (gap23_4_5).
        /// Format: "{ToolCallsExecuted} / {MaxToolCallsPerSession} tool calls"
        /// </summary>
        private string _toolCallCounterDisplay = "0 / 0 tool calls";

        /// <summary>
        /// Flag to control onboarding card visibility (gap25_6).
        /// Bound to Messages collection count: visible when empty (count == 0), hidden when populated.
        /// </summary>
        private bool _onboardingCardVisible = true;

        /// <summary>
        /// Backing collection for available chat mode options (gap27_1).
        /// </summary>
        private ObservableCollection<ModeOption>? _availableModes;

        /// <summary>
        /// Backing field for the currently selected mode option (gap27_1).
        /// </summary>
        private ModeOption? _selectedMode;

        /// <summary>
        /// Backing collection for available continuation policy options (gap27_12).
        /// </summary>
        private ObservableCollection<PolicyOption>? _continuationPolicies;

        /// <summary>
        /// Backing field for the currently selected continuation policy (gap27_12).
        /// </summary>
        private ContinuationPolicy _selectedPolicy = ContinuationPolicy.Interactive;

        /// <summary>
        /// Flag to track pause state for long-running sessions (gap31_1).
        /// When true, execution is paused; when false, execution can proceed.
        /// </summary>
        private bool _isPaused = false;

        public ObservableCollection<ChatMessage> Messages { get; }
        public ObservableCollection<ContextItem> SelectedContext { get; }
        public ObservableCollection<ModelInfo> AvailableModels { get; }

        /// <summary>
        /// Gets the available chat mode options for the mode dropdown (gap27_1).
        /// </summary>
        public ObservableCollection<ModeOption> AvailableModes
        {
            get
            {
                if (_availableModes == null)
                {
                    _availableModes = new ObservableCollection<ModeOption>
                    {
                        new ModeOption("Ask", ChatMode.Ask, "Basic Q&A with optional Apply button for code suggestions.", "💬"),
                        new ModeOption("Agent", ChatMode.Agent, "Autonomous tool calling and code editing with user approval.", "🤖"),
                        new ModeOption("Plan", ChatMode.Plan, "Read-only plan generation and review.", "📋"),
                        new ModeOption("Debug", ChatMode.Debug, "Instrumentation-driven error diagnosis with interactive refinement.", "🔧"),
                        new ModeOption("Reason", ChatMode.Reason, "Structured chain-of-thought reasoning before answering.", "🧠")
                    };
                }
                return _availableModes;
            }
}

/// <summary>
/// Gets the available continuation policy options for the policy dropdown (gap27_12).
/// Lazy-initialized collection of policy choices: Auto, Interactive, Deferred.
/// </summary>
public ObservableCollection<PolicyOption> ContinuationPolicies
{
    get
    {
        if (_continuationPolicies == null)
        {
            _continuationPolicies = new ObservableCollection<PolicyOption>
            {
                new PolicyOption("Automatically continue", ContinuationPolicy.Auto, "Continue to next tool without pause", "⚡"),
                new PolicyOption("Ask before each action", ContinuationPolicy.Interactive, "Show UI prompt before each tool execution", "❓"),
                new PolicyOption("Defer for review", ContinuationPolicy.Deferred, "Queue execution for later review (safest)", "⏸️")
            };
        }
        return _continuationPolicies;
    }
}

        /// <summary>
        /// Gets or sets the currently selected continuation policy (gap27_12, gap27_16).
        /// Changing this property persists the policy via IWorkflowService.SetContinuationPolicyAsync() 
        /// and saves to config via IConfigService.SaveDefaultPolicyAsync().
        /// Defaults to Interactive (safe choice).
        /// </summary>
        public ContinuationPolicy SelectedPolicy
        {
            get => _selectedPolicy;
            set
            {
                if (Set(ref _selectedPolicy, value))
                {
#pragma warning disable VSTHRD110
                    _workflowService?.SetContinuationPolicyAsync(value);
                    // gap27_16: Fire-and-forget policy persistence to config
                    _ = _configService.SaveDefaultPolicyAsync(value);
#pragma warning restore VSTHRD110
                }
            }
        }

public string? InputText
{
            get => _inputText;
            set 
            {
                if (Set(ref _inputText, value))
                {
                    SendMessageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set 
            {
                if (Set(ref _isStreaming, value))
                {
                    SendMessageCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? StreamingResponse
        {
            get => _streamingResponse;
            set => Set(ref _streamingResponse, value);
        }

        /// <summary>
        /// Gets or sets the currently selected mode option (gap27_1).
        /// Changing this property propagates the new ChatMode to CurrentMode.
        /// When set, delegates to IModeService.SetModeAsync() to fire mode-change events (gap27_3).
        /// Also saves mode to config as default (gap27_5).
        /// </summary>
        public ModeOption? SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (Set(ref _selectedMode, value) && value != null)
                {
                    CurrentMode = value.Value;
                    if (_modeService != null)
                    {
                        // Mode enums are compatible; cast to int for service call
                        _ = _modeService.SetModeAsync((int)value.Value);
                    }
                    // gap27_5: Save selected mode to config as default
                    _ = _configService.SaveDefaultModeAsync((int)value.Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the current operational mode (Ask, Agent, or Plan).
        /// </summary>
        public ChatMode CurrentMode
        {
            get => _currentMode;
            set
            {
                System.Diagnostics.Debug.WriteLine($"[a9-property-entry] CurrentMode setter: oldValue={_currentMode}, newValue={value}");
                if (Set(ref _currentMode, value))
                {
                    System.Diagnostics.Debug.WriteLine($"[a9-property-set-success] Set() returned true, property changed. New _currentMode={_currentMode}, PropertyChanged notification raised");
                    SendMessageCommand.RaiseCanExecuteChanged();
                    // gap27_1: keep SelectedMode in sync when CurrentMode is set externally (e.g. SetModeCommand)
                    var matching = AvailableModes.FirstOrDefault(m => m.Value == _currentMode);
                    if (matching != null && !ReferenceEquals(_selectedMode, matching))
                        Set(ref _selectedMode, matching, "SelectedMode");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[a9-property-set-noop] Set() returned false, property unchanged. _currentMode still={_currentMode}");
                }
            }
        }

        /// <summary>
        /// Gets whether the continuation policy dropdown should be visible (gap27_13).
        /// Returns true only in Agent or Plan modes; false in Ask mode.
        /// </summary>
        public bool IsPolicyVisible
        {
            get => CurrentMode == ChatMode.Agent || CurrentMode == ChatMode.Plan;
        }

        /// <summary>
        /// Gets or sets the currently selected model for chat messages.
        /// </summary>
        public ModelInfo? SelectedModel
        {
            get => _selectedModel;
            set => Set(ref _selectedModel, value);
        }

        /// <summary>
        /// Gets or sets whether to show the 80% warning banner (gap23_4_4).
        /// </summary>
        public bool ShowWarningBanner
        {
            get => _showWarningBanner;
            set => Set(ref _showWarningBanner, value);
        }

        /// <summary>
        /// Gets or sets whether to show the 100% error banner (gap23_4_4).
        /// </summary>
        public bool ShowErrorBanner
        {
            get => _showErrorBanner;
            set => Set(ref _showErrorBanner, value);
        }

        /// <summary>
        /// Gets the formatted tool call counter display (gap23_4_5).
        /// Format: "{ToolCallsExecuted} / {MaxToolCallsPerSession} tool calls"
        /// </summary>
        public string ToolCallCounterDisplay
        {
            get => _toolCallCounterDisplay;
            private set => Set(ref _toolCallCounterDisplay, value);
        }

        /// <summary>
        /// Gets or sets whether the onboarding card is visible (gap25_6).
        /// Card is visible when chat is empty (Messages.Count == 0); auto-hides on first message.
        /// </summary>
        public bool OnboardingCardVisible
        {
            get => _onboardingCardVisible;
            set => Set(ref _onboardingCardVisible, value);
        }

        /// <summary>
        /// Gets or sets the pause state for the current session (gap31_1).
        /// When true, execution is paused; when false, execution can proceed.
        /// gap31_2: When pause state changes, updates SendMessageCommand availability.
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (Set(ref _isPaused, value))
                {
                    // gap31_2: Notify SendMessageCommand that CanExecute may have changed
                    SendMessageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Gets the display text for the pause button (gap31_1).
        /// Returns "Pause" when not paused, "Resume" when paused.
        /// </summary>
        public string IsPausedDisplay => _isPaused ? "Resume" : "Pause";

        public RelayCommand SendMessageCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand<string> AddContextCommand { get; }
        /// <summary>
        /// Command to switch to a specified chat mode.
        /// </summary>
        public RelayCommand<ChatMode> SetModeCommand { get; }
        /// <summary>
        /// Command to delete a message by ID.
        /// </summary>
        public RelayCommand<string> DeleteMessageCommand { get; }
        /// <summary>
        /// Command to toggle pause state (gap31_1).
        /// </summary>
        public RelayCommand PauseCommand { get; }

        public ChatPageViewModel(
            ILlmService llmService,
            IContextService contextService,
            IToolService toolService,
            ISessionService sessionService,
            INotificationService notificationService,
            IConfigService configService,
            ISystemPromptService systemPromptService,
            IUIStateService uiStateService,
            IDebugSessionService debugSessionService,
            IModeService? modeService = null,
            IWorkflowService? workflowService = null,
            IIdeService? ideService = null)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));
            if (contextService == null) throw new ArgumentNullException(nameof(contextService));
            if (toolService == null) throw new ArgumentNullException(nameof(toolService));
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (systemPromptService == null) throw new ArgumentNullException(nameof(systemPromptService));
            if (uiStateService == null) throw new ArgumentNullException(nameof(uiStateService));
            if (debugSessionService == null) throw new ArgumentNullException(nameof(debugSessionService));

            _llmService = llmService;
            _contextService = contextService;
            _toolService = toolService;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _configService = configService;
            _systemPromptService = systemPromptService;
            _uiStateService = uiStateService;
            _debugSessionService = debugSessionService;
            _modeService = modeService;
            _workflowService = workflowService;
            _ideService = ideService;

            Messages = new ObservableCollection<ChatMessage>();
            SelectedContext = new ObservableCollection<ContextItem>();
            AvailableModels = new ObservableCollection<ModelInfo>();
            _inputText = string.Empty;
            _streamingResponse = string.Empty;

            // gap27_1: Initialize SelectedMode to match the default CurrentMode (Ask)
            _selectedMode = AvailableModes.FirstOrDefault(m => m.Value == _currentMode);

            // gap25_6: Subscribe to messages collection changes to sync onboarding card visibility
            Messages.CollectionChanged += OnMessages_CollectionChanged;

            SendMessageCommand = new RelayCommand(ExecuteSendMessage, CanSendMessage);
            CancelCommand = new RelayCommand(ExecuteCancel, () => IsStreaming);
            AddContextCommand = new RelayCommand<string>(ExecuteAddContext);
            SetModeCommand = new RelayCommand<ChatMode>(mode => CurrentMode = mode);
            DeleteMessageCommand = new RelayCommand<string>(ExecuteDeleteMessage);
            PauseCommand = new RelayCommand(ExecutePause, () => IsStreaming);

            _ = InitializeAsync();
            _configService.ConfigChanged += ConfigService_ConfigChanged;

            // gap23_4_3: Reset limit flag when session changes
            _sessionService.SessionChanged += (s, e) =>
            {
                if (e.IsNewSession)
                {
                    _limitReachedFlag = false;
                    ShowWarningBanner = false;
                    ShowErrorBanner = false;
                    DismissWarningBanner();
                    SendMessageCommand.RaiseCanExecuteChanged();
                    System.Diagnostics.Debug.WriteLine("[gap23_4_3-reset] Limit flag cleared on new session");
                }
                // gap23_4_5: Refresh counter display on any session change
                RefreshToolCallCounter();

                // gap27_5: Restore mode from session when loading (CurrentMode set in event)
                if (e.CurrentMode.HasValue)
                {
                    var restoredMode = Services.Utilities.ModeValidator.CoerceToValidMode(e.CurrentMode.Value);
                    System.Diagnostics.Debug.WriteLine($"[gap27_5-restore] Session loaded with mode {e.CurrentMode.Value}, coerced to {restoredMode}");
                    CurrentMode = (ChatMode)restoredMode;
                }
            };
        }

        /// <summary>
        /// Switches to the UI/main thread via Dispatcher.InvokeAsync.
        /// This is safe for both VS runtime and unit test contexts.
        /// </summary>
        private static async Task SwitchToMainThreadAsync()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                await dispatcher.InvokeAsync(() => { });
#pragma warning restore VSTHRD001
            }
        }

        private async Task InitializeAsync()
        {
            await _systemPromptService.LoadAsync();

            // Load models after ConfigService is initialized (only called after ServiceInitializer.InitializeAsync)
            await LoadModelsAsync();

            // Cache UIState for tool policy decisions in ExecuteToolCallsAsync
            try
            {
                _cachedUIState = await _uiStateService.GetUIStateAsync();
                System.Diagnostics.Debug.WriteLine($"[gap9-uistate-load] UIState cached: {_cachedUIState?.ToolSettings.Count ?? 0} tool policies loaded");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap9-uistate-error] Failed to load UIState: {ex.Message}");
                // Fall back to empty UIState (all tools default to AskFirst)
                _cachedUIState = new UIState();
            }

            // Reset limit flag on initialization
            _limitReachedFlag = false;

            // gap23_4_5: Initialize and display tool call counter
            RefreshToolCallCounter();

            // gap27_5: Load default mode from config on startup
            try
            {
                var defaultMode = await _configService.GetDefaultModeAsync();
                var coercedMode = Services.Utilities.ModeValidator.CoerceToValidMode(defaultMode);
                System.Diagnostics.Debug.WriteLine($"[gap27_5-init] Loaded default mode from config: {defaultMode}, coerced to {coercedMode}");

                // Update CurrentMode and sync SelectedMode
                CurrentMode = (ChatMode)coercedMode;
                var modeOption = AvailableModes.FirstOrDefault(m => (int)m.Value == coercedMode);
                if (modeOption != null && !ReferenceEquals(_selectedMode, modeOption))
                {
                    _selectedMode = modeOption;
                    RaisePropertyChanged("SelectedMode");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap27_5-init-error] Failed to load default mode: {ex.Message}");
                // Default to Ask (already the default in _currentMode)
            }

            // gap27_16: Restore default policy from config on startup
            try
            {
                var defaultPolicy = await _configService.GetDefaultPolicyAsync();
                System.Diagnostics.Debug.WriteLine($"[gap27_16-init] Loaded default policy from config: {defaultPolicy}");

                // Update _selectedPolicy backing field directly without triggering setter
                // to avoid saving immediately after loading
                _selectedPolicy = defaultPolicy;
                RaisePropertyChanged("SelectedPolicy");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap27_16-init-error] Failed to load default policy: {ex.Message}");
                // Default to Interactive (already set in field initialization)
            }
        }

        private async Task LoadModelsAsync()
        {
            try
            {
                var config = _configService.GetCurrentConfig();
                if (config?.Models != null && config.Models.Any())
                {
                    var models = config.Models.ToList();
                    await SwitchToMainThreadAsync();

                    AvailableModels.Clear();
                    foreach (var model in models)
                    {
                        AvailableModels.Add(model);
                    }

                    if (AvailableModels.Count > 0 && _selectedModel == null)
                    {
                        SelectedModel = AvailableModels[0];
                        System.Diagnostics.Debug.WriteLine($"[chat-model-load] Loaded {AvailableModels.Count} models, selected: {SelectedModel?.Name}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[chat-model-load] No models in config (config null={config == null}, Models null/empty={config?.Models == null || !config.Models.Any()})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[chat-model-load-error] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ConfigService_ConfigChanged(object? sender, EventArgs e)
        {
            _ = LoadModelsAsync();
        }

        /// <summary>
        /// Handles Messages collection changes to sync onboarding card visibility (gap25_6).
        /// Card is visible only when chat is empty (Messages.Count == 0).
        /// </summary>
        private void OnMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnboardingCardVisible = Messages.Count == 0;
            System.Diagnostics.Debug.WriteLine($"[gap25_6-sync] Onboarding card visibility updated: {OnboardingCardVisible} (Messages.Count={Messages.Count})");
        }

        /// <summary>
        /// Dismisses the warning banner and stops the auto-dismiss timer (gap23_4_4).
        /// </summary>
        private void DismissWarningBanner()
        {
            ShowWarningBanner = false;
            if (_warningDismissTimer != null)
            {
                _warningDismissTimer.Stop();
                _warningDismissTimer = null;
            }
        }

        /// <summary>
        /// Public command method for warning banner dismiss button (gap23_4_4).
        /// </summary>
        public void DismissWarningBannerCommand()
        {
            DismissWarningBanner();
        }

        /// <summary>
        /// Resets tool call limit state when a new user action (send) begins (gap23_4_4).
        /// The tool limit is per-action: each send resets the counter.
        /// If an ask/agent/plan exhausts tools, it stops. User can send again with fresh budget.
        /// </summary>
        private void ResetToolCallLimitForAction()
        {
            _limitReachedFlag = false;
            ShowWarningBanner = false;
            ShowErrorBanner = false;
            DismissWarningBanner(); // Stop any active dismissal timer
            SendMessageCommand.RaiseCanExecuteChanged();
            System.Diagnostics.Debug.WriteLine("[gap23_4_4-reset] Tool call limit reset for new user action. Fresh budget allocated.");
        }

        /// <summary>
        /// Refreshes the tool call counter display (gap23_4_5).
        /// Called when session changes or tool calls increment.
        /// </summary>
        private void RefreshToolCallCounter()
        {
            try
            {
                ToolCallCounterDisplay = GetToolCallCounterDisplay();
                System.Diagnostics.Debug.WriteLine($"[gap23_4_5-refresh] Counter display updated: {ToolCallCounterDisplay}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap23_4_5-refresh-error] Failed to refresh counter: {ex.Message}");
                ToolCallCounterDisplay = "0 / 0 tool calls";
            }
        }

        /// <summary>
        /// Gets the formatted tool call counter display string (gap23_4_5).
        /// Format: "{ToolCallsExecuted} / {MaxToolCallsPerSession} tool calls"
        /// Returns "0 / 0 tool calls" if session or config unavailable.
        /// </summary>
        private string GetToolCallCounterDisplay()
        {
            try
            {
                var session = _sessionService?.GetCurrentSession();
                var config = _configService?.GetCurrentConfig();

                if (session == null || config == null)
                    return "0 / 0 tool calls";

                int toolCallsExecuted = session.ToolCallsExecuted;
                object? maxVal = null;
                config.CustomSettings?.TryGetValue(UserSettings.Agent_MaxToolCallsPerSession, out maxVal);
                int maxToolCalls = (int)(maxVal ?? 100);
                if (maxToolCalls <= 0)
                    maxToolCalls = 100;

                System.Diagnostics.Debug.WriteLine($"[gap23_4_5-counter] toolCallsExecuted={toolCallsExecuted}, maxToolCalls={maxToolCalls}");
                return $"{toolCallsExecuted} / {maxToolCalls} tool calls";
            }
            catch
            {
                return "0 / 0 tool calls";
            }
        }

        /// <summary>
        /// Calculates the percentage of tool calls used in the current session (gap23_4_4).
        /// Returns null-safe value; defaults to 0 if session or settings not available.
        /// </summary>
        private double GetToolCallPercentage()
        {
            try
            {
                var session = _sessionService?.GetCurrentSession();
                var config = _configService?.GetCurrentConfig();

                if (session == null || config == null)
                    return 0.0;

                object? maxVal = null;
                config.CustomSettings?.TryGetValue(UserSettings.Agent_MaxToolCallsPerSession, out maxVal);
                int maxToolCalls = (int)(maxVal ?? 100);
                if (maxToolCalls <= 0)
                    maxToolCalls = 100;

                return (session.ToolCallsExecuted / (double)maxToolCalls) * 100.0;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Checks the tool call limit and shows warning/error banners as needed (gap23_4_4).
        /// At 80%: Shows auto-dismissing warning banner and logs analytics.
        /// At 100%: Shows persistent error banner, blocks send button, and logs analytics.
        /// </summary>
        private void CheckToolCallLimit()
        {
            try
            {
                double percentage = GetToolCallPercentage();
                System.Diagnostics.Debug.WriteLine($"[gap23_4_4-check] Tool call percentage: {percentage:F1}%");

                if (percentage >= 100.0)
                {
                    // At limit: Show persistent error banner, disable send
                    if (!ShowErrorBanner)
                    {
                        ShowErrorBanner = true;
                        _limitReachedFlag = true;
                        SendMessageCommand.RaiseCanExecuteChanged();
                        System.Diagnostics.Debug.WriteLine("[gap23_4_4-error] Tool call limit reached (100%). Error banner shown.");

                        // Log analytics event
                        _notificationService.ShowError("Tool call limit reached (100/100). Start a new session to continue.");
                    }
                }
                else if (percentage >= 80.0)
                {
                    // Approaching limit: Show auto-dismiss warning banner
                    if (!ShowWarningBanner)
                    {
                        ShowWarningBanner = true;
                        System.Diagnostics.Debug.WriteLine($"[gap23_4_4-warning] Approaching tool call limit ({percentage:F1}%). Warning banner shown.");

                        // Start 5-second auto-dismiss timer
                        if (_warningDismissTimer == null)
                        {
                            _warningDismissTimer = new DispatcherTimer();
                            _warningDismissTimer.Interval = TimeSpan.FromSeconds(5);
                            _warningDismissTimer.Tick += (s, e) => DismissWarningBanner();
                        }
                        _warningDismissTimer.Start();

                        // Log analytics event
                        _notificationService.ShowError($"Approaching tool call limit ({(int)percentage}/100 used). Consider starting a new session soon.");
                    }
                }
                else
                {
                    // Below 80%: Dismiss warning if shown
                    DismissWarningBanner();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap23_4_4-error] Exception in CheckToolCallLimit: {ex.Message}");
            }
        }

        #pragma warning disable VSTHRD100
        private async void ExecuteSendMessage()
#pragma warning restore VSTHRD100
        {
            try
            {
                IsStreaming = true;
                _streamingCts = new CancellationTokenSource();
                _pendingToolCalls.Clear();
                _toolCallIterationCount = 0;

                // Clear buffers for fresh stream session (gap31_3)
                _llmService.ClearStreamBuffer();
                _debugSessionService.ClearPauseCheckpoint();

                // Reset tool limit state for this action (gap23_4_4)
                // Each user-initiated send action gets its own tool call budget
                ResetToolCallLimitForAction();

                var userMessage = new ChatMessage
                {
                    Role = ChatMessageRole.User,
                    Content = InputText ?? string.Empty
                };

                // Clear input text immediately on UI thread before starting async operations
                InputText = string.Empty;

                await _sessionService.AddMessageAsync(userMessage);

                // Switch to main thread to update ObservableCollection
                await SwitchToMainThreadAsync();
                Messages.Add(userMessage);

                System.Diagnostics.Debug.WriteLine($"[a9-command-entry] ExecuteSendMessage started. CurrentMode={CurrentMode}");
                System.Diagnostics.Debug.WriteLine($"[a9-exec] ExecuteSendMessage: User message added. Role={userMessage.Role}, Content={userMessage.Content}, MessagesCount={Messages.Count}");

                // GAP22_4: Prune messages if needed before streaming
                var session = _sessionService.GetCurrentSession();
                if (session?.Messages.Count > 1)
                {
                    var selectedModel = _configService.GetSelectedModel();
                    if (selectedModel != null && selectedModel.ContextWindow > 0)
                    {
                        int availableTokens = (int)(selectedModel.ContextWindow * 0.75);
                        int approximateNewMessageTokens = (userMessage.Content?.Length ?? 0 + 3) / 4;

                        System.Diagnostics.Debug.WriteLine($"[gap22-prune-check] ContextWindow={selectedModel.ContextWindow}, Available={availableTokens}, NewMsgTokens~={approximateNewMessageTokens}");

                        if (approximateNewMessageTokens > availableTokens / 2)
                        {
                            var (removedCount, prunedMessages) = await _sessionService.PruneOldMessagesAsync(availableTokens);
                            System.Diagnostics.Debug.WriteLine($"[gap22-pruned] Removed {removedCount} messages to stay under token limit");
                        }
                    }
                }

                StreamingResponse = string.Empty;

                // gap34: Build combined system message (mode prompt + context block)
                var selectedModelForPackaging = _configService.GetSelectedModel();
                var systemContent = GetSystemMessageForMode(CurrentMode);

                // gap32_1: Build effective context — start from SelectedContext, inject active file if not already present
                var effectiveContext = new List<ContextItem>(SelectedContext);
                if (_ideService != null)
                {
                    var activePath = _ideService.GetActiveFilepath();
                    if (!string.IsNullOrEmpty(activePath)
                        && !effectiveContext.Any(c => string.Equals(c.FilePath, activePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        string nonNullPath = activePath!;
                        var activeContent = await _ideService.ReadFileAsync(nonNullPath);
                        if (!string.IsNullOrEmpty(activeContent))
                        {
                            effectiveContext.Insert(0, new ContextItem
                            {
                                Type = ContextItemType.File,
                                FilePath = activePath,
                                Content = activeContent,
                                Source = "active-file",
                                Relevance = 1.0
                            });
                            System.Diagnostics.Debug.WriteLine($"[gap32_1] Active file injected into context: {activePath}");
                        }
                    }
                }

                if (effectiveContext.Count > 0)
                {
                    var contextSummary = string.Join("\n",
                        effectiveContext.Select(c => c.FilePath + ": " + c.Content));
                    systemContent += "\n\nContext:\n" + contextSummary;
                }

                var systemMessage = new ChatMessage
                {
                    Role = ChatMessageRole.System,
                    Content = systemContent
                };

                // gap34-audit: log session history count vs. packaged payload
                var sessionForAudit = _sessionService.GetCurrentSession();
                System.Diagnostics.Debug.WriteLine(
                    $"[gap34-audit] history turns in session: {sessionForAudit?.Messages.Count ?? 0}, packaging with token-budget pruning");

                // gap34: Package messages — system + pruned history + new user turn
                var messages = _sessionService.PackageMessages(
                    selectedModelForPackaging,
                    systemMessage,
                    userMessage.Content ?? string.Empty);

                // Loop until no more tool calls or max iterations reached
                while (_toolCallIterationCount < MaxToolCallIterations)
                {
                    _toolCallIterationCount++;
                    _toolFailureCount = 0;  // Reset failure counter for this iteration
                    _pendingToolCalls.Clear();

                    var streamOptions = new StreamOptions
                    {
                        Messages = messages
                    };

                    // Create provisional assistant message BEFORE streaming starts
                    // This allows UI to display responses incrementally as chunks arrive
                    var assistantMessage = new ChatMessage
                    {
                        Role = ChatMessageRole.Assistant,
                        Content = string.Empty,
                        ToolCalls = null
                    };

                    // Switch to main thread to update ObservableCollection
                    await SwitchToMainThreadAsync();
                    Messages.Add(assistantMessage);

                    // Stream directly without retry wrapper:
                    // Streaming operations can't be safely retried because chunks are consumed as they arrive.
                    // The HTTP connection is stateful, and mid-stream retries would lose already-received chunks.
                    // If streaming fails, the entire message is lost and the caller (UI) handles the error.
                    await foreach (var chunk in _llmService.StreamAsync(messages, streamOptions, _streamingCts.Token))
                    {
                        if (chunk.Type == ChunkType.Text)
                        {
                            // Update the message content in place - this triggers PropertyChanged
                            // and the UI updates with the new content
                            assistantMessage.Content += chunk.Content;
                            StreamingResponse += chunk.Content;
                        }
                        else if (chunk.Type == ChunkType.ToolCall && chunk.ToolCall != null)
                        {
                            _pendingToolCalls.Add(chunk.ToolCall);
                        }
                    }

                    // Finalize the message with tool calls and add to session
                    if (_pendingToolCalls.Count > 0)
                    {
                        assistantMessage.ToolCalls = new List<ToolCall>(_pendingToolCalls);
                    }
                    await _sessionService.AddMessageAsync(assistantMessage);
                    System.Diagnostics.Debug.WriteLine($"[a9-command-assistant] Assistant message added. Role={assistantMessage.Role}, Content length={assistantMessage.Content.Length}, ToolCallsCount={_pendingToolCalls.Count}");

                    // gap23_4_4: Check tool call limit and show banners
                    CheckToolCallLimit();

                    // Only execute tools in Agent mode
                    System.Diagnostics.Debug.WriteLine($"[a9-command-toolcheck] Checking tool execution: CurrentMode={CurrentMode}, _pendingToolCalls.Count={_pendingToolCalls.Count}, ShouldExecute={CurrentMode == ChatMode.Agent && _pendingToolCalls.Count > 0}");
                    if (CurrentMode == ChatMode.Agent && _pendingToolCalls.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[a9-command-toolexec] Executing tools in Agent mode");
                        _toolFailureCount = await ExecuteToolCallsAsync(_pendingToolCalls);
                        System.Diagnostics.Debug.WriteLine($"[gap23_3-loop] Iteration {_toolCallIterationCount}: {_pendingToolCalls.Count} tools executed, {_toolFailureCount} failures");

                        // Check error accumulation: 2+ failures trigger loop termination
                        if (_toolFailureCount >= 2)
                        {
                            System.Diagnostics.Debug.WriteLine($"[gap23_3-error] Too many tool failures ({_toolFailureCount}). Terminating loop.");
                            break;
                        }

                        // Collect tool results for next iteration
                        var toolResultMessages = new List<ChatMessage>();
                        foreach (var toolMsg in Messages.Where(m => m.Role == ChatMessageRole.Tool))
                        {
                            toolResultMessages.Add(toolMsg);
                        }

                        // Add tool results to messages for next loop iteration
                        messages.AddRange(toolResultMessages);

                        // Reset streaming response for next iteration
                        StreamingResponse = string.Empty;
                    }
                    else
                    {
                        // No tools or not in Agent mode, break the loop
                        System.Diagnostics.Debug.WriteLine($"[gap23_3-loop] No tools or not in Agent mode. Breaking loop.");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[ChatPageViewModel.ExecuteSendMessage] OperationCanceledException: User cancelled");
                StreamingResponse += "\n[Cancelled by user]";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Exception caught: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Exception message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Exception stack trace: {ex.StackTrace}");

                // Gap23_3: Distinguish between LLM streaming failure and other errors
                if (ex is HttpRequestException || ex is InvalidOperationException && ex.Message.Contains("stream"))
                {
                    System.Diagnostics.Debug.WriteLine("[gap23_3-error] LLM streaming failed, terminating loop");
                }

                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"[ChatPageViewModel.ExecuteSendMessage] Showing error popup: {ex.Message}");
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
            finally
            {
                IsStreaming = false;
                _streamingCts?.Dispose();
            }
        }

        /// <summary>
        /// Gets the policy for a tool from cached UIState.
        /// Defaults to AskFirst (safe) if:
        ///   - Tool not found in UIState
        ///   - UIState is null
        /// </summary>
        private ToolPolicy GetToolPolicy(string toolName)
        {
            if (_cachedUIState == null || _cachedUIState.ToolSettings == null)
            {
                System.Diagnostics.Debug.WriteLine($"[gap9-policy-default] No UIState cached; defaulting {toolName} to AskFirst");
                return ToolPolicy.AskFirst;
            }

            if (_cachedUIState.ToolSettings.TryGetValue(toolName, out var policy))
            {
                System.Diagnostics.Debug.WriteLine($"[gap9-policy-lookup] Tool {toolName} policy: {policy}");
                return policy;
            }

            System.Diagnostics.Debug.WriteLine($"[gap9-policy-missing] Tool {toolName} not in UIState; defaulting to AskFirst");
            return ToolPolicy.AskFirst;
        }

        /// <summary>
        /// Executes pending tool calls and creates Tool role messages with results.
        /// Gap23_3: Returns the number of failures in this batch for error accumulation.
        /// Tracks cumulative failures for loop termination logic (2+ failures = stop).
        /// Gap9: Respects tool policies (AutoApprove, AskFirst, Disabled) from UIState.
        /// </summary>
        private async Task<int> ExecuteToolCallsAsync(List<ToolCall> toolCalls)
        {
            int failureCount = 0;
            foreach (var toolCall in toolCalls)
            {
                // Get tool policy from cached UIState
                var policy = GetToolPolicy(toolCall.Name);

                // Apply policy logic
                if (policy == ToolPolicy.Disabled)
                {
                    System.Diagnostics.Debug.WriteLine($"[gap9-policy-apply] Tool {toolCall.Name} is DISABLED; skipping execution");

                    await SwitchToMainThreadAsync();
                    var disabledMessage = new ChatMessage
                    {
                        Role = ChatMessageRole.Tool,
                        Content = $"[Policy: Disabled] Tool '{toolCall.Name}' is disabled and cannot be executed.",
                        InvocationStatus = ToolInvocationStatus.Skipped,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    Messages.Add(disabledMessage);
                    await _sessionService.AddMessageAsync(disabledMessage);
                    continue;
                }

                if (policy == ToolPolicy.AskFirst)
                {
                    System.Diagnostics.Debug.WriteLine($"[gap9-policy-apply] Tool {toolCall.Name} requires approval (AskFirst); skipping for now");

                    // TODO: Show approval dialog for AskFirst tools
                    // For now, stub it with a message
                    await SwitchToMainThreadAsync();
                    var askFirstMessage = new ChatMessage
                    {
                        Role = ChatMessageRole.Tool,
                        Content = $"[Policy: AskFirst] Tool '{toolCall.Name}' requires your approval. Use Agent Mode with tool approval dialog to execute.",
                        InvocationStatus = ToolInvocationStatus.Skipped,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    Messages.Add(askFirstMessage);
                    await _sessionService.AddMessageAsync(askFirstMessage);
                    continue;
                }

                // AutoApprove: Execute immediately
                System.Diagnostics.Debug.WriteLine($"[gap9-policy-apply] Tool {toolCall.Name} is AutoApprove; executing immediately");

                var toolMessage = new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Content = $"[Executing: {toolCall.Name}]",
                    InvocationStatus = ToolInvocationStatus.Running,
                    ExecutionStartTime = DateTime.Now
                };
                await _sessionService.AddMessageAsync(toolMessage);

                // Switch to main thread to update ObservableCollection
                await SwitchToMainThreadAsync();
                Messages.Add(toolMessage);
                try
                {
                    var result = await _toolService.InvokeAsync(
                        toolCall.Name,
                        toolCall.Arguments ?? new Dictionary<string, object>(),
                        _streamingCts?.Token ?? CancellationToken.None);

                    toolMessage.Content = $"Tool '{toolCall.Name}' result: {result.Output}";
                    toolMessage.InvocationStatus = ToolInvocationStatus.Complete;
                    toolMessage.ExecutionEndTime = DateTime.Now;

                    System.Diagnostics.Debug.WriteLine($"[gap9-exec] Tool '{toolCall.Name}' executed successfully. Result: {result.Output}");

                }
                catch (InvalidOperationException ex)
                {
                    // gap23_4_3: Tool call limit reached (gap23_4_3)
                    System.Diagnostics.Debug.WriteLine($"[gap23_4_3-limit-caught] {ex.Message}");
                    _limitReachedFlag = true;
                    await SwitchToMainThreadAsync();
                    _notificationService?.ShowError(ex.Message);
                    SendMessageCommand.RaiseCanExecuteChanged();
                    throw; // Stop tool execution loop when limit is hit
                }
                catch (Exception ex)
                {
                    toolMessage.Content = $"Tool '{toolCall.Name}' failed: {ex.Message}";
                    toolMessage.InvocationStatus = ToolInvocationStatus.Failed;
                    toolMessage.ExecutionEndTime = DateTime.Now;
                    failureCount++;

                    System.Diagnostics.Debug.WriteLine($"[gap9-exec] Tool '{toolCall.Name}' execution failed: {ex.Message}");
                }
            }
            return failureCount;
        }

        private bool CanSendMessage()
        {
            // gap23_4_3: Disable send while limit is active
            // gap23_4_4: Also disable when error banner is shown
            // gap31_2: Also disable send when paused (only resume/cancel allowed)
            return !IsStreaming && !string.IsNullOrWhiteSpace(InputText) && !_limitReachedFlag && !ShowErrorBanner && !IsPaused;
        }

        private void ExecuteCancel()
        {
            _streamingCts?.Cancel();
        }

        /// <summary>
        /// Executes the pause command by toggling the pause state (gap31_1).
        /// gap31_2: When pause is activated, cancels the active streaming token to interrupt LLM response.
        /// gap31_3: When pause is activated, captures a checkpoint of buffered streamed content.
        /// When pause is deactivated (resume), allows new streams to proceed.
        /// </summary>
#pragma warning disable VSTHRD100
        private async void ExecutePause()
#pragma warning restore VSTHRD100
        {
            IsPaused = !IsPaused;
            RaisePropertyChanged(nameof(IsPausedDisplay));

            // gap31_2: Cancel active stream if pause activated and streaming is in progress
            if (IsPaused && IsStreaming && _streamingCts != null && !_streamingCts.Token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine("[gap31_2-pause] Cancelling active stream due to pause signal");
                _streamingCts.Cancel();

                // gap31_3: Capture checkpoint with buffered stream state
                try
                {
                    var buffer = _llmService.GetStreamBuffer();
                    var streamedText = string.Concat(buffer.Select(c => c.Content));

                    var snapshot = new Dictionary<string, string>();
                    foreach (var ci in SelectedContext)
                    {
                        // Create a display text from available properties
                        var displayKey = ci.Type.ToString();
                        var displayValue = ci.FilePath ?? ci.Source ?? ci.Content?.Substring(0, Math.Min(50, ci.Content.Length)) ?? "Unknown";
                        snapshot[displayKey] = displayValue;
                    }

                    var checkpoint = new PauseCheckpoint
                    {
                        StreamedText = streamedText,
                        ChunkCount = buffer.Count,
                        PauseTimestamp = DateTime.UtcNow,
                        SessionContextSnapshot = snapshot
                    };

                    await _debugSessionService.SetPauseCheckpointAsync(checkpoint);
                    System.Diagnostics.Debug.WriteLine(
                        $"[gap31_3-checkpoint] Captured pause checkpoint: {checkpoint.ChunkCount} chunks, {streamedText.Length} chars");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[gap31_3-checkpoint] Error capturing checkpoint: {ex.Message}");
                }
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteAddContext(string query)
#pragma warning restore VSTHRD100
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return;

                var items = await _contextService.GetContextItemsAsync(query, maxItems: 5);
                SelectedContext.Clear();
                foreach (var item in items)
                {
                    SelectedContext.Add(item);
                }
            }
            catch (Exception ex)
            {
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
        }

        /// <summary>
        /// Gets the system message prompt for the specified chat mode.
        /// Uses the SystemPromptService to load from config file, with fallback to defaults.
        /// </summary>
        private string GetSystemMessageForMode(ChatMode mode)
        {
            var modeKey = mode.ToString().ToLowerInvariant();
            return _systemPromptService.GetPromptForMode(modeKey);
        }

        /// <summary>
        /// Executes the delete message command.
        /// Removes the message from the collection and persists the deletion to the session service.
        /// </summary>
        private void ExecuteDeleteMessage(string messageId)
        {
            System.Diagnostics.Debug.WriteLine($"[delete-cmd] ExecuteDeleteMessage called with ID: {messageId}");

            if (string.IsNullOrWhiteSpace(messageId))
            {
                System.Diagnostics.Debug.WriteLine($"[delete-cmd] messageId is null/empty, aborting");
                return;
            }

            // Find and remove message from collection
            var messageToDelete = Messages.FirstOrDefault(m => m.Id == messageId);
            if (messageToDelete == null)
            {
                System.Diagnostics.Debug.WriteLine($"[delete-cmd] Message with ID {messageId} not found in collection. Available: {string.Join(",", Messages.Select(m => m.Id))}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[delete-cmd] Found message, removing from collection. Current count: {Messages.Count}");
            Messages.Remove(messageToDelete);
            System.Diagnostics.Debug.WriteLine($"[delete-cmd] Message removed. New count: {Messages.Count}");

            // Persist deletion asynchronously (fire-and-forget with error handling)
            _ = ExecuteDeleteMessageAsync(messageId, messageToDelete);
        }

        /// <summary>
        /// Asynchronously persists message deletion to the service.
        /// If deletion fails, restores the message to the collection and notifies the user.
        /// </summary>
        private async Task ExecuteDeleteMessageAsync(string messageId, ChatMessage messageToRestore)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[delete-service] Calling DeleteMessageAsync for ID: {messageId}");
                await _sessionService.DeleteMessageAsync(messageId);
                System.Diagnostics.Debug.WriteLine($"[delete-service] Successfully deleted message ID: {messageId}");
            }
            catch (Exception ex)
            {
                // If service deletion fails, add message back and notify user
                System.Diagnostics.Debug.WriteLine($"[delete-service] Delete failed, restoring message: {ex.Message}");
                Messages.Add(messageToRestore);
                await _notificationService.ShowNotificationAsync("Delete Failed", 
                    $"Could not delete message: {ex.Message}", NotificationType.Error);
                System.Diagnostics.Debug.WriteLine($"[delete-error] Service deletion failed: {ex.Message}");
            }
        }
    }
}
