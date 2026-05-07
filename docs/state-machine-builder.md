# StateMachineBuilder - Fluent API Reference

`StateMachineBuilder` provides a chainable, readable API for constructing `StateMachine` instances.
It produces the same object graph as manual construction but eliminates boilerplate.

## Quick Start

```csharp
using TAuto.Automation.StateMachine;
using TAuto.Automation.Actions;

var sm = new StateMachineBuilder()
    .StartAt(nameof(States.CheckCity))

    .State(nameof(States.CheckCity))
        .Log("Checking city view...")
        .ClickImage("templates/city_button.png", delayAfterMs: 1000, timeoutMs: 5000)
        .TransitionTo(nameof(States.OpenMenu),
            When.ImageFound("templates/menu_icon.png"), priority: 10)
        .Fallback(nameof(States.RetryCheck))

    .State(nameof(States.OpenMenu))
        .Tap(500, 300)
        .Delay(1500)
        .TransitionTo("END")

    .Build();

sm.OnStateChanged += (_, stateName) => context.SetVariable("_currentState", stateName);
await sm.RunAsync(context, cancellationToken);
```

---

## Machine-Level Configuration

| Method | Description |
|---|---|
| `.StartAt(stateName)` | Set the initial state. If omitted, defaults to the first defined state. |
| `.MaxTransitions(n)` | Infinite-loop guard. Aborts after `n` transitions. |

### Event: `OnStateChanged`
Fires whenever the machine enters a new state. Use this to maintain external state (like a UI status or a `ScriptContext` variable) for gating logic.

---

## Defining States

### `.State(name)`

Begins a new state definition. Automatically finalizes the previous state.
Everything chained after `.State(...)` belongs to that state until the next `.State(...)` or `.Build()`.

### State Options

| Method | Description |
|---|---|
| `.MaxDuration(ms)` | Maximum time in this state before failing. |
| `.PollingIntervals(fastMs, slowMs, slowdownThreshold)` | Adaptive polling. Starts fast, slows down after `slowdownThreshold` checks. Defaults: `50ms / 500ms / 3`. |

---

## Entry Actions

These execute sequentially when the state is entered:

| Method | Description |
|---|---|
| `.Log(message)` | Log a message. |
| `.Delay(ms)` | Wait for a duration. |
| `.Tap(x, y)` | Tap screen coordinates. |
| `.PressKey(key)` | Send a key press. |
| `.ClickImage(templatePath, delayAfterMs?, timeoutMs?)` | Find and click a template image. |
| `.ExtractText(x, y, w, h, outputVar, ...)` | OCR a screen region into a context variable. |
| `.Action(IAction)` | Add any custom `IAction`. Use this for actions needing extra properties (e.g. `ContinueOnError`). |
| `.Action(Func<ScriptContext, CancellationToken, Task<ActionResult>>)` | Inline async delegate. |
| `.Action(Action<ScriptContext>)` | Inline synchronous delegate. |

### Using `.Action()` for custom properties

The shorthand methods (`.ClickImage()`, etc.) don't expose all properties.
Use `.Action(new SomeAction { ... })` when you need full control:

```csharp
.Action(new ClickImageAction
{
    TemplatePath = "templates/button.png",
    Threshold = 0.85,
    ContinueOnError = true  // not available via .ClickImage()
})
```

---

## Exit Actions

Execute when leaving a state (regardless of which transition fires):

| Method | Description |
|---|---|
| `.OnExit(IAction)` | Add any exit action. |
| `.OnExitLog(message)` | Log a message on exit. |

---

## Transitions

### `.TransitionTo(targetState, condition?, priority?, timeoutMs?, maxRetries?, isFallback?)`

Add a transition from the current state. If `condition` is `null`, the transition is immediate (unconditional).

```csharp
// Conditional: transition when image is found
.TransitionTo("NextState", When.ImageFound("templates/icon.png"), priority: 10)

// Unconditional: transition immediately after entry actions
.TransitionTo("NextState")
```

### `.TransitionTo(targetState, condition, priority, params IAction[] onTransitionActions)`

Transition with actions that execute during the transition:

```csharp
.TransitionTo("NextState", When.ImageFound("templates/done.png"), 10,
    new LogAction { Message = "Transitioning!" })
```

### `.Fallback(targetState, timeoutMs?)`

Shorthand for a fallback transition (lowest priority, no condition).
Use `END` as the terminal target when you want the machine to stop:

```csharp
.Fallback("RetryState")       // fallback to another state
.Fallback("END")              // end the state machine
```

---

## Global Transitions (Interrupts)

```csharp
.GlobalTransition("HandlePopup",
    When.ImageFound("templates/popup.png"),
    priority: 100)
```

### The "Protected State" Pattern (Interrupt Gating)
Global transitions run **before** state logic. Sometimes you want to disable an interrupt during a critical flow (e.g., while the bot is clicking a specific menu sequence).

1.  **Track the current state** using `OnStateChanged`.
2.  **Gate the interrupt** using `When.Condition`.

```csharp
var protectedStates = new[] { nameof(S.Marching), nameof(S.Confirming) };

builder.GlobalTransition(nameof(S.ClosePopup), When.Condition(async (ctx, ct) => {
    var current = ctx.GetString("_currentState");
    if (protectedStates.Contains(current)) return ActionResult.Fail();
    return await IsPopupVisibleAsync(ctx, ct) ? ActionResult.Ok() : ActionResult.Fail();
}), priority: 200);
```

---

## `When` – Condition Factory

The `When` class provides fluent shortcuts for transition conditions:

| Method | Description |
|---|---|
| `When.Always` | Always true (`null` – unconditional). |
| `When.ImageFound(templatePath, threshold?)` | True if template is found on screen. |
| `When.TextFound(text, caseSensitive?, partialMatch?)` | True if OCR finds text on screen. |
| `When.Variable(name, expectedValue, op?)` | True if a context variable matches the expected string value. Operators: `==`, `!=`, `>`, `<`, `>=`, `<=`. |
| `When.IsTrue(variableName)` | Shorthand for `Variable(name, "True")`. |
| `When.IsFalse(variableName)` | Shorthand for `Variable(name, "False")`. |
| `When.Condition(Func<ScriptContext, CancellationToken, Task<ActionResult>>)` | Custom async condition. |
| `When.Condition(Func<ScriptContext, bool>)` | Custom sync condition. |
| `When.ClickImage(templatePath, ...)` | Click + condition combo (succeeds if image found and clicked). |

---

## Loops with `foreach`

C# doesn't allow `foreach` inline in a method chain.
Break the chain, loop, then resume:

```csharp
var builder = new StateMachineBuilder()
    .StartAt("Process")
    .State("Process")
        .Log("Processing items...");

foreach (var item in items)
{
    builder.Action(new SomeAction { Data = item });
}

var sm = builder
        .Fallback("Done")
    .State("Done")
        .Log("All done.")
        .Fallback(null)
    .Build();
```

> **Note:** The builder remembers the current state across the chain break.
> All `.Action()` calls in the loop still belong to the same state.

---

## Best Practices

### 1. State Segregation
Keep business logic (data processing) out of the state machine's navigational structure. The state machine should only be concerned with "What screen am I on?".

### 2. Priority Gating
Use `priority` to ensure critical events are handled first.
- **Global Interrupts**: `100+`
- **Normal Transitions**: `1-50`
- **Fallback**: Implicitly `0` (lowest, checked last).

### 3. CancellationToken
Always ensure your custom `.Action()` delegates or `.When.Condition()` lambdas check `cancellationToken.IsCancellationRequested` if they perform long-running operations.

---

## Complete Example

```csharp
var sm = new StateMachineBuilder()
    .StartAt(nameof(States.CollectResources))
    .MaxTransitions(50)

    // Interrupt: help icon appears anywhere
    .GlobalTransition(nameof(States.CollectHelp),
        When.ImageFound("templates/help_icon.png", 0.85),
        priority: 100)

    .State(nameof(States.CollectResources))
        .Log("Collecting from resource buildings...")
        .ClickImage("templates/farm.png", delayAfterMs: 500, timeoutMs: 1500)
        .Delay(1000)
        .Fallback(nameof(States.CollectHelp))

    .State(nameof(States.CollectHelp))
        .Log("Checking for alliance help...")
        .Action(new ClickImageAction
        {
            TemplatePath = "templates/help_icon.png",
            Threshold = 0.85,
            TimeoutMs = 2000,
            ContinueOnError = true
        })
        .OnExitLog("Cycle complete.")
        .Fallback("END")

    .Build();
```
