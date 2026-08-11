
# ContinueVS Implementation Plan

**Phase:** Step-by-step implementation roadmap for WPF + C# backend refactor.  
**Target:** .NET Framework 4.7.2 + .NET 10 compatibility.  
**Integration:** Refactor existing handlers/services into ARCH service layer + new MVVM UI layer.  
**Execution:** Follow steps in order; each step is atomic and tracks dependencies.

---

## Execution Rules

- ✅ **Atomic steps**: One action per step (create, implement, wire, test)
- ✅ **Dependencies tracked**: Each step notes what it depends on
- ✅ **Existing code reuse**: Flag "use existing X vs. create new"
- ✅ **Build validation**: Every 5-10 steps, verify compilation
- ✅ **No skipping**: Steps are ordered; don't jump ahead

---

## Phase 1: Core Types & Contracts (Steps 1-15)

*Establish shared data models and service interfaces.*

### Step 1: Create Core Types Folder Structure
- **Action:** Create folder `src/VSIXProject1/Core/Types/`
- **Why:** Centralize all DTO/contract types used by services
- **Depends on:** None
- **Files created:** (folder only)

### Step 2: Define Chat Message Type
- **Action:** Create `Core/Types/ChatMessage.cs`
- **Content:** Class with Role, Content, ToolCalls properties
- **Depends on:** Step 1
- **Existing reference:** Likely partial in Handlers/Llm/*

### Step 3: Define LLM Completion Chunk Type
- **Action:** Create `Core/Types/CompletionChunk.cs`
- **Content:** Type, Content, ToolCall data; supports streaming
- **Depends on:** Step 1

### Step 4: Define Tool Types
- **Action:** Create `Core/Types/ToolDefinition.cs`, `ToolResult.cs`, `ToolError.cs`
- **Content:** Tool registry, arguments, results
- **Depends on:** Step 1
- **Existing reference:** Use/adapt from `Handlers/builtIn.ts` concept

### Step 5: Define Session Types
- **Action:** Create `Core/Types/Session.cs`, `SessionMetadata.cs`
- **Content:** Session state, turns, metadata
- **Depends on:** Step 2 (ChatMessage)

### Step 6: Define Config Types
- **Action:** Create `Core/Types/ContinueConfig.cs`, `ModelInfo.cs`, `ProfileInfo.cs`
- **Content:** Configuration schema
- **Depends on:** Step 1
- **Existing reference:** Refactor from `ConfigCache.cs` if possible

### Step 7: Define Indexing Types
- **Action:** Create `Core/Types/IndexingStatus.cs`, `IndexingProgressUpdate.cs`
- **Content:** Progress tracking, status enums
- **Depends on:** Step 1

### Step 8: Define Context Types
- **Action:** Create `Core/Types/ContextItem.cs`, `CodeSymbol.cs`
- **Content:** Context retrieval results
- **Depends on:** Step 1

### Step 9: Define Event Argument Types
- **Action:** Create `Core/Types/ConfigChangedEventArgs.cs`, `LlmErrorEventArgs.cs`, etc.
- **Content:** Event payload classes (9 total for 9 subsystems)
- **Depends on:** Steps 1-8

### Step 10: Create Service Interfaces Folder
- **Action:** Create folder `src/VSIXProject1/Services/Interfaces/`
- **Why:** Separate contracts from implementations
- **Depends on:** None

### Step 11: Create IConfigService Interface
- **Action:** Create `Services/Interfaces/IConfigService.cs`
- **Content:** From DESIGN.md section 2.1
- **Depends on:** Steps 6, 9

### Step 12: Create ILlmService Interface
- **Action:** Create `Services/Interfaces/ILlmService.cs`
- **Content:** From DESIGN.md section 2.1
- **Depends on:** Steps 2, 3, 9

### Step 13: Create Remaining Service Interfaces
- **Action:** Create `IToolService.cs`, `ISessionService.cs`, `IIndexingService.cs`, `IContextService.cs`, `IMcpService.cs`, `IIdeService.cs`, `IMessengerService.cs`, `INotificationService.cs`
- **Content:** From DESIGN.md section 2.1
- **Depends on:** Steps 1-9

### Step 14: Create Service Event Arguments
- **Action:** Create additional event arg types needed by services (LlmErrorEventArgs, ToolErrorEventArgs, IndexingProgressEventArgs, etc.)
- **Depends on:** Step 9

### Step 15: Build & Validate Phase 1
- **Action:** Compile solution; verify all types compile without errors
- **Command:** `dotnet build`
- **Depends on:** Steps 1-14

---

## Phase 2: Service Implementations (Steps 16-45)

*Implement service interfaces; wrap/refactor existing handlers.*

### Step 16: Create Service Implementations Folder
- **Action:** Create folder `src/VSIXProject1/Services/Implementations/`
- **Depends on:** None

### Step 17: Implement IConfigService
- **Action:** Create `Services/Implementations/ConfigService.cs`
- **Content:**
  - Refactor existing `ConfigCache.cs` OR wrap it
  - Load `~/.continue/config.json`
  - Expose via interface methods
  - Fire ConfigChanged events
- **Depends on:** Step 11
- **Existing reference:** Reuse/adapt `ConfigCache.cs`

### Step 18: Implement IIdeService
- **Action:** Create `Services/Implementations/VsIdeService.cs`
- **Content:**
  - Wrap existing `DTEAdapter.cs`
  - Implement file ops (readFile, writeFile, etc.)
  - Implement git ops, LSP stubs
  - Expose vs. wrapping decision here
- **Depends on:** Step 13
- **Existing reference:** Reuse `DTEAdapter.cs` + `ProcessAdapter.cs`

### Step 19: Implement IMessengerService
- **Action:** Create `Services/Implementations/MessengerService.cs`
- **Content:**
  - Wrap existing `MessageDispatcher.cs`
  - Implement request/response, send, on, stream patterns
  - Route to handler registry
- **Depends on:** Step 13
- **Existing reference:** Use existing `MessageDispatcher.cs` as backend

### Step 20: Implement IToolService
- **Action:** Create `Services/Implementations/ToolService.cs`
- **Content:**
  - Route built-in tools to IIdeService methods
  - Load tool definitions from config
  - Implement invoke routing (built-in, MCP, HTTP)
- **Depends on:** Steps 12, 18, 19
- **Existing reference:** Adapt logic from `Handlers/File/*` and `Handlers/callTool.ts` pattern

### Step 21: Implement ISessionService
- **Action:** Create `Services/Implementations/SessionService.cs`
- **Content:**
  - Maintain current session in memory
  - Persist/load from file (under `.continue/sessions/`)
  - Fire SessionChanged events
- **Depends on:** Step 5
- **Existing reference:** Check if session storage already exists

### Step 22: Implement ILlmService (Skeleton)
- **Action:** Create `Services/Implementations/LlmService.cs`
- **Content:**
  - Stub methods (will fill in later with LLM logic)
  - Route StreamAsync via IMessengerService
  - Implement model capability detection (from autodetect.ts pattern)
  - Implement token counting (stubs for now)
- **Depends on:** Steps 12, 19

### Step 23: Implement IIndexingService (Skeleton)
- **Action:** Create `Services/Implementations/IndexingService.cs`
- **Content:**
  - Stub methods for indexing control
  - Fire ProgressUpdates events
  - Defer actual indexing logic
- **Depends on:** Step 13

### Step 24: Implement IContextService (Skeleton)
- **Action:** Create `Services/Implementations/ContextService.cs`
- **Content:**
  - Stub context retrieval
  - Defer RAG logic
- **Depends on:** Step 13

### Step 25: Implement IMcpService (Skeleton)
- **Action:** Create `Services/Implementations/McpService.cs`
- **Content:**
  - Stub server lifecycle
  - Defer MCP process management
- **Depends on:** Step 13

### Step 26: Implement INotificationService
- **Action:** Create `Services/Implementations/WpfNotificationService.cs`
- **Content:**
  - Show MessageBox, notification toast (WPF implementation)
  - Show dialogs
- **Depends on:** Step 13

### Step 27: Create Service Exceptions Folder
- **Action:** Create folder `src/VSIXProject1/Services/Exceptions/`
- **Depends on:** None

### Step 28: Create Custom Service Exceptions
- **Action:** Create exception types: `ConfigLoadException.cs`, `LlmException.cs`, `ToolInvocationException.cs`, `IndexingException.cs`
- **Depends on:** Step 27

### Step 29: Update IConfigService to Throw Exceptions
- **Action:** Modify `ConfigService.cs` to throw `ConfigLoadException` on error
- **Depends on:** Steps 17, 28

### Step 30: Update ILlmService to Throw Exceptions
- **Action:** Modify `LlmService.cs` to throw `LlmException` on error
- **Depends on:** Steps 22, 28

### Step 31: Create DI Container Setup
- **Action:** Create `Services/ServiceBootstrapper.cs`
- **Content:** From DESIGN.md section 6.1; wire all services + ViewModels
- **Depends on:** Steps 17-26

### Step 32: Add NuGet Packages for Services
- **Action:** Add packages to .csproj:
  - `Microsoft.Bcl.AsyncInterfaces` (for IAsyncEnumerable)
  - `Microsoft.Extensions.DependencyInjection`
  - `System.Reactive` (for IObservable)
- **Depends on:** None (orthogonal)

### Step 33: Update App.xaml.cs to Initialize DI
- **Action:** Modify `ContinueVSPackage.cs` or app entry to call `ServiceBootstrapper.ConfigureServices()`
- **Depends on:** Steps 31, 32

### Step 34: Wire ConfigService to Handler Registry
- **Action:** Update `MessageDispatcher.cs` to resolve IConfigService and delegate config handler calls
- **Depends on:** Step 17, 19

### Step 35: Wire ToolService to Handler Registry
- **Action:** Update handlers to resolve IToolService
- **Depends on:** Step 20

### Step 36: Create Service Initialization Bootstrap
- **Action:** Create `Services/ServiceInitializer.cs`
- **Content:** Initialize services on startup (IConfigService.InitializeAsync, etc.)
- **Depends on:** Steps 17-26

### Step 37: Call ServiceInitializer in Plugin Startup
- **Action:** Modify `ContinueVSPackage.cs` to call ServiceInitializer
- **Depends on:** Step 36

### Step 38: Add Service Logging Infrastructure
- **Action:** Wire `IBridgeLogger` into services (dependency inject logging)
- **Depends on:** Step 31
- **Existing reference:** Reuse `BridgeLogger.cs`

### Step 39: Build & Validate Phase 2 (Part A)
- **Action:** Compile solution; verify service implementations compile
- **Command:** `dotnet build`
- **Depends on:** Steps 17-38

### Step 40: Add Unit Test Project Structure
- **Action:** Create folder `src/VSIXProject1.Tests/Services/`
- **Depends on:** None

### Step 41: Create Service Test Stubs
- **Action:** Create test classes for each service (stub tests, will flesh out later)
- **Depends on:** Step 40

### Step 42: Test IConfigService Initialization
- **Action:** Write test for ConfigService.InitializeAsync (read config file)
- **Depends on:** Steps 17, 41

### Step 43: Test IIdeService File Operations
- **Action:** Write test for VsIdeService.ReadFileAsync (mock file system)
- **Depends on:** Steps 18, 41

### Step 44: Test IMessengerService Request/Response
- **Action:** Write test for MessengerService.RequestAsync (mock dispatch)
- **Depends on:** Steps 19, 41

### Step 45: Build & Validate Phase 2 (Part B)
- **Action:** Compile + run tests; verify service layer works
- **Command:** `dotnet build && dotnet test`
- **Depends on:** Steps 42-44

---

## Phase 3: ViewModel Layer (Steps 46-70)

*Create MVVM Light ViewModels wired to services.*

### Step 46: Add MVVM Light NuGet Package
- **Action:** Add `MvvmLight` to .csproj
- **Depends on:** None

### Step 47: Create ViewModels Folder
- **Action:** Create folder `src/VSIXProject1/ViewModels/`
- **Depends on:** None

### Step 48: Create ViewModelBase (or use MVVM Light's)
- **Action:** Create `ViewModels/ViewModelBase.cs` or reference MVVM Light's `ViewModelBase`
- **Content:** RaisePropertyChanged, RelayCommand helpers
- **Depends on:** Step 46

### Step 49: Create MainViewModel
- **Action:** Create `ViewModels/MainViewModel.cs`
- **Content:** From DESIGN.md section 3
  - Properties: CurrentMessages, CurrentSession, CurrentRoute, IsLoading
  - Commands: NewSessionCommand, NavigateCommand, SaveSessionCommand
  - Inject: ISessionService, IMessengerService, INotificationService
- **Depends on:** Steps 48, 21, 19, 26

### Step 50: Create ChatPageViewModel
- **Action:** Create `ViewModels/ChatPageViewModel.cs`
- **Content:** From DESIGN.md section 3.2
  - Properties: Messages, InputText, IsStreaming, SelectedContext, StreamingResponse
  - Commands: SendMessageCommand, CancelCommand, AddContextCommand
  - Inject: ILlmService, IContextService, IToolService, ISessionService, INotificationService
- **Depends on:** Steps 48, 22, 24, 20, 21, 26

### Step 51: Create ConfigPageViewModel
- **Action:** Create `ViewModels/ConfigPageViewModel.cs`
- **Content:** From DESIGN.md section 3.3
  - Properties: AvailableModels, SelectedModel, AvailableTools, Profiles
  - Commands: AddModelCommand, RemoveModelCommand, SaveConfigCommand, ReindexCommand
  - Inject: IConfigService, IIndexingService
- **Depends on:** Steps 48, 17, 23

### Step 52: Create IndexingProgressViewModel
- **Action:** Create `ViewModels/IndexingProgressViewModel.cs`
- **Content:** From DESIGN.md section 3.4
  - Properties: ProgressPercentage, CurrentFile, Status, IsIndexing
  - Commands: PauseCommand, ResumeCommand, CancelCommand
  - Inject: IIndexingService
  - Subscribe to IIndexingService.ProgressChanged
- **Depends on:** Steps 48, 23

### Step 53: Create HistoryPageViewModel
- **Action:** Create `ViewModels/HistoryPageViewModel.cs`
- **Content:**
  - Properties: Sessions, SelectedSession
  - Commands: LoadSessionCommand, DeleteSessionCommand
  - Inject: ISessionService
- **Depends on:** Steps 48, 21

### Step 54: Create StatsPageViewModel
- **Action:** Create `ViewModels/StatsPageViewModel.cs`
- **Content:**
  - Properties: TokensUsed, ModelsUsed, CostEstimate
  - Commands: ExportStatsCommand
  - Inject: ILlmService (for stats)
- **Depends on:** Steps 48, 22

### Step 55: Create EditModeViewModel
- **Action:** Create `ViewModels/EditModeViewModel.cs`
- **Content:**
  - Properties: OriginalCode, NewCode, Diff, ShowAcceptPrompt
  - Commands: AcceptCommand, RejectCommand
  - Inject: INotificationService
- **Depends on:** Steps 48, 26

### Step 56: Add RelayCommand Helper (or use MVVM Light's)
- **Action:** Ensure `RelayCommand<T>` available (from MVVM Light)
- **Depends on:** Step 46

### Step 57: Wire MainViewModel to Services
- **Action:** Update MainViewModel to subscribe to service events
  - On SessionChanged → RaisePropertyChanged(CurrentSession)
  - On ConfigChanged → Refresh UI state
- **Depends on:** Step 49

### Step 58: Wire ChatPageViewModel to Streaming
- **Action:** Update ChatPageViewModel.ExecuteSendMessage to:
  - Call ILlmService.StreamAsync
  - Update StreamingResponse per chunk (via RaisePropertyChanged)
  - Handle cancellation (CancellationTokenSource)
- **Depends on:** Step 50

### Step 59: Add Converter Classes for Data Binding
- **Action:** Create `ViewModels/Converters/` folder
  - `BooleanToVisibilityConverter.cs`
  - `InverseBooleanConverter.cs`
  - `ProgressPercentageConverter.cs`
- **Depends on:** Step 47

### Step 60: Create ViewModel Locator (or inject via DI)
- **Action:** Create `ViewModels/ViewModelLocator.cs` or use DI container
- **Content:** Provide instances to Views (dependency injection)
- **Depends on:** Steps 49-55, 31

### Step 61: Update ServiceBootstrapper to Register ViewModels
- **Action:** Modify Step 31's ServiceBootstrapper to add ViewModel registrations
- **Depends on:** Steps 49-55, 31

### Step 62: Create ViewModel Tests (Skeleton)
- **Action:** Create `src/VSIXProject1.Tests/ViewModels/` + test classes
- **Depends on:** Step 40

### Step 63: Test MainViewModel Initialization
- **Action:** Write test: MainViewModel loads services, initializes properties
- **Depends on:** Steps 49, 62

### Step 64: Test ChatPageViewModel SendMessage Flow
- **Action:** Write test: SendMessage dispatches to ILlmService, updates UI
- **Depends on:** Steps 50, 62

### Step 65: Test ConfigPageViewModel Save
- **Action:** Write test: SaveConfig calls IConfigService.SaveConfigAsync
- **Depends on:** Steps 51, 62

### Step 66: Build & Validate Phase 3 (Part A)
- **Action:** Compile solution; fix any XAML/binding errors
- **Command:** `dotnet build`
- **Depends on:** Steps 49-61

### Step 67: Add async/await support to ViewModels
- **Action:** Ensure all async operations use proper await; add CancellationToken support
- **Depends on:** Steps 49-55

### Step 68: Add Error Handling to ViewModels
- **Action:** Wrap async calls in try/catch; call INotificationService.ShowNotificationAsync on error
- **Depends on:** Steps 49-55, 26

### Step 69: Wire Up IObservable Properties
- **Action:** Update ViewModels to subscribe to service IObservable properties (ConfigChanged, ProgressUpdates)
- **Depends on:** Steps 49-55, 17, 23

### Step 70: Build & Validate Phase 3 (Part B)
- **Action:** Compile + run ViewModel tests; verify all compile and logic works
- **Command:** `dotnet build && dotnet test`
- **Depends on:** Steps 63-69

---

## Phase 4: View Layer (Steps 71-95)

*Create WPF XAML Views with data bindings to ViewModels.*

### Step 71: Create Views Folder
- **Action:** Create folder `src/VSIXProject1/UI/Views/`
- **Depends on:** None

### Step 72: Create Pages Folder
- **Action:** Create folder `src/VSIXProject1/UI/Pages/`
- **Depends on:** None

### Step 73: Refactor ContinueToolWindowControl.xaml
- **Action:** Update existing XAML to host Frame/Router for page navigation
- **Content:**
  - Remove webview loading (or defer it)
  - Add Frame control for WPF page navigation
  - Set DataContext to MainViewModel
- **Depends on:** Step 49
- **Existing reference:** Refactor existing `UI/ContinueToolWindowControl.xaml`

### Step 74: Create MainWindow.xaml (or use existing ToolWindow pane)
- **Action:** Update/create `UI/MainWindow.xaml`
- **Content:** From DESIGN.md section 4.2
  - MenuBar (New Chat, Settings, History)
  - Frame for routing
  - IndexingProgressBar at bottom
  - DataContext binding to MainViewModel
- **Depends on:** Steps 49, 52, 73

### Step 75: Create ChatPage.xaml & Code-Behind
- **Action:** Create `UI/Pages/ChatPage.xaml` + `ChatPage.xaml.cs`
- **Content:** From DESIGN.md section 4.3
  - ContextPanel (collapsed)
  - MessagesList (ItemsControl)
  - InputBox (TextBox + SendButton)
  - DataContext to ChatPageViewModel
- **Depends on:** Steps 50, 59

### Step 76: Create ChatMessageControl.xaml
- **Action:** Create `UI/Views/ChatMessageControl.xaml`
- **Content:** Data template for individual chat message (user vs. assistant)
- **Depends on:** Step 75

### Step 77: Create ContextItemControl.xaml
- **Action:** Create `UI/Views/ContextItemControl.xaml`
- **Content:** Data template for context items in panel
- **Depends on:** Step 75

### Step 78: Create ConfigPage.xaml & Code-Behind
- **Action:** Create `UI/Pages/ConfigPage.xaml` + `ConfigPage.xaml.cs`
- **Content:** From DESIGN.md section 4 (paraphrased)
  - ModelsTab (model list, add/remove)
  - ToolsTab (tool checkboxes)
  - ProfilesTab (profile selector)
  - DataContext to ConfigPageViewModel
- **Depends on:** Step 51, 59

### Step 79: Create HistoryPage.xaml & Code-Behind
- **Action:** Create `UI/Pages/HistoryPage.xaml` + `HistoryPage.xaml.cs`
- **Content:**
  - SessionList (ItemsControl of sessions)
  - Load, Delete commands
  - DataContext to HistoryPageViewModel
- **Depends on:** Step 53

### Step 80: Create StatsPage.xaml & Code-Behind
- **Action:** Create `UI/Pages/StatsPage.xaml` + `StatsPage.xaml.cs`
- **Content:**
  - Token counter display
  - Usage chart
  - DataContext to StatsPageViewModel
- **Depends on:** Step 54

### Step 81: Create EditModePage.xaml & Code-Behind
- **Action:** Create `UI/Pages/EditModePage.xaml` + `EditModePage.xaml.cs`
- **Content:**
  - DiffViewer (code diff display)
  - AcceptButton, RejectButton
  - DataContext to EditModeViewModel
- **Depends on:** Step 55

### Step 82: Create IndexingProgressControl.xaml
- **Action:** Create `UI/Views/IndexingProgressControl.xaml`
- **Content:** ProgressBar, status text, pause/resume/cancel buttons
- **Depends on:** Step 52

### Step 83: Create Global Styles (Converters, Brushes)
- **Action:** Create `UI/Styles/Converters.xaml` + `UI/Styles/Brushes.xaml`
- **Content:**
  - Register converters from Step 59
  - Define theme colors (WPF equivalents of VSCode theme)
- **Depends on:** Step 59

### Step 84: Create Global Resource Dictionary
- **Action:** Create `UI/App.xaml`
- **Content:** Merge style/converter dictionaries
- **Depends on:** Steps 83

### Step 85: Update App.xaml.cs
- **Action:** Modify application startup (App.xaml.cs)
- **Content:**
  - Call ServiceBootstrapper.ConfigureServices()
  - Create MainWindow with MainViewModel
  - Call ServiceInitializer
- **Depends on:** Steps 31, 37, 49

### Step 86: Create Page Navigation Handler
- **Action:** Create `UI/Navigation/PageNavigator.cs`
- **Content:** Handle route changes in MainViewModel, navigate Frame to correct page
- **Depends on:** Step 74

### Step 87: Wire Up Navigation Commands in MainViewModel
- **Action:** Update MainViewModel.NavigateCommand to use PageNavigator
- **Depends on:** Steps 49, 86

### Step 88: Add Tooltip Portal & Modal Dialog Support to MainWindow
- **Action:** Update MainWindow.xaml to add:
  - Tooltip adorner layer
  - Dialog overlay for modals
- **Depends on:** Step 74

### Step 89: Create TextDialog Control
- **Action:** Create `UI/Views/TextDialog.xaml`
- **Content:** Modal dialog for user yes/no/text input
- **Depends on:** Step 88

### Step 90: Wire INotificationService to TextDialog
- **Action:** Update WpfNotificationService to show TextDialog
- **Depends on:** Steps 26, 89

### Step 91: Add Theme Support to XAML (VSCode Colors)
- **Action:** Map VSCode theme variables to WPF brushes (dynamic resources)
- **Content:** Create theme resource dictionary
- **Depends on:** Step 83

### Step 92: Build & Validate Phase 4 (Part A - XAML)
- **Action:** Compile solution; verify all XAML parses without errors
- **Command:** `dotnet build`
- **Depends on:** Steps 73-91

### Step 93: Add Data Binding Tests
- **Action:** Create visual tests for each page/control
- **Depends on:** Step 40

### Step 94: Test ChatPage Binding
- **Action:** Write test: ChatPageViewModel binds to XAML, UI updates on property change
- **Depends on:** Steps 75, 93

### Step 95: Build & Validate Phase 4 (Part B - Runtime)
- **Action:** Compile + launch UI; verify pages render and bindings work
- **Command:** `dotnet build && [launch Visual Studio in debug]`
- **Depends on:** Steps 73-94

---

## Phase 5: Integration & Cutover (Steps 96-115)

*Wire up message dispatch, test end-to-end, replace webview with WPF.*

### Step 96: Update MessageDispatcher to Use Services
- **Action:** Modify `MessageDispatcher.cs` to resolve services from DI, delegate to service methods
- **Depends on:** Steps 17-26, 31

### Step 97: Remove WebView2 Dependency (or Defer)
- **Action:** Comment out webview startup code in plugin initialization
- **Rationale:** WPF UI now primary; webview optional fallback
- **Depends on:** Step 85

### Step 98: Update ContinueVSPackage Plugin Initialization
- **Action:** Modify `ContinueVSPackage.cs` to:
  - Initialize ServiceBootstrapper
  - Initialize WPF views
  - Defer webview (or remove)
- **Depends on:** Steps 31, 85, 96

### Step 99: Create Integration Tests for Handler → Service Flow
- **Action:** Create `src/VSIXProject1.Tests/Integration/` with end-to-end tests
  - MessageDispatcher receives config/addModel → delegates to IConfigService.AddModelAsync
  - Chat message → delegates to ILlmService.StreamAsync
- **Depends on:** Steps 96

### Step 100: Test ConfigService ↔ MessageDispatcher
- **Action:** Write integration test: handler call → service method → event fired → MessageDispatcher responds
- **Depends on:** Steps 17, 99

### Step 101: Test LlmService ↔ MessageDispatcher
- **Action:** Write integration test: handler call → service streaming → chunks returned
- **Depends on:** Steps 22, 99

### Step 102: Test ViewModel ↔ Service Flow
- **Action:** Write integration test: ChatPageViewModel.SendMessage → ILlmService.StreamAsync → UI updated
- **Depends on:** Steps 50, 99

### Step 103: Load Plugin & Test End-to-End
- **Action:** Build VSIX, install in Visual Studio, test:
  - Open Continue panel (WPF)
  - Send message → LLM streams response
  - Navigate config → displays models
- **Depends on:** Steps 95, 98

### Step 104: Test File Operations (IToolService ↔ IIdeService)
- **Action:** Test: IToolService.ReadFileAsync calls IIdeService.ReadFileAsync → file contents returned
- **Depends on:** Steps 18, 20

### Step 105: Test Context Retrieval (Stub)
- **Action:** Test: ChatPageViewModel calls IContextService → stub returns empty context
- **Depends on:** Step 24

### Step 106: Verify Build & All Tests Pass
- **Action:** Full build + test suite
- **Command:** `dotnet build && dotnet test`
- **Depends on:** Steps 99-105

### Step 107: Update VSIX Manifest
- **Action:** Modify `source.extension.vsixmanifest` to reflect WPF-based UI (if changed)
- **Depends on:** None (configuration)

### Step 108: Document Service Layer Architecture
- **Action:** Update docs/ with service interface reference
- **Depends on:** Step 106

### Step 109: Create Developer Guide for Adding New Features
- **Action:** Document: "To add new service → implement interface → inject into ViewModel → wire UI"
- **Depends on:** Step 108

### Step 110: Remove Unused Webview Assets (Optional)
- **Action:** If webview fully replaced, archive `gui/` folder
- **Note:** Keep for now if fallback still needed
- **Depends on:** Step 97

### Step 111: Create Changelog Entry
- **Action:** Document refactor: "Backend now uses service layer; UI now WPF instead of webview"
- **Depends on:** None

### Step 112: Performance Baseline Testing
- **Action:** Measure chat latency, indexing speed, config load time (before → after)
- **Depends on:** Step 103

### Step 113: Stress Test: Rapid Model Switching
- **Action:** Test 50 model add/remove cycles → verify no memory leaks
- **Depends on:** Step 103

### Step 114: Stress Test: Long Message Streaming
- **Action:** Test LLM streaming with 5000+ token response → verify UI responsive
- **Depends on:** Step 103

### Step 115: Final Build & Sign VSIX
- **Action:** Build VSIX with release config; sign if required
- **Command:** `dotnet publish -c Release`
- **Depends on:** Steps 106-114

---

## Summary Table: Dependencies at a Glance

| Phase | Steps | Key Output | Depends On |
|-------|-------|-----------|-----------|
| **1: Core Types** | 1-15 | Types, interfaces, contracts | None |
| **2: Services** | 16-45 | Service implementations, DI setup | Phase 1 |
| **3: ViewModels** | 46-70 | MVVM Light ViewModels | Phases 1-2 |
| **4: Views** | 71-95 | WPF XAML pages, controls | Phases 1-3 |
| **5: Integration** | 96-115 | End-to-end wiring, testing, VSIX | Phases 1-4 |

---

## Critical Milestones

- ✅ **Step 15** — All types compile
- ✅ **Step 39** — Services compile
- ✅ **Step 45** — Service tests pass
- ✅ **Step 70** — ViewModels compile & tested
- ✅ **Step 92** — XAML compiles
- ✅ **Step 106** — Full build passes, all tests pass
- ✅ **Step 115** — VSIX ready

---

**End of Implementation Plan**
