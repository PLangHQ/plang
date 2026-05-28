# App Tree — High-Level Runtime Shape

A snapshot of the `app` object graph as it exists in `PLang/app/`. This is
the canonical naming dictionary for the current runtime: when proposing
fixes, explaining behavior, or wiring new features, use these real
`app.X.Y` paths instead of inventing parallel names. The source itself
(`PLang/app/**/this.cs`) carries the per-method detail.

## Case convention

Vocabulary folders are **lowercase** (`actor`, `goals`, `variables`,
`channels`, `errors`, `events`, `filesystem`, `formats`, `keepalive`,
`snapshot`, `tester`, `types`, `config`, `callstack`, `data`). C# infrastructure
folders are PascalCase (`Attributes`, `Diagnostics`, `Services`, `Statics`,
`Utils`). One keyword carve-out: `app/filesystem/Default/` stays PascalCase
because `default` is a C# keyword.

Property names on `app.@this` stay PascalCase (`.Cache`, `.Builder`, `.Modules`,
`.FileSystem`, `.Goals`, …) — only the *types* live in lowercase namespaces.
So `ctx.App.FileSystem.Read(...)` is property access; `app.filesystem.@this`
is the type behind it.

Seven engine concepts (`Cache`, `Builder`, `Callback`, `Settings`, `Modules`,
`Code`, `Debug`) were merged with their action-module counterparts under
`app/modules/<name>/` — no separate top-level folder remains for those.

## Source of truth

- Root: `PLang/app/this.cs` (the `app.@this` class)
- Each subtree: the matching folder under `PLang/app/<name>/this.cs`
- Action modules: `PLang/app/modules/<name>/` (one folder per registered module)

## Top-level tree

```
app
├── Id, Name, Version, Created, Updated
├── Parent                                  // child apps inherit parent filesystem scope (IsInRoot walks the chain)
├── AbsolutePath, OsDirectory
├── Environment, Culture
├── StartedAt, Uptime
├── ShutdownToken, Create
│
├── Statics                                  // app-scoped key/value (snapshotable)
├── Events                 → app/events/        // global event collection
├── Modules                → app/modules/       // flat action registry (module.action → handler)
├── Code                   → app/modules/code/  // type-keyed provider registry (was: Providers)
├── Navigators             → app/variables/Navigators/  // per-type Data navigation
├── Goals                  → app/goals/         // loaded goals + Setup
├── FileSystem             → app/filesystem/    // IPLangFileSystem abstraction
├── Cache                  → app/modules/cache/    // pluggable step cache (default: memory)
├── Config                 → app/config/        // goal-scoped strongly-typed module config
├── Settings               → app/modules/settings/  // shared key/value, SQLite-backed
├── SettingsStore                              // lazy IStore behind Settings
├── Debug                  → app/modules/debug/    // debug mode controller (Debugging)
├── Errors                 → app/errors/        // run-wide error scope + audit
├── Tester                 → app/tester/        // *.test.goal discovery + runner
├── Builder                → app/modules/builder/   // builder mode (in-memory datasources)
├── Callback               → app/modules/callback/  // callback config holder
├── Types                  → app/types/         // PLang ↔ CLR type identity
├── Formats                → app/formats/       // extension/MIME/Kind metadata
│
├── System                 → app/actor/         // root actor (cancellation root)
├── User                   → app/actor/         // end-user actor
├── Service                → app/Services/      // per-call I/O scope collection (Services is C# infra → PascalCase)
├── Services                                    // alias collection (same target)
├── CurrentActor                                // active actor (User by default)
│
├── KeepAlive              → app/keepalive/     // disposables tied to app lifetime
├── CallStack              → app/callstack/     // app-wide call tree + audit
│
├── Start()                                     // bootstrap: Load → Verify channels → run goal
├── Load() / Save()                             // .build/app.pr identity
├── Run(action, ctx, cause?)                    // dispatch one action through CallStack
├── RunAction<T>(action, ctx)                   // strongly-typed C# composition
├── RunGoalAsync(goal|GoalCall, ctx?, ct)       // run a goal in-flow
├── GetActor(name)                              // resolve "system" | "user"
├── RequestShutdown()                           // cancel ShutdownToken
└── DisposeAsync()
```

## Actor surface (one shape, three instances: System, User, Service)

```
Actor
├── Name                                       // "System" | "User" | "<service-id>"
├── App                                        // back-ref
├── CancellationToken                          // chains: System → User → Service
├── Context                → app/actor/context/
│   ├── Variables, Step, Goal, Event           // anchors
│   ├── AnchorScope(action)                    // scoped Step/Goal swap
│   └── ...
├── Variables              → app/variables/    // actor-scoped variables
├── Channels               → app/channels/     // Output / Error / Input role registry
│   ├── Register(channel), Contains(name)
│   ├── Verify()                               // all-three-roles invariant
│   └── WriteTextAsync(role, text, ct)
├── Permission             → app/actor/permission/  // per-actor grant store
│   ├── Find(path, verb)                       // in-memory then sqlite, verified
│   ├── Add(signedGrant)                       // unsigned → in-memory, signed → sqlite
│   └── Revoke(record)                         // drops from both homes by Path
├── Identity                                   // signing identity (Service only by default)
└── FreezeFoundational()                       // snapshot boot-time channels
```

## modules/ — the registered action set

Discovered at startup and exposed under `app.Modules.<name>`. Each lives at
`PLang/app/modules/<name>/`.

```
app.Modules
├── environment    — app lifecycle (execute, shutdown, …)   [renamed from "app" during the app-lowercase rename]
├── assert         — test assertions
├── builder        — builder-side actions (note: builder.app action renamed to builder.load)
├── cache          — cache.set / cache.get / cache.invalidate
├── callback       — signed callback URLs / handlers
├── channel        — channel register/write/read
├── code           — provider routing (run code against a [Code] target)
├── condition      — if / else
├── crypto         — hash / encrypt / decrypt
├── debug          — debug.write / debug.dump
├── error          — error.handle / error.wrap / error.rethrow
├── event          — event subscribe / raise
├── file           — read / save / copy / move / delete / exists / list
├── goal           — goal.call / goal.return
├── http           — http.request / http.serve
├── identity       — identity.create / identity.sign
├── list           — list.add / list.filter / list.map …
├── llm            — llm.ask / llm.tool
├── loop           — foreach
├── math           — arithmetic + comparison
├── mock           — test mocks
├── module         — module discovery / introspection
├── output         — output.write (user-facing channel)
├── settings       — settings.get / settings.set
├── signing        — signing.sign / signing.verify
├── test           — *.test.goal runner glue
├── timeout        — bounded async
├── timer          — schedule / interval
├── ui             — UI surface actions
└── variable       — variable.set / variable.append / variable.delete
```

The handler for each action is a record under `modules/<name>/`, paired with a
`*Handler` partial completed by the source generator (`PLang.Generators`).
See `Documentation/v0.2/object_pattern_formal.md` for OBP rules and
`good_to_know.md` for the property-kind contract (`Data<T>` vs `[Code] T`).

`app.Modules.Schema` (under `app/modules/Schema/`, PascalCase) is **not** a
registered action module — it's the LLM action catalog object owned by
Modules. Build a snapshot with `app.Modules.Schema.Build()`; the builder
template and the trace viewer read from there. Lives next to the
action-module folders for proximity to what it describes.

## Data — the universal result wrapper

Returned by every action. `app/data/this.cs` plus partials:

```
Data
├── Value, Properties, Error, Success
├── Ok(value) / Fail(error) / Merge(other)
├── Compare(...)              (this.Compare.cs)
├── Transport pipeline        (this.Transport.cs — Wrap/Compress/Encrypt/…/Unwrap)
├── Navigation                (this.Navigation.cs — drives %var.path% and %var!key%)
├── Result helpers            (this.Result.cs)
├── Snapshot                  (this.Snapshot.cs — capture/restore for the snapshot system)
├── Properties sidecar        (Properties.cs — IDictionary<string,object?>, primitive-only)
├── Normalize / Reconstruct   (this.Normalize.cs / this.Reconstruct.cs — uniform tree-walk)
├── Wire converter            (Wire.cs — {name,type,value,properties,signature}; renamed from WireJsonConverter)
└── Code-specific             (app/data/code/)
```

`Data<T>` is the typed variant; the `T = Variable` case is the marker used for
write-target parameters (see `IRawNameResolvable` in `app/variables/`).

## CallStack — the execution spine

`app/callstack/`. Every `app.Run(action, ctx)` pushes a `Call` frame whose
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

When `PLang/app/` changes:

| Change | Update here |
|---|---|
| New top-level property on `app` | Add a line under "Top-level tree" |
| New action module registered | Add a line under "modules/" |
| Property renamed | Update the line; add a row to "What's NOT on `app`" if the old name was widely used |
| New `Actor` surface | Update the "Actor surface" block (e.g. `Permission` added on the filesystem-permission branch) |

This file is hand-curated. Keep it short — one screen of structure is
more valuable than a complete-but-unreadable dump. Per-method detail
(parameters, return types, every action signature) lives in the source
itself.

To catch mechanical omissions (a new `app/modules/<name>/` folder, a new
public property on `app.@this` or `actor.@this`, a new `app/data/this.*.cs`
partial), run:

```bash
Documentation/v0.2/scripts/check-app-tree.sh
```

It reports drift only — it does not rewrite the doc. Narrative
(annotations, the "What's NOT on `app`" table, the casing convention)
stays hand-curated.
