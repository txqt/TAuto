# TAuto

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Core](https://img.shields.io/badge/.NET-8.0-blue.svg)]()

TAuto is the automation runtime used by AutoBot. It is a platform-agnostic .NET library for building bots from actions, shared execution context, and validated state machines.

## What TAuto Owns

- `TAuto.Core`
  - core contracts such as `IAction`, `IDeviceController`, `IVisionService`, and `IOcrService`
  - `ScriptContext` for device access, captures, variables, events, and session-scoped runtime state
- `TAuto.Automation`
  - concrete actions such as `TapAction`, `PressKeyAction`, `ClickImageAction`, and `ExtractTextAction`
  - the state machine runtime: `StateMachine`, `StateMachineAction`, `StateMachineBuilder`, and `When`
  - JSON serialization and editor-facing action metadata

TAuto does not embed platform-specific drivers. Host applications supply the concrete device, vision, and OCR implementations.

## Core Concepts

### `IAction`

Every action executes against a `ScriptContext` and can opt into breakpoints, retries, and continue-on-error behavior.

### `ScriptContext`

`ScriptContext` is the shared runtime facade. It exposes:

- `Device`, `Vision`, and `Ocr`
- capture APIs such as `UpdateScreenCaptureAsync()`
- global variables, scoped local variables, and events
- metadata such as `SessionId`, `TargetId`, `Persona`, `Session`, and `HealthMonitor`
- cached match results reused across actions in the same frame

### `StateMachine`

`StateMachine` is the primary execution model in this repo. It runs named states, entry actions, local transitions, and global transitions through `RunAsync(context, ct)`.

### `StateMachineBuilder`

`StateMachineBuilder` is the preferred authoring API. It provides fluent methods for:

- machine setup: `StartAt`, `MaxTransitions`
- state setup: `State`, `MaxDuration`, `PollingIntervals`
- entry actions: `Log`, `Delay`, `PressKey`, `Tap`, `TapScaled`, `ClickImage`, `ExtractText`, `Action`
- transitions: `TransitionTo`, `Fallback`, `GlobalTransition`

## Quick Start

```csharp
using TAuto.Automation.StateMachine;
using TAuto.Core;

IDeviceController device = new YourDeviceController();
IVisionService vision = new YourVisionService();
IOcrService ocr = new YourOcrService();

var context = new ScriptContext(device, vision, ocr);

var machine = new StateMachineBuilder()
    .StartAt("CheckCity")
    .State("CheckCity")
        .Log("Checking city view...")
        .ClickImage("templates/city_button.png", delayAfterMs: 500, timeoutMs: 1500)
        .TransitionTo("OpenMenu", When.ImageFound("templates/menu_icon.png"), priority: 10)
        .Fallback("END")
    .State("OpenMenu")
        .TapScaled(640, 360)
        .Delay(1000)
        .Fallback("END")
    .Build();

machine.OnStateChanged += (_, stateName) => context.SetVariable("_currentState", stateName);

var result = await machine.RunAsync(context, CancellationToken.None);
```

## Design Notes

- Global transitions are checked as interrupts across all states.
- `Build()` validates the graph and rejects broken state machines early.
- `END` is treated as a terminal transition target by the executor.
- `TapScaled` records coordinates against the default reference resolution and scales them to the active device at runtime.

## Documentation

- [docs/state-machine-builder.md](./docs/state-machine-builder.md)
- [docs/bot-development-guide.md](./docs/bot-development-guide.md)
- [TESTING.md](./TESTING.md)
- [CONTRIBUTING.md](./CONTRIBUTING.md)

## License

MIT License. Copyright (c) 2026 txqt.

