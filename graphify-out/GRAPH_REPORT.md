# Graph Report - .  (2026-04-12)

## Corpus Check
- 125 files · ~50,000 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 649 nodes · 726 edges · 72 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 5,000 input · 800 output

## God Nodes (most connected - your core abstractions)
1. `ScriptContext` - 25 edges
2. `StateMachineBuilder` - 22 edges
3. `BotBase` - 21 edges
4. `SchedulerService` - 14 edges
5. `ScriptState` - 12 edges
6. `ProcessManagerService` - 12 edges
7. `DefaultVisionHelper` - 11 edges
8. `ImageWrapper` - 10 edges
9. `JobObject` - 10 edges
10. `ScreenCaptureManager` - 8 edges

## Surprising Connections (you probably didn't know these)
- `StateMachineBuilder (Fluent)` --builds_internal_logic_for--> `TAuto Core Engine`  [EXTRACTED]
  TAuto.Automation/StateMachine/StateMachineBuilder.cs → README.md
- `ScriptContext (Runtime)` --provides_services_to--> `TAuto Core Engine`  [EXTRACTED]
  TAuto.Core/ScriptContext.cs → README.md

## Communities

### Community 0 - "TAuto Core: 0"
Cohesion: 0.03
Nodes (21): ActionBase, ClickImageAction, ClickTextAction, DelayAction, DelegateAction, ExtractTextAction, FindColorAction, FindImageAction (+13 more)

### Community 1 - "TAuto Core: 1"
Cohesion: 0.08
Nodes (6): ComputeTokenService, DefaultProcessSpawner, IDisposable, IProcessSpawner, SchedulerService, ZombieWorkerReaper

### Community 2 - "TAuto Core: 2"
Cohesion: 0.1
Nodes (1): ScriptContext

### Community 3 - "TAuto Core: 3"
Cohesion: 0.09
Nodes (4): IImage, IImage, ImageWrapper, RawImage

### Community 4 - "TAuto Core: 4"
Cohesion: 0.09
Nodes (5): CompositeLogger, FileLoggerService, ILoggerService, ILoggerService, RelayLogger

### Community 5 - "TAuto Core: 5"
Cohesion: 0.15
Nodes (1): StateMachineBuilder

### Community 6 - "TAuto Core: 6"
Cohesion: 0.12
Nodes (1): BotBase

### Community 7 - "TAuto Core: 7"
Cohesion: 0.16
Nodes (2): DefaultVisionHelper, IVisionHelper

### Community 8 - "TAuto Core: 8"
Cohesion: 0.16
Nodes (6): BotBase, BotPermissions, BotProfile, ExampleFarmingBot, ProfileBot, StateMachine

### Community 9 - "TAuto Core: 9"
Cohesion: 0.17
Nodes (5): IAppLifecycleDevice, IDeviceController, IKeyboardInputDevice, IScreenCaptureDevice, ITouchInputDevice

### Community 10 - "TAuto Core: 10"
Cohesion: 0.18
Nodes (2): DefaultBotConfiguration, IBotConfiguration

### Community 11 - "TAuto Core: 11"
Cohesion: 0.14
Nodes (6): StateMachineMetrics, StateMachineTrace, StateMachineTraceEntry, StateMachineValidator, StateMetrics, ValidationResult

### Community 12 - "TAuto Core: 12"
Cohesion: 0.15
Nodes (1): ScriptState

### Community 13 - "TAuto Core: 13"
Cohesion: 0.2
Nodes (4): IColorDetector, ITemplateMatcher, ITemplateRepository, IVisionService

### Community 14 - "TAuto Core: 14"
Cohesion: 0.27
Nodes (3): ClipboardHelper, GetClipboardAction, Kernel32

### Community 15 - "TAuto Core: 15"
Cohesion: 0.17
Nodes (2): DefaultHeartbeatMonitor, IHeartbeatMonitor

### Community 16 - "TAuto Core: 16"
Cohesion: 0.29
Nodes (1): ProcessManagerService

### Community 17 - "TAuto Core: 17"
Cohesion: 0.21
Nodes (3): ILogStreamer, ILogStreamer, WorkerLogService

### Community 18 - "TAuto Core: 18"
Cohesion: 0.2
Nodes (2): DefaultBotPausable, IBotPausable

### Community 19 - "TAuto Core: 19"
Cohesion: 0.22
Nodes (4): ActionBase, IAction, IAction, StateMachineAction

### Community 20 - "TAuto Core: 20"
Cohesion: 0.2
Nodes (2): DefaultVariableStore, IVariableStore

### Community 21 - "TAuto Core: 21"
Cohesion: 0.27
Nodes (1): JobObject

### Community 22 - "TAuto Core: 22"
Cohesion: 0.22
Nodes (1): When

### Community 23 - "TAuto Core: 23"
Cohesion: 0.36
Nodes (1): GameHealthMonitor

### Community 24 - "TAuto Core: 24"
Cohesion: 0.25
Nodes (2): DefaultGameLifecycle, IGameLifecycle

### Community 25 - "TAuto Core: 25"
Cohesion: 0.29
Nodes (3): ActionDefinition, ActionMetadataService, ActionParameterDefinition

### Community 26 - "TAuto Core: 26"
Cohesion: 0.25
Nodes (2): DefaultCrashLoopProtector, ICrashLoopProtector

### Community 27 - "TAuto Core: 27"
Cohesion: 0.25
Nodes (2): DefaultNamedPipeRegistry, INamedPipeRegistry

### Community 28 - "TAuto Core: 28"
Cohesion: 0.46
Nodes (1): ScreenCaptureManager

### Community 29 - "TAuto Core: 29"
Cohesion: 0.29
Nodes (2): IOcrService, OcrResultBlock

### Community 30 - "TAuto Core: 30"
Cohesion: 0.33
Nodes (2): EventTransition, StateTransition

### Community 31 - "TAuto Core: 31"
Cohesion: 0.33
Nodes (2): DefaultActionExecutor, IActionExecutor

### Community 32 - "TAuto Core: 32"
Cohesion: 0.29
Nodes (1): EpisodicMemory

### Community 33 - "TAuto Core: 33"
Cohesion: 0.52
Nodes (1): PersonaManager

### Community 34 - "TAuto Core: 34"
Cohesion: 0.33
Nodes (1): ActionResult

### Community 35 - "TAuto Core: 35"
Cohesion: 0.47
Nodes (2): ActionJsonConverter, JsonConverter

### Community 36 - "TAuto Core: 36"
Cohesion: 0.33
Nodes (1): SessionManager

### Community 37 - "TAuto Core: 37"
Cohesion: 0.33
Nodes (2): DefaultTransitionEvaluator, ITransitionEvaluator

### Community 38 - "TAuto Core: 38"
Cohesion: 0.33
Nodes (2): DefaultExecutionLoopMonitor, IExecutionLoopMonitor

### Community 39 - "TAuto Core: 39"
Cohesion: 0.6
Nodes (1): BotPersona

### Community 40 - "TAuto Core: 40"
Cohesion: 0.4
Nodes (3): ColorMatchResult, ColorSearchOptions, IColorDetector

### Community 41 - "TAuto Core: 41"
Cohesion: 0.6
Nodes (1): IfTextFoundAction

### Community 42 - "TAuto Core: 42"
Cohesion: 0.5
Nodes (2): DefaultRetryPolicy, IRetryPolicy

### Community 43 - "TAuto Core: 43"
Cohesion: 0.4
Nodes (3): ActionMetadataAttribute, ActionParameterAttribute, Attribute

### Community 44 - "TAuto Core: 44"
Cohesion: 0.4
Nodes (1): StateMachineTraceRouter

### Community 45 - "TAuto Core: 45"
Cohesion: 0.4
Nodes (1): IDeviceProviderService

### Community 46 - "TAuto Core: 46"
Cohesion: 0.5
Nodes (1): WorkerIpcListener

### Community 47 - "TAuto Core: 47"
Cohesion: 0.5
Nodes (1): CoordinateScaler

### Community 48 - "TAuto Core: 48"
Cohesion: 0.5
Nodes (1): BotProfileSerializer

### Community 49 - "TAuto Core: 49"
Cohesion: 0.5
Nodes (1): DetectionConfirmation

### Community 50 - "TAuto Core: 50"
Cohesion: 0.67
Nodes (1): MemoryDiagnostics

### Community 51 - "TAuto Core: 51"
Cohesion: 0.5
Nodes (1): BotSession

### Community 52 - "TAuto Core: 52"
Cohesion: 0.5
Nodes (2): ScheduleCalculator, ScheduleDefinition

### Community 53 - "TAuto Core: 53"
Cohesion: 0.5
Nodes (4): Hybrid Polling Logic, ScriptContext (Runtime), StateMachineBuilder (Fluent), TAuto Core Engine

### Community 54 - "TAuto Core: 54"
Cohesion: 0.67
Nodes (1): ClickAction

### Community 55 - "TAuto Core: 55"
Cohesion: 0.67
Nodes (1): DeviceInfo

### Community 56 - "TAuto Core: 56"
Cohesion: 0.67
Nodes (2): EventArgs, VariableChangedEventArgs

### Community 57 - "TAuto Core: 57"
Cohesion: 1.0
Nodes (2): IWorkerProcess, WorkerProcess

### Community 58 - "TAuto Core: 58"
Cohesion: 1.0
Nodes (1): AutomationDefaults

### Community 59 - "TAuto Core: 59"
Cohesion: 1.0
Nodes (1): IDeviceProvider

### Community 60 - "TAuto Core: 60"
Cohesion: 1.0
Nodes (1): TemplateMatchResult

### Community 61 - "TAuto Core: 61"
Cohesion: 1.0
Nodes (1): BotArgument

### Community 62 - "TAuto Core: 62"
Cohesion: 1.0
Nodes (1): BotConfiguration

### Community 63 - "TAuto Core: 63"
Cohesion: 1.0
Nodes (1): State

### Community 64 - "TAuto Core: 64"
Cohesion: 1.0
Nodes (1): ErrorPolicy

### Community 65 - "TAuto Core: 65"
Cohesion: 1.0
Nodes (1): ScheduledJob

### Community 66 - "TAuto Core: 66"
Cohesion: 1.0
Nodes (1): SessionState

### Community 67 - "TAuto Core: 67"
Cohesion: 1.0
Nodes (0): 

### Community 68 - "TAuto Core: 68"
Cohesion: 1.0
Nodes (0): 

### Community 69 - "TAuto Core: 69"
Cohesion: 1.0
Nodes (0): 

### Community 70 - "TAuto Core: 70"
Cohesion: 1.0
Nodes (0): 

### Community 71 - "TAuto Core: 71"
Cohesion: 1.0
Nodes (1): BotBase (Framework)

## Knowledge Gaps
- **24 isolated node(s):** `AutomationDefaults`, `ColorSearchOptions`, `ColorMatchResult`, `IDeviceProvider`, `OcrResultBlock` (+19 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `TAuto Core: 58`** (2 nodes): `AutomationDefaults.cs`, `AutomationDefaults`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 59`** (2 nodes): `IDeviceProvider.cs`, `IDeviceProvider`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 60`** (2 nodes): `TemplateMatchResult.cs`, `TemplateMatchResult`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 61`** (2 nodes): `BotArgument.cs`, `BotArgument`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 62`** (2 nodes): `BotConfiguration.cs`, `BotConfiguration`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 63`** (2 nodes): `State.cs`, `State`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 64`** (2 nodes): `ErrorPolicy.cs`, `ErrorPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 65`** (2 nodes): `ScheduledJob.cs`, `ScheduledJob`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 66`** (2 nodes): `SessionState.cs`, `SessionState`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 67`** (1 nodes): `BotRunMode.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 68`** (1 nodes): `ConditionLogicMode.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 69`** (1 nodes): `TransitionType.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 70`** (1 nodes): `DeviceInputMode.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TAuto Core: 71`** (1 nodes): `BotBase (Framework)`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.