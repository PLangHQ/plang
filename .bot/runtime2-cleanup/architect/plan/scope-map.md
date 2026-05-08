# Scope map — what's shared (app-level) vs per-actor in PLang

This document maps every long-lived `@this` and major property in the App graph to its scope. The goal is to settle, with explicit agreement, **what lives once per App and what lives once per Actor**, so future stages don't drift.

The distinction matters because it determines:
- *How many instances exist* — one or many.
- *What back-ref the class holds* — App, Context, parent-ref, none.
- *Whether registrations propagate* — a runtime registration on a shared thing applies everywhere; on a per-actor thing, only to that actor.
- *Where bugs about "wrong actor seeing wrong state" come from* — usually a mismatch between intended scope and actual allocation.

## Scope vocabulary

- **Shared (App-level)** — one instance per App. Every actor sees the same instance. Allocated by App at boot.
- **Per-actor** — one instance per Actor. System and User each have their own; they do *not* share. Allocated by the Actor in its ctor.
- **Per-context** — one instance per Actor.Context. Today this is essentially "per-actor" because each Actor owns one Context, but the distinction matters for things scoped to an execution context (Trace).
- **Per-call / per-channel / per-step** — finer scopes that come up for individual operations or entities. Out of scope for this map; flagged where relevant.

## App.@this — the shared root

App is the only class without a back-ref. Everything below either lives on App (shared) or under an Actor (per-actor).

### Shared (one instance, on App)

These are properties exposed on `App.@this`. Each is allocated once at App construction.

| Property | Type | Reason for shared scope |
|----------|------|-------------------------|
| `app.FileSystem` | `IPLangFileSystem` | Process-level resource (the OS file system). Shared by every actor. |
| `app.Modules` | `AppModules` | Action handler registry. Modules ship with the runtime; same set serves every actor. |
| `app.Providers` | `AppProviders` | Pluggable C# implementations registry (Provider→Code in stage 19). Boot-time registrations + DLL loads apply app-wide. |
| `app.Goals` | `AppGoals` | Goal registry (.goal/.pr files loaded once). Same goals run for every actor. |
| `app.Build` | `Build.@this` | Build subsystem. App-level mode flag (`Build.IsEnabled`) flipped by `plang --builder` at CLI launch. Single mode for the whole app run. ★ Future direction — Ingi sketched per-context build mode (`- build my.goal` from inside PLang) — out of scope for this branch. |
| `app.Debug` | `Debugging.@this` | Debug subsystem. App-level flag flipped by `plang --debug=...` at CLI launch. ★ Same future direction as Build. |
| `app.Debug.CallStack` | `CallStack.@this` | One call tree per app run, across actors. Per-context view via `Context.CallStack` is read-through. After stage 7 the property moves to `app.CallStack` directly. |
| `app.Testing` | `Testing.@this` | Test runner. Single mode + result collection per app run. App-level flag at CLI launch. ★ Same future direction as Build/Debug. |
| `app.Cache` | `ICache` | Step cache. **Shared by design** (Ingi 2026-05-08: "per app. this is global cache"). Cross-actor memoization is intended. |
| `app.Statics` | `AppStatics` | Static state container. |
| `app.Callback` | `Callback.@this` | **Config holder only** (e.g., `app.Callback.Signature.ExpiresInMs`). Not a callback registry — actual callbacks live with the Data they belong to. Shared because it's app config. |
| `app.Catalog` | `Catalog.@this` | Going away in stage 9 (dissolves into `app.Modules.Schema`). |
| `app.Choices` | `Choices.@this` | Going to `app.Builder.Choices` in stage 17. Build-time concept; app-level. |
| `app.Config` | `Config.@this` | Configuration registry. |
| `app.Types` | `Types.@this` | Type registry (CLR ↔ plang names). |
| `app.Services` | `Services.@this` | The Services *collection*. (Individual `Service.@this` instances are per-call.) |
| `app.SettingsVariable` | `SettingsVariable` | The shared "Settings" variable that gets registered on every actor's `Context.Variables`. After stage 13 the rework lands as a shared `Settings.@this` collection on App (settled 2026-05-08 — one per app). |
| `SettingsStore` | `ISettingsStore` (today on Actor — drift) | **Should be App-level (shared, single `system.sqlite`).** Today each Actor allocates a `Lazy<ISettingsStore>` and `user.sqlite` gets created — but no consumer uses User's store; every real access path goes through `app.System.SettingsStore` (Goals/Setup, identity provider, llm provider, settings module). Per-actor allocation is dead drift. Future stage (probably stage 13): move to `app.SettingsStore`, drop the per-actor `_dataSource`. |
| `app.Variables.Navigators` (after stage 21) | `Variables.Navigators.@this` | Data navigation registry. **Moves from App to Variables** (Ingi 2026-05-08). Today at `app.Navigators` (App.this.cs:138); folder `App/Data/Navigators/` relocates to `App/Variables/Navigators/`; namespace renames; access path becomes `app.Variables.Navigators`. Stage 21 (`navigators-to-variables`). |
| `app.Serializers` | `Serializers.@this` | **Going away in stage 1.** Currently a shortcut that bypasses actors. Per-actor `actor.Channels.Serializers` becomes the only path. |

### Intentional multi-tier scoping (kept as-is for this branch)

- **Events — three-tier design (kept 2026-05-08).** I initially called this drift; that was wrong. `Channel.@this.MatchingBindings` (Channels/Channel/this.cs:170-192) intentionally checks bindings at three tiers when an event fires:
  - **Per-channel** (`Channel.Events`) — one Events.@this per Channel instance, scoped to that channel.
  - **Per-actor** (`Context.Events`) — where PLang's `event.on` writes (`event/on.cs:65`, `mock/action.cs:73`, `test/run.cs:120`).
  - **App-level** (`App.Events`) — cross-actor bindings ("one binding covers every channel-of-name 'logger' regardless of which actor"). Reader infrastructure exists; **no writer path today** — half-built.
  
  Ingi's call: keep as-is. The three tiers are coherent; the missing writer for the app-level tier is a future design pass (filed in `Documentation/Runtime2/todos.md` 2026-05-08). Not in scope for this cleanup.

- **`app.Errors.Trail`** — run-wide trail; User and System errors mix into one collection (Errors/this.cs:39). The AsyncLocal `_current` is correctly flow-scoped, only Trail is shared. Architect's read: shared Trail isn't actively breaking anything; "all errors that happened in this app run" is a coherent meaning for a diagnostic collection. **Lean: leave shared, no change in this branch.** Revisit if a use case surfaces (e.g., per-actor error reporting in a UI).

### Future architectural direction — out of scope for this plan

The mode flags `app.Build`, `app.Debug`, `app.Testing` are CLI-launch app-level flags today. Ingi's mental model of `- set debug = true`, `- build my.goal`, `- run tests on '/tests'` from *inside PLang code* describes per-context modes — useful in a web-pool runtime where the App object is rented per request and reset on return. That's a real direction but **not in this cleanup branch**; deserves its own focused plan once this lands.

### Per-operation (instantiated and discarded per call)

These are NOT shared and NOT per-actor — they're transient per-operation:

| Class | Lifetime | Notes |
|-------|----------|-------|
| `Snapshot.@this` | Per snapshot operation | Instantiated `new Snapshot.@this()` at App.this.Snapshot.cs:18, CallStack/this.Snapshot.cs:94, Callback/ErrorCallback.cs:102, etc. Not exposed as `app.Snapshot`. Used as a write-store for capturing a single snapshot's state. |
| `CallStack.Call.@this` | Per call frame | Lives within the CallStack tree. |

### Per-actor (one instance per Actor)

Each Actor holds these directly. System and User have their own; they don't share.

| Property | Type | Reason for per-actor scope |
|----------|------|----------------------------|
| `actor.Context` | `Actor.Context.@this` | Each actor's execution context. Variables, trace, identity all flow through here. |
| `actor.Context.Variables` | `Variables.@this` | Per-actor variable scope. `%Settings.X%` resolution, `%MyIdentity%`, etc. resolve in the actor's scope. |
| `actor.Context.Trace` | `TraceContext` | Per-context trace identity (groups diagnostic output for one execution). |
| `actor.Context.Events` | `AppEvents` | Per-context event registry. Each Context allocates its own (Actor/Context/this.cs:92). The middle tier of the three-tier event scoping (per-channel / per-actor / app-level). PLang `event.on` etc. write here. |
| `actor.Channels` | `AppChannels` | Per-actor channel registry. System has its own Output/Error/Input streams; User has its own. **Settled this branch — was confused; now explicit.** |
| `actor.Channels.Serializers` | `Serializers.@this` | Per-actor — same scope as Channels (each actor allocates its own at boot, registered with identical defaults). **Settled in stage 1.** |
| ~~`actor.SettingsStore`~~ | ~~`ISettingsStore`~~ | **Removed from per-actor — see shared list above.** Per-actor allocation is dead drift; all real consumers use `app.System.SettingsStore`. Future stage moves to App level. |
| `actor.Identity` | `Identity?` | Per-actor identity. System has system identity; User has user identity. |
| `actor.CancellationToken` | `CancellationToken` | Per-actor cancellation linked to App's shutdown CTS. |

### Mixed cases (worth explicit agreement)

**1. `app.SettingsVariable` and `Settings.@this` after stage 13 — shared, one per app.**

Today: one `SettingsVariable` lives on App; it gets registered on every actor's `Context.Variables` (Actor.this.cs:133). Navigation hook exists once per actor; the object itself is shared.

After stage 13 (`settings-collection-rework`): the `Settings.@this` collection is **shared (one per app)** — Ingi confirmed 2026-05-08. There's only going to be `system.sqlite` for settings (per-app, not per-actor). The current per-actor allocation in `Actor.@this._dataSource` is dead drift; future stage 13 work also moves SettingsStore to App level.

**2. SettingsStore — should be App-level (settled 2026-05-08).**

Today each Actor allocates a `Lazy<ISettingsStore>` (Actor.this.cs:15) and the ctor creates `{name}.sqlite` per actor. Reality: zero consumers use User's store; every real path goes through `app.System.SettingsStore` (Goals/Setup, identity provider, llm provider, settings module). Per-actor allocation is dead drift.

Future stage (stage 13): move to `app.SettingsStore` (one shared instance backed by `system.sqlite`); drop the per-actor `_dataSource` field; sweep `app.System.SettingsStore` references to `app.SettingsStore`.

**3. `app.Debug.CallStack` — shared today, but the scope is wrong for parallel execution.**

`Debug/this.cs:101` allocates a single `App.CallStack.@this()` per app. `Context.CallStack` is a read-through accessor (`Context/this.cs:48: CallStack => App.Debug?.CallStack`). Inside CallStack, `_current` is `AsyncLocal<Call>` (flow-scoped, fork-safe), but `_root`, `Audit`, and the tree structure itself are shared.

Sequential CLI execution: works correctly (one flow, one tree).
Parallel/web-pool execution: **broken** — concurrent request flows interleave pushes into the same tree; Audit mixes traces; `%!callStack%` may show frames from another flow.

**Kept as-is for this branch (Ingi 2026-05-08).** Stage 7 (`callstack-promote-app-property`) stays a pure property-promotion; CallStack scope remains shared on App.Debug. The deeper scope question (per-context CallStack vs split-config-from-state) is filed in `Documentation/Runtime2/todos.md` 2026-05-08 — substantial work, deserves a focused pass when parallel execution becomes the active concern.

**4. `app.Variables` and `app.Context` shortcuts — REMOVE (settled 2026-05-08).**

Looking at App.this.cs:240–241:
```
public Actor.Context.@this Context => CurrentActor.Context;
public Variables.@this Variables => Context.Variables;
```

Both delegate to "current actor." Under parallel execution (same App, multiple Contexts in flight), these shortcuts return *whichever actor happens to be current* on the calling thread — fragile and meaning-changing depending on `AsyncLocal` propagation. Ingi 2026-05-08: "it actually might be issue having these shortcuts, if we run parallel, we have the same app but new context, so maybe we should remove it."

Settled: **remove both.** All call sites that today reach `app.Variables` or `app.Context` should require an explicit Context (or Actor) reference instead. Likely fold into stage 10 (`app-run-redesign`) since the same caller-cleanup work overlaps. If too big to fold, it's its own small stage.

## Per-channel scope (sub-actor)

Inside an Actor's Channels collection, individual channels have their own scope:

| Item | Scope | Notes |
|------|-------|-------|
| `Channel.@this` (entity) | per-channel | One per registered channel; lives within an Actor's Channels collection. |
| `Channel.@this.App` | back-ref | Set during registration. Likely redundant after `Channel.Channels` back-ref lands in stage 1; not removed in stage 1. |
| `Channel.@this.Channels` | back-ref (NEW in stage 1) | Set during registration. Stream uses this to reach `Channels.Serializers`. |
| `Channel.Stream.@this` | per-channel | Concrete Stream-backed channel. Each Stream channel is its own instance. |

## Per-call scope (sub-actor)

These are scoped to a single outbound I/O call:

| Item | Scope | Notes |
|------|-------|-------|
| `Service.@this` | per-call | Per outbound HTTP/TCP/WS call's I/O scope. Created via `app.Services.New(parent)`. |

## Quick lookup

If you need to know "how many of X exist at runtime":
- One per **app** → on `app.X` directly (shared list above).
- One per **actor** → on `actor.X` (per-actor list above).
- One per **call/channel/step** → see sub-actor scopes.

## Open questions

Updated 2026-05-08 after code dig (per Ingi's instruction "dont assume, dig into the code").

### Settled (latest round, 2026-05-08)

- **Q3 (Channel.App back-ref)** → **remove** (architect's call, Ingi delegated). Once stage 1's `Channel.Channels` back-ref lands, `App` is reachable via `Channels.Actor.App`; direct back-ref is redundant and violates the single-navigation-point discipline. Filed as **stage 20** (`channel-app-backref-drop`).
- **Q4 (app.Cache)** → **shared, intentional** (Ingi: "per app. this is global cache"). No change. Updated annotation in shared list.
- **Q7 (Navigators placement)** → **move to Variables** (Ingi confirmed). Folder `App/Data/Navigators/` → `App/Variables/Navigators/`; namespace `App.Data.Navigators` → `App.Variables.Navigators`; property `app.Navigators` → `app.Variables.Navigators`. Filed as **stage 21** (`navigators-to-variables`).

### Settled (prior round, 2026-05-08)

- **Settings.@this after stage 13** → **shared, one per app** (Ingi). Was Q1.
- **SettingsStore** → **shared, app-level** (Ingi: only `system.sqlite`, per-app, the per-actor allocation is dead drift). Future stage 13 makes it `app.SettingsStore`.
- **`app.Variables` / `app.Context` shortcuts** → **remove** (Ingi: parallel execution problem — under multi-Context concurrency, these return whichever actor is current and that's fragile). Was Q2; folds into stage 10 or a small dedicated stage.
- **`app.CallStack` after stage 7** — there was no question, just a statement. Wording corrected.
- **`app.Build` / `app.Debug` / `app.Testing` stay app-level** for this branch (Ingi: don't change in this branch). Per-context mode flag direction is a separate future plan.
- **`app.Events` / `Context.Events`** → **kept as-is**. Initial "drift" diagnosis was wrong; the three-tier design is intentional (Channel.this.cs:170-192). App-level tier has reader infrastructure but no writer path — half-built. Filed in `Documentation/Runtime2/todos.md` 2026-05-08 for a focused design pass. Out of scope for this branch.
- **`app.Errors.Trail` shared** → **leave shared, no change in this branch**. Architect's lean: shared isn't actively breaking; Trail-as-run-wide is a coherent meaning. Revisit if a per-actor use case surfaces.

### Already settled by code dig (prior round)

- **Snapshot** is per-operation, not shared. Map corrected.
- **Events de facto pattern** — PLang `event.on` writes to `Context.Events` (per-actor), the middle of the three intentional tiers. (Earlier this map called `app.Events` "dead drift" — that was wrong; correction recorded above.)
- **Errors `_current`** is correctly AsyncLocal (flow-scoped); only Trail was at issue (now settled above).

### Still open

(Three previously-open questions settled this round — see "Settled (this round)" below for Q3, Q4, Q7.)

### Acknowledged for the future, not actionable now

- **Per-context spawning from async calls** (Ingi 2026-05-08): when `- call goal Run, dont wait` lands as an async-fire-and-forget construct, a new Context will be created for the spawned work, belonging to the same actor. Not implemented yet; the "per-context" scope vocabulary in this map already accommodates this.
- **Per-context mode flags (Build/Debug/Testing)** — Ingi's `- set debug = true` / `- build my.goal` / `- run tests on /tests` direction. Future plan after this cleanup completes.
- **Events three-tier design** — keep as-is. Future design pass to build the writer path for app-level cross-actor bindings (or remove the tier if unneeded). Filed in `Documentation/Runtime2/todos.md` 2026-05-08.

## Stages affected by this map

- **Stage 1** (`serializers-single-home`) — settled per-actor explicit. Done.
- **Stage 13** (`settings-collection-rework`) — settled: `Settings.@this` shared (one per app); SettingsStore moves from per-actor (dead drift) to App level (`app.SettingsStore` backed by `system.sqlite`); per-actor `_dataSource` field deleted; `app.System.SettingsStore` callers (Goals/Setup, identity provider, llm provider, settings module) sweep to `app.SettingsStore`. Stage 13's brief grows to include the SettingsStore relocation.
- **Stage 10** (`app-run-redesign`) — gains the `app.Context` / `app.Variables` shortcut removal. If too big to fold, carves into its own small stage.
- **Stage 11** (`errors-app-backref-drop`) — scope unchanged. Trail stays shared in this branch.
- **Future cleanup branches** — open questions 3, 4, 7 (Channel.App back-ref redundancy; Cache scope; Navigators placement); plus the Events writer-path design pass (filed in todos.md); plus the per-context-mode-flags direction.

## How to use this map

When carving any future stage:
1. Identify which classes the stage touches.
2. Check the scope of each in this map.
3. Verify the stage's design respects that scope (per-actor classes don't get shared; shared classes don't drift to per-actor).
4. If a stage proposes changing a scope (e.g., "make Settings per-actor"), call that out explicitly and update this map alongside.
