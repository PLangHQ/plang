# Task: Generate Technical Documentation for the PLang Runtime

You are documenting the PLang Runtime — the core execution engine of the PLang programming language. This is the **technical reference** aimed at experienced programmers who need to understand the internals: contributors, module authors, and anyone working deeply with PLang's execution model.

**Important naming**: Call it "Runtime" throughout. Do not call it "App" — it will be renamed.

## Source Material

Read all C# source files under `PLang/App/` before writing anything. The documentation must reflect the actual code, not assumptions.

## Output

Produce a directory of files:

```
docs/runtime/
├── README.md              ← High-level overview with links to each section
├── engine.md
├── contexts.md
├── io-channels.md
├── goals-steps.md
├── goal-result.md
├── memory-stack.md
├── call-stack.md
├── events.md
├── modules.md
├── serializers.md
├── pr-file-format.md
├── exceptions.md
└── complete-example.md
```

**README.md** is the entry point. It contains:
- Architecture overview (object-based design philosophy, component diagram)
- One-paragraph summary of each component with a link to its detail page
- File structure reference (`PLang/App/` listing)

**Each section file** follows this template:

1. **Purpose** — one paragraph, what this component does and why it exists
2. **API Surface** — properties and methods with full C# signatures
3. **Behavior & Rules** — how it works, invariants, edge cases
4. **Code Examples** — C# for internal/runtime APIs. For module public methods (`public Task<GoalResult> Execute(...)` and similar), show PLang usage examples since the caller is a PLang developer, not C#
5. **Relationships** — how it connects to other components, with links to those docs

Use relative links between files (e.g., `[Variables](memory-stack.md)`, `[GoalResult](goal-result.md)`).

---

### README.md — Architecture Overview

The entry point. A programmer with no prior PLang knowledge should understand the Runtime's design in under 5 minutes.

Cover:
- PLang is a natural language programming language. The Runtime executes compiled PLang goals (`.pr` files)
- The Engine is minimal — it loads goals and runs them. `plang.exe` is conceptually just a goal: `- run plang app %data.path%, %data.parameters%, write to %app%` then `%app.wait%`
- Object-based architecture: modules expose a single entry point `Execute(string method, object? data)` and receive typed request objects. The module implementation handles dispatch internally. This keeps the interface surface small and uniform across all modules
- IO is stream-based with named channels. Output and input flow through `engine.Out` and `engine.In`, each supporting named channels (e.g., `engine.Out["debug"]`) that can be bound to handlers or goals
- `GoalResult` is the universal return type — `{ type, data, channels }`. Error handling uses result checking (`result.IsError`), not exceptions for control flow
- All core classes (`Engine`, `Goal`, `Step`) are `partial` — extensible by users in their own files
- CallStack is opt-in per component. When enabled, it activates variable change tracking with clone-before-change semantics for debugging and audit

Include the component diagram (text-based):

```
Engine
├── System          (PLangAppContext — app lifetime)
├── User            (PLangContext — per-request)
├── Out             (IO — named output channels)
├── In              (IO — named input channels)
├── Goals           (Goal collection loaded from .pr files)
├── CallStack       (optional, inherited)
├── Events          (before/after goals, variable changes)
├── Variables     (variable storage with change tracking)
├── Serializers     (pluggable, content-type based)
└── Modules         (registry of Execute-based modules)
```

Then list each component with a one-paragraph summary and a link to its detail page.

### engine.md — Engine

The central object. Cover:
- Constructor — what gets initialized
- Properties: `System` (PLangAppContext), `User` (PLangContext), `Out` (IO), `In` (IO), `Goals`, `CallStack`, `Events`, `Variables`, `Serializers`, `Modules`
- `Run(string path, object? parameters = null)` — loads goal by path, pushes to CallStack, fires before events, executes, fires after events, pops CallStack, returns GoalResult
- Lifecycle: construction → load goals → run
- The Engine class is `partial` — users can extend it in their own files
- Parameter validation syntax: `parameters:{ name:string, notnull, age:number(<150) }`

Show a complete bootstrap example:
```csharp
var engine = new Engine();
engine.Goals.LoadDirectory("./goals");
var result = await engine.Run("/CreateUser", new { name = "John", email = "john@example.com" });
```

### contexts.md — Contexts

Two contexts with different lifetimes:

**PLangAppContext (engine.System)**
- App lifetime — created once
- Holds SystemActor and trusted UserActor
- For console/desktop: both actors default to console sink

**PLangContext (engine.User)**
- Per-request lifetime
- For console: mirrors AppContext
- For web requests: new untrusted UserActor with HTTP sink, System escalates to AppContext.User (server operator)

Show the web request flow:
```
plang.exe starts
    → new Engine()
    → engine.System = new PLangAppContext(appIdentity)
    → engine.User = PLangContext.FromAppContext(appContext)

web request arrives
    → engine.User = PLangContext.ForWebRequest(appContext, requestIdentity, httpSink)
```

Note: Actors (SystemActor, UserActor, ServiceActor) will be documented separately. Keep actor coverage minimal here — just enough to explain the context properties.

### io-channels.md — IO & Channels

Cover:
- `IO` inherits from `Stream` — standard .NET pattern
- Named channels: `engine.Out["debug"]`, `engine.Out["error"]`, `engine.Out.Default`
- `"default"` is the default channel name, but can be anything
- `GetOrCreate(name)` — channels created on demand
- Binding channels to handlers: `channel.Bind(async data => { ... })`
- Binding channels to goals: `channel.Bind(goalReference)`
- `Unbind()` to remove handler
- `Write(data)` — fire and forget
- `Request(data)` — send and await response
- Unbound channels silently drop data (or console default — document what the code does)
- Pluggable serializers per channel: `engine.Serializers[contentType].Serialize(data, stream)`
- Opportunity for `Span<T>` / low-allocation patterns in Channel implementation

### goals-steps.md — Goals, Steps & Execution

Cover:
- `Goals` collection — `Load(path)`, `LoadDirectory(path, pattern)`, access by path `engine.Goals["/CreateUser"]`
- `Goal` class — `Path`, `Steps`, `CallStack` (optional), `BeforeRun`/`AfterRun` events
- `Step` class — `Line`, `Text`, `Module`, `Method`. Inherits CallStack from parent Goal
- Execution flow: Engine.Run → push CallStack → fire before events → Goal.Run → iterate Steps → each Step calls Module.Execute → fire after events → pop CallStack → return GoalResult
- All classes are `partial` — user can extend Goal, Step in their own files
- User-defined variables on Step: `step.Variables.User["Hello"] = "world"` → accessible as `%step.Hello%`

### goal-result.md — GoalResult

The universal return type. Cover:
- `GoalResult` is a struct with: `Type` ("goal" or "error"), `Data` (result object), `Channels` (ChannelData)
- `IsSuccess`, `IsError` convenience properties
- Static factories: `GoalResult.Success(data)`, `GoalResult.Error(message, exception?)`
- Channel-specific data: `result.Channels["debug"]`, `result.Channels.Error`
- `ErrorInfo` struct: `Message`, `StackTrace`, `Exception`
- Pattern: no exceptions for control flow — check `result.IsError` instead

### memory-stack.md — Variables & Variables

Cover:
- `Set(string name, object value, TypeInfo type)` — stores variable with type metadata
- `Get(string name)` — returns `ObjectValue?`
- `ObjectValue` wraps: `Name`, `Value`, `Type` (TypeInfo record)
- `TypeInfo` record: `TypeInfo(string ShortName)` with `FullName` property
- `Properties` — a collection of `ObjectValue` items
- Change tracking: before `Set()`, fires `OnVariableChanging` event with clone of previous value. After `Set()`, fires `OnVariableChanged`. This only happens when CallStack is enabled
- `%variable%` syntax in PLang maps to Variables lookups at runtime
- PLang handles type conversion automatically — never manually serialize/convert types, just use variables directly

### call-stack.md — CallStack & Debugging

Cover:
- `CallStack` holds `CallFrame` entries
- `CallFrame` constructor takes `(Goal goal, Step step)` — nothing more
- Inheritance: if a Goal has CallStack enabled, its Steps inherit the same CallStack
- `engine.CallStack`, `engine.Goals.CallStack`, `goal.CallStack`, `channel.CallStack` — each component can track its own execution
- CallStack is optional — only tracks when enabled. Disabling = faster execution
- When enabled, Variables fires variable change events (clone-before-change for undo/audit)
- When disabled, no change tracking overhead

### events.md — Events

Cover:
- `EventCollection` — central event registry
- Goal events: `AddBefore(goalPattern?, handler)`, `AddAfter(goalPattern?, handler)`
- `null` pattern = all goals. String pattern for path matching (e.g., `"/admin/*"`)
- Variable events: `OnVariableChanging(handler)`, `OnVariableChanged(handler)`
- `handler` receives `(key, beforeValue, afterValue)` — before value is a clone
- Async vs sync: `evt.IsAsync` — if true, fire-and-forget (no await). If false, await and capture result data into `channels[channel].data`
- Events on individual goals: `goal.BeforeRun += handler`, `goal.AfterRun += handler`

### modules.md — Modules

Cover:
- `BaseModule` — abstract base class all modules inherit from
- Properties available on every module: `Engine`, `Goal`, `Step` (injected by the runtime before each `Execute` call)
- Single entry point: `Execute(string method, object? data)` returns `Task<GoalResult>`. The module receives a method name and a typed request object, dispatches internally
- `ModuleRegistry` — `Register(name, module)`, access by name
- Injectable executors: `module.SetExecutor("path.dll")` or `module.SetExecutor("/goal/path")` — swap implementation at runtime without changing the caller
- `TypeMapping` — bidirectional map between simple names and CLR types: `"string" → typeof(string)`, `"int" → typeof(int)`, etc. Extensible: `TypeMapping.Register("money", typeof(decimal))`

Show a complete module implementation in C#:
```csharp
public class DbModule : BaseModule
{
    public override async Task<GoalResult> Execute(string method, object? data)
    {
        return method switch
        {
            "insert" => await Insert(data),
            "select" => await Select(data),
            "update" => await Update(data),
            "delete" => await Delete(data),
            _ => throw new NotSupportedException($"Unknown method: {method}")
        };
    }
}
```

Then show how a PLang developer calls it:
```plang
CreateUser
- insert into users, name=%name%, email=%email%, write to %user%
- select * from users where id=%user.id%, return 1, write to %result%
```

Module examples should always show both sides: the C# implementation for module authors, and the PLang usage for PLang developers.

### serializers.md — Serializers

Cover:
- `SerializerRegistry` — content-type based lookup
- `engine.Serializers.Add("path.dll")` — load serializer from DLL
- `engine.Serializers[contentType].Serialize(data, stream)` — stream-based, not byte arrays
- Pluggable: add custom serializers for msgpack, protobuf, etc.
- Channels use serializers based on their content type

### pr-file-format.md — .pr File Format

The compiled goal format. Cover:
- JSON structure:
```json
{
  "path": "/CreateUser",
  "steps": [
    {
      "line": 1,
      "text": "validate %name% is not empty",
      "module": "validation",
      "method": "notEmpty"
    },
    {
      "line": 2,
      "text": "insert into users, name=%name%, write to %user%",
      "module": "db",
      "method": "insert"
    }
  ]
}
```
- `path` — goal identifier, used by `engine.Run(path)` and `engine.Goals[path]`
- `steps[].text` — the original PLang natural language step
- `steps[].module` — which module handles this step
- `steps[].method` — which method on the module
- `steps[].line` — line number in original .goal file
- Variable syntax in text: `%name%` resolved at runtime from Variables
- Type hints in text: `%name%(type:object)` — parsed by builder, stored as TypeInfo

### exceptions.md — Exceptions

Cover the custom exception types:
- `GoalNotFoundException` — thrown when `engine.Run(path)` can't find the goal. Properties: `GoalPath`
- `ModuleNotFoundException` — thrown when step references unregistered module. Properties: `ModuleName`
- General philosophy: prefer `GoalResult.Error` over exceptions for expected failures. Exceptions for truly exceptional cases (goal not found, module not found).

### complete-example.md — Complete Example

A full end-to-end example showing:
1. Engine creation
2. Module registration
3. Serializer setup
4. Channel binding (debug channel to console)
5. Event registration (before all goals, variable change tracking)
6. Goal loading
7. Running a goal with parameters
8. Handling the GoalResult
9. Error handling

Use the example from the source README if one exists, or construct one that exercises all major components.

---

## Writing Style

- Technical and precise. No hand-holding, no "let's explore" language
- Show C# signatures exactly as they appear in the code
- Use code examples liberally — this is a programmer audience
- Document what the code actually does, not what it should do
- If something is unimplemented or TODO, say so explicitly
- Keep prose tight. If a code example explains it, don't also explain it in words
- No marketing language. No "powerful", "elegant", "seamless"
- Use tables for comparisons and property listings
- Cross-reference between files using relative links (e.g., "See [Variables](memory-stack.md) for variable storage details")

## File Structure Reference

```
PLang/App/
├── Engine.cs
├── Contexts.cs            (PLangAppContext, PLangContext)
├── IO.cs
├── Channel.cs
├── Goals.cs
├── Goal.cs
├── Step.cs
├── GoalResult.cs
├── ChannelData.cs
├── ErrorInfo.cs
├── CallStack.cs
├── CallFrame.cs
├── EventCollection.cs
├── Variables.cs
├── ObjectValue.cs
├── Properties.cs
├── SerializerRegistry.cs
├── ModuleRegistry.cs
├── TypeMapping.cs
├── Exceptions.cs
└── GoalData.cs
```

## What NOT to Document

- **Builder/Compiler** — not built yet, skip entirely
- **Actor details** — will be documented separately. Only mention actors enough to explain contexts
- **PLang syntax** — this doc is about the Runtime C# internals, not the PLang language itself
- **Installation/setup** — separate concern
