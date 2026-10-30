# PLang App Execution Flow

How C# boots the PLang runtime, runs `run.pr`, manages contexts, and dispatches user code.

---

## 1. The Bootstrap: C# → run.pr

PLang has **one C# loop**. Everything else is PLang code.

### App.Start()

```
App.Start()
  ├─ CurrentActor = System          // switch to system actor
  ├─ GoalCall { PrPath = "system/.build/run.pr" }
  ├─ goal = GoalCall.GetGoalAsync() // load + deserialize the .pr file
  └─ RunSteps(goal.Steps, system.Context)   // THE loop
```

`RunSteps` is the only step-iteration loop in C#:

```csharp
for each step:
    for each action in step:
        result = app.Run(action, context)   // dispatch one action
```

`app.Run()` finds the handler via `Modules.GetCodeGenerated(module, action)`, calls `ExecuteAsync`, and maps return variables onto `context.Variables`.

**After Start(), C# never iterates steps again.** The PLang code in `run.pr` takes over — it reads goals, iterates steps, fires events, checks errors.

---

## 2. What run.pr Does

The `.pr` file is compiled PLang. Here's the logical flow:

```
Run (entry point)
  ├─ if %!build% → call Build, return
  ├─ if %!test%  → call Test, return
  ├─ if %!debug% → call SetupDebug (registers debug events)
  └─ call RunGoal

RunGoal
  ├─ if %goal% is null → read file %goalFile%, write to %goal%
  └─ foreach %goal.Steps% → call RunStep, item=%step%

RunStep
  ├─ foreach before events                    ← PLang loop over registered events
  ├─ execute %step% as "user" → %data%        ← CONTEXT SWITCH happens here
  └─ foreach after events                     ← PLang loop over registered events
```

### Key insight: RunStep is PLang, not C#

Step-level events run in PLang. The C# `app.execute` action handles step dispatch — including the per-action modifier fold that implements caching, timeouts, and error handling:
- **Events run in PLang** — they're `foreach` loops calling `goal.call`
- **Per-action modifiers run in C#** — `cache.wrap`, `timeout.after`, and `error.handle` are `[Modifier]`-attributed actions on `Action.Modifiers`, folded right-to-left around the action's dispatch (see [architecture.md](architecture.md#action-modifiers))

---

## 3. Actors and Contexts

### Three actors, three contexts

```
App
  ├─ System actor  (escalation=2)  → System.Context  → System.Variables
  ├─ Service actor (escalation=1)  → Service.Context  → Service.Variables
  └─ User actor    (escalation=1)  → User.Context     → User.Variables
```

Each `Actor` owns:
- A `PLangContext` — execution state (current goal, step, error, events, callstack)
- A `Variables` — variable storage (`%varName%` resolution)
- Its events (`actor.event`) — every event it bound, loaded on the first ask (`events.md`)

Actors are created lazily and live for the app's lifetime.

### What lives on each Variables

Both system and user Variabless get context variables registered automatically:

```
%!app%       → App instance
%!context%      → the PLangContext itself
%!memoryStack%  → the Variables itself
%!goal%         → DynamicData → context.Goal (changes as goals execute)
%!step%         → DynamicData → context.Step
%!error%        → DynamicData → context.CurrentError
%!event%        → DynamicData → context.Event
%!test%         → DynamicData → context.Test
%Now%, %GUID%   → DynamicData (evaluated fresh each access)
```

**System Variables** also gets: `%goal%`, `%goalFile%`, `%step%`, `%data%`, `%event%` — the working variables used by `run.pr`.

**User Variables** gets: whatever the user's `.goal` code sets via `variable.set`, `file.read`, etc.

---

## 4. The Context Switch: System → User

This is the critical part. Here's what happens at `execute %step% as "user"`:

### Before the switch (System context)

```
run.pr's RunStep is executing on System.Context
  System.Variables has: %step%, %data%, %goal%, %event%, etc.
  app.CurrentActor = System
```

### app.execute (C#) does the switch

```csharp
// app/execute.cs — Run()
var targetActor = Actor;                    // resolved from "user" string
var execContext = targetActor.Context;       // User.Context
var previousActor = app.CurrentActor;
app.CurrentActor = targetActor;          // switch

try {
    return await ExecuteActions(app, execContext);  // runs on User.Context
} finally {
    app.CurrentActor = previousActor;    // restore to System
}
```

### During the switch (User context)

```
ExecuteActions iterates step.Actions
  Each action runs via app.Run(action, User.Context)
  Handler resolves %variables% from User.Variables
  Result stored as %!data% on User.Variables
```

### After the switch (back to System context)

```
app.execute returns Data result to run.pr
  run.pr stores it as %data% on System.Variables
  run.pr continues: after events, etc.
```

### Diagram

```
        System Context                    User Context
        ──────────────                    ────────────
 run.pr │                                │
 RunStep│ %step%                         │ %name% = "Alice"
        │ %data%                         │ %result% = 42
        │ %goal%                         │
        │                                │
        │ execute %step% as "user" ──────┤→ handler runs here
        │     ↑                          │   reads %name% from User.Variables
        │     │ Data result              │   writes %result% to User.Variables
        │←────┘                          │
        │                                │
        │ %data% = result                │
        │ foreach after events           │
```

---

## 5. Events

An event lives on the actor that bound it, loads on the first ask, and runs around an item's moment (a goal's start, a step's, an action's). How events are bound, loaded and run is in `events.md`.


---

## 6. Error Flow

### error.handle (per-action modifier)

Error handling is **per action, not per step**. When an action's step text carries `on error ...`, the builder attaches an `error.handle` modifier to that action's `Modifiers` collection. At runtime the modifier fold wraps the action's dispatch: if the inner `next()` returns a failed `Data`, `error.handle.Wrap` matches filters (StatusCode/Key/Message), optionally retries, optionally calls an error goal, and optionally ignores.

```csharp
// error/handle.cs (simplified)
return async () =>
{
    var result = await next();
    if (result.Success) return result;
    if (!MatchesError(result.Error)) return result;

    // retry and/or call error goal, per Order
    // Ignore is the final fallback
    ...
};
```

### Error propagation

```
action.RunAsync returns Data { Success=false, Error=... }
  → error.handle modifier (if present on that action) processes it
    → filter match? retry? error goal? ignore?
    → if all exhausted, returns the failure unchanged
  → if no modifier caught it, Step.RunAsync returns the failure
  → Goal.RunAsync sees the failure, passes it up to the caller
  → if the caller has no on error, Data propagates through foreach → RunGoal → caller
```

### Step-level catch — the last line of defence

`Step.RunAsync` wraps its action loop in a `try/catch` that converts any unexpected exception (anything that isn't `OperationCanceledException`, `OutOfMemoryException`, or `StackOverflowException`) into a `StepError`. This is belt-and-braces: handlers should always convert exceptions into `Data.FromError`, but if one slips through, the step still returns a well-formed failed `Data` instead of bubbling a raw exception.

---

## 7. Variable Resolution Path

When a handler property is `%user.name%`:

1. Source generator resolves `%user.name%` at property access time
2. Calls `Variables.Resolve<string>("user.name")`
3. Variables splits on `.` → root="user", path="name"
4. Looks up `%user%` in `_variables` → gets a `Data` object
5. Data.Navigate("name") → checks `!` prefix, then:
   - Value properties (if Value is an object with a Name property)
   - Properties collection
   - Navigator registry (List, Dictionary, CLR reflection)
   - Whitelisted Data base props (Name, Success, Error) — last resort

### ! vs . navigation

- `%step.text%` → navigates the step's **value/domain** properties → `step.Text`
- `%step!error%` → navigates the step's **Data infrastructure** → `step.Error`
- `%!app%` → the `!` prefix on the root name means "context variable" → `Variables["!app"]`

---

## 8. Test Execution Flow

```
plang --test
  ├─ Executor sets %!test% on System.Variables
  ├─ App.Start() → runs run.pr
  ├─ run.pr: if %!test% → call Test
  │
  Test (system/test.goal)
  │  ├─ find *.test.goal files
  │  └─ foreach → call RunTest, item=%testFile%
  │
  RunTest
  │  ├─ write out 'Running %testFile%'
  │  └─ runtime.run %testFile.GoalCall%, context=child
  │
  runtime.run (C#)
     ├─ Save user events + Variables snapshot
     ├─ Load goal from file
     ├─ app.RunGoalAsync(goal, User.Context)  ← runs test goal on User context
     ├─ Collect assertion results → %!test.results%
     └─ Restore user events + Variables from snapshot
```

`context=child` provides test isolation: each test gets a clean slate of events and variables, restored after the test completes.

---

## 9. Current Problems

### Problem 1: Action-level events don't fire

Action events (BeforeAction/AfterAction) are registered but never executed. The action dispatch loop is in C# (`app.execute.ExecuteActions`), not in PLang. Step/goal events fire from `run.pr`, but there's no equivalent PLang loop for actions.

**Options:**
- Fire action events from C# in `ExecuteActions` (breaks "everything in PLang" principle)
- Restructure so actions are dispatched one-at-a-time from PLang (would require run.pr to loop over step.Actions and call app.run per-action)

### Problem 2: Type conversion in comparisons

When a test asserts `%stepCount% > 0`, the assert module hits "Object must be of type String" — a numeric comparison bug. Variables stored via `variable.set` may be boxed as different numeric types (int vs long vs string) depending on how they were set and how JSON deserializes them.

### Problem 3: Sub-goal PrPath resolution across directories

Test goals with sub-goals (e.g., TrackStep with PrPath `/.build/trackstep.pr`) work when running from the test folder but fail when running from project root. The `.pr` PrPath is relative to the goal file's location, but `GetGoalAsync` resolves relative to app root. This affects any test that references sub-goals via PrPath.

### Problem 4: Event handler runs on correct context but can't find goal

When RunEvent calls `goal %event% as "user"`, the goal resolution uses User.Context. But sub-goal PrPaths in .pr files are relative to the parent goal's folder, and User.Context may not have the parent goal set correctly for resolution.
