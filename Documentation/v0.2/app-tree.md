# App Tree — High-Level Runtime Shape

A snapshot of the `app` object graph as it exists in `PLang/app/`. This is
the canonical naming dictionary for the current runtime: when proposing
fixes, explaining behavior, or wiring new features, use these real
`app.X.Y` paths instead of inventing parallel names. The source itself
(`PLang/app/**/this.cs`) carries the per-method detail.

## Case convention

Vocabulary folders are **lowercase + singular** (`actor`, `goal`,
`variable`, `channel`, `error`, `event`, `filesystem`, `format`,
`keepalive`, `snapshot`, `tester`, `type`, `config`, `callstack`, `data`,
`module`). C# infrastructure folders are PascalCase (`Attributes`,
`Diagnostics`, `Services`, `Statics`, `Utils`). One keyword carve-out:
`app/filesystem/Default/` stays PascalCase because `default` is a C#
keyword.

Property names on `app.@this` stay PascalCase (`.Cache`, `.Builder`,
`.Module`, `.FileSystem`, `.Goal`, `.Type`, …) — only the *types* live in
lowercase singular namespaces. So `ctx.App.FileSystem.Read(...)` is
property access; `app.filesystem.@this` is the type behind it.

Seven engine concepts (`Cache`, `Builder`, `Callback`, `Settings`,
`Modules`, `Code`, `Debug`) were merged with their action-module
counterparts under `app/module/<name>/` — no separate top-level folder
remains for those.

## Collection-node accessors (`app.X`)

`app.X` is the **collection node**, not a wrapper. Each concept exposes
its collection at `app.X` (type `X.list.@this`, folder `X/list/this.cs`),
owned once by the singleton `app` (or `actor` for `channel`):

```
app.X["name"]      — select one element (throws on miss)
app.X.list         — enumerate the collection
app.X.current      — the element execution is currently inside (only for
                     X ∈ {goal, callstack}; type/channel/event/module/
                     format have no .current)
```

There are no entities-vs-services here — only collections, some with a
current. The collection never lives on the element and is never a flat
`App<Plural>` property (the deleted `AppGoals` / `AppChannels` /
`AppEvents` / `AppModules` aliases were that smell). Registry = selection
+ lifecycle; all behavior lives on the element.

## Source of truth

- Root: `PLang/app/this.cs` (the `app.@this` class)
- Each subtree: the matching folder under `PLang/app/<name>/this.cs`
- Action modules: `PLang/app/module/<name>/` (one folder per registered
  action vocabulary; `module/this.cs` itself is the action registry —
  reached at `app.Module`, no `app.module.current`)

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
├── Event                 → app/event/         // event collection
├── Module                → app/module/        // action registry (module.action → handler)
├── Code                  → app/module/code/   // type-keyed provider registry
├── Navigator             → app/variable/Navigators/  // per-type Data navigation
├── Goal                  → app/goal/          // loaded goals + Setup
├── FileSystem            → app/filesystem/    // IPLangFileSystem abstraction
├── Cache                 → app/module/cache/  // pluggable step cache (default: memory)
├── Config                → app/config/        // goal-scoped strongly-typed module config
├── Settings              → app/module/settings/  // shared key/value, SQLite-backed
├── SettingsStore                              // lazy IStore behind Settings
├── Debug                 → app/module/debug/  // debug mode controller
├── Error                 → app/error/         // run-wide error scope + audit
├── Tester                → app/tester/        // *.test.goal discovery + runner
├── Builder               → app/module/builder/  // builder mode (in-memory datasources)
├── Callback              → app/module/callback/ // callback config holder
├── Type                  → app/type/          // PLang ↔ CLR type identity + entities
├── Format                → app/format/        // extension/MIME/Kind metadata
│
├── System                → app/actor/         // root actor (cancellation root)
├── User                  → app/actor/         // end-user actor
├── Service               → app/service/       // per-call I/O scope collection
├── CurrentActor                                // active actor (User by default)
│
├── KeepAlive             → app/keepalive/     // disposables tied to app lifetime
├── CallStack             → app/callstack/     // app-wide call tree + audit
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
├── Variables             → app/variable/       // actor-scoped variables
├── Channels              → app/channel/list/   // Output / Error / Input role registry
│   ├── Register(channel), Contains(name)
│   ├── Verify()                                // all-three-roles invariant
│   └── WriteTextAsync(role, text, ct)
├── Permission            → app/actor/permission/  // per-actor grant store
│   ├── Find(path, verb)                        // in-memory then sqlite, verified
│   ├── Add(signedGrant)                        // unsigned → in-memory, signed → sqlite
│   └── Revoke(record)                          // drops from both homes by Path
└── Identity                                    // signing identity (Service only by default)
```

The actor no longer carries a foundational-channels snapshot or an
`AsyncLocal` channel-resolution overlay; goal-channel recursion isolation
moved onto the channel itself as `Channel.Goal.@this.IsExecuting`
(see [io-channels.md](io-channels.md)).

## modules — the registered action set

Discovered at startup and exposed under `app.Module["<name>"]` /
`app.Module.<name>` (PascalCase property). Each lives at
`PLang/app/module/<name>/`.

```
app.Module
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

The handler for each action is a record under `module/<name>/`, paired
with a `*Handler` partial completed by the source generator
(`PLang.Generators`). See `Documentation/v0.2/object_pattern_formal.md`
for OBP rules and `good_to_know.md` for the property-kind contract
(`Data<T>` vs `[Code] T`).

`app.Module.Schema` (under `app/module/Schema/`, PascalCase) is **not** a
registered action module — it's the LLM action catalog object owned by
Module. Build a snapshot with `app.Module.Schema.Build()`; the builder
template and the trace viewer read from there. Lives next to the
action-module folders for proximity to what it describes.

## Data — the universal result wrapper

Returned by every action. `app/data/this.cs` plus partials:

```
Data
├── Value, Properties, Error, Success
├── Type                                            // non-null end-to-end; unset → type.@this.Null
├── Ok(value) / Fail(error) / Merge(other)
├── Compare(...)              (this.Compare.cs)
├── Transport pipeline        (this.Transport.cs — Wrap/Compress/Encrypt/…/Unwrap)
├── Navigation                (this.Navigation.cs — drives %var.path% and %var!key%)
├── Result helpers            (this.Result.cs)
├── Snapshot                  (this.Snapshot.cs — capture/restore for the snapshot system)
├── Properties sidecar        (Properties.cs — IDictionary<string,object?>, primitive-only)
├── Normalize / Reconstruct   (this.Normalize.cs / this.Reconstruct.cs — uniform tree-walk)
├── Wire converter            (Wire.cs — {name,type,value,properties,signature})
└── Code-specific             (app/data/code/)
```

`Data<T>` is the typed variant; the `T = Variable` case is the marker
used for write-target parameters (see `IRawNameResolvable` in
`app/variable/`).

## Type — promoted entity behind `Data.Type`

`app/type/this.cs` is the **entity** that carries every type's identity
and its fold properties (`Fields`, `Values`, `Example`, etc.). On
construction `_type` is set directly on `Data`; `type.Context` propagates
only via the `Data.Context` setter. Reading a fold property on a
**non-primitive** entity before the Data has been stamped throws
`InvalidOperationException` via `type.Promote()` — fail-loud-at-source.
Primitive entities carve out via the 2-arg ctor (`_foldLoaded = true`),
so `app.Type["string"].Example` is reachable without an App stamped.

`Data.Type` is non-null end-to-end; unset returns the **`type.@this.Null`
sentinel** (`IsNull = true`, `ClrType = typeof(object)`). The wire
converter skips Null emission, and the `Data.Type` setter clears
`_type` on Null assignment so callers can copy `source.Type`
unconditionally without re-introducing the sentinel.

Full rule + tests:
`PLang.Tests/App/SingularNamespaces/NullabilityTests/NonNullInvariantTests.cs`
(`TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard`,
`TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext`,
`ClrType_OnUnstampedDomainType_ReturnsNull`).

## CallStack — the execution spine

`app/callstack/`. Every `app.Run(action, ctx)` pushes a `Call` frame whose
lifetime (AsyncLocal Current, Children, Variables.OnSet subscription) is
managed by `await using`. Structural data is always captured; richer
capture (timing, tags, history) is gated by `CallStack.Flags`, populated
from `--debug={callstack:{...}}` via `Debug.Apply`.

## What's NOT on `app` (and where it lives instead)

| You might expect | Actually lives at |
|---|---|
| `app.Providers` | `app.Code` (renamed in runtime2-cleanup) |
| `app.Console` | nowhere — write through `app.CurrentActor.Channels` |
| `app.Logger` | nowhere — diagnostics through `app.Debug.Write` |
| `app.HttpClient` | nowhere — actions in `app.Module["http"]` use the `[Code]`-injected client |
| `app.Goals` (plural) | `app.Goal` (singular collection-node); enumerate with `.list`, select with `["path"]`, read current with `.current` |
| `app.Modules` (plural) | `app.Module` (singular action registry); the property on `app.@this` is `Module`, the type is `app.module.@this` |
| `app.Channels` (plural) | per-actor: `app.CurrentActor.Channels` (`app.channel.list.@this`) |
| Per-call I/O state | `app.Service` (one entry per outbound call), not `app.User` |

## Maintenance

When `PLang/app/` changes:

| Change | Update here |
|---|---|
| New top-level property on `app` | Add a line under "Top-level tree" |
| New action module registered | Add a line under "modules" |
| Property renamed | Update the line; add a row to "What's NOT on `app`" if the old name was widely used |
| New `Actor` surface | Update the "Actor surface" block (e.g. `Permission` added on the filesystem-permission branch) |

This file is hand-curated. Keep it short — one screen of structure is
more valuable than a complete-but-unreadable dump. Per-method detail
(parameters, return types, every action signature) lives in the source
itself.

To catch mechanical omissions (a new `app/module/<name>/` folder, a new
public property on `app.@this` or `actor.@this`, a new `app/data/this.*.cs`
partial), run:

```bash
Documentation/v0.2/scripts/check-app-tree.sh
```

It reports drift only — it does not rewrite the doc. Narrative
(annotations, the "What's NOT on `app`" table, the casing convention)
stays hand-curated.
