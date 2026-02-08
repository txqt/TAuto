# TAuto

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Core](https://img.shields.io/badge/.NET-8.0-blue.svg)]()

**TAuto** is a standalone, lightweight, and high-performance automation library for .NET. It provides a robust engine for executing complex logic, state machines, and event-driven automation scripts without the need for a graphical interface.

## 🚀 Purpose
TAuto is designed for developers who need a powerful automation core to integrate into their own applications, whether they are console tools, background services, or custom UI frameworks. It abstracts away the complexity of state management and platform interaction.

### 1. Hybrid State Machine
A professional-grade state machine engine that balances performance and responsiveness.
- **Hybrid Polling**: Dynamically switches between fast (50ms) and slow (500ms) polling based on activity.
- **Event-Driven Transitions**: Respond to signals in <1ms without waiting for the next poll cycle.
- **Composite Logic**: Build complex transitions using **AND**, **OR**, and **NOT** operators.
- **Timing & Retries**: Per-transition timeouts and max retry limits for robust failure recovery.

### 2. High-Performance Runner
An asynchronous, non-blocking execution engine optimized for low-latency automation.
- **Thread Safety**: Built on `SemaphoreSlim` and `CancellationToken` for robust execution control.
- **Polymorphic Serialization**: Custom JSON converters for complex, nested action structures.

### 3. Comprehensive Feature Set
- **Rich Interaction**: Humanized Tap, Swipe, Type, and multi-point gestures.
- **Vision & OCR**: Native integration with OpenCV and Tesseract.
- **Execution Tracing**: Real-time performance metrics and visit counts for every state and transition.
- **Validation Suite**: Built-in tools to detect unreachable states, infinite loops, and missing targets before execution.

## 🚀 Quick Start

### 1. Basic Script Runner
Execute a linear sequence of actions.

```csharp
using TAuto.Automation;
using TAuto.Automation.Actions;
using TAuto.Core;

// 1. Setup controllers & services
IDeviceController device = new AdbDeviceController("emulator-5554");
IVisionService vision = new OpenCVVisionService();
IOcrService ocr = new TesseractOcrService();
ILoggerService logger = new ConsoleLogger();

// 2. Initialize Context
var context = new ScriptContext(device, vision, ocr);

// 3. Define Actions
var actions = new List<IAction>
{
    new TapAction { X = 100, Y = 200, DisplayName = "Open App" },
    new DelayAction { DurationMs = 2000 },
    new SwipeAction { X1 = 500, Y1 = 800, X2 = 500, Y2 = 200, DurationMs = 500 }
};

// 4. Run Script
var runner = new ScriptRunner(logger);
await runner.RunAsync(actions, context, CancellationToken.None);
```

### 2. State Machine
Manage complex automation with states and dynamic transitions.

```csharp
var sm = new StateMachineAction();

var searchState = new State 
{ 
    Name = "Search",
    FastCheckIntervalMs = 50,
    SlowCheckIntervalMs = 1000 
};

// Add an event-driven transition (responsive <1ms)
searchState.Transitions.Add(new EventTransition 
{ 
    ToState = "Combat", 
    EventName = "EnemySpotted",
    Priority = 10
});

// Add a vision-based polling transition
searchState.Transitions.Add(new StateTransition 
{ 
    ToState = "Victory", 
    Condition = new IfImageFoundAction { TemplatePath = "victory.png" }
});

sm.Machine.States.Add(searchState);

// Run with metrics tracking
var result = await sm.ExecuteAsync(context, CancellationToken.None);
Console.WriteLine($"State visited {sm.Machine.Metrics.GetMetrics("Search").VisitCount} times.");
```

## Architecture
TAuto is split into:
- **TAuto.Core**: Interfaces, Models, and Base types.
- **TAuto.Automation**: Implementation of the Engine, Actions, and Services.

## 📚 Documentation & Community
- **[Contributing](./CONTRIBUTING.md)**: How to help improve the engine.
- **[Contributors](./CONTRIBUTORS.md)**: People behind the project.
- **[Security](./SECURITY.md)**: Security reporting policy.

## ⚖️ License
MIT License. Copyright (c) 2026 txqt.

