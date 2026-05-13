# App Tree — High-Level Runtime Shape

A snapshot of the `app` object graph as it exists in `PLang/App/`. This is the
canonical naming dictionary for the current runtime: when proposing fixes,
explaining behavior, or wiring new features, use these real `app.X.Y` paths
instead of inventing parallel names.

For the per-surface deep dive (every method and parameter), see
`/shared/app-tree/` mounted in bot containers — this file is the entry
point that fits in one screen. When the two disagree, this file wins
(it's tracked in the repo); update `/shared/app-tree/` to match.

## Source of truth

- Root: `PLang/App/this.cs` (the `App.@this` class)
- Each subtree: the matching folder under `PLang/App/<Name>/this.cs`
- Action modules: `PLang/App/modules/<name>/` (one folder per registered module)

## Top-level tree

```
app
├── Id, Name, Version, Created, Updated
├── AbsolutePath, OsDirectory
├── Environment, Culture
├── StartedAt, Uptime
├── ShutdownToken, Create
│
├── Statics                                  // app-scoped key/value (snapshotable)
├── Events                 → App/Events/      // global event collection
├── Modules                → App/Modules/     // flat action registry (module.action → handler)
├── Code                   → App/Code/        // type-keyed provider registry (was: Providers)
├── Navigators             → App/Variables/Navigators/  // per-type Data navigation
├── Goals                  → App/Goals/       // loaded goals + Setup
├── FileSystem             → App/FileSystem/  // IPLangFileSystem abstraction
├── Cache                  → App/Cache/       // pluggable step cache (default: memory)
├── Config                 → App/Config/      // goal-scoped strongly-typed module config
├── Settings               → App/Settings/    // shared key/value, SQLite-backed
├── SettingsStore                              // lazy IStore behind Settings
├── Debug                  → App/Debug/       // debug mode controller (Debugging)
├── Errors                 → App/Errors/      // run-wide error scope + audit
├── Tester                 → App/Tester/      // *.test.goal discovery + runner
├── Builder                → App/Builder/     // builder mode (in-memory datasources)
├── Callback               → App/Callback/    // callback config holder
├── Types                  → App/Types/       // PLang ↔ CLR type identity
├── Formats                → App/Formats/     // extension/MIME/Kind metadata
│
├── System                 → App/Actor/       // root actor (cancellation root)
├── User                   → App/Actor/       // end-user actor
├── Service                → App/Services/    // per-call I/O scope collection
├── Services                                   // alias collection (same target)
├── CurrentActor                               // active actor (User by default)
│
├── KeepAlive              → App/KeepAlive/   // disposables tied to app lifetime
├── CallStack              → App/CallStack/   // app-wide call tree + audit
│
├── Start()                                    // bootstrap: Load → Verify channels → run goal
├── Load() / Save()                            // .build/app.pr identity
├── Run(action, ctx, cause?)                   // dispatch one action through CallStack
├── RunAction<T>(action, ctx)                  // strongly-typed C# composition
├── RunGoalAsync(goal|GoalCall, ctx?, ct)      // run a goal in-flow
├── GetActor(name)                             // resolve "system" | "user"
├── RequestShutdown()                          // cancel ShutdownToken
└── DisposeAsync()
```

## Actor surface (one shape, three instances: System, User, Service)

```
Actor
├── Name                                       // "System" | "User" | "<service-id>"
├── App                                        // back-ref
├── CancellationToken                          // chains: System → User → Service
├── Context                → App/Actor/Context/
│   ├── Variables, Step, Goal, Event           // anchors
│   ├── AnchorScope(action)                    // scoped Step/Goal swap
│   └── ...
├── Variables              → App/Variables/    // actor-scoped variables
├── Channels               → App/Channels/     // Output / Error / Input role registry
│   ├── Register(channel), Contains(name)
│   ├── Verify()                               // all-three-roles invariant
│   └── WriteTextAsync(role, text, ct)
├── Identity                                   // signing identity (Service only by default)
└── FreezeFoundational()                       // snapshot boot-time channels
```

## modules/ — the registered action set

Discovered at startup and exposed under `app.Modules.<name>`. Each lives at
`PLang/App/modules/<name>/`.

```
app.Modules
├── app          — app lifecycle (execute, shutdown, …)
├── assert       — test assertions
├── builder      — builder-side actions
├── cache        — cache.set / cache.get / cache.invalidate
├── callback     — signed callback URLs / handlers
├── channel      — channel register/write/read
├── code         — provider routing (run code against a [Code] target)
├── condition    — if / else
├── crypto       — hash / encrypt / decrypt
├── debug        — debug.write / debug.dump
├── error        — error.handle / error.wrap / error.rethrow
├── event        — event subscribe / raise
├── file         — read / save / copy / move / delete / exists / list
├── goal         — goal.call / goal.return
├── http         — http.request / http.serve
├── identity     — identity.create / identity.sign
├── list         — list.add / list.filter / list.map …
├── llm          — llm.ask / llm.tool
├── loop         — foreach
├── math         — arithmetic + comparison
├── mock         — test mocks
├── module       — module discovery / introspection
├── output       — output.write (user-facing channel)
├── settings     — settings.get / settings.set
├── signing      — signing.sign / signing.verify
├── test         — *.test.goal runner glue
├── timeout      — bounded async
├── timer        — schedule / interval
├── ui           — UI surface actions
└── variable     — variable.set / variable.append / variable.delete
```

The handler for each action is a record under `modules/<name>/`, paired with a
`*Handler` partial completed by the source generator (`PLang.Generators`).
See `Documentation/v0.2/object_pattern_formal.md` for OBP rules and
`good_to_know.md` for the property-kind contract (`Data<T>` vs `[Code] T`).

## Data — the universal result envelope

Returned by every action. `App/Data/this.cs` plus partials:

```
Data
├── Value, Properties, Error, Success
├── Ok(value) / Fail(error) / Merge(other)
├── Compare(...)       (Data.Compare.cs)
├── Envelope wrapping  (Data.Envelope.cs)
├── Navigation         (Data.Navigation.cs — drives %var.path%)
├── Result helpers     (Data.Result.cs)
└── Code-specific      (Data/Code/)
```

`Data<T>` is the typed variant; the `T = Variable` case is the marker used for
write-target parameters (see `IRawNameResolvable` in `App/Variables/`).

## CallStack — the execution spine

`App/CallStack/`. Every `app.Run(action, ctx)` pushes a `Call` frame whose
lifetime (AsyncLocal Current, Children, Variables.OnSet subscription) is
managed by `await using`. Structural data is always captured; richer capture
(timing, tags, history) is gated by `CallStack.Flags`, populated from
`--debug={callstack:{...}}` via `Debug.Apply`.

## What's NOT on `app` (and where it lives instead)

| You might expect | Actually lives at |
|---|---|
| `app.Providers` | `app.Code` (renamed in runtime2-cleanup) |
| `app.Console` | nowhere — write through `app.CurrentActor.Channels` |
| `app.Logger` | nowhere — diagnostics through `app.Debug.Write` |
| `app.HttpClient` | nowhere — actions in `app.Modules.http` use the `[Code]`-injected client |
| Per-call I/O state | `app.Services` (one entry per outbound call), not `app.User` |

## Maintenance

When `PLang/App/` changes:

| Change | Update here |
|---|---|
| New top-level property on `App` | Add a line under "Top-level tree" |
| New action module registered | Add a line under "modules/" |
| Property renamed | Update the line; add a row to "What's NOT on `app`" if the old name was widely used |
| New `Actor` surface | Update the "Actor surface" block |

This file is hand-curated, like `/shared/app-tree/`. Keep it short — one
screen of structure is more valuable than a complete-but-unreadable dump.
The deep tree (parameters, return types, every action signature) lives in
`/shared/app-tree/` and the source itself.
