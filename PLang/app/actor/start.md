# Actors and the execution context — a C# developer's guide

An actor is an execution identity: the party a run acts as, with its own
channels, settings, permissions and task list. The execution context is the
object every handler receives, and it is how C# reaches the per-run state
(variables, the call chain, settings, the disk). This page covers both for the
developer working on Runtime2 in C#, and answers three questions: **what is
this, where is the source, and how does the plang I write reach this C#?**

```
app.actor.@this            PLang/app/actor/this.cs             namespace app.actor
app.actor.list.@this       PLang/app/actor/list/this.cs        namespace app.actor.list
app.actor.context.@this    PLang/app/actor/context/this.cs     namespace app.actor.context
app.actor.Name             PLang/app/actor/Name.cs             namespace app.actor (enum: system, user)
```

> **Audience.** These docs are for the developer working *on* Runtime2 in C#.
> The root of the runtime is described in `PLang/app/start.md`. The collection
> shape (`app.X`, `X.list`, `.current`) used here is defined there in §4.

---

## 1. What an actor is

An actor is one identity the app runs as. The app has two, and they are
well-known members of one list:

| Actor | Source | Role |
|---|---|---|
| `System` | `actor.list.@this.System` | The cancellation root. Bootstrap work runs here. Cancelling it cascades to `User`. |
| `User` | `actor.list.@this.User` | Runs user code. Linked to `System`'s cancellation token, and falls back to `System`'s settings. |

`Name` is the closed set of actors a step can name: `system` and `user`.
Its doc comment notes that the service is not an actor. Per-call service
scopes live on `app.Services` instead.

`app.actor` is a `type.current` node (`PLang/app/this.cs`):

```csharp
public global::app.type.current.@this<global::app.actor.@this, global::app.actor.list.@this> actor { get; }
```

So `app.actor` has a `.list` (the two actors) and a `.current` (the element the
asker acts as). The node is read at `app.actor.list` and the C# is
`actor.list.System`, `actor.list.User`.

The actor element itself is `actor.@this`
(`PLang/app/actor/this.cs`). It is a type-item (`[PlangType("actor")]`), so it
navigates like every other value.

> **There is no global current actor.** `actor.@this.Current(context)` is a
> static method that takes the context and returns `context.Actor`. The actor
> an operation acts as is read from the context passed into it. Nothing
> stores a "current" in a static field.

---

## 2. The context

`actor.context.@this` (`PLang/app/actor/context/this.cs`) is the request-level
state for one execution. Every actor owns one (`actor.Context`), and every
handler receives one. A child context is made for a task (`Child(runner,
token)`), so the task's own calls and cancellation stay separate from its
parent's.

Constructed like this (from `actor/this.cs`):

```csharp
Context = new context.@this(app, this, parentToken: _cts.Token);
```

Its constructor takes the app and the owning actor. The owner is passed in
and never changes, so `context.Actor` is never null.

### The members a handler uses

| Member | Type | Use |
|---|---|---|
| `App` | `app.@this` | The root. Reach any concept from here. |
| `Actor` | `actor.@this` | The actor this context belongs to. |
| `Variable` | `Variables` (alias for `app.type.item.variable.list.@this`) | The variable memory for this run. |
| `call` | `app.call.list.@this` | The chain of frames running in this context. Each frame holds its own variables. |
| `Setting` | `actor.setting.@this` | The in-memory settings door (`%!%`). Chains to the parent context, then to the actor's settings. |
| `FileSystem` | `app.type.item.path.file.filesystem.@this` | The disk a path's verbs reach after their gate. Born from the parent or the app. |
| `Trace` | `actor.context.trace.@this` | Per-context trace identity (`Id`, `Started`). Read as `%!trace.id%`. |
| `Parent` | `context.@this?` | The parent context, for a child. |
| `CancellationToken` | `CancellationToken` | This run's token. A timeout pushed with `PushCancellation` replaces it until popped. |
| `Test`, `Setup` | `data.@this?`, `Setup?` | Set only under `--test`, and during setup. |

The actor's channels are not on the context. They are on the actor
(`actor.Channel`). A handler that writes to the console reaches them as
`context.Actor.Channel`.

### Making a value from the context

A value is born from the context, so the context is always the one handle
in scope. Use these doors rather than constructing `data` by hand:

| Call | Result |
|---|---|
| `context.Ok()` / `context.Ok(value)` / `context.Ok<T>(value)` | A success `Data`. |
| `context.Error(error)` / `context.Error<T>(error)` | A failure `Data` carrying the `Error`. The error's first run here is stamped with this context. |
| `context.NotFound(name)` | A not-found `Data`. |
| `context.Null(name)` | A present-null `Data`. |
| `context.Data<T>(() => ...)` | Runs the compute. An `AppException` in it becomes an error `Data`. |

A failed result is answered, not thrown. The error is built with its key and
status, the same way the rest of the runtime builds it:

```csharp
return context.Error(new Error($"no {Name} is current", "NotFound", 404));
```

(`PLang/app/type/current/this.cs`.) Handlers answer with `Context.Ok()` the
same way, as `PLang/app/module/start.md` §4 shows.

### Variables, and the context's own names

`context.Variable` is the variable memory. Two names are always there, both
registered by the context itself (`RegisterContextVariables`):

| Name | Reads as |
|---|---|
| `!app` | `context.App` |
| `!context` | the context itself |

The variable store takes root names only. A path goes through a variable
(`actor/this.cs` sets `MyIdentity` and `Identity` this way, as lazy
`DynamicData`).

### Lifetime

- One context per actor, plus one per child (a task). A context runs one
  flow at a time. A task runs in a child context of its own.
- `Dispose()` cancels the context's token, disposes disposable values held in
  it, and clears them.
- `Actor.DisposeAsync()` cancels the actor, disposes its context, its
  channels and its settings.

---

## 3. What each actor owns

`actor.@this` owns the per-identity state. Each member is one object on the
actor, so a handler reaches it through the context's `Actor`:

| Member | Type | What it holds |
|---|---|---|
| `Name` | `string` | `"System"`, `"User"`, … Matched case-insensitively by `Match(key)`. |
| `Context` | `actor.context.@this` | The actor's root context. |
| `Permission` | `actor.permission.@this` | The grants on paths (see §4). |
| `Channel` | `channel.list.@this` | The actor's named channels, such as `Output` and `Error`. |
| `Task` | `task.list.@this` | The goal calls running in parallel on this actor. |
| `Setting` | `actor.setting.@this` | The actor's settings. A user's falls back to `System`'s. |
| `Identity` | `Identity?` | This actor's identity. `null` falls back to the app's default. |
| `CancellationToken` | `CancellationToken` | Cancelled by `Cancel()`. System cancels User too. |
| `App` | `app.@this` | Back-reference to the app. |

The `System` actor is built with the app's shutdown token. The `User` actor is
built with System's token and `fallback: System`, so both settings and
cancellation flow down from System.

---

## 4. Permissions gate the disk

A path verb asks the actor before it touches the disk. The gate is
`path.Authorize(verb, context)` (`PLang/app/type/item/path/this.Authorize.cs`).
In order, it does this:

1. **In-root paths are granted.** The actor owns its own root.
2. **A step of an OS goal, naming its own path, is granted without asking.**
   This applies only when the path is named by the step itself.
3. **A stored grant is looked up.** `actor.Permission.Find(path, verb)`.
4. **Otherwise the actor is asked.** `actor.Permission.Ask(...)` prompts the
   user on the actor's channel. The answer is stored through
   `Permission.Add`.

The `Permission` object (`PLang/app/actor/permission/this.cs`) holds the
grants. Its public surface is `Find`, `Add`, `Revoke` and `Ask`. Its doc
comment says a "y" answer is kept in memory for the app's lifetime, and an
"a" answer is saved in the actor's permission setting.

`System` is given a standing grant of `read`, `execute`, `write` and `delete`
in `actor/list/this.cs`. `User` has none of its own and inherits none; it
asks. The comment there says the shared `os` folder is the system's to write,
and every actor reads and runs there.

> **Rule.** A filesystem verb is `path.Verb(context)`. Do not call
> `System.IO` from a handler. The verb passes the gate, and the gate is the
> only check between a handler and the disk. See
> `PLang/app/type/item/path/file/this.Operations.cs`, where every `AuthGate`
> call sits.

---

## 5. How a handler gets its context

A generated action handler takes the context in its constructor. The
`IContext` interface (`PLang/app/module/IContext.cs`) declares the property,
and the generated primary constructor sets it:

```csharp
public interface IContext
{
    actor.context.@this Context { get; set; }
}
```

The module registry mints the handler with the run's context and starts it
(see `../module/start.md` §7):

```csharp
internal ICodeGenerated? Create(string actionName, actor.context.@this context)
    => Handler(actionName) is { } clr && typeof(ICodeGenerated).IsAssignableFrom(clr)
        ? (ICodeGenerated)Activator.CreateInstance(clr, context)!
        : null;
```

So the handler's `Context` is set before `Start()` runs, and it is the same
context that the step's goal runs in. Nothing passes a context through a
parameter list, and nothing reads one from a global.

---

## 6. Find …

| You want | Look at | Where |
|---|---|---|
| the two actors | `actor.list.System`, `actor.list.User` | `PLang/app/actor/list/this.cs` |
| the actor a name picks | `actor.list.this[Name]` | `PLang/app/actor/list/this.cs` |
| the actor a context belongs to | `context.Actor` | `PLang/app/actor/context/this.cs` |
| the variable memory | `context.Variable` | `PLang/app/actor/context/this.cs` |
| the call chain | `context.call` | `PLang/app/actor/context/this.cs`, `PLang/app/call/this.cs` |
| a setting for this run | `context.Setting` | `PLang/app/actor/setting/this.cs` |
| the disk for a path | `context.FileSystem` | `PLang/app/type/item/path/file/` |
| the grant on a path | `actor.Permission.Find`, `path.Authorize` | `PLang/app/actor/permission/this.cs`, `PLang/app/type/item/path/this.Authorize.cs` |
| the channels | `actor.Channel` | `PLang/app/channel/list/this.cs` |
| the trace id (`%!trace.id%`) | `context.Trace` | `PLang/app/actor/context/trace/this.cs` |

---

## The tree from here

| Doc | Covers |
|---|---|
| `PLang/app/start.md` | the app root, the collection-node pattern, and the reading fundamentals |
| `PLang/app/module/start.md` | the module system, the action handler pattern, and dispatch from a step |
| `PLang/app/module/file/start.md` | the `file` module, which reaches the disk through path verbs and the gate |
| `PLang/app/type/start.md` | the type system, which names the values the context hands out |
| `../call/start.md` | the call chain and variable memory |
| `../channel/start.md` | the channels an actor owns |
