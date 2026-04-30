# PLang App Architecture

## Overview

PLang App is built on the **Object-Based Pattern (OBP)** — objects own their data and behavior, the object graph IS the architecture, and everything flows as `Data`.

The runtime has one root: `App`. Everything hangs off it. Navigate to what you need. No service layers, no dependency injection, no parameter threading.

```
App
  .System / .Service / .User    — Actors (execution identities)
  .Goals                        — loaded goals
  .Modules                      — action registry (dispatcher)
  .Channels                     — named I/O streams
  .Providers                    — pluggable implementations
  .FileSystem                   — sandboxed file access
  .Events                       — app-level lifecycle hooks
  .Config                       — goal-scoped settings
  .Cache                        — step result cache
  .Types                        — PLang names <-> CLR types
  .Navigators                   — per-type Data navigation
```

---

## Everything is Data

`Data` is the universal type. Every value in the runtime — variables, file contents, HTTP responses, errors — circulates as `Data`. Created at the boundary where the value originates, it flows through every layer unchanged.

```csharp
public class Data
{
    string Name            // variable name
    object? Value          // the actual value
    Type? Type             // PLang type descriptor ("string", "image/jpeg", etc.)
    Properties Properties  // child properties (extensible metadata)
    IError? Error          // if this Data represents a failure
    bool Success           // Error == null
}
```

**Factory methods**: `Data.Ok()`, `Data.Ok(value)`, `Data.FromError(error)`

**Key subtypes**:
- `Data<T>` — generic typed wrapper, `Value` returns `T`
- `DynamicData` — computed on each access via `Func<object?>` (lazy variables like `%!goal%`)
- `DataList<T>` — typed list that extends Data, carries error state

**Relay, don't repackage**: Intermediate layers pass Data as-is. Never extract `.Value` and rewrap it — that loses Type, Properties, and metadata. Use `Merge()` to combine results.

**Navigation**: Data supports dot-path traversal — `data.GetChild("user.address[0].city")` navigates nested structures uniformly across dictionaries, CLR objects, JSON, and lists.

---

## Actors

Three actors represent execution identities with isolated resources:

```
App._shutdownCts (infrastructure root)
  |
  System (linked to App shutdown)
    |--- User (linked to System)
    |--- Service (linked to System)
```

| Actor | Level | Purpose |
|-------|-------|---------|
| **System** | 2 | Root of cancellation hierarchy. Runs bootstrap (`system/.build/run.pr`). Persistent settings store. |
| **User** | 1 | End-user operations. Default actor. |
| **Service** | 1 | External service operations. |

Each actor owns:
- **Context** — request-level execution state
- **Channels** — isolated I/O streams
- **SettingsStore** — persistent key-value storage (SQLite, per-actor)
- **CancellationToken** — linked to parent for cascading cancellation
- **Identity** — cryptographic identity

### Cancellation hierarchy

Cancel **User** -> only user context stops, System and Service keep running.
Cancel **System** -> cascades to User and Service (everything stops).
`app.RequestShutdown()` -> cancels the root, cascades through everything.

Per-action timeouts layer on top via a push/pop stack on Context — the `timeout.after` modifier creates a linked CancellationTokenSource for the wrapped action's duration without affecting the actor token.

---

## Context

Request-level state for a single execution. Created per actor, carries everything an execution needs.

```
Context
  .Id                   unique execution identifier
  .App                  back-reference to App
  .Actor                owning actor
  .Variables            %variable% storage (thread-safe)
  .CallStack            optional frame tracking
  .Goal                 currently executing goal
  .Step                 currently executing step
  .Events               context-specific event bindings
  .CancellationToken    from actor (with timeout stack on top)
  .Parent               for nested execution (child contexts)
  .ConfigScope          goal-scoped settings
  .Setup                during setup execution (run-once semantics)
  .Test                 test context when --test flag active
  .Event                current event context (in event handlers)
  .Trace                per-execution diagnostic identity (see trace.md)
```

`Trace` — created in the Context constructor and carried for the lifetime of the execution. Sub-goal calls share the parent's Trace; forking a new Context creates a new one. Used by builder pipeline + LLM debug to correlate diagnostic files. See [trace.md](trace.md).

### Context variables (lazy)

All prefixed with `!`, resolved on access via `DynamicData`:

```
%!app%  %!context%  %!goal%  %!step%
%!variables%  %!callStack%  %!channels%  %!event%  %!test%
```

### Child contexts

`CreateChild()` — new context with cloned variables, parent reference maintained.
`Clone()` — sibling context, copies data dictionary.

---

## Execution Flow

### Bootstrap

```
app.Start()
  -> CurrentActor = System
  -> reads system/.build/run.pr via GoalCall
  -> RunSteps(goal.Steps, context)
```

After bootstrap, PLang code drives everything. The C# runtime is just the dispatcher.

### The execution loop

```
RunSteps(steps, context)
  for each step:
    for each action in step.Actions:
      Run(action, context)
        -> Modules.GetCodeGenerated(action)
        -> handler.ExecuteAsync(action, context)
        -> result stored as %__data__% on context.Variables
    
    sub-step control:
      condition.if sets step.Disabled on indented children
        -> disabled steps are skipped by the runner
```

### Dispatch kernel

```csharp
Run(action, context)
  var executor = Modules.GetCodeGenerated(action.Module, action.ActionName, context);
  var result = await executor.ExecuteAsync(action, this, context);
  
  // Result stored as %__data__% — available to the next action or variable.set
  result.Name = "__data__";
  context.Variables.Put(result);
  
  return result;
```

### Goal resolution (GoalCall)

1. If `PrPath` is set -> file.read directly
2. Walk up the step's goal chain (sub-goals)
3. Check `app.Goals` (in-memory cache)
4. Derive PrPath: `.build/{name}.pr`
5. Try relative to calling goal's folder
6. Try root-relative

---

## Goal / Step / Action Hierarchy

```
Goal
  .Name, .Description
  .Steps          Steps collection (owns the loop)
  .Goals          sub-goals (nested)
  .Path / .PrPath file locations
  .Visibility     Public or Private
  .Parent         back-reference

Step
  .Text           the PLang instruction
  .Actions        Actions collection
  .Index          position in goal
  .Indent         sub-step nesting level
  .Goal           back-reference

Action
  .Module         "file", "variable", "http", etc.
  .ActionName     "read", "set", "request", etc.
  .Parameters     List<Data> (named inputs)
  .Modifiers      Modifiers collection — per-action wrappers (cache/timeout/error)
  .Step           back-reference
```

Navigation reads naturally: `goal.Steps[0].Actions[0].Module` — "goal's first step's first action's module."

Collections own their loops (OBP rule 5). Steps iterates its own steps, Actions runs its own actions. Parents delegate, never iterate children directly.

---

## Modules (Action Registry)

Flat `module.action` registry. No hierarchy, no inheritance.

The catalog rendered into the LLM builder's system prompt — module/action names, parameter type tags, examples — is derived from these registered handlers via `App.Modules.Describe()` plus `App.Catalog.@this.Build()`. See [action-catalog.md](action-catalog.md) for the attribute model (`[Action]`, `[ModuleDescription]`, `[Example]`, `[VariableName]`, `[Default]`, etc.) and the rules for writing structured `ExamplesForLlm()` static methods.

### Discovery

Scans assemblies for types with `[Action]` attribute implementing `ICodeGenerated`. Extracts module from namespace: `App.modules.{module}.{actionName}`.

### Handler pattern

```
Record (parameters):  lowercase action name     -> set, save, read
Handler (execution):  PascalCase + Handler      -> SetHandler, SaveHandler
Namespace:            App.modules.{module}      -> modules.variable
Registry key:         {module}.{record}         -> variable.set
```

The source generator emits a partial-class extension on the action record itself — resolves `%var%` references in parameters lazily at property access time, wires capability interfaces (`IContext`, `IChannel`, `IStep`), eagerly resolves `[Provider]`s, and emits `__SnapshotParams` for error reporting. No separate `*__Generated` records — the action record IS the partial class.

### Source-generator OBP shape

`PLang.Generators/` mirrors the per-folder `@this` convention:

```
PLang.Generators/this.cs                — IIncrementalGenerator entry point
  ├ Discovery/this.cs                   — Roslyn predicate + GetActionClassInfo + property factory
  └ Emission/
      ├ Action/this.cs                  — per-handler emitter (partial-class shell, ExecuteAsync, __SnapshotParams)
      └ Property/
          ├ this.cs                     — abstract base (EmitProperty, EmitSnapshotEntry)
          ├ Data/this.cs                — Data<T> / plain Data emission
          ├ Provider/this.cs            — [Provider]-attributed emission
          └ Legacy/this.cs              — raw-scalar emission for handlers still on [VariableName]/partial-string
```

`Discovery.GetActionClassInfo` builds an `ActionClassInfo` record (with `EquatableArray<T>` collections for incremental-cache stability — see [`good_to_know.md`](good_to_know.md)). `Emission/Action` consumes that record and dispatches per-property to the right `Emission/Property/*` leaf via the polymorphic `ActionProperty` base.

### Property kinds (PLNG001 build-time gate)

Action property positions are constrained at build time. `Discovery.IsValidActionProperty` accepts only:

| Shape | Resolution | Used for |
|-------|------------|----------|
| `Data<T>` | `Action.GetParameter(name).As<T>(Context)` lazily on read | Standard handler param |
| `Data` (plain) | Same as `Data<object>` | Untyped passthrough |
| `[Provider] T` | Eager `app.Providers.Get<T>()` in `ExecuteAsync` | Pluggable infrastructure (HTTP, signing, LLM) |
| `[VariableName] string` | `__StripPercent(name)` — bare identifier | Handlers that need the variable's *name* not its value (variable.set, list.*) |

Any other shape (raw `partial string`, `partial int`, untagged primitives) reports the **PLNG001** error: *Property '{0}' on action '{1}' must be Data<T>, [Provider], or [VariableName] string. Raw scalars are not permitted.* The diagnostic carries the full identifier span so IDE squiggles underline the property name.

### Key interface

```csharp
public interface ICodeGenerated
{
    Task<Data> ExecuteAsync(Action action, Context context);
    List<ParamSnapshot> SnapshotParams() => new();    // default-impl; generator overrides per handler
}
```

All handlers register through this. The generator adds it automatically — handlers never write `: ICodeGenerated` themselves.

`SnapshotParams()` is stamped onto `Error.Params` by `App.Run` on failure (see *App.Run dispatch* in [`good_to_know.md`](good_to_know.md)) so the resulting error carries "param X arrived as Y" without re-running.

---

## Action Modifiers

Modifiers wrap a single action with cross-cutting behavior — caching, timeouts, error handling — without touching the action's own logic. They are regular actions whose handlers implement `IModifier` and carry a `[Modifier(Order = N)]` attribute.

### Shape

A modifier is an action that appears in an action's `Modifiers` collection (not in the step's `Actions` list) in the `.pr` file. At build time, modifier actions are grouped onto the preceding executable action and sorted by `Order`.

```
Step
  Actions: [
    Action("file", "read") {
      Modifiers: [
        Action("timeout", "after") { Ms = 1000 },   // Order = 1 (outermost)
        Action("cache",   "wrap")  { DurationMs = 60000 }, // Order = 2
        Action("error",   "handle") { RetryCount = 3 }     // Order = 3 (innermost)
      ]
    }
  ]
```

### Runtime: right-to-left fold

`Action.RunAsync` builds an innermost dispatch delegate and asks its `Modifiers` collection to fold the list around it:

```csharp
Func<Task<Data>> dispatch = () => context.App!.Run(this, context);
var result = await Modifiers.RunAsync(dispatch, context);
```

`Modifiers.RunAsync` walks the list right-to-left. Each action resolves its own handler via `Action.WrapAround`, which populates the source-generated parameters and returns the wrapped delegate. First in the list = outermost wrapper.

### IModifier

One contract:

```csharp
public interface IModifier
{
    Func<Task<Data>> Wrap(Func<Task<Data>> next, Context context);
}
```

- `cache.wrap` (Order = 2) — check the cache before `next`; store the result after if it succeeded.
- `timeout.after` (Order = 1) — cap `next` with a CTS on the context's cancellation stack.
- `error.handle` (Order = 3) — await `next`; on failure, match filters → retry → call error goal → ignore.

### Builder grouping

The LLM returns a flat action list. `Actions.GroupModifiers(modules)` walks that list and attaches every modifier action (any action whose handler carries `[Modifier]`) to the nearest preceding executable action. Inside each action, modifiers are sorted by `Order` so runtime gets a pre-ordered fold input. A leading modifier with no preceding executable is dropped and recorded as a `DroppedLeadingModifier` warning on the step.

### Adding a modifier

1. Write a handler class with `[Modifier(Order = N)]` and implement `IModifier.Wrap`.
2. Give it an `[Action(...)]` name so it registers through normal module discovery.

No Step changes, no runtime changes, no prompt changes — the new modifier appears in the action registry and the LLM can pick it like any other action.

---

## Events

Lifecycle hooks at every level: Goal, Step, Action.

### Event types

```
BeforeGoal / AfterGoal
BeforeStep / AfterStep
BeforeAction / AfterAction
OnError
OnBeforeGoalLoad / OnAfterGoalLoad
OnBeforeStepLoad / OnAfterStepLoad
OnCacheHit / OnCacheMiss
```

### Execution flow

```
goal lifecycle.Before -> run bindings
  step lifecycle.Before -> run bindings
    action lifecycle.Before -> run bindings
    action -> handler.ExecuteAsync(action, app, context)
    action lifecycle.After -> run bindings
  step lifecycle.After -> run bindings
goal lifecycle.After -> run bindings
```

Events don't call methods — they call goals via `GoalCall`. Event handlers are PLang code, integrated into the execution flow.

Context caches lifecycle resolution per goal/step/action. `InvalidateEventCache()` clears it when events are registered during execution.

Re-entrancy protection: `TryEnterEvent` / `ExitEvent` prevents recursive event handler execution.

---

## Channels

Named I/O streams for reading and writing data.

```
App.Channels        app-level channel registry
Actor.Channels      per-actor channel collection
```

Standard channels: `default`, `stdin`, `stdout`, `stderr`.

Each channel has a Stream, Direction (Input/Output/Bidirectional), and ContentType.

### Serializers

Routes content-type to serializer:
- `JsonStreamSerializer` — JSON
- `PlangSerializer` — .pr files (PLang binary format)
- `TextStreamSerializer` — plain text

`Channels.ReadAsync<T>(path)` reads a file, determines content type from extension, deserializes via the matching serializer.

---

## Variables

Thread-safe storage for `%variableName%` resolution.

```csharp
Variables.Put(data)              // store Data by Name
Variables.Get("name")            // retrieve Data
Variables.Set("name", value)     // simple value setter
```

### Dot-path resolution

```csharp
Variables.Set("obj.property[0].nested", value)
```

Extracts root name, navigates via `GetChild()`, sets the value at the leaf. Works across dictionaries, CLR objects, and lists.

### System variables (auto-registered)

`%Now%`, `%NowUtc%`, `%GUID%` — lazy, computed on access.

---

## Config

Goal-scoped module configuration with scope chain resolution.

```
context.ConfigScope -> parent.ConfigScope -> ... -> app.Config.Defaults -> class default
```

Modules define `IConfig` types. The source generator wires `app.Config.For<T>(context)` to create a `ModuleView<T>` — a context-bound view that resolves each property through the scope chain.

---

## CallStack

Optional frame tracking for debugging and error reporting.

```csharp
CallStack.Push(action)              // create CallFrame, check MaxDepth (1000)
CallStack.PopAsync()                // remove frame
CallStack.PushError(action, error)  // create frame on error (even when disabled)
CallStack.Errors                    // all errors that occurred (collection, not proxy)
```

Each `CallFrame` captures: Action (navigate trace on demand: `action.Step.Goal.Parent...`), Parent frame, Variables snapshot, Errors, Phase, timing.

Can be disabled for performance (`IsEnabled = false`). When disabled, zero overhead on happy path — error frames are created on demand via `PushError()`.

`%!error%` is not a context variable — it's passed as a parameter to the error goal via the `error.handle` modifier's `Goal.Parameters`.

---

## Error Handling

Errors implement `IError` and carry rich context:

```
Error
  .Message, .Key, .StatusCode
  .Step, .Goal              execution location
  .CallFrames               full stack trace
  .Variables                snapshot at time of error
  .Category                 Application (<500) or Runtime (>=500)
  .FixSuggestion, .HelpfulLinks
```

Error hierarchy: `Error` -> `ActionError`, `StepError`, `GoalError`, `ServiceError`, `ValidationError`, etc.

**Convention**: Match error mechanism to return type.
- `Data` / `Data?` return -> `Data.FromError()`
- Constructor / void -> throw
- `string` / `Type?` -> null

Error handling is a per-action **modifier** (`error.handle`), written as `on error ...` in the step text. Each modifier carries its own match filters (StatusCode/Key/Message), retry count, retry budget, error-goal call, and ignore flag. Multiple actions in the same step can have independent handlers.

When an error goal is called, the error is passed as `%!error%` parameter to the goal call — it flows in as data, not as context state. After the error goal returns, `%!error%` is scoped to that goal's variables.

---

## Directory Structure

```
App/
  this.cs                     App runtime root
  GlobalUsings.cs             type aliases
  
  Actor/
    this.cs                   Actor (System/Service/User)
    Context/
      this.cs                 request-level context
  
  Data/
    this.cs                   universal Data type
    this.Result.cs            error/success concern
    this.Navigation.cs        dot-path traversal
    this.Envelope.cs          compress/wrap/encrypt
    Navigators/               per-type navigation
  
  Goals/
    this.cs                   goal collection
    Goal/
      this.cs                 goal entity
      GoalCall.cs             goal resolution
      Steps/
        this.cs               steps collection
        Step/
          this.cs             step entity
          Actions/
            this.cs           actions collection
            Action/
              this.cs         action entity
    Setup/
      this.cs                 run-once setup system
  
  Events/
    this.cs                   event registry
    Lifecycle/
      this.cs                 before/after bindings
      Bindings/
        this.cs               binding collection
        Binding/
          this.cs             single event binding
  
  Channels/
    this.cs                   channel registry
    Channel/                  named I/O stream
    Serializers/              content-type routing
  
  Modules/
    this.cs                   action registry & dispatcher
  
  modules/
    ICodeGenerated.cs         handler interface
    IContext.cs               context injection
    IChannel.cs               channel injection
    app/run.cs                unified run action
    goal/call.cs              goal call action
    variable/                 variable operations
    file/                     file operations
    condition/                condition operations
    http/                     HTTP operations
    ...                       20+ more modules
  
  Variables/                  %variable% storage
  Config/                     goal-scoped settings
  CallStack/                  frame tracking
  Providers/                  pluggable implementations
  Types/                      type knowledge system
  Errors/                     error hierarchy
  Settings/                   persistent key-value storage
  FileSystem/                 sandboxed I/O
  Cache/                      step result cache
```

---

## OBP Design Principles (Summary)

1. **Behavior belongs to the owner** — whose data does this method touch? That's the owner.
2. **Navigate, don't pass** — reach dependencies through the object graph, never decompose into parameters.
3. **Names describe what the object IS** — nouns for properties, verbs for methods.
4. **Keep object references, not extracted fields** — store `Step`, not `step.Text`.
5. **Collections are the API** — expose the collection, don't proxy it. Parents delegate, never iterate children directly.
6. **Request state is a parameter, never stored** — if per-request, pass it. If per-object, store it.
7. **Data flows — relay, don't repackage** — never extract `.Value` and rewrap. Relay the Data.
8. **No redundant wrappers** — if the data already exists on an object, pass that object.

**The fix progression**: Create the type -> move behavior to owner -> store root, navigate internally -> pass the caller as a whole. Each step feels done but may still violate OBP at the next level.

See [object_pattern_formal.md](object_pattern_formal.md) for the full OBP specification with examples.
