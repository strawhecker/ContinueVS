# ContinueVS Design Document

**Purpose:** Map architecture subsystems to .NET design patterns (services, ViewModels, Views).  
**Scope:** Service contracts, MVVM structure, state management, DI patterns.  
**Output:** Blueprint for WPF + C# backend implementation without implementation details.

---

## 1. Design Philosophy

**Principle: Subsystem → Service → ViewModel → View**

Each architecture subsystem maps to a service interface with async operations. Services are injected into ViewModels, which manage state and commands for Views. Redux state becomes WPF ObservableCollections and dependency properties.

```
Architecture Subsystems (9)
         ↓
Service Layer (Interfaces + Implementations)
         ↓
ViewModel Layer (MVVM Light with RelayCommand)
         ↓
View Layer (WPF UserControls + MainWindow)
```

---

## 2. Service Layer Design

### 2.1 Service Interfaces (Core Contracts)

Each service encapsulates one or more subsystems. All methods are async (Task-based) to support non-blocking operations.

#### **IConfigService** (Configuration Subsystem)

```csharp
public interface IConfigService
{
    // Initialization
    Task InitializeAsync();

    // Config access
    ContinueConfig GetCurrentConfig();
    IObservable<ContinueConfig> ConfigChanged { get; }

    // Model management
    Task AddModelAsync(ModelInfo model);
    Task RemoveModelAsync(string modelId);
    Task SelectModelAsync(string modelId);
    ModelInfo GetSelectedModel();

    // Tool management
    IEnumerable<ToolDefinition> GetEnabledTools();
    Task SetToolEnabledAsync(string toolName, bool enabled);

    // Profile management
    IEnumerable<ProfileInfo> GetProfiles();
    Task SelectProfileAsync(string profileId);

    // Persistence
    Task SaveConfigAsync();
    Task ReloadConfigAsync();

    // Events
    event EventHandler<ConfigChangedEventArgs> ConfigChanged;
}
```

**Responsibilities:**
- Load/parse `~/.continue/config.json`
- Validate model/tool configuration
- Notify on changes (via event or IObservable)
- Persist mutations back to disk

**Dependencies:**
- File system (IFileService)
- JSON serialization (Newtonsoft.Json or System.Text.Json)

---

#### **ILlmService** (LLM Orchestration Subsystem)

```csharp
public interface ILlmService
{
    // Stream completion
    IAsyncEnumerable<CompletionChunk> StreamAsync(
        IEnumerable<ChatMessage> messages,
        StreamOptions? options = null,
        CancellationToken ct = default);

    // Model capabilities
    bool SupportsStreaming(string modelId);
    bool SupportsFunctionCalling(string modelId);
    int GetContextWindowSize(string modelId);

    // Token counting
    Task<int> CountTokensAsync(string text, string modelId);
    Task<int> CountMessagesTokensAsync(IEnumerable<ChatMessage> messages, string modelId);

    // Logging
    Task LogInteractionAsync(LlmInteractionLog log);

    // Events
    event EventHandler<LlmErrorEventArgs> Error;
}
```

**Responsibilities:**
- Route requests to correct LLM provider (OpenAI, Anthropic, Ollama, etc.)
- Handle streaming response chunks
- Count tokens (use js-tiktoken wrapper or equivalent)
- Log interactions for analytics

**Dependencies:**
- IConfigService (model selection)
- IToolService (for function calling)
- HTTP client factory (LLM API access)
- Token counter library

---

#### **IToolService** (Tool Execution Subsystem)

```csharp
public interface IToolService
{
    // Tool registry
    IEnumerable<ToolDefinition> GetAvailableTools();
    ToolDefinition GetTool(string toolName);

    // Tool invocation
    Task<ToolResult> InvokeAsync(
        string toolName,
        IDictionary<string, object> args,
        CancellationToken ct = default);

    // Built-in tools
    Task<string> ReadFileAsync(string filepath);
    Task WriteFileAsync(string filepath, string contents);
    Task<IEnumerable<CodeSearchResult>> SearchCodebaseAsync(string query, int maxResults);
    Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd);

    // MCP tool management
    Task LoadMcpToolsAsync(string serverId);
    Task<ToolResult> InvokeMcpToolAsync(string serverId, string toolName, IDictionary<string, object> args);

    // Events
    event EventHandler<ToolErrorEventArgs> Error;
}
```

**Responsibilities:**
- Route tool calls (built-in, MCP, HTTP)
- Execute file operations via IDE abstraction
- Handle MCP server tool registry
- Return structured results

**Dependencies:**
- IConfigService (tool definitions)
- IIdeService (file ops)
- MCP server management

---

#### **ISessionService** (Session & History Subsystem)

```csharp
public interface ISessionService
{
    // Current session
    Session GetCurrentSession();
    IObservable<Session> CurrentSessionChanged { get; }

    // Session management
    Task CreateNewSessionAsync(string? title = null);
    Task SaveCurrentSessionAsync();
    Task LoadSessionAsync(string sessionId);

    // Message operations
    Task AddMessageAsync(ChatMessage message);
    Task UpdateMessageAsync(string messageId, ChatMessage updatedMessage);
    Task DeleteMessageAsync(string messageId);

    // History
    IAsyncEnumerable<SessionMetadata> ListSessionsAsync(int limit = 50);
    Task DeleteSessionAsync(string sessionId);

    // Events
    event EventHandler<SessionChangedEventArgs> SessionChanged;
    event EventHandler<MessageAddedEventArgs> MessageAdded;
}
```

**Responsibilities:**
- Maintain current session in memory
- Persist sessions to storage (file or DB)
- Support session navigation (load, new, delete)

**Dependencies:**
- File/DB storage abstraction
- ILlmService (for message content)

---

#### **IIndexingService** (Codebase Indexing Subsystem)

```csharp
public interface IIndexingService
{
    // Indexing control
    Task StartIndexingAsync();
    Task PauseIndexingAsync();
    Task ResumeIndexingAsync();
    Task CancelIndexingAsync();

    // Progress
    IndexingStatus GetCurrentStatus();
    IObservable<IndexingProgressUpdate> ProgressUpdates { get; }

    // Index query
    Task<bool> IsIndexedAsync(string filepath);
    Task<IEnumerable<CodeSymbol>> SearchIndexAsync(string query, int maxResults);

    // Rebuild
    Task RebuildIndexAsync();

    // Events
    event EventHandler<IndexingProgressEventArgs> ProgressChanged;
    event EventHandler<IndexingErrorEventArgs> Error;
}
```

**Responsibilities:**
- Walk workspace directories (respecting .gitignore, .continueignore)
- Compute embeddings (batch)
- Persist to vector DB (SQLite or LanceDB)
- Support pause/resume/cancel

**Dependencies:**
- IConfigService (ignore rules, embeddings model)
- IIdeService (file system access)
- Vector DB abstraction

---

#### **IContextService** (Context Retrieval Subsystem)

```csharp
public interface IContextService
{
    // Context retrieval
    Task<IEnumerable<ContextItem>> GetContextItemsAsync(
        string query,
        string? selectedCode = null,
        int maxItems = 10);

    // Context providers
    IEnumerable<IContextProvider> GetEnabledProviders();

    // Add explicit context
    Task AddContextItemAsync(ContextItem item);
    Task RemoveContextItemAsync(string itemId);
}
```

**Responsibilities:**
- Query indexed codebase (RAG)
- Gather context from multiple providers (recent files, symbols, etc.)
- De-duplicate and intelligently rank results

**Dependencies:**
- IIndexingService (vector search)
- IIdeService (editor state, current file)

---

#### **IMcpService** (MCP Integration Subsystem)

```csharp
public interface IMcpService
{
    // Server lifecycle
    Task InitializeServerAsync(McpServerConfig config);
    Task ShutdownServerAsync(string serverId);
    Task RestartServerAsync(string serverId);

    // Server status
    McpServerStatus GetServerStatus(string serverId);
    IEnumerable<McpServerStatus> GetAllServers();
    IObservable<McpServerStatus> ServerStatusChanged { get; }

    // Tool discovery
    IEnumerable<ToolDefinition> GetServerTools(string serverId);

    // Resource access
    Task<string> GetResourceAsync(string serverId, string resourceUri);

    // Prompt templates
    Task<PromptTemplate> GetPromptAsync(string serverId, string promptName, IDictionary<string, object>? args = null);

    // Events
    event EventHandler<McpServerEventArgs> ServerConnected;
    event EventHandler<McpServerEventArgs> ServerDisconnected;
}
```

**Responsibilities:**
- Spawn and manage MCP server processes
- Register tools from servers
- Pass results back to Tool subsystem

**Dependencies:**
- IConfigService (server definitions)
- Process management (.NET Process API)

---

#### **IIdeService** (IDE Abstraction Layer)

```csharp
public interface IIdeService
{
    // File operations
    Task<string> ReadFileAsync(string filepath);
    Task WriteFileAsync(string filepath, string contents);
    Task<string> ReadRangeInFileAsync(string filepath, int startLine, int endLine);
    Task SaveFileAsync(string filepath);
    Task DeleteFileAsync(string filepath);

    // Git
    Task<string> GetBranchAsync();
    Task<string> GetRepoNameAsync();
    Task<string> GetGitRootPathAsync();

    // LSP (Language Server Protocol)
    Task<IEnumerable<Location>> GotoDefinitionAsync(Location location);
    Task<IEnumerable<Location>> GetReferencesAsync(Location location);
    Task<IEnumerable<DocumentSymbol>> GetDocumentSymbolsAsync(string filepath);
    Task<IEnumerable<Diagnostic>> GetProblemsAsync(string filepath);

    // Subprocess
    Task<(string stdout, string stderr)> RunSubprocessAsync(string command, string cwd);

    // Editor state
    string GetActiveFilepath();
    string GetSelectedText();
    Selection GetCursorSelection();
    IObservable<string> ActiveFileChanged { get; }

    // File validation
    bool FileExists(string filepath);
    IEnumerable<string> GetWorkspaceFiles(string pattern = "*");

    // Events
    event EventHandler<FileChangedEventArgs> FileChanged;
    event EventHandler<ActiveFileChangedEventArgs> ActiveFileChanged;
}
```

**Responsibilities:**
- Abstract IDE APIs (DTE in this case, but interface-compatible)
- Provide file, git, LSP operations
- Track active file and editor state

**Dependencies:**
- EnvDTE (Visual Studio automation)
- May use separate LSP client

---

#### **IMessengerService** (Message Routing)

```csharp
public interface IMessengerService
{
    // Request-response (async)
    Task<TResponse> RequestAsync<TRequest, TResponse>(
        string messageType,
        TRequest data,
        CancellationToken ct = default);

    // Fire-and-forget (send)
    void Send<TData>(string messageType, TData data);

    // Listen for messages (register handler)
    void On<TData, TResponse>(
        string messageType,
        Func<TData, Task<TResponse>> handler);

    // Streaming (IAsyncEnumerable)
    IAsyncEnumerable<TChunk> StreamAsync<TRequest, TChunk>(
        string messageType,
        TRequest data,
        CancellationToken ct = default);
}
```

**Responsibilities:**
- Route messages between services and GUI
- Support request/response, fire-and-forget, and streaming patterns
- Hide transport details (in-process, webview, stdio)

**Dependencies:**
- Channel-based communication (.NET 4.7.2 compat: use TPL + Queue or upgrade to .NET 5+ Channels)

---

#### **INotificationService** (UI Notifications)

```csharp
public interface INotificationService
{
    // Show notification
    Task ShowNotificationAsync(string title, string message, NotificationType type);

    // Show progress
    Task ShowProgressAsync(string title, Action<IProgress<int>> workAction);

    // Dialogs
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task<string> ShowInputAsync(string title, string prompt, string defaultValue = "");

    // Events
    event EventHandler<NotificationEventArgs> NotificationShown;
}
```

**Responsibilities:**
- Display toast notifications
- Show dialogs and confirmations
- Report progress to user

**Dependencies:**
- WPF UI (MainWindow, MessageBox)

---

### 2.2 Service Implementation Strategy

**For .NET Framework 4.7.2:**
- Use `async Task` / `await` (supported)
- Use `IObservable<T>` from Rx.NET (lightweight reactivity without full Rx stack)
- Use TPL (Task Parallel Library) for concurrency
- Use standard .NET events alongside observables

**Singleton Services:**
- IConfigService (load once at startup, watch for changes)
- ILlmService (reuse HTTP client, model cache)
- ISessionService (current session in memory)
- IIndexingService (single background task)
- IContextService (queries other services)
- IMcpService (server lifecycle management)
- IMessengerService (master dispatcher)
- IIdeService (singleton Ide abstraction)

**Scoped Dependencies:**
- INotificationService (scope to ViewModel lifetime in edit mode)

---

## 3. ViewModel Layer Design

### 3.1 ViewModel Hierarchy

**Root ViewModel: `MainViewModel`**

Owns:
- Session state (current messages, metadata)
- Navigation state (current page)
- Global commands (NewSession, OpenConfig, ShowStats)
- Error handling

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly ISessionService _sessionService;
    private readonly IMessengerService _messengerService;
    private readonly INotificationService _notificationService;

    // Properties
    public ObservableCollection<ChatMessage> CurrentMessages { get; }
    public Session CurrentSession { get; set; }
    public string CurrentRoute { get; set; }
    public bool IsLoading { get; set; }

    // Commands
    public RelayCommand NewSessionCommand { get; }
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand SaveSessionCommand { get; }

    // Events
    public event EventHandler<SessionChangedEventArgs> SessionChanged;

    public MainViewModel(
        ISessionService sessionService,
        IMessengerService messengerService,
        INotificationService notificationService)
    {
        _sessionService = sessionService;
        _messengerService = messengerService;
        _notificationService = notificationService;

        CurrentMessages = new ObservableCollection<ChatMessage>();

        // Bind session service changes to properties
        _sessionService.SessionChanged += OnSessionChanged;

        // Commands
        NewSessionCommand = new RelayCommand(ExecuteNewSession);
        NavigateCommand = new RelayCommand<string>(ExecuteNavigate);
        SaveSessionCommand = new RelayCommand(ExecuteSaveSession);
    }

    private async void ExecuteNewSession() { ... }
    private void ExecuteNavigate(string route) { ... }
    private async void ExecuteSaveSession() { ... }
    private void OnSessionChanged(object? sender, SessionChangedEventArgs e) { ... }
}
```

**Page ViewModels (inherit from ViewModelBase):**

1. **ChatPageViewModel** — chat input, message list, streaming
2. **ConfigPageViewModel** — model selection, tool settings, profiles
3. **HistoryPageViewModel** — session list, load/delete
4. **StatsPageViewModel** — token counts, LLM usage
5. **EditModeViewModel** — code diff, accept/reject, apply

---

### 3.2 ChatPageViewModel (Core Workflow)

```csharp
public class ChatPageViewModel : ViewModelBase
{
    private readonly ILlmService _llmService;
    private readonly IContextService _contextService;
    private readonly IToolService _toolService;
    private readonly ISessionService _sessionService;
    private readonly INotificationService _notificationService;

    // Properties
    public ObservableCollection<ChatMessage> Messages { get; }
    public string InputText { get; set; }
    public bool IsStreaming { get; set; }
    public ObservableCollection<ContextItem> SelectedContext { get; }
    public string StreamingResponse { get; set; }

    // Commands
    public RelayCommand SendMessageCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand AddContextCommand { get; }

    private CancellationTokenSource _streamingCts;

    public ChatPageViewModel(
        ILlmService llmService,
        IContextService contextService,
        IToolService toolService,
        ISessionService sessionService,
        INotificationService notificationService)
    {
        _llmService = llmService;
        _contextService = contextService;
        _toolService = toolService;
        _sessionService = sessionService;
        _notificationService = notificationService;

        Messages = new ObservableCollection<ChatMessage>();
        SelectedContext = new ObservableCollection<ContextItem>();

        SendMessageCommand = new RelayCommand(ExecuteSendMessage, CanSendMessage);
        CancelCommand = new RelayCommand(ExecuteCancel, () => IsStreaming);
        AddContextCommand = new RelayCommand<string>(ExecuteAddContext);
    }

    private async void ExecuteSendMessage()
    {
        IsStreaming = true;
        _streamingCts = new CancellationTokenSource();

        try
        {
            // 1. Retrieve context
            var contextItems = await _contextService.GetContextItemsAsync(InputText);
            SelectedContext.Clear();
            foreach (var item in contextItems)
                SelectedContext.Add(item);

            // 2. Build messages (system prompt + context + user input)
            var messages = BuildPrompt(InputText, SelectedContext);

            // 3. Stream LLM response
            StreamingResponse = "";
            await foreach (var chunk in _llmService.StreamAsync(messages, cancellationToken: _streamingCts.Token))
            {
                if (chunk.Type == CompletionChunkType.TextDelta)
                    StreamingResponse += chunk.Content;
                else if (chunk.Type == CompletionChunkType.ToolCall)
                    await HandleToolCall(chunk.ToolCall);
            }

            // 4. Save message to session
            var assistantMessage = new ChatMessage { Role = "assistant", Content = StreamingResponse };
            await _sessionService.AddMessageAsync(assistantMessage);

            // 5. Add to UI
            Messages.Add(assistantMessage);
            InputText = "";
        }
        catch (OperationCanceledException)
        {
            StreamingResponse += "\n[Cancelled by user]";
        }
        finally
        {
            IsStreaming = false;
            _streamingCts?.Dispose();
        }
    }

    private bool CanSendMessage() => !IsStreaming && !string.IsNullOrWhiteSpace(InputText);

    private void ExecuteCancel() => _streamingCts?.Cancel();
}
```

---

### 3.3 ConfigPageViewModel (Settings)

```csharp
public class ConfigPageViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IIndexingService _indexingService;

    // Properties
    public ObservableCollection<ModelInfo> AvailableModels { get; }
    public ModelInfo SelectedModel { get; set; }
    public ObservableCollection<ToolDefinition> AvailableTools { get; }
    public ObservableCollection<ProfileInfo> Profiles { get; }

    // Commands
    public RelayCommand AddModelCommand { get; }
    public RelayCommand RemoveModelCommand { get; }
    public RelayCommand SaveConfigCommand { get; }
    public RelayCommand ReindexCommand { get; }

    public ConfigPageViewModel(
        IConfigService configService,
        IIndexingService indexingService)
    {
        _configService = configService;
        _indexingService = indexingService;

        AvailableModels = new ObservableCollection<ModelInfo>();
        AvailableTools = new ObservableCollection<ToolDefinition>();
        Profiles = new ObservableCollection<ProfileInfo>();

        AddModelCommand = new RelayCommand(ExecuteAddModel);
        RemoveModelCommand = new RelayCommand(ExecuteRemoveModel);
        SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
        ReindexCommand = new RelayCommand(ExecuteReindex);

        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        var config = _configService.GetCurrentConfig();

        AvailableModels.Clear();
        foreach (var model in config.Models)
            AvailableModels.Add(model);

        AvailableTools.Clear();
        foreach (var tool in _configService.GetEnabledTools())
            AvailableTools.Add(tool);

        SelectedModel = _configService.GetSelectedModel();
    }

    private async void ExecuteSaveConfig()
    {
        await _configService.SaveConfigAsync();
        // Notify user
    }

    private async void ExecuteReindex()
    {
        await _indexingService.RebuildIndexAsync();
    }
}
```

---

### 3.4 IndexingProgressViewModel (Background Task)

```csharp
public class IndexingProgressViewModel : ViewModelBase
{
    private readonly IIndexingService _indexingService;

    // Properties
    public double ProgressPercentage { get; set; }
    public string CurrentFile { get; set; }
    public string Status { get; set; }
    public bool IsIndexing { get; set; }

    // Commands
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand CancelCommand { get; }

    public IndexingProgressViewModel(IIndexingService indexingService)
    {
        _indexingService = indexingService;

        PauseCommand = new RelayCommand(ExecutePause);
        ResumeCommand = new RelayCommand(ExecuteResume);
        CancelCommand = new RelayCommand(ExecuteCancel);

        // Subscribe to progress updates
        _indexingService.ProgressChanged += OnProgressChanged;
    }

    private void OnProgressChanged(object? sender, IndexingProgressEventArgs e)
    {
        ProgressPercentage = e.Percentage;
        CurrentFile = e.CurrentFile;
        Status = e.Status;
        IsIndexing = !e.IsComplete;
    }
}
```

---

## 4. View Layer Design

### 4.1 View Hierarchy (WPF)

```
MainWindow (MainViewModel)
├── MenuBar
├── ChatPage (ChatPageViewModel)
│   ├── ContextPanel
│   │   └── ContextItemList
│   ├── MessagesList
│   │   └── ChatMessageItem
│   │       ├── UserMessage
│   │       └── AssistantMessage (streaming)
│   └── InputBox
│       ├── TextInput
│       └── SendButton
├── ConfigPage (ConfigPageViewModel)
│   ├── ModelsTab
│   │   ├── ModelList
│   │   └── AddModelDialog
│   ├── ToolsTab
│   │   └── ToolCheckList
│   └── ProfilesTab
│       └── ProfileSelector
├── HistoryPage (HistoryPageViewModel)
│   ├── SessionList
│   └── SessionActions (Delete, Load)
├── StatsPage (StatsPageViewModel)
│   ├── TokenCounter
│   └── UsageChart
├── EditModePage (EditModeViewModel)
│   ├── DiffViewer
│   ├── AcceptButton
│   └── RejectButton
├── IndexingProgressBar (IndexingProgressViewModel)
│   └── ProgressControl
└── NotificationContainer
    └── ToastNotifications
```

---

### 4.2 MainWindow.xaml (Root)

```xml
<Window x:Class="ContinueVS.UI.MainWindow"
        Title="Continue" Width="600" Height="800">
    <Grid RowDefinitions="Auto,*,Auto">
        <!-- Menubar -->
        <StackPanel Grid.Row="0" Orientation="Horizontal">
            <Button Command="{Binding NewSessionCommand}" Content="New Chat" />
            <Button Command="{Binding NavigateCommand}" 
                    CommandParameter="config" Content="Settings" />
            <Button Command="{Binding NavigateCommand}" 
                    CommandParameter="history" Content="History" />
        </StackPanel>

        <!-- Content (Router outlet) -->
        <Frame Grid.Row="1" x:Name="ContentFrame" NavigationUIVisibility="Hidden" />

        <!-- Indexing Progress (always visible) -->
        <local:IndexingProgressControl Grid.Row="2" 
            DataContext="{Binding IndexingProgressViewModel}" />
    </Grid>
</Window>
```

---

### 4.3 ChatPage.xaml (Chat Interface)

```xml
<UserControl x:Class="ContinueVS.UI.Pages.ChatPage"
             DataContext="{Binding ChatPageViewModel, Mode=OneWay}">
    <Grid RowDefinitions="Auto,*,Auto">
        <!-- Context Panel -->
        <Expander Grid.Row="0" Header="Context" IsExpanded="False">
            <ItemsControl ItemsSource="{Binding SelectedContext}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <local:ContextItemControl DataContext="{Binding}" />
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </Expander>

        <!-- Messages (Chat List) -->
        <ItemsControl Grid.Row="1" ItemsSource="{Binding Messages}" 
                      VerticalAlignment="Stretch">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <local:ChatMessageControl DataContext="{Binding}" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <!-- Input Area -->
        <Grid Grid.Row="2" RowDefinitions="*,Auto">
            <TextBox Grid.Row="0" Text="{Binding InputText, UpdateSourceTrigger=PropertyChanged}" 
                     IsEnabled="{Binding IsStreaming, Converter={local:InverseBooleanConverter}}"
                     TextWrapping="Wrap" Height="80" />
            <Grid Grid.Row="1" ColumnDefinitions="*,Auto,Auto">
                <Button Grid.Column="1" Command="{Binding SendMessageCommand}" 
                        Content="Send" Padding="10" />
                <Button Grid.Column="2" Command="{Binding CancelCommand}" 
                        Content="Cancel" Visibility="{Binding IsStreaming, Converter={local:BooleanToVisibilityConverter}}" />
            </Grid>
        </Grid>
    </Grid>
</UserControl>
```

---

### 4.4 Data Binding Strategy

**For .NET Framework 4.7.2 + MVVM Light:**

1. **OneWay Bindings** for read-only data (MessageList, ContextItems)
2. **TwoWay Bindings** for user input (TextBox, ComboBox)
3. **Commanding** for button actions (Send, Cancel, Save)
4. **InotifyPropertyChanged** automation via MVVM Light `ViewModelBase`
5. **ObservableCollection** for dynamic lists (Messages, Models, Tools)
6. **Value Converters** for boolean/visibility conversions (IsStreaming → ButtonVisibility)

---

## 5. State Management Design

### 5.1 Redux → MVVM Mapping

| TS Redux | .NET MVVM | Storage |
|----------|-----------|---------|
| Slice: `session` | SessionService + SessionViewModel | In-memory ObservableCollection |
| Slice: `config` | ConfigService + ConfigViewModel | File-based (config.json) |
| Slice: `ui` | MainViewModel properties | Memory |
| Slice: `editState` | EditModeViewModel | Memory |

**Key difference:**
- Redux = single centralized store with reducers
- MVVM = distributed ViewModels with bound properties
- But same **data flow** (unidirectional: Service → ViewModel → View)

### 5.2 Event Propagation

All service changes → ViewModel properties → UI re-binds

```
ISessionService.SessionChanged event
    ↓
MainViewModel.OnSessionChanged()
    ↓ RaisePropertyChanged("CurrentSession")
View re-binds {Binding CurrentSession}
```

### 5.3 Cross-ViewModel Communication

Use either:
1. **MVVM Light Messenger** (lightweight publish-subscribe)
2. **Direct service events** (simpler, no extra abstraction)
3. **Shared ObservableCollection** (when a ViewModel owns the data)

**Recommended:** Use services as the source of truth; ViewModels bind to service state.

---

## 6. Dependency Injection Container

### 6.1 DI Setup (Startup)

```csharp
public class ServiceBootstrapper
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Singleton services (shared across app lifetime)
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILlmService, LlmService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IToolService, ToolService>();
        services.AddSingleton<IIndexingService, IndexingService>();
        services.AddSingleton<IContextService, ContextService>();
        services.AddSingleton<IMcpService, McpService>();
        services.AddSingleton<IIdeService, VsIdeService>(); // DTE-specific adapter
        services.AddSingleton<IMessengerService, MessengerService>();

        // ViewModel factories
        services.AddSingleton<Func<MainViewModel>>(sp => () => new MainViewModel(
            sp.GetRequiredService<ISessionService>(),
            sp.GetRequiredService<IMessengerService>(),
            sp.GetRequiredService<INotificationService>()));

        services.AddSingleton<Func<ChatPageViewModel>>(sp => () => new ChatPageViewModel(
            sp.GetRequiredService<ILlmService>(),
            sp.GetRequiredService<IContextService>(),
            sp.GetRequiredService<IToolService>(),
            sp.GetRequiredService<ISessionService>(),
            sp.GetRequiredService<INotificationService>()));

        // Add more ViewModel factories...

        services.AddSingleton<INotificationService, WpfNotificationService>();

        return services.BuildServiceProvider();
    }
}
```

### 6.2 Application Class Integration

```csharp
public partial class App : Application
{
    private IServiceProvider _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _serviceProvider = ServiceBootstrapper.ConfigureServices();

        // Create main window with injected ViewModel
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        MainWindow = new MainWindow { DataContext = mainViewModel };
        MainWindow.Show();

        // Initialize services
        var configService = _serviceProvider.GetRequiredService<IConfigService>();
        configService.InitializeAsync().GetAwaiter().GetResult();

        var indexingService = _serviceProvider.GetRequiredService<IIndexingService>();
        indexingService.StartIndexingAsync().FireAndForget();
    }
}
```

---

## 7. Message Flow Patterns

### 7.1 User Sends Message Flow

```
User types in ChatPage.xaml
    ↓ TextBox binding
ChatPageViewModel.InputText = "..."
    ↓ User clicks SendMessageCommand
ChatPageViewModel.ExecuteSendMessage()
    ↓ await _llmService.StreamAsync()
For each chunk:
    ChatPageViewModel.StreamingResponse += chunk
    RaisePropertyChanged("StreamingResponse")
    ↓ View binds {Binding StreamingResponse}
AssistantMessageControl re-renders chunk
    ↓ Stream completes
await _sessionService.AddMessageAsync()
    ↓ ISessionService.SessionChanged event
MainViewModel.OnSessionChanged()
    ↓ RaisePropertyChanged("CurrentSession")
HistoryPage re-binds {Binding CurrentSession.Messages}
```

### 7.2 Config Change Flow

```
User changes model in ConfigPage.xaml
    ↓ ComboBox binding
ConfigPageViewModel.SelectedModel = Model
    ↓ User clicks SaveConfigCommand
ConfigPageViewModel.ExecuteSaveConfig()
    ↓ await _configService.SelectModelAsync(model)
    ↓ _configService.SaveConfigAsync()
    ↓ IConfigService.ConfigChanged event
MainViewModel.OnConfigChanged()
    ↓ RaisePropertyChanged("ActiveModel")
ChatPageViewModel.RefreshContextProviders()
    ↓ All ViewModels react
```

---

## 8. Async/Await Pattern Guidelines

### 8.1 For .NET Framework 4.7.2

- Use `async Task` for all long-running operations
- Use `await` to prevent blocking UI thread
- Use `CancellationToken` for cancel-ability
- Use `IAsyncEnumerable<T>` for streaming (standard, backported via NuGet)

**Setup:**
Add NuGet package `Microsoft.Bcl.AsyncInterfaces` to enable `IAsyncEnumerable<T>` on .NET Framework 4.7.2.

**Example:**

```csharp
private async void ExecuteSendMessage()
{
    try
    {
        var messages = BuildPrompt(InputText, SelectedContext);
        await foreach (var chunk in _llmService.StreamAsync(messages, _streamingCts.Token))
        {
            StreamingResponse += chunk.Content;
            RaisePropertyChanged(nameof(StreamingResponse));
        }
    }
    catch (OperationCanceledException)
    {
        // Handle cancellation
    }
}
```

**Note:** `Microsoft.Bcl.AsyncInterfaces` provides `IAsyncEnumerable<T>`, `IAsyncEnumerator<T>`, and `IAsyncDisposable` for .NET Framework 4.7.2, enabling idiomatic async enumeration patterns without workarounds.

---

## 9. Error Handling Strategy

### 9.1 Service-Level Error Handling

Each service catches and logs its own errors:

```csharp
public async Task<IAsyncEnumerable<CompletionChunk>> StreamAsync(...)
{
    try
    {
        // Call LLM API
    }
    catch (HttpRequestException ex)
    {
        Error?.Invoke(this, new LlmErrorEventArgs { Exception = ex, IsRetryable = true });
        throw;
    }
    catch (Exception ex)
    {
        Error?.Invoke(this, new LlmErrorEventArgs { Exception = ex, IsRetryable = false });
        throw;
    }
}
```

### 9.2 ViewModel-Level Error Handling

ViewModels subscribe to service errors and display notifications:

```csharp
_llmService.Error += (s, e) =>
{
    _notificationService.ShowNotificationAsync(
        "LLM Error",
        e.Exception.Message,
        NotificationType.Error);
};
```

### 9.3 Global Error Handler

Register a global exception handler in App.xaml.cs:

```csharp
Application.Current.DispatcherUnhandledException += (s, e) =>
{
    _notificationService.ShowNotificationAsync(
        "Unhandled Exception",
        e.Exception.Message,
        NotificationType.Error);
    e.Handled = true;
};
```

---

## 10. Integration Points Summary

| Layer | Responsibility | Key Pattern |
|-------|-----------------|------------|
| **View (XAML)** | User input/output | Data binding, value converters, commanding |
| **ViewModel** | State + logic | ObservableCollection, RelayCommand, async methods |
| **Service** | Business logic + async ops | Async Task, event notifications, error handling |
| **IDE Abstraction** | Platform-specific ops | Protocol messages or direct DTE calls |

---

## 11. Technology Stack Decision

For .NET Framework 4.7.2 + WPF:

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **ViewModel Base** | MVVM Light | Lightweight, battle-tested, supports 4.7.2 |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection | Standard, NuGet available for 4.7.2 |
| **Async Enumeration** | Microsoft.Bcl.AsyncInterfaces (NuGet) | Backports `IAsyncEnumerable<T>` to .NET 4.7.2; idiomatic streaming |
| **Logging** | Serilog | Structured logging, works on 4.7.2 |
| **HTTP** | HttpClient (System.Net.Http) | Built-in, reusable per service |
| **JSON** | Newtonsoft.Json | Already used in codebase |
| **Testing** | xUnit + Moq | Modern, easy mocking of services |
| **Vector DB** | SQLite + custom wrapper or LanceDB if .NET bindings available | File-based, no server |

---

## 12. Known Gaps & Future Decisions

1. **MCP subprocess** spawning → Coordinate with IDE (VSIX process or separate)
2. **Vector DB choice** → LanceDB .NET wrapper maturity unknown, consider SQLite FTS5 fallback
3. **Theme injection** → WPF doesn't have VSCode theme variables natively; may need XAML resource manager
4. **Edit mode diff viewer** → WPF text control vs. external library (DiffPlex?) vs. simple HTML rendering
5. **Token counting library** → TS uses `js-tiktoken`; .NET needs equivalent .NET library or P/Invoke wrapper

---

**End of Design Document**
