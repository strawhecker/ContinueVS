using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Utilities;
using ContinueVS.ViewModels.Models;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Newtonsoft.Json;

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
        private readonly IInstructionExecutorService _instructionExecutorService;
        private readonly IChangeStackService _changeStackService;
        private readonly IMarkdownService _markdownService;
        private readonly ILlmQuestionService? _llmQuestionService;
        private IModeService? _modeService;
        private IWorkflowService? _workflowService;
        private readonly IIdeService? _ideService;
        // gap43_3: Optional plan output service for persisting Plan mode responses
        private readonly IPlanOutputService? _planOutputService;
        private IModeConfigRegistry _modeConfigRegistry;
        // gap59: Agent command dispatcher for policy-enforced tool execution
        private readonly IAgentCommandDispatcher _agentCommandDispatcher;
        // gap72: MessengerService for tool call schema conversion (optional; used when present)
        private readonly IMessengerService? _messengerService;
        // gap78: Tool call aggregator for buffering streaming tool call fragments
        private readonly IToolCallAggregator _toolCallAggregator;
        // gap80_1: Tool-result lifetime engine (coverage supersession, mutation invalidation,
        // directory-snapshot staleness, failed-mutation pivot). Applies to collected tool results
        // before the next LLM iteration.
        private readonly IToolResultLifetimeService _toolResultLifetimeService;
        // gap80_1: Tool-result messages accumulated within the current auto-continuation turn so
        // the lifetime engine can supersede across recursive Ollama iterations (tool results are
        // in-flight, never persisted to session history). Cleared on each continuation pass.
        private readonly List<ChatMessage> _continuationToolResults = new List<ChatMessage>();
        private UIState? _cachedUIState;

        private string? _inputText;
        private bool _isStreaming;
        private string? _streamingResponse;
        private CancellationTokenSource? _streamingCts;
        private ChatMode _currentMode = ChatMode.Ask;
        private ModelInfo? _selectedModel;
        private List<ToolCall> _pendingToolCalls = new List<ToolCall>();
        private bool _isInitialized = false;
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
        /// Tracks per-block action selections (gap53).
        /// Key: block ID (GUID); Value: selected action string ("Copy" or "Apply").
        /// Allows independent action decisions for each code block in multi-block responses.
        /// </summary>
        private Dictionary<string, string> _codeBlockActions = new Dictionary<string, string>();

        /// 
        /// Backing collection for available chat mode options (gap27_1).
        /// </summary>
        private ObservableCollection<ModeOption>? _availableModes;

        /// <summary>
        /// Backing field for the currently selected mode option (gap27_1).
        /// </summary>
        private ModeOption? _selectedMode;

        /// <summary>
        /// gap74: Current context budget state (Safe/Caution/Locked).
        /// Updated by MessengerService.ContextBudgetStateChanged event.
        /// </summary>
        private ContextBudgetState _currentBudgetState = ContextBudgetState.Safe;

        /// <summary>
        /// gap74: Computed text for Optimize & Continue button.
        /// Normal: "Optimize & Continue"
        /// Caution: "⚠️ CAUTION: Optimize & Continue"
        /// Locked: "🔴 LOCKED: Optimize & Continue"
        /// </summary>
        private string _optimizeButtonText = "Optimize & Continue";

        /// <summary>
        /// gap74: Whether user input is enabled (disabled when budget is Locked).
        /// </summary>
        private bool _isInputEnabled = true;


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

        /// <summary>
        /// Flag to track if the latest assistant response contains a file path (gap49).
        /// Used to enable/disable the Apply button in the Copy/Apply dropdown.
        /// </summary>
        private bool _currentResponseHasFilePath = false;

        /// <summary>
        /// Enum for code action selection (gap49).
        /// Tracks whether user selected Copy or Apply from the dropdown.
        /// </summary>
        private int _selectedCodeAction = 0; // 0=Copy, 1=Apply

        /// <summary>
        /// Backing field for current session metadata (gap76).
        /// </summary>
        private SessionMetadata? _currentSession;

        /// <summary>
        /// Backing collection for available sessions (gap76).
        /// </summary>
        private ObservableCollection<SessionMetadata>? _availableSessions;

        /// <summary>
        /// Available message width for text wrapping (calculated from ChatPage container).
        /// Set by ChatPage.xaml.cs based on scrollviewer width minus scrollbar and padding.
        /// Bound by StreamingReasoningRenderer and other message controls for proper text wrapping.
        /// </summary>
        private double _availableMessageWidth = 600; // Default fallback

        public ObservableCollection<ChatMessage> Messages { get; }
        public ObservableCollection<ContextItem> SelectedContext { get; }
        public ObservableCollection<ModelInfo> AvailableModels { get; }

        /// <summary>
        /// Filtered view of Messages for UI display (gap75: Hide internal messages from user).
        /// Only includes User and Assistant messages with content.
        /// System, Tool, and internal messages are excluded from user display but still persisted for LLM context.
        /// </summary>
        public ObservableCollection<ChatMessage> DisplayMessages { get; }

        /// <summary>
        /// Gets or sets the currently active session metadata (gap76).
        /// Updated when user selects a session from history or creates a new one.
        /// </summary>
        public SessionMetadata? CurrentSession
        {
            get => _currentSession;
            set => Set(ref _currentSession, value);
        }

        /// <summary>
        /// Gets the collection of available sessions (gap76).
        /// Populated by RefreshSessionsAsync() and displayed in HistoryView.
        /// Sorted by Most Recent first (LastModifiedAt descending).
        /// </summary>
        public ObservableCollection<SessionMetadata> AvailableSessions
        {
            get
            {
                if (_availableSessions == null)
                {
                    _availableSessions = new ObservableCollection<SessionMetadata>();
                }
                return _availableSessions;
            }
        }

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
                            new ModeOption("Ask", ChatMode.Ask, "Basic Q&A with optional Apply button for code suggestions.", "\U0001F4AC"),
                            new ModeOption("Agent", ChatMode.Agent, "Autonomous tool calling and code editing with user approval.", "\U0001F916"),
                            new ModeOption("Plan", ChatMode.Plan, "Read-only plan generation and review.", "\U0001F4CB"),
                            new ModeOption("Debug", ChatMode.Debug, "Instrumentation-driven error diagnosis with interactive refinement.", "\U0001F527"),
                            new ModeOption("Reason", ChatMode.Reason, "Structured chain-of-thought reasoning before answering.", "\U0001F9E0")
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
                new PolicyOption("Automatically continue", ContinuationPolicy.Auto, "Continue to next tool without pause", "?"),
                new PolicyOption("Ask before each action", ContinuationPolicy.Interactive, "Show UI prompt before each tool execution", "?"),
                new PolicyOption("Defer for review", ContinuationPolicy.Deferred, "Queue execution for later review (safest)", "??")
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
                    NewChatCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string? StreamingResponse
        {
            get => _streamingResponse;
            set => Set(ref _streamingResponse, value);
        }

        /// <summary>
        /// Gets or sets the available message width for text wrapping.
        /// Calculated by ChatPage.xaml.cs based on ScrollViewer width minus scrollbar and padding.
        /// Bound by StreamingReasoningRenderer to constrain text wrapping.
        /// </summary>
        public double AvailableMessageWidth
        {
            get => _availableMessageWidth;
            set => Set(ref _availableMessageWidth, value);
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
                var previousMode = _currentMode;
                LoggerService.Current.WriteDebug($"[a9-property-entry] CurrentMode setter: oldValue={_currentMode}, newValue={value}");
                if (Set(ref _currentMode, value))
                {
                    LoggerService.Current.WriteDebug($"[a9-property-set-success] Set() returned true, property changed. New _currentMode={_currentMode}, PropertyChanged notification raised");
                    SendMessageCommand.RaiseCanExecuteChanged();
                    // gap27_1: keep SelectedMode in sync when CurrentMode is set externally (e.g. SetModeCommand)
                    var matching = AvailableModes.FirstOrDefault(m => m.Value == _currentMode);
                    if (matching != null && !ReferenceEquals(_selectedMode, matching))
                        Set(ref _selectedMode, matching, "SelectedMode");

                    // gap27_13: keep the continuation policy up to date across mode changes (no surprises)
                    UpdateContinuationPolicyForMode(previousMode, value);

                    // Notify dependents of CurrentMode so the dropdown updates immediately
                    // instead of only when WPF happens to re-render (root cause of flickering).
                    RaisePropertyChanged(nameof(IsPolicyVisible));
                    RaisePropertyChanged(nameof(IsPolicyEnabled));
                }
                else
                {
                    LoggerService.Current.WriteDebug($"[a9-property-set-noop] Set() returned false, property unchanged. _currentMode still={_currentMode}");
                }
            }
        }

        /// <summary>
        /// Gets whether the continuation policy dropdown should be shown (gap27_13).
        /// Always returns true so the control never flickers/disappears; it is simply
        /// disabled when not in Agent or Debug mode (see <see cref="IsPolicyEnabled"/>).
        /// </summary>
        public bool IsPolicyVisible => true;

        /// <summary>
        /// Gets whether the continuation policy dropdown is editable.
        /// Enabled only in Agent and Debug modes; disabled (greyed out) everywhere else.
        /// </summary>
        public bool IsPolicyEnabled => IsContinuationPolicyRelevant(CurrentMode);

        /// <summary>
        /// Returns true only for modes where the continuation policy is actually used
        /// (Agent and Debug). In all other modes the dropdown is shown but disabled.
        /// </summary>
        private static bool IsContinuationPolicyRelevant(ChatMode mode) =>
            mode == ChatMode.Agent || mode == ChatMode.Debug;

        /// <summary>
        /// Keeps the selected continuation policy consistent across mode changes (no surprises):
        /// - Entering Agent/Debug from a non-tool mode (Ask/Plan/Reason) always resets to
        ///   Interactive, since that is what the user is using there.
        /// - Switching between Agent and Debug preserves the user's current choice.
        /// </summary>
        private void UpdateContinuationPolicyForMode(ChatMode previousMode, ChatMode newMode)
        {
            if (IsContinuationPolicyRelevant(newMode) && !IsContinuationPolicyRelevant(previousMode))
            {
                Set(ref _selectedPolicy, ContinuationPolicy.Interactive, "SelectedPolicy");
            }
            // If both old and new are tool modes (Agent <-> Debug), the policy stays as-is.
            // If the new mode is not a tool mode, the dropdown is disabled and the value is irrelevant.
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

        /// <summary>
        /// Gets the icon display for the pause button (gap49 polish).
        /// Returns "?" (pause icon) when not paused, "?" (play/resume icon) when paused.
        /// </summary>
        public string IsPausedDisplayIcon => _isPaused ? "?" : "?";

        /// <summary>
        /// Gets or sets whether the latest assistant response contains a file path (gap49).
        /// Used to enable/disable the Apply button in the Copy/Apply dropdown.
        /// </summary>
        public bool CurrentResponseHasFilePath
        {
            get => _currentResponseHasFilePath;
            set => Set(ref _currentResponseHasFilePath, value);
        }

        /// <summary>
        /// Gets or sets the currently selected code action (gap49).
        /// 0=Copy: copy code to clipboard; 1=Apply: apply code to file (if path detected).
        /// </summary>
        public int SelectedCodeAction
        {
            get => _selectedCodeAction;
            set => Set(ref _selectedCodeAction, value);
        }

        /// <summary>
        /// gap74: Gets the current context budget state (Safe/Caution/Locked).
        /// Reflects the current conversation's token usage relative to model context window.
        /// Updated when MessengerService.ContextBudgetStateChanged event fires.
        /// </summary>
        public ContextBudgetState CurrentBudgetState
        {
            get => _currentBudgetState;
            set => Set(ref _currentBudgetState, value);
        }

        /// <summary>
        /// gap74: Gets the computed text for the Optimize & Continue button.
        /// Normal state: "Optimize & Continue"
        /// Caution state: "⚠️ CAUTION: Optimize & Continue"
        /// Locked state: "🔴 LOCKED: Optimize & Continue"
        /// </summary>
        public string OptimizeButtonText
        {
            get => _optimizeButtonText;
            set => Set(ref _optimizeButtonText, value);
        }

        /// <summary>
        /// gap74: Gets whether user input should be enabled.
        /// False when budget is Locked; true otherwise.
        /// Disables the message input field to prevent buffer overflow.
        /// </summary>
        public bool IsInputEnabled
        {
            get => _isInputEnabled;
            set => Set(ref _isInputEnabled, value);
        }

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
        /// Command to soft-delete (prune) a message by ID (gap81).
        /// Marks the message IsDeleted = true so it is excluded from the LLM payload,
        /// but retains the bytes in the session for undelete. Never hard-removes.
        /// </summary>
        public RelayCommand<string> SoftDeleteMessageCommand { get; }

        /// <summary>
        /// Command to undelete a previously soft-deleted message by ID (gap81).
        /// Clears the IsDeleted tombstone, restoring the entry to the LLM payload.
        /// Visibility only; performs no file-state/restore operation.
        /// </summary>
        public RelayCommand<string> UndeleteMessageCommand { get; }
        /// <summary>
        /// Command to toggle a message's minimize/maximize state (gap85).
        /// Flips the message's IsMinimized flag (UI visibility only, not a tombstone).
        /// Applies uniformly to user, reason, response, and tool-call entries.
        /// </summary>
        public RelayCommand<string> ToggleMinimizeMessageCommand { get; }
        /// <summary>
        /// Command to toggle pause state (gap31_1).
        /// </summary>
        public RelayCommand PauseCommand { get; }

        /// <summary>
        /// Command to start a new chat session (gap47).
        /// Disabled while a stream is in progress.
        /// </summary>
        public RelayCommand NewChatCommand { get; }

        /// <summary>
        /// Command to copy the selected code block to clipboard (gap49).
        /// Placeholder for codeblock template wiring.
        /// </summary>
        public RelayCommand<string> CopyCodeBlockCommand { get; }

        /// <summary>
        /// Command to apply the selected code block to the file (gap49).
        /// Enabled only when CurrentResponseHasFilePath is true.
        /// Placeholder for codeblock template wiring.
        /// </summary>
        public RelayCommand<string> ApplyCodeBlockCommand { get; }

        /// <summary>
        /// gap74: Command to backtrack and optimize conversation history.
        /// Removes newest units until conversation fits within token budget.
        /// Updates CurrentBudgetState to Safe after optimization.
        /// </summary>
        public RelayCommand OptimizeAndContinueCommand { get; }

        private Dictionary<string, TaskCompletionSource<string>> _pendingInlineQuestions = new();

        public ChatPageViewModel(
            ILlmService llmService,
            IContextService contextService,
            IToolService toolService,
            ISessionService sessionService,
            INotificationService notificationService,
            IConfigService configService,
            ISystemPromptService systemPromptService,
            IUIStateService uiStateService,
            IInstructionExecutorService instructionExecutorService,
            IChangeStackService changeStackService,
            IMarkdownService markdownService,
            ILlmQuestionService? llmQuestionService = null,
            IModeService? modeService = null,
            IWorkflowService? workflowService = null,
            IIdeService? ideService = null,
            IModeConfigRegistry? modeConfigRegistry = null,
            IPlanOutputService? planOutputService = null,
            IAgentCommandDispatcher? agentCommandDispatcher = null,
            IMessengerService? messengerService = null,
            IToolCallAggregator? toolCallAggregator = null,
            IToolResultLifetimeService? toolResultLifetimeService = null)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));
            if (contextService == null) throw new ArgumentNullException(nameof(contextService));
            if (toolService == null) throw new ArgumentNullException(nameof(toolService));
            if (sessionService == null) throw new ArgumentNullException(nameof(sessionService));
            if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (systemPromptService == null) throw new ArgumentNullException(nameof(systemPromptService));
            if (uiStateService == null) throw new ArgumentNullException(nameof(uiStateService));
            if (instructionExecutorService == null) throw new ArgumentNullException(nameof(instructionExecutorService));
            if (changeStackService == null) throw new ArgumentNullException(nameof(changeStackService));
            if (markdownService == null) throw new ArgumentNullException(nameof(markdownService));

            _llmService = llmService;
            _contextService = contextService;
            _toolService = toolService;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _configService = configService;
            _systemPromptService = systemPromptService;
            _uiStateService = uiStateService;
            _instructionExecutorService = instructionExecutorService;
            _changeStackService = changeStackService;
            _markdownService = markdownService;
            _llmQuestionService = llmQuestionService;
            _modeService = modeService;
            _workflowService = workflowService;
            _ideService = ideService;
            // gap43_3: planOutputService is optional (nullable); no null-check required
            _planOutputService = planOutputService;
            // gap44_3: fall back to default registry if none supplied — keeps existing call sites unchanged
            _modeConfigRegistry = modeConfigRegistry ?? new ModeConfigRegistry(_systemPromptService);
            // gap59: fall back to create default dispatcher if none supplied
            _agentCommandDispatcher = agentCommandDispatcher ?? new AgentCommandDispatcher(_toolService, _llmService, _modeConfigRegistry, LoggerService.Current);
            // gap72: MessengerService for tool call schema conversion (optional; used when present)
            _messengerService = messengerService;
            // gap78: Tool call aggregator; fall back to create default if none supplied
            _toolCallAggregator = toolCallAggregator ?? new ToolCallAggregator();
            // gap80_1: Tool-result lifetime engine; fall back to create default if none supplied
            _toolResultLifetimeService = toolResultLifetimeService ?? new ToolResultLifetimeService(
                new ReadDeduplicator());

            Messages = new ObservableCollection<ChatMessage>();
            SelectedContext = new ObservableCollection<ContextItem>();
            AvailableModels = new ObservableCollection<ModelInfo>();
            DisplayMessages = new ObservableCollection<ChatMessage>();
            _inputText = string.Empty;
            _streamingResponse = string.Empty;

            // gap27_1: Initialize SelectedMode to match the default CurrentMode (Ask)
            _selectedMode = AvailableModes.FirstOrDefault(m => m.Value == _currentMode);

            // gap25_6: Subscribe to messages collection changes to sync onboarding card visibility
            Messages.CollectionChanged += OnMessages_CollectionChanged;

            // gap75: Subscribe to messages changes to update DisplayMessages (filtered view for UI)
            Messages.CollectionChanged += (s, e) => UpdateDisplayMessages(e);

            SendMessageCommand = new RelayCommand(ExecuteSendMessage, CanSendMessage);
            CancelCommand = new RelayCommand(ExecuteCancel, () => IsStreaming);
            AddContextCommand = new RelayCommand<string>(ExecuteAddContext);
            SetModeCommand = new RelayCommand<ChatMode>(mode => CurrentMode = mode);
            DeleteMessageCommand = new RelayCommand<string>(ExecuteDeleteMessage);
            SoftDeleteMessageCommand = new RelayCommand<string>(ExecuteSoftDeleteMessage);
            UndeleteMessageCommand = new RelayCommand<string>(ExecuteUndeleteMessage);
            ToggleMinimizeMessageCommand = new RelayCommand<string>(ExecuteToggleMinimizeMessage);
            PauseCommand = new RelayCommand(ExecutePause, () => IsStreaming);
            NewChatCommand = new RelayCommand(() => _ = ExecuteNewChatAsync(), () => !IsStreaming);
            CopyCodeBlockCommand = new RelayCommand<string>(ExecuteCopyCodeBlock);
            ApplyCodeBlockCommand = new RelayCommand<string>(codeContent =>
            {
                if (CanApplyCodeBlock())
                    ExecuteApplyCodeBlock(codeContent);
            });
            OptimizeAndContinueCommand = new RelayCommand(() => _ = ExecuteOptimizeAndContinueAsync());

            _ = InitializeAsync();
            _configService.ConfigChanged += ConfigService_ConfigChanged;

            // gap23_4_3: Reset limit flag when session changes
            _sessionService.SessionChanged += (s, e) =>
            {
                // gap23_4_5: Refresh counter display on any session change
                RefreshToolCallCounter();

                // gap27_5: Restore mode from session when loading (CurrentMode set in event)
                if (e.CurrentMode.HasValue)
                {
                    var restoredMode = ContinueVS.Services.Utilities.ModeValidator.CoerceToValidMode(e.CurrentMode.Value);
                    LoggerService.Current.WriteDebug($"[gap27_5-restore] Session loaded with mode {e.CurrentMode.Value}, coerced to {restoredMode}");
                    CurrentMode = (ChatMode)restoredMode;
                }
            };
        }

        /// <summary>
        /// Resets tool call limit state when a new user action (send) begins (gap79).
        /// The tool budget is per-action: ONLY a real user Send resets the counter.
        /// Auto-continuations (ContinueConversationWithOllamaAsync) never call this,
        /// so they accumulate toward the loop-stopper.
        /// </summary>
        private void ResetToolCallLimitForAction()
        {
            _limitReachedFlag = false;
            ShowWarningBanner = false;
            ShowErrorBanner = false;
            DismissWarningBanner(); // Stop any active dismissal timer
            SendMessageCommand.RaiseCanExecuteChanged();

            // gap79: Reset the per-action counter on a real Send click
            try
            {
                var session = _sessionService?.GetCurrentSession();
                if (session != null)
                    session.ToolCallsExecuted = 0;
            }
            catch
            {
                // Ignore session access errors in unit test contexts
            }

            // gap80_1: A new user action starts a fresh lifetime scope — clear the accumulated
            // auto-continuation tool results.
            _continuationToolResults.Clear();

            LoggerService.Current.WriteDebug("[gap79-reset] Per-action tool budget reset for new user action. Fresh budget allocated.");
        }
        /// <summary>
        /// Records the action selection for a specific code block (gap53).
        /// Called from MarkdownBlockRenderer when per-block dropdown selection changes.
        /// </summary>
        /// <param name="blockId">Unique identifier for the code block (GUID).</param>
        /// <param name="language">Programming language hint from the code block.</param>
        /// <param name="content">Raw code content of the block.</param>
        /// <param name="action">Selected action: "Copy" or "Apply".</param>
        public void RecordCodeBlockAction(string blockId, string language, string content, string action)
        {
            if (string.IsNullOrEmpty(blockId))
                return;

            _codeBlockActions[blockId] = action;
            LoggerService.Current.WriteDebug($"[gap53-block-registry] Block {blockId} (lang={language}) action recorded: {action}");
        }

        /// 
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

        /// <summary>
        /// Initializes the ChatPageViewModel asynchronously after construction.
        /// Must be called after the ViewModel is created to load all required data.
        /// </summary>
        public async Task InitializeAsync()
        {
            await _systemPromptService.LoadAsync();

            // Load models after ConfigService is initialized (only called after ServiceInitializer.InitializeAsync)
            await LoadModelsAsync();

            // Cache UIState for tool policy decisions in ExecuteToolCallsAsync
            try
            {
                _cachedUIState = await _uiStateService.GetUIStateAsync();
                LoggerService.Current.WriteDebug($"[gap9-uistate-load] UIState cached: {_cachedUIState?.ToolSettings.Count ?? 0} tool policies loaded");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap9-uistate-error] Failed to load UIState: {ex.Message}", ex);
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
                var coercedMode = ContinueVS.Services.Utilities.ModeValidator.CoerceToValidMode(defaultMode);
                LoggerService.Current.WriteDebug($"[gap27_5-init] Loaded default mode from config: {defaultMode}, coerced to {coercedMode}");

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
                LoggerService.Current.WriteError($"[gap27_5-init-error] Failed to load default mode: {ex.Message}", ex);
                // Default to Ask (already the default in _currentMode)
            }

            // gap27_16: Restore default policy from config on startup
            try
            {
                var defaultPolicy = await _configService.GetDefaultPolicyAsync();
                LoggerService.Current.WriteDebug($"[gap27_16-init] Loaded default policy from config: {defaultPolicy}");

                // Update _selectedPolicy backing field directly without triggering setter
                // to avoid saving immediately after loading
                _selectedPolicy = defaultPolicy;
                RaisePropertyChanged("SelectedPolicy");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap27_16-init-error] Failed to load default policy: {ex.Message}", ex);
                // Default to Interactive (already set in field initialization)
            }

            // gap76: Load available sessions for history view
            await RefreshSessionsAsync();

            // Mark initialization as complete
            _isInitialized = true;
            LoggerService.Current.WriteDebug("[ChatPageViewModel.InitializeAsync] Initialization complete");
        }

        /// <summary>
        /// Adds an inline LLM question to the chat message stream.
        /// Returns a Task that completes when user answers or cancels the question.
        /// </summary>
        public async Task<string> AddInlineQuestionAsync(Core.Types.LLMQuestionMessage question)
        {
            if (question == null)
                throw new ArgumentNullException(nameof(question));

            var questionId = question.Id;
            if (string.IsNullOrEmpty(questionId))
                throw new InvalidOperationException("LLMQuestionMessage Id cannot be null or empty");

            var tcs = new TaskCompletionSource<string>();
#pragma warning disable CS8604
            _pendingInlineQuestions[questionId] = tcs;
#pragma warning restore CS8604

            question.OnAnswerAsync = async (answer) =>
            {
                await RemoveInlineQuestionAsync(questionId);
                tcs.TrySetResult(answer);
            };

            question.OnCancelAsync = async () =>
            {
                await RemoveInlineQuestionAsync(questionId);
                var defaultAnswer = AutoAnswerPolicyRegistry.GetDefaultAnswer(question.QuestionType, AutoAnswerResponse.Default);
                tcs.TrySetResult(defaultAnswer);
            };

            await SwitchToMainThreadAsync();
            Messages.Add((ChatMessage)(object)question);

            return await tcs.Task;
        }

        /// <summary>
        /// Removes an inline question from the chat message stream.
        /// </summary>
        public async Task RemoveInlineQuestionAsync(string questionId)
        {
            if (questionId == null)
                throw new ArgumentNullException(nameof(questionId));

            await SwitchToMainThreadAsync();

            var question = Messages.OfType<Core.Types.LLMQuestionMessage>()
                .FirstOrDefault(q => q.Id == questionId);

            if (question != null)
            {
                Messages.Remove((ChatMessage)(object)question);
                _pendingInlineQuestions.Remove(questionId);
            }
        }

        // Chat message rendering and streaming methods continue below:


        private async Task LoadModelsAsync()
        {
            try
            {
                var config = _configService.GetCurrentConfig();
                if (config?.Models != null && config.Models.Any())
                {
                    var models = config.Models.ToList();

                    // Dispatch collection updates to the main UI thread
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        AvailableModels.Clear();
                        foreach (var model in models)
                        {
                            AvailableModels.Add(model);
                        }

                        if (AvailableModels.Count > 0 && _selectedModel == null)
                        {
                            // Restore the persisted/configured selected model (SelectedModelId).
                            // Only fall back to the first/default model when no model has been set,
                            // so the user's chosen model is not reset on every restart.
                            SelectedModel = ResolveInitialSelectedModel(AvailableModels);
                            LoggerService.Current.WriteDebug($"[chat-model-load] Loaded {AvailableModels.Count} models, selected: {SelectedModel?.Name}");
                        }
                    });
#pragma warning restore VSTHRD001
                }
                else
                {
                    LoggerService.Current.WriteDebug($"[chat-model-load] No models in config (config null={config == null}, Models null/empty={config?.Models == null || !config.Models.Any()})");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[chat-model-load-error] {ex.GetType().Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolves the initial selected model when the view model first loads models.
        /// Restores the persisted/configured selection (GetSelectedModel → SelectedModelId)
        /// so the user's chosen model is not reset to the first/default model on every restart.
        /// Only falls back to the first/default model when no model has been configured.
        /// </summary>
        internal ModelInfo? ResolveInitialSelectedModel(ObservableCollection<ModelInfo> available)
        {
            var configured = _configService.GetSelectedModel();
            if (configured != null && available.Any(m => m.Id == configured.Id))
            {
                return configured;
            }

            // No valid configured model — use the first/default model.
            return available.FirstOrDefault();
        }

        private void ConfigService_ConfigChanged(object? sender, EventArgs e)
        {
            _ = LoadModelsAsync();
        }

        /// <summary>
        /// Handles Messages collection changes to sync onboarding card visibility (gap25_6).
        /// Card is visible only when chat is empty (Messages.Count == 0).
        /// Also detects file paths in the latest assistant message (gap49).
        /// </summary>
        private void OnMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnboardingCardVisible = Messages.Count == 0;
            LoggerService.Current.WriteDebug($"[gap25_6-sync] Onboarding card visibility updated: {OnboardingCardVisible} (Messages.Count={Messages.Count})");

            // gap49: Detect file path in latest assistant message
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
            {
                var latestMessage = e.NewItems[0] as ChatMessage;
                if (latestMessage?.Role == ChatMessageRole.Assistant && latestMessage.Content != null)
                {
                    CurrentResponseHasFilePath = DetectFilePathInResponse(latestMessage.Content);
                    LoggerService.Current.WriteDebug($"[gap49-detect] File path detected in response: {CurrentResponseHasFilePath}");
                }
            }
        }

        /// <summary>
        /// gap75: Updates DisplayMessages (UI display) to show user-visible messages.
        /// Filters out System messages (internal only).
        /// Shows: User, Assistant, Thinking (reasoning), and Tool (tool-call bubble) messages.
        /// All messages (including internal) are still persisted in session for LLM context.
        /// </summary>
        private void UpdateDisplayMessages(NotifyCollectionChangedEventArgs? e = null)
        {
            // Fall back to a full rebuild when we have no event info or on Reset/Move/Replace.
            if (e == null ||
                e.Action == NotifyCollectionChangedAction.Reset ||
                e.Action == NotifyCollectionChangedAction.Move ||
                e.Action == NotifyCollectionChangedAction.Replace)
            {
                RebuildDisplayMessages();
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Add &&
                e.NewItems != null && e.NewStartingIndex >= 0)
            {
                int displayIndex = CountVisibleBefore(e.NewStartingIndex);
                foreach (var item in e.NewItems)
                {
                    if (item is ChatMessage msg && IsVisibleMessage(msg))
                    {
                        DisplayMessages.Insert(
                            displayIndex <= DisplayMessages.Count ? displayIndex : DisplayMessages.Count,
                            msg);
                        displayIndex++;
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is ChatMessage msg)
                        DisplayMessages.Remove(msg);
                }
            }

            LoggerService.Current.WriteDebug($"[gap75-filter] DisplayMessages updated: {DisplayMessages.Count} visible messages from {Messages.Count} total");
        }

        /// <summary>
        /// gap75: Full rebuild of DisplayMessages from Messages (filtering user-visible roles).
        /// Used for resets, moves, replaces, and on session load.
        /// </summary>
        private void RebuildDisplayMessages()
        {
            DisplayMessages.Clear();

            foreach (var msg in Messages)
            {
                if (IsVisibleMessage(msg))
                {
                    DisplayMessages.Add(msg);
                }
            }

            LoggerService.Current.WriteDebug($"[gap75-filter] DisplayMessages rebuilt: {DisplayMessages.Count} visible messages from {Messages.Count} total");
        }

        private static bool IsVisibleMessage(ChatMessage msg)
        {
            // Display User, Assistant, Thinking (reasoning), and Tool messages.
            // Tool messages (gap85 tool-call bubbles + gap87 fabricated descriptions)
            // are user-visible so the user can see what a tool call did and prune/evaluate it.
            // Only System messages remain internal/LLM-only.
            return msg.Role == ChatMessageRole.User ||
                   msg.Role == ChatMessageRole.Assistant ||
                   msg.Role == ChatMessageRole.Thinking ||
                   msg.Role == ChatMessageRole.Tool;
        }

        private int CountVisibleBefore(int messagesIndex)
        {
            int visible = 0;
            for (int i = 0; i < messagesIndex && i < Messages.Count; i++)
            {
                if (IsVisibleMessage(Messages[i])) visible++;
            }
            return visible;
        }

        private bool DetectFilePathInResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var patterns = new[]
            {
                @"""path""\s*:\s*""[^""]*""",  // JSON: "path": "..."
                @"path\s*:\s*[^\s,]+",          // path: /some/path
                @"file\s*:\s*[^\s,]+",          // file: /some/file
                @"filepath\s*:\s*[^\s,]+",      // filepath: /some/path
                @"filename\s*:\s*[^\s,]+",      // filename: something.txt
                @"[A-Za-z]:\\[^\s]+",           // Windows path: C:\path\to\file
                @"/[^\s]+"                      // Unix path: /path/to/file
            };

            foreach (var pattern in patterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(content, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts the first file path found in the response content using regex patterns (gap49_advanced).
        /// Matches: JSON path, path:, file:, filepath:, filename:, Windows paths, Unix paths.
        /// Returns null if no path is found.
        /// </summary>
        private string? ExtractFilePathFromResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            var patterns = new[]
            {
                // JSON: "path": "..." or "file": "..." patterns
                new { pattern = @"""(path|file|filepath|filename)""\s*:\s*""([^""]+)""", groupIndex = 2 },
                // path: /some/path or file: /some/file patterns
                new { pattern = @"(?:path|file|filepath|filename)\s*:\s*([^\s,;]+)", groupIndex = 1 },
                // Windows path: C:\path\to\file
                new { pattern = @"([A-Za-z]:\\[^\s;,""']+)", groupIndex = 1 },
                // Unix path: /path/to/file (but not single / or root-like paths to avoid false positives)
                new { pattern = @"(/[a-zA-Z0-9._\-/]+(?:\.\w+)?)", groupIndex = 1 }
            };

            foreach (var patternObj in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    content,
                    patternObj.pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success && match.Groups.Count > patternObj.groupIndex)
                {
                    var filePath = match.Groups[patternObj.groupIndex].Value;
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        return filePath;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the first code block content from markdown response (gap49_advanced).
        /// Looks for fenced code blocks (```language ... ```).
        /// Returns null if no code block is found.
        /// </summary>
        private string? ExtractCodeContentFromMarkdown(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            // Match fenced code blocks: ```language\ncode\n```
            var codeBlockPattern = @"```[\w]*\n([\s\S]*?)\n```";
            var match = System.Text.RegularExpressions.Regex.Match(content, codeBlockPattern);

            if (match.Success && match.Groups.Count > 1)
            {
                var codeContent = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(codeContent))
                {
                    return codeContent;
                }
            }

            // Fallback: if no matched groups, return the entire content between ``` markers
            codeBlockPattern = @"```[\s\S]*?\n([\s\S]*?)\n```";
            match = System.Text.RegularExpressions.Regex.Match(content, codeBlockPattern);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Applies a code change by writing it to disk via ChangeStackService (gap49_advanced).
        /// Creates a temporary change stack for isolation, applies the change, and shows notifications.
        /// Logs success or error based on the outcome.
        /// </summary>
        private async Task ApplyCodeChangeAsync(string filePath, string codeContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentNullException(nameof(filePath));
                if (string.IsNullOrWhiteSpace(codeContent))
                    throw new ArgumentNullException(nameof(codeContent));

                // Create a temporary change stack for this apply operation (isolated per-apply)
                var tempStackId = _changeStackService.CreateChangeStack();

                // Create the CodeChange object
                var existingContent = string.Empty;
                try
                {
                    if (File.Exists(filePath))
                    {
                        existingContent = File.ReadAllText(filePath);
                    }
                }
                catch (Exception ex)
                {
                    // File may not exist yet, which is fine for new files
                    System.Diagnostics.Debug.WriteLine($"[gap49-apply] Could not read existing file: {ex.Message}");
                }

                var change = new CodeChange
                {
                    FilePath = filePath,
                    OldContent = existingContent,
                    NewContent = codeContent
                };

                // Apply the change via ChangeStackService
                await _changeStackService.ApplyChangeAsync(tempStackId, change, filePath);

                // Show success notification
                await _notificationService.ShowNotificationAsync(
                    "Apply Successful",
                    $"File created/updated: {Path.GetFileName(filePath)}",
                    NotificationType.Success);

                System.Diagnostics.Debug.WriteLine($"[gap49-apply] ? File written successfully: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[gap49-apply] ? Error applying code change: {ex.Message}");
                await _notificationService.ShowErrorAsync(
                    $"Failed to apply changes: {ex.Message}");
            }
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
        /// Refreshes the tool call counter display (gap23_4_5).
        /// Called when session changes or tool calls increment.
        /// </summary>
        private void RefreshToolCallCounter()
        {
            try
            {
                ToolCallCounterDisplay = GetToolCallCounterDisplay();
                LoggerService.Current.WriteDebug($"[gap23_4_5-refresh] Counter display updated: {ToolCallCounterDisplay}");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap23_4_5-refresh-error] Failed to refresh counter: {ex.Message}", ex);
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
                int maxToolCalls = UserSettings.DefaultsAsInt(config.CustomSettings, UserSettings.Agent_MaxToolCallsPerAction, 100);
                if (maxToolCalls <= 0)
                    maxToolCalls = 100;

                LoggerService.Current.WriteDebug($"[gap23_4_5-counter] toolCallsExecuted={toolCallsExecuted}, maxToolCalls={maxToolCalls}");
                return $"{toolCallsExecuted} / {maxToolCalls} tool calls";
            }
            catch
            {
                return "0 / 0 tool calls";
            }
        }

        /// <summary>
        /// Calculates the percentage of the per-action tool budget used (gap79).
        /// Returns null-safe value; defaults to 0 if session or settings not available.
        /// Reset happens only on a real Send (ResetToolCallLimitForAction).
        /// </summary>
        private double GetToolCallPercentage()
        {
            try
            {
                var session = _sessionService?.GetCurrentSession();
                var config = _configService?.GetCurrentConfig();

                if (session == null || config == null)
                    return 0.0;

                int maxToolCalls = UserSettings.DefaultsAsInt(config.CustomSettings, UserSettings.Agent_MaxToolCallsPerAction, 100);
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
                LoggerService.Current.WriteDebug($"[gap23_4_4-check] Tool call percentage: {percentage:F1}%");

                if (percentage >= 100.0)
                {
                    // At limit: Show persistent error banner, disable send
                    if (!ShowErrorBanner)
                    {
                        ShowErrorBanner = true;
                        _limitReachedFlag = true;
                        SendMessageCommand.RaiseCanExecuteChanged();
                        LoggerService.Current.WriteError("[gap23_4_4-error] Tool call limit reached (100%). Error banner shown.", new InvalidOperationException());

                        // Log analytics event
                        _notificationService.ShowError($"Per-action tool limit reached (100/100). Send again for a fresh budget.");
                    }
                }
                else if (percentage >= 80.0)
                {
                    // Approaching limit: Show auto-dismiss warning banner
                    if (!ShowWarningBanner)
                    {
                        ShowWarningBanner = true;
                        LoggerService.Current.WriteDebug($"[gap23_4_4-warning] Approaching tool call limit ({percentage:F1}%). Warning banner shown.");

                        // Start 5-second auto-dismiss timer
                        if (_warningDismissTimer == null)
                        {
                            _warningDismissTimer = new DispatcherTimer();
                            _warningDismissTimer.Interval = TimeSpan.FromSeconds(5);
                            _warningDismissTimer.Tick += (s, e) => DismissWarningBanner();
                        }
                        _warningDismissTimer.Start();

                        // Log analytics event
                        _notificationService.ShowError($"Approaching per-action tool limit ({(int)percentage}/100 used). Sending again grants a fresh budget.");
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
                LoggerService.Current.WriteError($"[gap23_4_4-error] Exception in CheckToolCallLimit: {ex.Message}", ex);
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteSendMessage()
#pragma warning restore VSTHRD100
        {
            try
            {
                // Guard: Ensure ChatPageViewModel initialization is complete
                // Since ChatPage now awaits InitializeAsync() before allowing user interaction,
                // this should never be false, but we keep it as a safety check.
                // Do NOT show messagebox here (VSIX environment issues with MessageBox.Show in certain contexts)
                if (!_isInitialized)
                {
                    LoggerService.Current.WriteWarning(
                        "[ExecuteSendMessage] WARNING: ViewModel not yet initialized; discarding user input");
                    return;
                }

                // Guard 2: Ensure ConfigService is properly initialized before proceeding
                try
                {
                    var guardModel = _configService.GetSelectedModel();
                    // If we can successfully get past this line, ConfigService is ready
                }
                catch (InvalidOperationException configEx)
                {
                    LoggerService.Current.WriteError(
                        $"[ExecuteSendMessage] ConfigService not ready: {configEx.Message}", configEx);
                    return;
                }

                IsStreaming = true;
                // Dispose old CancellationTokenSource before creating a new one
                try
                {
                    _streamingCts?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    LoggerService.Current.WriteDebug("[ExecuteSendMessage] Previous _streamingCts already disposed");
                }
                _streamingCts = new CancellationTokenSource();
                _pendingToolCalls.Clear();
                _toolCallIterationCount = 0;

                // Clear buffers for fresh stream session (gap31_3)
                _llmService.ClearStreamBuffer();
                _instructionExecutorService.ClearPauseCheckpoint();

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

                LoggerService.Current.WriteDebug($"[a9-command-entry] ExecuteSendMessage started. CurrentMode={CurrentMode}");
                LoggerService.Current.WriteDebug($"[a9-exec] ExecuteSendMessage: User message added. Role={userMessage.Role}, Content={userMessage.Content}, MessagesCount={Messages.Count}");

                // GAP22_4: Prune messages if needed before streaming
                var session = _sessionService.GetCurrentSession();
                if (session?.Messages.Count > 1)
                {
                    var selectedModel = _configService.GetSelectedModel();
                    if (selectedModel != null && selectedModel.ContextWindow > 0)
                    {
                        int availableTokens = (int)(selectedModel.ContextWindow * 0.75);
                        int approximateNewMessageTokens = (userMessage.Content?.Length ?? 0 + 3) / 4;

                        LoggerService.Current.WriteDebug($"[gap22-prune-check] ContextWindow={selectedModel.ContextWindow}, Available={availableTokens}, NewMsgTokens~={approximateNewMessageTokens}");

                        if (approximateNewMessageTokens > availableTokens / 2)
                        {
                            var (removedCount, prunedMessages) = await _sessionService.PruneOldMessagesAsync(availableTokens);
                            LoggerService.Current.WriteDebug($"[gap22-pruned] Removed {removedCount} messages to stay under token limit");
                        }
                    }
                }

                StreamingResponse = string.Empty;

                // gap44_3: Resolve mode policy once per send — drives tool-loop gate and StreamOptions
                var modeConfig = _modeConfigRegistry.GetConfig(CurrentMode);

                // gap34: Build combined system message (mode prompt + context block)
                var selectedModelForPackaging = _configService.GetSelectedModel();
                var systemContent = GetSystemMessageForMode(CurrentMode);

                // gap32_1: Build effective context — start from SelectedContext, inject active file if not already present
                var effectiveContext = new List<ContextItem>(SelectedContext);

                // gap32_1: Check if active file injection is enabled via setting
                var addCurrentFileByDefault = false;
                if (_configService != null)
                {
                    var config = _configService.GetCurrentConfig();
                    if (config?.CustomSettings?.TryGetValue(UserSettings.Experimental_AddCurrentFileByDefault, out var val) == true)
                    {
                        addCurrentFileByDefault = val switch
                        {
                            true => true,
                            "true" => true,
                            1 or 1L => true,
                            _ => false
                        };
                    }
                }
                LoggerService.Current.WriteDebug($"[gap32-setting] experimental.addCurrentFileByDefault={addCurrentFileByDefault}");

                if (addCurrentFileByDefault && _ideService != null)
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
                            LoggerService.Current.WriteDebug($"[gap32_1] Active file injected into context: {activePath}");
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
                LoggerService.Current.WriteDebug(
                    $"[gap34-audit] history turns in session: {sessionForAudit?.Messages.Count ?? 0}, packaging with token-budget pruning");

                // gap34: Package messages — system + pruned history + new user turn
                var messages = _sessionService.PackageMessages(
                    selectedModelForPackaging,
                    systemMessage,
                    userMessage.Content ?? string.Empty);

                // Loop until no more tool calls or max iterations reached
                while (_toolCallIterationCount < MaxToolCallIterations)
                {
                    // gap79: Per-action budget guard — stop the turn cleanly if the loop-stopper is hit.
                    // Auto-continuations accumulate here; a real Send (ResetToolCallLimitForAction) resets.
                    var budgetSession = _sessionService.GetCurrentSession();
                    if (budgetSession != null)
                    {
                        var budgetConfig = _configService?.GetCurrentConfig();
                        int perActionMax = budgetConfig == null ? 100
                            : UserSettings.DefaultsAsInt(budgetConfig.CustomSettings, UserSettings.Agent_MaxToolCallsPerAction, 100);
                        if (budgetSession.ToolCallsExecuted >= perActionMax)
                        {
                            _limitReachedFlag = true;
                            ShowErrorBanner = true;
                            SendMessageCommand.RaiseCanExecuteChanged();
                            LoggerService.Current.WriteWarning($"[gap79-loop] Per-action budget ({perActionMax}) exhausted; stopping turn cleanly. Send again for a fresh budget.");
                            break;
                        }
                    }

                    var streamOptions = new StreamOptions
                    {
                        Messages = messages,
                        SystemPrompt = systemContent,
                        AllowWriteTools = modeConfig.AllowWriteTools,
                        Mode = CurrentMode
                    };

                    // Create provisional assistant message BEFORE streaming starts
                    // This allows UI to display responses incrementally as chunks arrive
                    var assistantMessage = new ChatMessage
                    {
                        Role = ChatMessageRole.Assistant,
                        Content = string.Empty,
                        ToolCalls = null
                    };

                    // Add assistantMessage to UI collection immediately so binding updates work during streaming
                    await SwitchToMainThreadAsync();
                    Messages.Add(assistantMessage);

                    // Optional reasoning message to hold provider reasoning (separate from content)
                    // *** SECURITY WARNING: LLM keyword conflict risk ***
                    // Previous attempts to defer adding reasoning (commented-out .Add) broke streaming UI.
                    // THE REASONING MESSAGE MUST BE ADDED TO THE UI COLLECTION IMMEDIATELY DURING STREAMING.
                    // Do NOT comment out, defer, or conditionally add reasoning after streaming completes.
                    // The streaming UI visibility depends on real-time collection updates during chunk arrival.
                    ChatMessage? reasoningMessage = null;

                    // Stream directly without retry wrapper:
                    // Streaming operations can't be safely retried because chunks are consumed as they arrive.
                    // The HTTP connection is stateful, and mid-stream retries would lose already-received chunks.
                    // If streaming fails, the entire message is lost and the caller (UI) handles the error.
                    await foreach (var chunk in _llmService.StreamAsync(messages, streamOptions, _streamingCts.Token))
                    {
                        if (chunk.Type == ChunkType.Text)
                        {
                            // *** CRITICAL STREAMING FIX: Reasoning incremental update ***
                            // Handle reasoning content by creating a separate reasoning message
                            // CRITICAL: Must marshal to UI thread for WPF binding updates to fire correctly
                            // CRITICAL: Must ADD reasoning to Messages collection DURING streaming, not after.
                            // If reasoning add is deferred until post-stream completion, the UI will wait until
                            // the entire response is received before showing reasoning. This breaks incremental streaming.
                            if (!string.IsNullOrEmpty(chunk.Reasoning))
                            {
                                await SwitchToMainThreadAsync();

                                // Create reasoning message on first reasoning chunk
                                if (reasoningMessage == null)
                                {
                                    reasoningMessage = new ChatMessage
                                    {
                                        Role = ChatMessageRole.Thinking,
                                        Content = string.Empty,
                                        IsThinking = true,
                                        IsExpanded = false
                                    };
                                    // *** DO NOT DEFER THIS ADD ***
                                    // ADD reasoning message to UI IMMEDIATELY when first chunk arrives.
                                    // Only way to show real-time reasoning streaming in the UI.
                                    // Previous deferred approach caused reasoning to hide until completion.
                                    Messages.Add(reasoningMessage);
                                    LoggerService.Current.WriteDebug($"[ChatPageViewModel.ExecuteSendMessage] Reasoning message created and added to UI for streaming");
                                }

                                // Append reasoning to reasoning message
                                // *** INCREMENTAL UPDATE CRITICAL ***
                                // Do NOT skip this += or replace with assignment. Each chunk must accumulate.
                                reasoningMessage.AppendChunk(chunk.Reasoning!);
                                var reasoningPreview = chunk.Reasoning?.Substring(0, Math.Min(50, chunk.Reasoning?.Length ?? 0)) ?? string.Empty;
                                LoggerService.Current.WriteDebug($"[ChatPageViewModel.ExecuteSendMessage] Reasoning accumulated: {reasoningPreview}...");
                            }

                            // *** CORE STREAMING BEHAVIOR: Keep response message in collection during stream ***
                            // Update the message content in place - this triggers PropertyChanged
                            // and the UI updates with the new content
                            // CRITICAL: Must marshal to UI thread for WPF binding updates to fire correctly
                            // CRITICAL: assistantMessage stays in Messages collection during streaming.
                            // This is ESSENTIAL for incremental UI updates. Do NOT remove it during streaming.
                            if (!string.IsNullOrEmpty(chunk.Content))
                            {
                                await SwitchToMainThreadAsync();
                                assistantMessage.Content += chunk.Content;
                                StreamingResponse += chunk.Content;

                            }
                        }
                        else if (chunk.Type == ChunkType.ToolCall)
                        {
                            // gap78: Accumulate tool call text fragments
                            _toolCallAggregator.AccumulateToolCallsText(chunk.ToolCallsText);

                            // gap78: Check if completion signal received
                            if (_toolCallAggregator.CheckCompletion(chunk.DoneReason))
                            {
                                // gap78: Attempt to parse and validate accumulated tool calls
                                if (_toolCallAggregator.TryGetCompleteToolCalls(out var validToolCalls) && validToolCalls != null)
                                {
                                    LoggerService.Current.WriteDebug(
                                        $"[gap78-parsed] Successfully parsed {validToolCalls.Count} tool calls from accumulated stream");

                                    // gap72: Convert provider schemas to canonical ToolCall objects
                                    // Integrate with existing _pendingToolCalls handling
                                    foreach (var toolCallSchema in validToolCalls)
                                    {
                                        ToolCall toolCall;
                                        if (_messengerService != null)
                                        {
                                            // Use MessengerService converter if available
                                            toolCall = _messengerService.ConvertToolCallSchemaToToolCall(toolCallSchema);
                                        }
                                        else
                                        {
                                            // Fallback: manual conversion if MessengerService not injected
                                            toolCall = ConvertToolCallSchemaManually(toolCallSchema);
                                        }

                                        // gap72: Validate tool call before queuing
                                        if (toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name))
                                        {
                                            LoggerService.Current.WriteWarning(
                                                $"[gap78-convert] Skipping incomplete tool call after aggregation: Name is null or empty (id={toolCall?.Id})");
                                            continue;
                                        }

                                        _pendingToolCalls.Add(toolCall);
                                        LoggerService.Current.WriteDebug(
                                            $"[gap78-queue] Queued tool: {toolCall.Name} (id={toolCall.Id})");
                                    }

                                    // gap78: Reset aggregator for next batch
                                    _toolCallAggregator.Clear();
                                }
                                else
                                {
                                    LoggerService.Current.WriteWarning(
                                        "[gap78-parse] Failed to parse accumulated tool calls or validation failed");
                                    _toolCallAggregator.Clear();
                                }
                            }
                        }
                    }

                    // gap54: Detect and handle any embedded LLM questions in the response.
                    // IMPORTANT: The interactive question/answer wait must NOT be bound to the
                    // streaming cancellation token (_streamingCts). If it were, answering the
                    // question (or any pause/stop of the already-finished stream) would cancel
                    // the pending tcs.Task inside AddInlineQuestionAsync, throwing
                    // OperationCanceledException which the outer catch records as
                    // "User cancelled" even though the user simply answered the question.
                    // Using CancellationToken.None keeps the answer-wait isolated from the
                    // streaming lifecycle so answering is never misreported as a cancel.
                    if (!string.IsNullOrWhiteSpace(assistantMessage.Content) && _llmQuestionService != null)
                    {
                        var detectedQuestion = await _llmQuestionService.DetectLLMQuestionAsync(assistantMessage.Content, CancellationToken.None);
                        if (detectedQuestion != null)
                        {
                            LoggerService.Current.WriteDebug($"[gap54-detect] Question detected in response: {detectedQuestion.QuestionText}");
                            var answer = await _llmQuestionService.HandleLLMQuestionAsync(detectedQuestion, isAutonomous: false, AutoAnswerResponse.Default, CancellationToken.None);
                            LoggerService.Current.WriteDebug($"[gap54-handle] Question answered: {answer}");
                        }
                    }

                    // Finalize the message with tool calls and add to session
                    if (_pendingToolCalls.Count > 0)
                    {
                        assistantMessage.ToolCalls = new List<ToolCall>(_pendingToolCalls);
                    }

                    // gap68: Parse and separate thinking from response content
                    // Only parse for thinking tags if we didn't already extract reasoning via streaming
                    ChatMessage? thinkingMessage = null;

                    if (reasoningMessage == null)
                    {
                        var (parsedThinkingMessage, cleanedResponseContent) = await ParseThinkingFromResponseAsync(
                            assistantMessage.Content,
                            _streamingCts.Token);

                        thinkingMessage = parsedThinkingMessage;

                        // Update assistant message content to exclude thinking (if any was extracted)
                        if (thinkingMessage != null && !string.IsNullOrWhiteSpace(cleanedResponseContent))
                        {
                            assistantMessage.Content = cleanedResponseContent;
                            LoggerService.Current.WriteDebug(
                                "[gap68-separate] Thinking separated from response. Response length now: " + cleanedResponseContent.Length);
                        }
                        else if (thinkingMessage != null && string.IsNullOrWhiteSpace(cleanedResponseContent))
                        {
                            // Thinking was extracted but no content remained, add the thinking message
                            await SwitchToMainThreadAsync();
                            Messages.Add(thinkingMessage);
                        }
                    }
                    else
                    {
                        LoggerService.Current.WriteDebug(
                            "[gap68-skip-parse] Reasoning already extracted via streaming; skipping post-stream parsing");
                    }

                    // gap43_3 / gap45_3: Persist plan output when ExportsPlanFile is true for this mode (Agent, Plan, Debug)
                    if (modeConfig.ExportsPlanFile && _planOutputService != null && !string.IsNullOrWhiteSpace(assistantMessage.Content))
                    {
                        var savedPath = await _planOutputService.SavePlanAsync("plan", assistantMessage.Content, _streamingCts.Token);
                        LoggerService.Current.WriteDebug($"[gap43_3] Plan saved to: {savedPath}");
                    }

                    // Finalize reasoning message streaming if present
                    reasoningMessage?.FinalizeStreaming();
                    if (reasoningMessage != null)
                        await _sessionService.AddMessageAsync(reasoningMessage);

                    // Finalize the assistant message streaming to convert buffer to cached string
                    assistantMessage.FinalizeStreaming();
                    await _sessionService.AddMessageAsync(assistantMessage);
                    LoggerService.Current.WriteDebug($"[a9-command-assistant] Assistant message added. Role={assistantMessage.Role}, Content length={assistantMessage.Content.Length}, ToolCallsCount={_pendingToolCalls.Count}");

                    // *** POST-STREAM REORDERING: Collection already has all messages ***
                    // Reorder messages to ensure correct display: thinking, reasoning, response
                    // The assistant message was added DURING streaming (for incremental UI updates),
                    // The reasoning message was added DURING streaming (for incremental UI updates),
                    // but we need to reorder them to display in the correct order: thinking, reasoning, response
                    // NOTE: Reasoning message is already in the collection from streaming, so we reorder, not re-add.
                    // Do NOT skip the removal/re-add of reasoning; it must be repositioned after thinking.
                    await SwitchToMainThreadAsync();

                    // Remove assistant message from its current position (it should be the last item or near it)
                    // This prepares it to be re-added in the correct order at the end
                    Messages.Remove(assistantMessage);

                    // Add thinking message first (if present)
                    if (thinkingMessage != null && !string.IsNullOrEmpty(thinkingMessage.Content))
                    {
                        // Add debug cookie to verify thinking content is present
                        //thinkingMessage.Content += "\n\n🍪 [DEBUG: Thinking message cookie]";
                        Messages.Add(thinkingMessage);
                        LoggerService.Current.WriteDebug($"[UI-ordering] Thinking message added to UI");
                    }

                    // *** REASONING ALREADY EXISTS - REORDER ONLY ***
                    // Reasoning message is already in the collection from streaming,
                    // so only move it if it's not already in the right position.
                    // Do NOT add it twice or skip this reordering step.
                    if (reasoningMessage != null && !string.IsNullOrEmpty(reasoningMessage.Content))
                    {
                        // Add debug cookie to verify reasoning content is present
                        //reasoningMessage.Content += "\n\n🍪 [DEBUG: Reasoning message cookie]";
                        // Remove and re-add to ensure correct position after thinking
                        Messages.Remove(reasoningMessage);
                        Messages.Add(reasoningMessage);
                        LoggerService.Current.WriteDebug($"[UI-ordering] Reasoning message reordered to UI");
                    }

                    // Add the response (assistant message) last
                    Messages.Add(assistantMessage);
                    LoggerService.Current.WriteDebug($"[UI-ordering] Assistant message re-added to UI in correct position (response)");

                    // After assistant message completes:
                    if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
                    {
                        // This call is NOT optional—it's required for ScrollViewer + 
                        // MarkdownBlockRenderer to function correctly
                        assistantMessage.RenderedMarkdown = await _markdownService.ParseMarkdownAsync(
                            assistantMessage.Content
                        );
                    }
                    // gap23_4_4: Check tool call limit and show banners
                    CheckToolCallLimit();

                    // gap78: Add assistant message with tool call definitions to messages array BEFORE tool execution/collection
                    // This ensures the LLM sees the full context: [System, User, Assistant(toolCalls=[...]), Tool(result1), Tool(result2), ...]
                    if (_pendingToolCalls.Count > 0)
                    {
                        messages.Add(assistantMessage);
                        LoggerService.Current.WriteDebug($"[gap78-context] Added assistant message with {assistantMessage.ToolCalls?.Count ?? 0} tool calls to LLM context");
                    }

                    // Execute all pending tool calls (no orchestration gate; tool availability controlled by mode registry + user settings)
                    if (_pendingToolCalls.Count > 0)
                    {
                        LoggerService.Current.WriteDebug($"[a9-command-toolexec] Executing tools in Agent mode");

                        // gap78-fix: Capture how many Tool messages already exist BEFORE this
                        // iteration's execution, so we can collect ONLY the results produced now.
                        int existingToolMessageCount = Messages.Count(m => m.Role == ChatMessageRole.Tool);

                        _toolFailureCount = await ExecuteToolCallsAsync(_pendingToolCalls);
                        LoggerService.Current.WriteDebug($"[gap23_3-loop] Iteration {_toolCallIterationCount}: {_toolFailureCount} failures");

                        // Check error accumulation: 2+ failures trigger loop termination
                        if (_toolFailureCount >= 2)
                        {
                            LoggerService.Current.WriteError($"[gap23_3-error] Too many tool failures ({_toolFailureCount}). Terminating loop.", new InvalidOperationException());
                            break;
                        }

                        // Collect ONLY the tool results created in THIS iteration (gap78-fix).
                        // Previously ALL Tool messages in the UI collection were re-added on every
                        // iteration, causing duplicate results (and duplicate tool_call_ids) to
                        // accumulate in the LLM context. Ids are preserved so each result still
                        // correlates 1:1 back to its assistant tool call.
                        var toolResultMessages = new List<ChatMessage>();
                        foreach (var toolMsg in Messages
                            .Where(m => m.Role == ChatMessageRole.Tool)
                            .Skip(existingToolMessageCount))
                        {
                            toolResultMessages.Add(toolMsg);
                            LoggerService.Current.WriteDebug(
                                $"[gap78-fix-collect] Collected tool result for id={toolMsg.ToolCallId}");
                        }

                        // Add tool results to messages for next loop iteration
                        // gap80_1: Apply lifetime rules (coverage supersession, mutation
                        // invalidation, directory-snapshot staleness, failed-mutation pivot)
                        // before re-sending so superseded results are tombstoned and stale
                        // snapshots are visibly marked (never silent removal).
                        ApplyToolResultLifetime(toolResultMessages);
                        messages.AddRange(toolResultMessages);

                        // Reset streaming response for next iteration
                        StreamingResponse = string.Empty;
                    }
                    else
                    {
                        // No tools or not in a tool-loop mode — break the loop
                        LoggerService.Current.WriteDebug($"[gap23_3-loop] No tools or not in Agent mode. Breaking loop.");

                        // gap45_3: If phase execution is enabled for this mode, hand off to InstructionExecutorService
                        if (modeConfig.AllowPhaseExecution && !string.IsNullOrWhiteSpace(assistantMessage.Content))
                        {
                            var changeStackId = _changeStackService.CreateChangeStack();
                            var targetDir = System.Environment.CurrentDirectory;
                            if (_ideService != null)
                            {
                                var gitRoot = await _ideService.GetGitRootPathAsync();
                                if (!string.IsNullOrWhiteSpace(gitRoot))
                                    targetDir = gitRoot;
                            }
                            var execInstruction = new ContinueVS.Core.Types.ExecutionInstruction
                            {
                                Text = assistantMessage.Content
                            };
                            LoggerService.Current.WriteDebug($"[gap45_3] Handing off to InstructionExecutorService (mode={CurrentMode})");
                            await _instructionExecutorService.ExecuteInstructionAsync(
                                execInstruction, changeStackId, targetDir, cancellationToken: _streamingCts.Token);
                        }

                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LoggerService.Current.WriteDebug("[ChatPageViewModel.ExecuteSendMessage] OperationCanceledException: User cancelled");
                StreamingResponse += "\n[Cancelled by user]";
            }
            catch (Exception ex)
            {
#if DEBUG
                //if (DebuggerHelper.ShouldBreakOnException("ServiceName"))
                Debugger.Break();
#endif

                LoggerService.Current.WriteError($"[ChatPageViewModel.ExecuteSendMessage] Exception caught: {ex.GetType().Name}", ex);
                LoggerService.Current.WriteError($"[ChatPageViewModel.ExecuteSendMessage] Exception message: {ex.Message}", ex);
                LoggerService.Current.WriteError($"[ChatPageViewModel.ExecuteSendMessage] Exception stack trace: {ex.StackTrace}", ex);

                // Gap23_3: Distinguish between LLM streaming failure and other errors
                if (ex is HttpRequestException || ex is InvalidOperationException && ex.Message.Contains("stream"))
                {
                    LoggerService.Current.WriteError("[gap23_3-error] LLM streaming failed, terminating loop", ex);
                }

                if (ex.InnerException != null)
                {
                    LoggerService.Current.WriteError($"[ChatPageViewModel.ExecuteSendMessage] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", ex.InnerException);
                }

                LoggerService.Current.WriteError($"[ChatPageViewModel.ExecuteSendMessage] Showing error popup: {ex.Message}", ex);
                await _notificationService.ShowNotificationAsync("Error", ex.Message, NotificationType.Error);
            }
            finally
            {
                IsStreaming = false;
                // Don't dispose _streamingCts here - let it be disposed when a new send starts
                // or when its timeout expires. This prevents ObjectDisposedException when
                // streaming is still in progress but another exception occurs.
                try
                {
                    _streamingCts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    LoggerService.Current.WriteDebug("[ExecuteSendMessage] _streamingCts already disposed in finally");
                }
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
                LoggerService.Current.WriteDebug($"[gap9-policy-default] No UIState cached; defaulting {toolName} to AskFirst");
                return ToolPolicy.AskFirst;
            }

            if (_cachedUIState.ToolSettings.TryGetValue(toolName, out var policy))
            {
                LoggerService.Current.WriteDebug($"[gap9-policy-lookup] Tool {toolName} policy: {policy}");
                return policy;
            }

            LoggerService.Current.WriteDebug($"[gap9-policy-missing] Tool {toolName} not in UIState; defaulting to AskFirst");
            return ToolPolicy.AskFirst;
        }

        /// <summary>
        /// Fallback manual conversion of ToolCallSchema to ToolCall when MessengerService is unavailable.
        /// gap72: Provides provider-agnostic schema conversion without dependency on MessengerService.
        /// </summary>
        private ToolCall ConvertToolCallSchemaManually(ToolCallSchema schema)
        {
            var toolCall = new ToolCall
            {
                Id = schema.Id,
                Name = schema.Function?.Name ?? "unknown"
            };

            // Parse Arguments from JSON string to IDictionary<string, object>
            if (schema.Function?.Arguments != null && !string.IsNullOrEmpty(schema.Function.Arguments))
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<IDictionary<string, object>>(
                        schema.Function.Arguments);
                    toolCall.Arguments = parsed;
                }
                catch (JsonException argEx)
                {
                    LoggerService.Current.WriteWarning(
                        $"[gap72-fallback-convert] Failed to parse arguments for tool '{toolCall.Name}': {argEx.Message}");
                    toolCall.Arguments = new Dictionary<string, object>();
                }
            }
            else
            {
                toolCall.Arguments = new Dictionary<string, object>();
            }

            return toolCall;
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
            var modeConfig = _modeConfigRegistry.GetConfig(CurrentMode);

            var toolCallsTem = toolCalls.ToList();
            toolCalls.Clear();

            foreach (var toolCall in toolCallsTem)
            {
                // Validate tool call before execution
                if (toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name))
                {
                    LoggerService.Current.WriteWarning(
                        $"[gap59-dispatch] Skipping invalid tool call: Name is null or empty (id={toolCall?.Id})");
                    failureCount++;
                    continue;
                }

                try
                {
                    LoggerService.Current.WriteDebug(
                        $"[gap59-dispatch] Dispatching tool {toolCall.Name} (id={toolCall.Id}) via IAgentCommandDispatcher");

                    // gap59: Execute tool through dispatcher for policy validation
                    var commandArguments = toolCall.Arguments ?? new Dictionary<string, object>();
                    var toolResult = await _agentCommandDispatcher.DispatchAgentCommandAsync(
                        toolCall.Name,
                        commandArguments,
                        CurrentMode,
                        _streamingCts?.Token ?? CancellationToken.None);

                    // Convert ToolResult to ChatMessage for UI display only (not persisted to session)
                    await SwitchToMainThreadAsync();
                    var toolMessage = new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = ChatMessageRole.Tool,
                        Content = toolResult.Output,
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                        // gap87: Fabricate a display-only description from the tool-call arguments.
                        ToolCallDescription = ToolCallDescriptionBuilder.Build(toolCall),
                        InvocationStatus = toolResult.IsSuccess ? ToolInvocationStatus.Complete : ToolInvocationStatus.Failed,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    // gap80_1: Tag the message with lifetime metadata (coverage key for reads,
                    // mutation target path for mutations) so the lifetime engine can supersede.
                    ApplyLifetimeMetadataForTool(message: toolMessage, toolCall: toolCall);
                    // gap73: Tool results are displayed in UI but NOT persisted to session file
                    // They are only used for LLM context in the current loop via ConvertToolCallToSchema
                    Messages.Add(toolMessage);

                    if (!toolResult.IsSuccess)
                    {
                        failureCount++;
                        LoggerService.Current.WriteDebug(
                            $"[gap59-dispatch] Tool {toolCall.Name} completed with failure: {toolResult.Output}");
                    }
                    else
                    {
                        LoggerService.Current.WriteDebug(
                            $"[gap59-dispatch] Tool {toolCall.Name} completed successfully");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // gap59: Policy violation or mode doesn't support tool loop
                    LoggerService.Current.WriteDebug($"[gap59-dispatch-denied] Tool {toolCall.Name} policy violation: {ex.Message}");

                    await SwitchToMainThreadAsync();
                    var deniedMessage = new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = ChatMessageRole.Tool,
                        Content = $"[Policy Denied] Tool '{toolCall.Name}' cannot be executed: {ex.Message}",
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                        // gap87: Fabricate a display-only description from the tool-call arguments.
                        ToolCallDescription = ToolCallDescriptionBuilder.Build(toolCall),
                        InvocationStatus = ToolInvocationStatus.Failed,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    // gap73: Tool error messages displayed in UI but NOT persisted to session
                    Messages.Add(deniedMessage);
                    failureCount++;
                }
                catch (OperationCanceledException)
                {
                    LoggerService.Current.WriteWarning(
                        $"[gap59-dispatch] Tool {toolCall.Name} was cancelled");

                    await SwitchToMainThreadAsync();
                    var cancelledMessage = new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = ChatMessageRole.Tool,
                        Content = $"Tool '{toolCall.Name}' execution was cancelled",
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                        // gap87: Fabricate a display-only description from the tool-call arguments.
                        ToolCallDescription = ToolCallDescriptionBuilder.Build(toolCall),
                        InvocationStatus = ToolInvocationStatus.Failed,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    // gap73: Tool error messages displayed in UI but NOT persisted to session
                    Messages.Add(cancelledMessage);
                    failureCount++;
                }
                catch (Exception ex)
                {
                    // gap59-dispatch exception: log and continue loop
                    LoggerService.Current.WriteError(
                        $"[gap59-dispatch-error] Tool {toolCall.Name} execution failed: {ex.Message}", ex);

                    await SwitchToMainThreadAsync();
                    var errorMessage = new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = ChatMessageRole.Tool,
                        Content = $"Tool '{toolCall.Name}' failed: {ex.Message}",
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                        // gap87: Fabricate a display-only description from the tool-call arguments.
                        ToolCallDescription = ToolCallDescriptionBuilder.Build(toolCall),
                        InvocationStatus = ToolInvocationStatus.Failed,
                        ExecutionStartTime = DateTime.Now,
                        ExecutionEndTime = DateTime.Now
                    };
                    // gap73: Tool error messages displayed in UI but NOT persisted to session
                    Messages.Add(errorMessage);
                    failureCount++;
                }
            }
            return failureCount;
        }

        /// <summary>
        /// gap80_1: Tags a just-created tool-result message with lifetime metadata so the
        /// lifetime engine can supersede it later. Reads get a CoverageKey (path|FULL or
        /// path|RANGE(start,end)); mutations get a MutationTargetPath. Errors/denials just get
        /// the tool name (the engine treats content-signals as failure/invalidation notes).
        /// </summary>
        private void ApplyLifetimeMetadataForTool(ChatMessage message, ToolCall toolCall)
        {
            if (message == null || toolCall == null || string.IsNullOrEmpty(toolCall.Name))
                return;

            var readDeduplicator = new ReadDeduplicator();
            var keyPath = readDeduplicator.ExtractKeyPath(toolCall);
            if (keyPath != null)
            {
                var coverage = readDeduplicator.ExtractCoverage(toolCall);
                message.CoverageKey = keyPath.Replace('\\', '/') + "|" + coverage;
            }

            if (readDeduplicator.IsMutatingTool(toolCall.Name))
            {
                var mutationPath = readDeduplicator.ExtractMutationPath(toolCall);
                if (mutationPath != null)
                    message.MutationTargetPath = mutationPath;
            }
        }

        /// <summary>
        /// gap80_1: Applies the tool-result lifetime engine to the collected results for the
        /// next LLM iteration, converting Superseded messages to tombstoned and keeping Stale
        /// markers visible (per MessengerService serialize handling).
        /// </summary>
        private void ApplyToolResultLifetime(List<ChatMessage> toolResults)
        {
            if (toolResults == null || toolResults.Count == 0)
                return;

            _toolResultLifetimeService.ApplyLifetime(toolResults);
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
            try
            {
                _streamingCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                LoggerService.Current.WriteDebug("[ExecuteCancel] _streamingCts already disposed");
            }
            // gap78: Clear tool call aggregator buffer on user cancel
            _toolCallAggregator.Clear();
            IsStreaming = false;
        }

        /// <summary>
        /// Starts a new chat session (gap47).
        /// Calls CreateNewSessionAsync, then clears InputText and SelectedContext.
        /// </summary>
        private async Task ExecuteNewChatAsync()
        {
            try
            {
                await _sessionService.SaveCurrentSessionAsync();
                await _sessionService.CreateNewSessionAsync();
                Messages.Clear();
                InputText = string.Empty;
                SelectedContext.Clear();
                await RefreshSessionsAsync();
                LoggerService.Current.WriteDebug("[gap47] New chat session started");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap47] ExecuteNewChatAsync failed: {ex.Message}", ex);
                await _notificationService.ShowErrorAsync($"Failed to start new chat: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the list of available sessions from storage (gap76).
        /// Called after session operations to update HistoryView.
        /// </summary>
        private async Task RefreshSessionsAsync()
        {
            try
            {
                var sessions = new List<SessionMetadata>();
                await foreach (var sessionMeta in _sessionService.ListSessionsAsync(limit: 100))
                {
                    sessions.Add(sessionMeta);
                }

                var orderedSessions = sessions.OrderByDescending(s => s.UpdatedAt).ToList();

                // Resolve the current session id on a worker/async context before touching the UI collection.
                string? currentSessionId = null;
                try
                {
                    currentSessionId = await _sessionService.GetCurrentSessionIdAsync();
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError($"[gap76-refresh] Failed to read current session id: {ex.Message}", ex);
                }

                // gap76-fix: Mutate the ObservableCollection on the Dispatcher thread deterministically.
                // Simply awaiting SwitchToMainThreadAsync() and mutating afterward is NOT reliable here:
                // ListSessionsAsync()/GetCurrentSessionIdAsync() may break the WPF SynchronizationContext,
                // so the continuation after those awaits can resume on a thread-pool thread where there is
                // no DispatcherSynchronizationContext to route back to. Await a DispatcherOperation alone
                // resumes on the caller's captured context, not necessarily the dispatcher — leading to
                // "CollectionView does not support changes to its SourceCollection from a thread different
                // from the Dispatcher thread" when AvailableSessions is bound to a CollectionView.
                // Running the mutation inside Dispatcher.Invoke guarantees it executes on the UI thread.
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                    dispatcher.Invoke(() =>
#pragma warning restore VSTHRD001
                    {
                        AvailableSessions.Clear();
                        foreach (var session in orderedSessions)
                        {
                            AvailableSessions.Add(session);
                        }

                        if (currentSessionId != null)
                        {
                            CurrentSession = orderedSessions.FirstOrDefault(s => s.Id == currentSessionId);
                        }
                    });
                }
                else
                {
                    // No dispatcher (unit test contexts) or already on the dispatcher thread: update directly.
                    AvailableSessions.Clear();
                    foreach (var session in orderedSessions)
                    {
                        AvailableSessions.Add(session);
                    }

                    if (currentSessionId != null)
                    {
                        CurrentSession = orderedSessions.FirstOrDefault(s => s.Id == currentSessionId);
                    }
                }

                LoggerService.Current.WriteDebug($"[gap76-refresh] Loaded {sessions.Count} sessions");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap76-refresh] Failed to refresh sessions: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a session by ID and switches to it (gap76).
        /// Also updates DisplayMessages with the loaded session's messages.
        /// </summary>
        public async Task LoadSessionAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    LoggerService.Current.WriteError("[gap76-load] Session ID cannot be null or empty");
                    return;
                }

                await _sessionService.LoadSessionAsync(sessionId);
                await _sessionService.SetCurrentSessionIdAsync(sessionId);

                // Refresh the message display
                var currentSession = _sessionService.GetCurrentSession();
                Messages.Clear();
                foreach (var msg in currentSession.Messages)
                {
                    Messages.Add(msg);
                }

                InputText = string.Empty;
                SelectedContext.Clear();

                await RefreshSessionsAsync();
                LoggerService.Current.WriteDebug($"[gap76-load] Loaded session {sessionId}");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap76-load] Failed to load session: {ex.Message}", ex);
                await _notificationService.ShowErrorAsync($"Failed to load session: {ex.Message}");
            }
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
            RaisePropertyChanged(nameof(IsPausedDisplayIcon));

            // gap31_2: Cancel active stream if pause activated and streaming is in progress
            if (IsPaused && IsStreaming && _streamingCts != null && !_streamingCts.Token.IsCancellationRequested)
            {
                LoggerService.Current.WriteDebug("[gap31_2-pause] Cancelling active stream due to pause signal");
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

                    await _instructionExecutorService.SetPauseCheckpointAsync(checkpoint);
                    LoggerService.Current.WriteDebug(
                        $"[gap31_3-checkpoint] Captured pause checkpoint: {checkpoint.ChunkCount} chunks, {streamedText.Length} chars");
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError($"[gap31_3-checkpoint] Error capturing checkpoint: {ex.Message}", ex);
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
            LoggerService.Current.WriteDebug($"[delete-cmd] ExecuteDeleteMessage called with ID: {messageId}");

            if (string.IsNullOrWhiteSpace(messageId))
            {
                LoggerService.Current.WriteDebug($"[delete-cmd] messageId is null/empty, aborting");
                return;
            }

            // Find and remove message from collection
            var messageToDelete = Messages.FirstOrDefault(m => m.Id == messageId);
            if (messageToDelete == null)
            {
                LoggerService.Current.WriteDebug($"[delete-cmd] Message with ID {messageId} not found in collection. Available: {string.Join(",", Messages.Select(m => m.Id))}");
                return;
            }

            LoggerService.Current.WriteDebug($"[delete-cmd] Found message, removing from collection. Current count: {Messages.Count}");
            Messages.Remove(messageToDelete);
            LoggerService.Current.WriteDebug($"[delete-cmd] Message removed. New count: {Messages.Count}");

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
                LoggerService.Current.WriteDebug($"[delete-service] Calling DeleteMessageAsync for ID: {messageId}");
                await _sessionService.DeleteMessageAsync(messageId);
                LoggerService.Current.WriteDebug($"[delete-service] Successfully deleted message ID: {messageId}");
            }
            catch (Exception ex)
            {
                // If service deletion fails, add message back and notify user
                LoggerService.Current.WriteError($"[delete-service] Delete failed, restoring message: {ex.Message}", ex);
                Messages.Add(messageToRestore);
                await _notificationService.ShowNotificationAsync("Delete Failed",
                    $"Could not delete message: {ex.Message}", NotificationType.Error);
                LoggerService.Current.WriteError($"[delete-error] Service deletion failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Executes the soft-delete (prune) command (gap81).
        /// Marks the message IsDeleted = true in place (retains the element in the collection
        /// so the user can undelete it) and persists asynchronously. Never removes the element.
        /// </summary>
        private void ExecuteSoftDeleteMessage(string messageId)
        {
            LoggerService.Current.WriteDebug($"[gap81-softdelete-cmd] ExecuteSoftDeleteMessage called with ID: {messageId}");

            if (string.IsNullOrWhiteSpace(messageId))
            {
                LoggerService.Current.WriteDebug("[gap81-softdelete-cmd] messageId is null/empty, aborting");
                return;
            }

            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
            {
                LoggerService.Current.WriteDebug($"[gap81-softdelete-cmd] Message with ID {messageId} not found in collection.");
                return;
            }

            message.IsDeleted = true;
            LoggerService.Current.WriteDebug($"[gap81-softdelete-cmd] Message {messageId} marked soft-deleted.");

            // Persist asynchronously (fire-and-forget with error handling)
            _ = ExecuteSoftDeleteMessageAsync(messageId, message);
        }

        /// <summary>
        /// Asynchronously persists message soft-deletion to the service (gap81).
        /// If persistence fails, rolls back the in-memory tombstone to false and notifies the user.
        /// </summary>
        private async Task ExecuteSoftDeleteMessageAsync(string messageId, ChatMessage messageToRollback)
        {
            try
            {
                LoggerService.Current.WriteDebug($"[gap81-softdelete-service] Calling SoftDeleteMessageAsync for ID: {messageId}");
                await _sessionService.SoftDeleteMessageAsync(messageId);
                LoggerService.Current.WriteDebug($"[gap81-softdelete-service] Successfully soft-deleted message ID: {messageId}");
            }
            catch (Exception ex)
            {
                // Tool-result cards in the Agent loop are display-only (gap73): they are never
                // added to the session store, so persistence correctly reports "not found". This
                // is NOT a failure - the in-memory tombstone already applied is the only durable
                // state these cards have. Keep the tombstone and skip the rollback/popup so the
                // delete toggle works on tool cards. All other roles still roll back + notify.
                if (messageToRollback?.Role == ChatMessageRole.Tool)
                {
                    LoggerService.Current.WriteDebug(
                        $"[gap81-softdelete-service] Tool card {messageId} is display-only (not in session store); keeping in-memory tombstone.");
                    return;
                }

                // If persistence fails, roll back the tombstone and notify
                messageToRollback?.IsDeleted = false;
                LoggerService.Current.WriteError($"[gap81-softdelete-error] Soft-delete failed, rolling back: {ex.Message}", ex);
                await _notificationService.ShowNotificationAsync("Prune Failed",
                    $"Could not prune message: {ex.Message}", NotificationType.Error);
            }
        }

        /// <summary>
        /// Executes the undelete command (gap81).
        /// Clears the IsDeleted tombstone in place (restores visibility / LLM payload inclusion)
        /// and persists asynchronously. Visibility only; performs no file-state/restore operation.
        /// </summary>
        private void ExecuteUndeleteMessage(string messageId)
        {
            LoggerService.Current.WriteDebug($"[gap81-undelete-cmd] ExecuteUndeleteMessage called with ID: {messageId}");

            if (string.IsNullOrWhiteSpace(messageId))
            {
                LoggerService.Current.WriteDebug("[gap81-undelete-cmd] messageId is null/empty, aborting");
                return;
            }

            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
            {
                LoggerService.Current.WriteDebug($"[gap81-undelete-cmd] Message with ID {messageId} not found in collection.");
                return;
            }

            message.IsDeleted = false;
            LoggerService.Current.WriteDebug($"[gap81-undelete-cmd] Message {messageId} cleared soft-deleted tombstone.");

            // Persist asynchronously (fire-and-forget with error handling)
            _ = ExecuteUndeleteMessageAsync(messageId, message);
        }

        /// <summary>
        /// Executes the minimize/maximize toggle command (gap85).
        /// Flips the message's IsMinimized flag (UI visibility only; NOT persisted, NOT a tombstone).
        /// When minimized the bubble collapses and the toggle shows the maximize "▢/⤢" symbol;
        /// when expanded it shows the minimize "_" symbol. Applies to all message roles alike.
        /// </summary>
        private void ExecuteToggleMinimizeMessage(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                LoggerService.Current.WriteDebug("[gap85-minimize-cmd] messageId is null/empty, aborting");
                return;
            }

            var message = Messages.FirstOrDefault(m => m.Id == messageId);
            if (message == null)
            {
                LoggerService.Current.WriteDebug($"[gap85-minimize-cmd] Message with ID {messageId} not found in collection.");
                return;
            }

            message.IsMinimized = !message.IsMinimized;
            LoggerService.Current.WriteDebug($"[gap85-minimize-cmd] Message {messageId} minimized={message.IsMinimized}.");
        }

        /// <summary>
        /// Asynchronously persists message undelete to the service (gap81).
        /// If persistence fails, rolls back the in-memory tombstone to true and notifies the user.
        /// </summary>
        private async Task ExecuteUndeleteMessageAsync(string messageId, ChatMessage messageToRollback)
        {
            try
            {
                LoggerService.Current.WriteDebug($"[gap81-undelete-service] Calling UndeleteMessageAsync for ID: {messageId}");
                await _sessionService.UndeleteMessageAsync(messageId);
                LoggerService.Current.WriteDebug($"[gap81-undelete-service] Successfully undeleted message ID: {messageId}");
            }
            catch (Exception ex)
            {
                // Tool-result cards in the Agent loop are display-only (gap73): they are never
                // added to the session store, so persistence correctly reports "not found". This
                // is NOT a failure - the in-memory cleared tombstone already applied is the only
                // durable state these cards have. Keep the cleared tombstone and skip the
                // rollback/popup so the undelete toggle works on tool cards. All other roles
                // still roll back + notify.
                if (messageToRollback?.Role == ChatMessageRole.Tool)
                {
                    LoggerService.Current.WriteDebug(
                        $"[gap81-undelete-service] Tool card {messageId} is display-only (not in session store); keeping in-memory tombstone.");
                    return;
                }

                // If persistence fails, roll back the tombstone and notify
                messageToRollback?.IsDeleted = true;
                LoggerService.Current.WriteError($"[gap81-undelete-error] Undelete failed, rolling back: {ex.Message}", ex);
                await _notificationService.ShowNotificationAsync("Undelete Failed",
                    $"Could not undelete message: {ex.Message}", NotificationType.Error);
            }
        }
        /// <summary>
        /// Executes copy command for code blocks (gap49).
        /// Copies the code block content to clipboard.
        /// </summary>
        private void ExecuteCopyCodeBlock(string? codeContent)
        {
            if (string.IsNullOrWhiteSpace(codeContent))
            {
                LoggerService.Current.WriteDebug("[gap49-copy] Code content is empty");
                return;
            }

            try
            {
                System.Windows.Forms.Clipboard.SetText(codeContent);
                LoggerService.Current.WriteDebug("[gap49-copy] Code copied to clipboard");
                _ = _notificationService.ShowNotificationAsync("Copied", "Code block copied to clipboard", NotificationType.Success);
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap49-copy-error] Failed to copy: {ex.Message}", ex);
                _ = _notificationService.ShowNotificationAsync("Copy Failed", $"Could not copy code: {ex.Message}", NotificationType.Error);
            }
        }

        /// <summary>
        /// Determines if Apply button should be enabled (gap49).
        /// Returns true only if CurrentResponseHasFilePath is true.
        /// </summary>
        private bool CanApplyCodeBlock()
        {
            return CurrentResponseHasFilePath;
        }

        /// <summary>
        /// Executes apply command for code blocks (gap49).
        /// Applies the code block to the detected file path.
        /// Disabled if no file path is detected in the response.
        /// </summary>
        private void ExecuteApplyCodeBlock(string? codeContent)
        {
            if (string.IsNullOrWhiteSpace(codeContent))
            {
                LoggerService.Current.WriteDebug("[gap49-apply] Code content is empty");
                return;
            }

            if (!CurrentResponseHasFilePath)
            {
                _ = _notificationService.ShowNotificationAsync("Warning",
                    "No file path detected in response. Unable to apply changes.", NotificationType.Warning);
                LoggerService.Current.WriteDebug("[gap49-apply] No file path detected");
                return;
            }

            // Fire-and-forget the async apply operation with proper error handling.
            // At this point codeContent is guaranteed non-null by the check above.
            _ = ExecuteApplyCodeBlockAsync(codeContent!);
        }

        /// <summary>
        /// Async implementation for applying code block to file (gap49_advanced).
        /// Extracts file path and code content, then writes to disk using ChangeStackService.
        /// </summary>
        private async Task ExecuteApplyCodeBlockAsync(string codeContent)
        {
            try
            {
                // Get the latest message to extract file path from
                if (Messages.Count == 0)
                {
                    LoggerService.Current.WriteDebug("[gap49-apply] No messages available to extract file path from");
                    await _notificationService.ShowErrorAsync("No context available for file path extraction.");
                    return;
                }

                var latestMessage = Messages[Messages.Count - 1];

                // Extract file path from the latest message content
                string? extractedPath = ExtractFilePathFromResponse(latestMessage.Content);
                if (string.IsNullOrWhiteSpace(extractedPath))
                {
                    LoggerService.Current.WriteDebug("[gap49-apply] Could not extract file path from message");
                    await _notificationService.ShowErrorAsync("Could not extract file path from response.");
                    return;
                }

                // Extract code content from markdown if needed
                var actualCode = ExtractCodeContentFromMarkdown(codeContent) ?? codeContent;

                LoggerService.Current.WriteDebug($"[gap49-apply] Applying code to file: {extractedPath}");

                // Apply the code change (extractedPath is now guaranteed non-null by the check above)
                await ApplyCodeChangeAsync(extractedPath!, actualCode);
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteDebug($"[gap49-apply] Unhandled exception: {ex.Message}");
                await _notificationService.ShowErrorAsync(
                    $"Apply operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// gap55_4: Executes Ollama tool calls via ToolService with timeout and error handling.
        /// Routes ToolCallSchema objects from CompletionChunk.ToolCalls through ToolService.InvokeAsync().
        /// Handles argument deserialization, timeout protection, and collects results.
        /// </summary>
        public async Task<List<ToolResult>> ExecuteToolCallsFromOllamaAsync(
            List<ToolCallSchema> toolCalls,
            CancellationToken ct)
        {
            var toolResults = new List<ToolResult>();
            var maxIterations = 5;
            var iteration = 0;

            foreach (var toolCall in toolCalls)
            {
                if (++iteration > maxIterations)
                {
                    LoggerService.Current.WriteWarning(
                        $"[gap55_4-limit] Max tool calls ({maxIterations}) reached in single invocation");
                    break;
                }

                try
                {
                    LoggerService.Current.WriteDebug(
                        $"[gap55_4-tool-execution] Executing tool={toolCall.Function?.Name ?? "unknown"}, id={toolCall.Id}");

                    // Parse tool arguments (JSON string to dict)
                    var args = new Dictionary<string, object>();
                    if (!string.IsNullOrEmpty(toolCall.Function?.Arguments))
                    {
                        try
                        {
                            args = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                                toolCall.Function?.Arguments ?? "") ?? new Dictionary<string, object>();
                        }
                        catch (JsonException ex)
                        {
                            LoggerService.Current.WriteError(
                                $"[gap55_4-tool-error] Failed to deserialize arguments for tool {toolCall.Function?.Name ?? "unknown"}: {ex.Message}");
                            toolResults.Add(new ToolResult
                            {
                                ToolName = toolCall.Function?.Name ?? "unknown",
                                ToolCallId = toolCall.Id,
                                Output = $"Error: Failed to deserialize tool arguments: {ex.Message}"
                            });
                            continue;
                        }
                    }

                    // Invoke tool with timeout
                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
                    {
                        var result = await _toolService.InvokeAsync(
                            toolCall.Function?.Name ?? "unknown",
                            args,
                            linkedCts.Token);

                        result.ToolCallId = toolCall.Id;
                        toolResults.Add(result);

                        LoggerService.Current.WriteDebug(
                            $"[gap55_4-tool-success] Tool {toolCall.Function?.Name ?? "unknown"} completed: " +
                            $"{(result.Output?.Substring(0, Math.Min(100, result.Output?.Length ?? 0)) ?? "(empty)")}...");
                    }
                }
                catch (OperationCanceledException)
                {
                    LoggerService.Current.WriteWarning(
                        $"[gap55_4-tool-timeout] Tool {toolCall.Function?.Name ?? "unknown"} timed out (30s)");
                    toolResults.Add(new ToolResult
                    {
                        ToolName = toolCall.Function?.Name ?? "unknown",
                        ToolCallId = toolCall.Id,
                        Output = "Error: Tool execution timed out (30 seconds)"
                    });
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError(
                        $"[gap55_4-tool-error] Tool {toolCall.Function?.Name ?? "unknown"} failed: {ex.Message}");
                    toolResults.Add(new ToolResult
                    {
                        ToolName = toolCall.Function?.Name ?? "unknown",
                        ToolCallId = toolCall.Id,
                        Output = $"Error: {ex.Message}"
                    });
                }
            }

            return toolResults;
        }

        /// <summary>
        /// gap55_4: Re-invokes Ollama with tool results added to message context for multi-turn loops.
        /// Reconstructs full message history including tool results from session, then streams continuation.
        /// If continuation yields more tool calls, recursively executes them.
        /// </summary>
        public async Task ContinueConversationWithOllamaAsync(CancellationToken ct)
        {
            try
            {
                LoggerService.Current.WriteDebug(
                    "[gap55_4-continuation] Re-invoking Ollama with tool results in context");

                // Get updated session with all messages including tool results
                var currentSession = _sessionService.GetCurrentSession();
                if (currentSession?.Messages.Count == 0)
                    return;

                // Reconstruct full message list with all context
                var allMessages = new List<ChatMessage>(currentSession!.Messages);

                // gap80_1: Apply the lifetime engine to this turn's accumulated tool results
                // before streaming, so coverage supersession / mutation invalidation / directory
                // staleness are reflected in the continuation context (tool results are in-flight,
                // not persisted history — the engine mutates them here, not the session store).
                ApplyToolResultLifetime(_continuationToolResults);

                // Get mode config for this continuation
                var modeConfig = _modeConfigRegistry.GetConfig(CurrentMode);

                // Filter tools based on mode
                var availableTools = GetAvailableToolsForCurrentMode();

                // Reconstruct stream options
                var continuationOptions = new StreamOptions
                {
                    Messages = allMessages,
                    AllowWriteTools = modeConfig.AllowWriteTools,
                    Mode = CurrentMode
                };

                // Re-invoke Ollama with full context and tool results already present
                var continuation = new StringBuilder();
                ChatMessage? thinkingMessage = null;

                await foreach (var chunk in _llmService.StreamAsync(allMessages, continuationOptions, ct))
                {
                    if (chunk.Type == ChunkType.Text)
                    {
                        // Handle reasoning content by accumulating in a thinking message
                        if (!string.IsNullOrEmpty(chunk.Reasoning))
                        {
                            // Create thinking message on first reasoning chunk
                            if (thinkingMessage == null)
                            {
                                thinkingMessage = new ChatMessage
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Role = ChatMessageRole.Thinking,
                                    Content = string.Empty,
                                    IsThinking = true,
                                    IsExpanded = false
                                };

                                await _sessionService.AddMessageAsync(thinkingMessage);
                                LoggerService.Current.WriteDebug($"[continuation] Thinking message created for reasoning accumulation");
                            }

                            // Append reasoning to thinking message
                            thinkingMessage.Content += chunk.Reasoning;
                            var reasoningPreview = chunk.Reasoning?.Substring(0, Math.Min(50, chunk.Reasoning?.Length ?? 0)) ?? string.Empty;
                            LoggerService.Current.WriteDebug($"[continuation] Reasoning accumulated: {reasoningPreview}...");
                        }

                        if (!string.IsNullOrEmpty(chunk.Content))
                        {
                            continuation.Append(chunk.Content);
                        }
                    }
                    else if (chunk.Type == ChunkType.ToolCall && !string.IsNullOrEmpty(chunk.ToolCallsText))
                    {
                        // gap78: Accumulate tool call text fragments in Ollama continuation path
                        _toolCallAggregator.AccumulateToolCallsText(chunk.ToolCallsText);

                        // gap78: Check if completion signal received
                        if (_toolCallAggregator.CheckCompletion(chunk.DoneReason))
                        {
                            // gap78: Attempt to parse and validate accumulated tool calls
                            if (_toolCallAggregator.TryGetCompleteToolCalls(out var validToolCalls) && validToolCalls != null)
                            {
                                LoggerService.Current.WriteDebug(
                                    $"[gap78-parsed-continuation] Successfully parsed {validToolCalls.Count} tool calls from accumulated stream");

                                // gap72: Handle batch tool calls in continuation path
                                foreach (var toolCallSchema in validToolCalls)
                                {
                                    ToolCall toolCall;
                                    if (_messengerService != null)
                                    {
                                        toolCall = _messengerService.ConvertToolCallSchemaToToolCall(toolCallSchema);
                                    }
                                    else
                                    {
                                        toolCall = ConvertToolCallSchemaManually(toolCallSchema);
                                    }
                                    LoggerService.Current.WriteDebug(
                                        $"[gap78-continuation-toolcall] Queued tool: {toolCall.Name} (id={toolCall.Id})");
                                }

                                // gap78: Reset aggregator for next batch
                                _toolCallAggregator.Clear();
                            }
                            else
                            {
                                LoggerService.Current.WriteWarning(
                                    "[gap78-parse-continuation] Failed to parse accumulated tool calls or validation failed");
                                _toolCallAggregator.Clear();
                            }
                        }
                    }

                    if (chunk.IsDone)
                    {
                        // Check if completion chunk has tool calls (from ToolCallsText aggregation)
                        // Note: in Ollama path, tool calls are accumulated via aggregator and stored in _pendingToolCalls
                        if (_pendingToolCalls.Count > 0)
                        {
                            // Add the continuation text (if any) as assistant message
                            if (!string.IsNullOrEmpty(continuation.ToString()))
                            {
                                var continuationMsg = new ChatMessage
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Role = ChatMessageRole.Assistant,
                                    Content = continuation.ToString()
                                };
                                await _sessionService.AddMessageAsync(continuationMsg);
                            }

                            // Another round of tool calls - execute them from _pendingToolCalls
                            var moreResults = new List<ToolResult>();

                            // gap87: Capture arguments per tool-call id BEFORE clearing so we can
                            // fabricate display-only descriptions for the result messages below.
                            var resultDescById = new Dictionary<string, IDictionary<string, object>?>(StringComparer.OrdinalIgnoreCase);
                            foreach (var toolCall in _pendingToolCalls)
                            {
                                resultDescById[toolCall.Id ?? string.Empty] = toolCall.Arguments;
                            }

                            foreach (var toolCall in _pendingToolCalls)
                            {
                                try
                                {
                                    LoggerService.Current.WriteDebug(
                                        $"[gap78-continuation-execute] Executing tool={toolCall.Name}, id={toolCall.Id}");
                                    var result = await _toolService.InvokeAsync(toolCall.Name, toolCall.Arguments ?? new Dictionary<string, object>(), ct);
                                    moreResults.Add(result);
                                }
                                catch (Exception toolEx)
                                {
                                    LoggerService.Current.WriteWarning(
                                        $"[gap78-continuation-error] Failed to execute tool {toolCall.Name}: {toolEx.Message}");
                                }
                            }
                            _pendingToolCalls.Clear();

                            // Add results to session
                            foreach (var result in moreResults)
                            {
                                var resultMsg = new ChatMessage
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Role = ChatMessageRole.Tool,
                                    Content = result.Output,
                                    ToolCallId = result.ToolCallId,
                                    ToolName = result.ToolName,
                                    // gap87: Fabricate a display-only description from the tool-call arguments.
                                    ToolCallDescription = ToolCallDescriptionBuilder.Build(
                                        result.ToolName ?? "tool",
                                        resultDescById.TryGetValue(result.ToolCallId ?? string.Empty, out var captured) ? captured : null)
                                };
                                // gap80_1: Tag lifetime metadata (coverage key / mutation path).
                                resultDescById.TryGetValue(result.ToolCallId ?? string.Empty, out var lifetimeArgs);
                                ApplyLifetimeMetadataForTool(resultMsg, new ToolCall
                                {
                                    Name = result.ToolName ?? resultMsg.ToolCallId ?? string.Empty,
                                    Arguments = lifetimeArgs ?? new Dictionary<string, object>()
                                });
                                await _sessionService.AddMessageAsync(resultMsg);
                                _continuationToolResults.Add(resultMsg);
                            }

                            // gap69: Create and add execution impact message
                            var executionImpact = new ExecutionImpactMessage();
                            // Create phase results from tool execution
                            var phases = new List<PhaseExecutionResult>();
                            for (int i = 0; i < moreResults.Count; i++)
                            {
                                var phase = new PhaseExecutionResult
                                {
                                    PhaseId = $"tool_{i}",
                                    Status = ExecutionStatus.Succeeded,
                                    Evidence = moreResults[i].ToolName ?? "tool"
                                };
                                phase.EndTime = DateTime.UtcNow;
                                phases.Add(phase);
                            }
                            executionImpact.Initialize(phases);
                            await _sessionService.AddMessageAsync(executionImpact);
                            LoggerService.Current.WriteDebug(
                                $"[gap69-dispatch] Created execution impact message with {phases.Count} phases");

                            // Continue again recursively
                            await ContinueConversationWithOllamaAsync(ct);
                        }
                        else
                        {
                            // Final response with no tool calls
                            continuation.Append(chunk.Content ?? string.Empty);
                            var finalMsg = new ChatMessage
                            {
                                Id = Guid.NewGuid().ToString(),
                                Role = ChatMessageRole.Assistant,
                                Content = continuation.ToString()
                            };
                            await _sessionService.AddMessageAsync(finalMsg);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError(
                    $"[gap55_4-continuation-error] Failed to continue conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// gap55_4: Filters available tools based on current ChatMode policy.
        /// Ask mode: read-only tools only (read_file, list_files, search_code).
        /// Agent mode: all tools allowed.
        /// Other modes have their own policies.
        /// </summary>
        public List<ToolDefinition> GetAvailableToolsForCurrentMode()
        {
            var allTools = _toolService.GetAvailableTools().ToList();

            return CurrentMode switch
            {
                ChatMode.Ask =>
                    allTools.Where(t => !IsWriteTool(t.Name) && IsReadTool(t.Name)).ToList(),
                ChatMode.Agent =>
                    allTools,  // All tools allowed
                _ => new List<ToolDefinition>()
            };
        }

        /// <summary>
        /// Helper: Checks if a tool name represents a write operation.
        /// </summary>
        private bool IsWriteTool(string name) =>
            name is "write_files" or "delete_file" or "run_command";

        /// <summary>
        /// Helper: Checks if a tool name represents a read operation.
        /// </summary>
        private bool IsReadTool(string name) =>
            name is "read_file" or "list_files" or "search_code";

        /// <summary>
        /// gap68: Parses thinking/reasoning content from LLM response and creates separate ChatMessage objects.
        /// Handles models that return reasoning tags (e.g., &lt;thinking&gt;...&lt;/thinking&gt;).
        /// Returns tuple of (thinkingMessage, responseContent) where thinkingMessage is null if no thinking found.
        /// Respects user setting Chat_ShowThinkingAfterStreaming to control final visibility.
        /// </summary>
        private async Task<(ChatMessage? ThinkingMessage, string ResponseContent)> ParseThinkingFromResponseAsync(
            string responseContent,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(responseContent))
                return (null, responseContent);

            // Pattern 1: Extract <thinking>...</thinking> blocks
            var thinkingPattern = @"<thinking>(.*?)</thinking>";
            var thinkingMatch = System.Text.RegularExpressions.Regex.Match(
                responseContent,
                thinkingPattern,
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (thinkingMatch.Success && thinkingMatch.Groups.Count > 1)
            {
                var thinkingContent = thinkingMatch.Groups[1].Value.Trim();
                var cleanedResponse = System.Text.RegularExpressions.Regex.Replace(
                    responseContent,
                    thinkingPattern,
                    "",
                    System.Text.RegularExpressions.RegexOptions.Singleline).Trim();

                if (!string.IsNullOrWhiteSpace(thinkingContent))
                {
                    // Read user setting for showing thinking after streaming
                    var showThinkingAfterStreaming = true;
                    if (_configService != null)
                    {
                        var config = _configService.GetCurrentConfig();
                        if (config?.CustomSettings?.TryGetValue(UserSettings.Chat_ShowThinkingAfterStreaming, out var val) == true)
                        {
                            showThinkingAfterStreaming = val switch
                            {
                                true => true,
                                "true" => true,
                                1 or 1L => true,
                                _ => false
                            };
                        }
                    }

                    LoggerService.Current.WriteDebug(
                        $"[gap68-parse-thinking] Thinking block detected. Length: {thinkingContent.Length}, ShowAfterStreaming: {showThinkingAfterStreaming}");

                    var thinkingMessage = new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = ChatMessageRole.Thinking,
                        Content = thinkingContent,
                        IsThinking = true,  // Mark for context exclusion
                        IsExpanded = false,  // Collapsed by default at rest
                        Timestamp = DateTime.Now
                    };

                    // If setting is false, create message but mark it invisible to UI
                    // Message still persists in session for history but won't display in transcript
                    if (!showThinkingAfterStreaming)
                    {
                        LoggerService.Current.WriteDebug(
                            "[gap68-setting-applied] Thinking message created but hidden per user setting");
                    }

                    return (thinkingMessage, cleanedResponse);
                }
            }

            return (null, responseContent);
        }

        /// <summary>
        /// gap68: Adds thinking message to session and UI if visible per user settings.
        /// </summary>
        private async Task AddThinkingMessageIfVisibleAsync(ChatMessage thinkingMessage)
        {
            if (thinkingMessage == null)
                throw new ArgumentNullException(nameof(thinkingMessage));

            var showThinkingAfterStreaming = true;
            if (_configService != null)
            {
                var config = _configService.GetCurrentConfig();
                if (config?.CustomSettings?.TryGetValue(UserSettings.Chat_ShowThinkingAfterStreaming, out var val) == true)
                {
                    showThinkingAfterStreaming = val switch
                    {
                        true => true,
                        "true" => true,
                        1 or 1L => true,
                        _ => false
                    };
                }
            }

            if (showThinkingAfterStreaming)
            {
                // Add to session for persistence
                await _sessionService.AddMessageAsync(thinkingMessage);

                // Add to UI on main thread
                await SwitchToMainThreadAsync();
                Messages.Add(thinkingMessage);

                LoggerService.Current.WriteDebug(
                    "[gap68-ui-add] Thinking message added to UI transcript");
            }
            else
            {
                // Still add to session for history, but don't show in UI
                await _sessionService.AddMessageAsync(thinkingMessage);
                LoggerService.Current.WriteDebug(
                    "[gap68-session-only] Thinking message added to session only (not visible in UI)");
            }
        }

        /// <summary>
        /// gap60: Public entry point for external callers (GUI bridge, tests, CI/CD) to trigger agent commands.
        /// Validates mode, dispatch via IAgentCommandDispatcher, adds results to session history.
        /// Logs with [gap60-*] tags for audit trail.
        /// </summary>
        public async Task ExecuteAgentCommandAsync(string commandName, IDictionary<string, object> commandArguments, CancellationToken ct = default)
        {
            // 1. Validate mode
            if (CurrentMode != ChatMode.Agent)
            {
                throw new InvalidOperationException(
                    $"Agent commands only valid in Agent mode. Current mode: {CurrentMode}");
            }

            // 2. Validate command format
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("Command name cannot be null or empty.", nameof(commandName));

            try
            {
                // 3. Get mode config
                var modeConfig = _modeConfigRegistry.GetConfig(CurrentMode);

                // 4. Dispatch via IAgentCommandDispatcher
                LoggerService.Current.WriteDebug(
                    $"[gap60-agent-cmd] Executing agent command: {commandName}");

                var result = await _agentCommandDispatcher.DispatchAgentCommandAsync(
                    commandName,
                    commandArguments ?? new Dictionary<string, object>(),
                    CurrentMode,
                    ct);

                // 5. Add to chat history for display only (not persisted to session)
                var toolMsg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = ChatMessageRole.Tool,
                    Content = result.Output,
                    ToolName = commandName,
                    // gap87: Fabricate a display-only description from the tool-call arguments.
                    ToolCallDescription = ToolCallDescriptionBuilder.Build(commandName, commandArguments ?? new Dictionary<string, object>()),
                    InvocationStatus = result.Output?.Contains("Error") ?? false ? ToolInvocationStatus.Failed : ToolInvocationStatus.Complete
                };
                // gap73: Tool results are displayed in UI but NOT persisted to session file
                // They are only used for LLM context in the current loop via ConvertToolCallToSchema
                Messages.Add(toolMsg);

                LoggerService.Current.WriteDebug(
                    $"[gap60-agent-cmd-complete] Command {commandName} finished");
            }
            catch (InvalidOperationException ex)
            {
                LoggerService.Current.WriteError(
                    $"[gap60-agent-error] Agent command validation failed: {ex.Message}");
                throw;
            }
            catch (OperationCanceledException ex)
            {
                LoggerService.Current.WriteWarning(
                    $"[gap60-agent-error] Agent command cancelled: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError(
                    $"[gap60-agent-error] Unexpected error during agent command execution: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// gap74: Executes the Optimize and Continue action.
        /// Backtracks conversation history to fit within token budget, then resets UI state.
        /// </summary>
        private async Task ExecuteOptimizeAndContinueAsync()
        {
            try
            {
                var session = _sessionService.GetCurrentSession();
                if (session?.Messages == null || session.Messages.Count == 0)
                {
                    await _notificationService.ShowNotificationAsync("No Messages", "No messages to optimize.", NotificationType.Warning);
                    return;
                }

                // Conservative defaults
                int maxTokens = 4096;
                int reserve = 512;

                var (trimmedHistory, summary) = await _sessionService.BacktrackAndOptimizeAsync(
                    session.Messages, maxTokens, reserve);

                // Update session with trimmed history
                session.Messages.Clear();
                foreach (var msg in trimmedHistory)
                {
                    session.Messages.Add(msg);
                }

                // Update UI and persist
                await _sessionService.SaveCurrentSessionAsync();

                // Reset budget state
                CurrentBudgetState = ContextBudgetState.Safe;
                OptimizeButtonText = "Optimize & Continue";
                IsInputEnabled = true;

                // Show optimization summary
                await _notificationService.ShowNotificationAsync("Optimized", summary, NotificationType.Information);

                LoggerService.Current.WriteDebug($"[gap74-optimize] {summary}");
            }
            catch (InvalidOperationException ex)
            {
                LoggerService.Current.WriteError($"[gap74-error] Optimization failed: {ex.Message}");
                await _notificationService.ShowErrorAsync($"Cannot optimize: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[gap74-error] Unexpected error: {ex.Message}");
                await _notificationService.ShowErrorAsync("Optimization failed: " + ex.Message);
            }
        }
    }
}

