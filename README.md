# TAuto

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Core](https://img.shields.io/badge/.NET-8.0-blue.svg)]()

**TAuto** is a standalone, lightweight, and high-performance automation library for .NET. It provides a robust engine for executing complex logic, state machines, and event-driven automation scripts without the need for a graphical interface.

## 🚀 Purpose
TAuto is designed for developers who need a powerful automation core to integrate into their own applications, whether they are console tools, background services, or custom UI frameworks. It abstracts away the complexity of state management and platform interaction.

## Key Features

### 1. State Machine
A robust state machine implementation that allows for complex logic flows, transition management, and error handling.
- **States**: Define discrete states in your automation flow.
- **Transitions**: Control flow between states based on conditions (Image, Text, Variable).
- **Actions**: Execute actions on entry, exit, or periodically within a state.

### 2. Script Runner
An efficient script runner capable of executing linear and branching automation scripts. Support for loops, conditionals, and variables.

### 3. Logic Actions
A rich set of predefined actions:
- **Interaction**: Tap, Swipe, Click, Input.
- **Decision**: If Image/Text/Variable.
- **Control**: Loop, Delay, Stop, Goto.
- **Variables**: Set, Modify, Compare variables dynamically.

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

// Define states
var idleState = new State { Name = "Idle" };
var activeState = new State { Name = "Active" };

// Add transitions
idleState.Transitions.Add(new StateTransition 
{ 
    TargetState = "Active", 
    Type = TransitionType.Image,
    ConditionValue = "start_button.png" 
});

sm.States.Add(idleState);
sm.States.Add(activeState);

// Run within a script runner or standalone
await sm.ExecuteAsync(context, CancellationToken.None);
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

