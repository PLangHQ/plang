# Callback — State-Machine Restoration via Seek + Bind

## Why this exists

PLang executes goals as stateless flows. Two real situations need to interrupt that flow and continue it later, in a *different* process:

1. **`ask user`** — the goal needs a value the user has not provided yet. The web request that triggered the goal returns immediately with a form (or an external system holds the request). Hours or days later, the user submits, a fresh process picks up the response, and execution must continue from the line after `ask user` with the user's answer bound to the declared variable.
2. **Errors that need durable retry** — a step fails, the developer wants to capture the failure and retry it later (different shift, different infrastructure state, different inputs). The original process is gone by the time retry happens.

Both cases share the same underlying mechanic: **construct a fresh App, jump to a specific position in a goal, hydrate the variable bag, and continue running from there as if the gap never happened.** No replay of earlier steps. No threads parked in memory. No coroutine state. Just *seek + bind*.

This branch designs that mechanic.

## The shape

One **resume primitive** at the engine level. Two **issuers** that produce values consumed by it. Each issuer follows its own capture policy because the size and source of the carried state differ, but the resume contract is identical.

```
                   ┌──────────────┐
   ask user ────►  │              │
                   │  Callback    │  ──►  - run %callback%  ──►  Engine.Seek(g,s,a, vars, snapshot)
   error.handler   │  (record)    │
   ────►  %!error.callback%  ────►│              │
                   └──────────────┘
```

A `Callback` is a record (full schema in "The seek primitive" below). Issuers produce a `Data<Callback>`; the whole envelope is signed. The engine consumes it through `Seek`. Storage and transport (wire, DB, file) are developer-driven PLang code — neither issuer nor engine cares where the envelope lives between issue and resume.

## Settled design

### Seek + bind, never replay

Resume jumps directly to `(goal X, step Y, action Z)` and continues from there. No prior step is re-executed. No actions before Z within step Y are re-executed. The engine, on first tick after a `Seek`, lands at Z with the carried Variables and ISnapshotted state already bound, and continues to Z+1 (or step Y+1 if Z was the last action in Y).

This is the fundamental difference from event-sourcing-flavoured durable execution: we *don't* require producer steps to be deterministic. We just snapshot enough state that the world *looks the same* at resume.

### Two issuers, one primitive

| Aspect | `ask user` | error retry |
|---|---|---|
| Capture policy | Developer-declared minimal slice (`vars: %x%, %y%`) | Full app state via `ISnapshotted` |
| Storage medium | Wire (hidden form field, encrypted, signed) | Server-side, developer's choice (DB, file, queue) |
| Size constraint | Tight (HTTP form fields, tokens) | Effectively none |
| Post-resume contract | Lossy — developer reloads `%order%` from `%orderId%` | Transparent — state is as it was |
| Materialisation | At handler return time (action returns `Callback` as its `Data` value) | Lazily on read of `%!error.callback%` |
| Variable timing | Values at the moment of issuance | Values at *throw time* (handler scope is isolated) |

The handler-end vs. throw-time asymmetry is deliberate: `ask user` exists because the developer chose to issue a callback, so handler-end-state is the issuance moment. Error-retry is automatic, and the developer writing the error handler should not have to worry about whether their convenience-variable assignments collide with names from the failed code. Frozen view of the moment things broke.

### Per-type `ISnapshotted` — three buckets

Each OBP `@this` type declares its own snapshot discipline. No central registry, no classifier table.

```csharp
public interface ISnapshotted
{
    void Capture(SnapshotWriter w);
    static abstract void Restore(SnapshotReader r, RestoreContext ctx);
}
```

Three buckets emerge naturally:

1. **Snapshot-and-restore** — type implements `ISnapshotted`. Variables (per-actor), Errors.Trail, App.Providers (registry-layer selection state), App._statics, plus any third-party `IProvider` that opts in. Captured on issuance, restored on resume.
2. **Reconstruct-on-build** — type does *not* implement `ISnapshotted`. Modules, Goals, Catalog, Types, Navigators, Config, Settings (sqlite-backed), FileSystem, Events, Channels, Debug, all built-in `IProvider` instances. Normal App construction rebuilds them; resume just runs construction.
3. **Drop** — runtime-only state with no resume relevance. MemoryStepCache (see "Cache as audit-derived" below for the alternative), live CallStack tree, Timing tier, Children-as-history, in-flight network state. Just gone.

The classifier *is* the type system. A new subsystem's author makes the bucket choice by deciding whether to implement the interface. No coordination across the codebase.

### Islands rule — values only, no graph identity

Each `ISnapshotted` captures **values**, not graph identity. If `Cache` holds an entry that came from `Variables`, `Cache.Capture` deep-clones the value into its own payload. Variables snapshots itself separately. Two independent islands.

After `Restore`, no pointer fix-up phase, no inter-type ordering dependency. Each subsystem rehydrates from its own bytes. References across types are by *name* (the same way PLang already works at runtime), resolved at lookup time, never restored as object pointers.

**Rejected:** ref-capture syntax like `{name:"x", value:{ref:"Variables:name"}}`. We brainstormed it. No use case survived examination — every candidate (cache as live view, indexes, late-bound config, current-selection pointers) collapsed into either "use the existing language mechanism" or "use the bucket system, this isn't what it's for." Size concerns belong to the serializer (blob-dedup with internal IDs), not the API surface. Closed.

**Limitation accepted:** intra-island graph identity (two list entries pointing to the same object) is not preserved across resume — JSON loses it. PLang is value-based; this rarely bites. Future serializer work can add `PreserveReferencesHandling`-style ID/IDREF if a real case appears.

### Variable capture semantics for error-retry

`%!error.callback%` is a **synthetic, lazily materialised** PLang property. It has no cost until read.

When read, the runtime computes Variables-at-throw-time by:
1. Take current `App.Variables.@this`.
2. Walk the callstack diff stream, filtered to events with `timestamp > error.throwTime`.
3. Reverse-apply each `Set` (restore each variable's `Before` value).

This means:
- Error handler mutations (`- set %name% = "ble"` inside the handler) appear in the live App's Variables but are *reversed* in the callback's view.
- The developer writing the error handler never has to reason about variable name collisions with the failed code.

**Diff is required.** When `Flags.Diff` is off and an error occurs, the runtime auto-flips it for the duration of error processing. Cost is paid only on the error path, which is rare. No conditional code path on the consumer side.

Providers do **not** have diff tracking. The callback captures their registry-layer selection state (see "Providers — two layers" below) at materialisation time. Convention: error handlers should not mutate provider selections. If they do, the callback reflects the post-handler selection — the runtime will not catch this. Honest asymmetry: vars get rich treatment because we have rich tooling for them; providers get the pragmatic one.

### Cache as audit-derived

`MemoryStepCache` does not implement `ISnapshotted` directly. Instead, the cache's contribution to the snapshot is computed *from the callstack audit trail* at materialisation time, exploiting a property already true of the design: every cache mutation flows through a Call frame whose Action is a `cache.*` action.

Mechanism:

1. Walk the callstack tree and collect distinct keys touched by `cache.set` (and `cache.tryAdd`) Calls during the run. Cache action handlers `Call.Tag("cache.key", resolvedKey)` after variable resolution, so the snapshot reads tags rather than re-parsing parameters.
2. For each key, call `cache.GetAsync(key)` against the live cache. The current value already reflects the net of every set/remove/re-set during the run — no replay engine needed.
3. Capture each `(key, value, absoluteExpiry)` tuple. Skip keys whose lookup returns null (set-then-removed). Tuples are stored as part of the App's snapshot bag, keyed by a synthetic `cache.snapshot` slot.

On restore, replay each tuple via `SetAsync` with TTL = `absoluteExpiry - now`. Negative TTLs mean the entry expired between issue and resume — skip. Original semantic preserved: `tryAdd`-based nonce keys remain present for their real remaining lifetime, replay window stays closed.

This approach has three properties worth calling out. **(a)** It's pluggable-cache friendly — `ICache` exposes `GetAsync`/`SetAsync`, nothing else; Redis or any other backend works without changes. **(b)** It's per-run scoped — only keys this run touched are in the snapshot, not whatever else is in the cache. **(c)** It generalises — the callstack as audit trail is a substrate any future "I did this at runtime, replay it" subsystem can use the same way.

### Position semantics

`Seek` lands at `(goal X, step Y, action Z)` and **re-executes action Z**. This is intentional — the developer's handler has presumably addressed the cause of failure (network restored, file recreated, %name% set to a valid value), and re-running the action with prepared state is the natural retry shape. If it fails again, a new `%!error.callback%` materialises in that error's handler. Self-similar.

For `ask user`, position is the action *after* the ask — the ask itself is satisfied by the bound `%name%` from the wire payload, and execution resumes at the next action.

### Names vs values — the synthesis

After walking every subsystem in `App/`, one principle organises the whole snapshot story:

> **Snapshots store names; they store values only when the name doesn't determine the value.**

- **Values side** — Variables and Cache. The name is just a key; the value is real state that can't be re-derived from anywhere else. These need full payloads (carried via `ISnapshotted` for Variables; carried via the audit-derived path for Cache).
- **Names side** — provider selections, identity, datasource, encryption choice, mode flags, runtime-loaded DLLs. The name is a stable reference into system state that exists outside the callback. The provider/registry/store knows how to resolve it.

The names-side carries qualifications when needed — a runtime-registered provider's name is paired with its DLL path so the loader knows how to find it; a cache entry's key is paired with its absolute expiry so TTL semantics survive the resume gap. These are *qualifications of the name*, not full values.

**Two separate trust layers gate a resume:**

1. **Signature integrity** — the signed `Data<Callback>` envelope guarantees the captured contents weren't tampered with. Without a valid signature, the resume is rejected wholesale.
2. **Referent integrity** — names in the snapshot assume the system state they reference still exists. If `myidentity2` was deleted between issue and resume, the resume fails. Same shape as goal_hash mismatch (redeployed goal → invalid). The signature does *not* guarantee referent integrity, by design — those are different layers.

Both must hold. Future edge cases ("what if X was deleted between issue and retry?") get answered by these two layers: signature integrity fails loud as auth rejection; referent integrity fails loud as resolve-by-name failure. No silent degradation.

### Cross-process causal trace

When a callback resumes, the new run gets a fresh callstack with its own root Call. The engine writes a `CallbackOrigin` typed item onto that root via `Call.SetItem<T>` — see the `Callback` schema above for the field; the `Call.Cause` field stays same-process only.

## PLang surfaces

### `%!error.callback%`

Synthetic, read-only, lazy. Available inside any error handler scope. Materialises a `Callback` record on read. Multiple reads in one handler with var mutations between them produce different callbacks — convention: read once at the end of the handler.

```plang
- insert into users, name=%name%
   on error
      - write %!error.callback% to file callbacks/%!error.id%.bin
```

### `- run %callback%`

Consumes a callback. Verifies signature, decrypts vars (sensitive ones only, in v1), confirms `goal_hash` against current build (mismatch = signed-but-stale → hard error), constructs an App with the seek directive, hands off to the engine.

```plang
Recover
- read file callbacks/%id%.bin, write to %callback%
- run %callback%
```

### Ask-user `vars:` annotation

Step-level on issuing actions only:

```plang
- ask user 'What is your name?', vars: %orderId%, write to %name%
```

`vars:` is **not** a Step-level concept (rejected in design — would imply error-retry needs declared vars too, which it doesn't). It belongs to ask-family actions specifically.

## The seek primitive — engine-side

The wire/storage shape is a signed `Data<Callback>` envelope — single blob, one signature covering the whole content. Tampering with any field (actor name, goal_hash, position, captured values, expiry) breaks the signature; the envelope is rejected wholesale. Leans on `Data` already being the universal envelope type — no separate "header" sitting outside the signature.

The engine-internal view of that envelope, after verification + decryption, is a `ResumeDirective`:

```csharp
record Callback(
    string GoalHash,
    int StepIndex,
    int ActionIndex,
    string ActorName,                 // System / Service / User
    Dictionary<string, object?> VariablesByActor,  // per-actor name → value bag
    Dictionary<string, object?> Selections,        // App.Providers + identity choices, by name
    List<CacheEntry> CacheEntries,                 // (key, value, absoluteExpiry)
    Dictionary<string, object?> Statics,           // App._statics until that TODO closes
    List<IError> ErrorTrail,                       // read-only at restore
    bool BuildEnabled,
    bool TestingEnabled,
    DateTimeOffset Expiry,
    CallbackOrigin? Origin
    // Signature is on the Data<Callback> envelope, not a Callback field.
);
```

When a `Callback` is consumed by `Engine.Seek(callback)`:
1. App construction runs normally for reconstruct-on-build types (Modules, Goals, Catalog, Settings, FileSystem, Events, Channels, Debug, built-in Providers).
2. `App.Providers` registry replays runtime-registered (non-default) providers and applies the captured default selections.
3. Per-actor `Variables` instances are populated from `VariablesByActor` after `RegisterDefaults` runs. `App._statics` is populated from `Statics`. `Errors.Trail` is populated from `ErrorTrail`.
4. Cache entries are replayed via `cache.SetAsync` with TTL = `absoluteExpiry - now` (skip if negative).
5. Engine's main loop, on first tick, navigates to `(GoalHash → goal, StepIndex, ActionIndex)` and runs from there.
6. Root Call gets `SetItem(Origin)` if Origin is non-null.

That's the entire engine-side surface. The `Callback` record is the shared shape between issuance, wire/storage, and engine consumption. Everything else (signing, encryption, transport, UI, store/load actions) is layers on top.

```csharp
record CallbackOrigin(string PriorRunId, string PriorCallId, DateTimeOffset IssuedAt);
```

The same-process `Call.Cause` field is *not* extended — its invariant ("live ref, same process only") stays clean. Cross-process identity goes through the typed metadata bag (`Call.SetItem<CallbackOrigin>`), exactly the use case the bag was designed for.

## Subsystem inventory — final buckets after the walk

Walked type-by-type through `App/`. Refined buckets below.

| Subsystem | Bucket | Notes |
|---|---|---|
| `App.Variables` (per actor — System, Service, User) | snapshot-and-restore | Existing `Snapshot()` partition is exactly the boundary: skip `!`-prefixed system vars, skip `DynamicData` (Now/GUID/`!app`/`MyIdentity`), skip `SettingsVariable` (sqlite-backed). Capture `(name, value, Type, Properties)` for everything else. |
| `App.Errors.Trail` | snapshot-and-restore | Read-only after restore. Resumed run needs `%!error.trail%` to read naturally. |
| `App.Providers` | snapshot-and-restore (registry layer only) | See "Providers — two layers" below. |
| `App._statics` | snapshot-and-restore (with caveat) | App-scoped mutable dict. Snapshot until `TODO: Replace with goal-backed dynamic property` closes. Flagged in the doc as a known fragility. |
| `App.Cache` (`MemoryStepCache`) | drop the implementation; snapshot via callstack audit | See "Cache as audit-derived" above. Tuples live in the snapshot bag, not on `ICache`. |
| `App.Modules` | reconstruct | Deterministic from assembly scan + DLL discovery. |
| `App.Goals` | reconstruct | `.pr.json` on disk; `goal_hash` invariant gates any drift. |
| `App.Catalog` | reconstruct | Pure derivation from Modules. |
| `App.Types` | reconstruct | Static registry. |
| `App.Navigators` | reconstruct | Computed from Types. |
| `App.Config` | reconstruct | Loaded from `.pr.json`. |
| `App.Settings` (`SettingsVariable` + `SqliteSettingsStore`) | reconstruct | Sqlite file persists; reopen on resume. |
| `App.FileSystem` | reconstruct | Handles don't survive process death. |
| `App.Channels` | reconstruct | Std streams reconstruct trivially. Memory channels with buffered data are dropped — channels are I/O, not inter-step state. |
| `App.Events` (lifecycle) | reconstruct | Re-attach during App boot. |
| `App.Debug` | reconstruct | Event handlers re-register from CLI flags. |
| `App.Testing` / `App.Build` mode flags | inside the Callback record | `BuildEnabled` / `TestingEnabled` fields on `Callback`. Not ISnapshotted payload. |
| `App.Actor` instances | reconstruct | Three actors constructed normally. Per-actor Variables are restored as a separate snapshot step. |
| Actor identity | name-only, in the Callback record | `Identity.Name` carried; provider's `GetOrCreateDefaultAsync(name)` resolves on resume. The `Identity` object itself is never carried — referent integrity gates the resume. |
| `App.Test` runner | reconstruct | No live state. |
| Built-in `IProvider` instances | reconstruct | None implements `ISnapshotted` today (Ed25519 keys on disk, HTTP/LLM/Identity wrap external state, etc.). The interface is a future-facing hook for third parties that hold inter-action mutable state. |
| Live `App.CallStack` tree | drop (live tree) — but Caller chain is captured separately | See "CallStack as positional context" below. |

### Providers — two layers

The `App.Providers` registry holds two kinds of mutable state, both load-bearing:

1. **Default selections per type.** `SetDefault(IIdentityProvider, "myidentity2")` flips which named provider is returned by `Get<IIdentityProvider>()`. The snapshot captures, per type, the current default name.
2. **Runtime registrations.** Anything registered after `RegisterDefaults()` — typically by PLang `- use 'mycrypto.dll' for encryption` style actions. The snapshot captures `(type, name, source)` tuples where `source` is the DLL path or whatever identifier the loader needs. Built-in registrations from `RegisterDefaults` are *not* in the snapshot — they reconstruct on App boot.

On restore: `RegisterDefaults()` runs as normal, then runtime registrations are loaded and registered, then default selections are applied. Concrete chacha example walked in the conversation transcript — composes cleanly.

The provider *instances* themselves remain reconstruct-on-build. None of the built-ins hold inter-action mutable state. Third-party providers that do can opt in to `ISnapshotted` separately — that's a per-instance concern, orthogonal to the registry-layer snapshot.

### CallStack as positional context

Not history (drop), not timing (drop). The *Caller chain* of the throwing Call is captured as a sequence of `(goal, step, action)` resume points so when the resumed action finishes and unwinds, control returns to the caller correctly. Concretely: if goal A's step 3 called goal B and B's step 2 errored, resume needs to know "after this resumed B step 2 finishes, return to A step 3 action N+1." A small list of positions, not a tree.

The CallStack *is* used at materialisation time as the audit substrate for the cache snapshot (walk Calls, find `cache.*` action frames, read `cache.key` tags). That's a read of the live tree, not a serialisation of it.

## Smallest meaningful first cut

One in-process test, no wire, no storage, no UI:

```csharp
[Fact]
public async Task Error_callback_resumes_at_failed_action_with_throwtime_vars()
{
    var app = new App(/* goal with: set %x%=1; throw; set %x%=2 */);
    await app.Run();  // throws
    var callback = app.GetItem<ErrorCallback>();  // synthetic %!error.callback%

    var resumed = new App(callback.ToResumeDirective());
    await resumed.Run();

    Assert.Equal(2, resumed.Variables["x"]);  // step 3 ran, %x% is 2
    Assert.Equal(1, callback.Variables["x"]); // throw-time view, never 2
}
```

If that passes, every remaining piece (signing, encryption, wire format, storage backends, ask-user web flow) is mechanical. The seek primitive is the keystone — get it green first, layer the rest.

## Open threads — for future sessions

None blocks the seek primitive (smallest first cut).

1. **Wire format and key management for ask-user.** Signing scheme over `Data<Callback>`, encryption (AES-GCM probably), key rotation, expiry semantics, how `goal_hash` is computed and stored.
2. **Storage shape for error-retry.** Recommended patterns — file, DB provider, queue. Probably no runtime opinion; developer chooses via PLang. But might want a built-in `callback.store` / `callback.load` action pair for ergonomics.
3. **`goal_hash` mismatch handling.** Signed but stale → hard error. Settled in principle; surface design (error code, `%!callback.error%` shape) is open.
4. **PLang surface for `- run %callback%`.** Is it a normal action in a `callback` module, or a special infrastructure verb? Lean: normal action.
5. **Ask-user builder annotation** — exact `.pr.json` shape for `vars: %x%, %y%` on a step.
6. **`CallbackOrigin` payload** — what fields are useful for telemetry stitching beyond the three currently sketched.
7. **`App._statics` TODO closure.** When that's replaced with goal-backed dynamic property, drop `Statics` from the `Callback` schema.
8. **Cache snapshot as a known-cost item.** If the per-key `GetAsync` walk becomes a bottleneck for runs with thousands of cache touches, batch via a single `GetManyAsync` extension on `ICache`. Not v1.

## Settled rejections (context for future sessions)

- **`Data.Pause` lane.** Callback is a successful `Data` value whose payload happens to be a Callback record. Engine has one extra branch ("is the value a Callback? unwind without continuing"). No new Ok/Fail/Pause trichotomy.
- **Step-level `vars:` capture annotation.** Only on ask-family actions. Error-retry doesn't need declared vars — it captures everything.
- **`Cause` extension to cross-process.** Stays same-process only. Cross-process causality goes through `Call.SetItem<CallbackOrigin>`.
- **Replay-style resume.** Never. Seek + bind only.
- **Re-running producer steps for stateful provider re-acquisition.** Not in v1. Built-in providers are pure or wrap external state (no inter-action mutable state). Third parties opt in via `ISnapshotted`. Event-sourcing is a separate, larger conversation.
- **Ref-capture across islands.** Brainstormed, no real use case, closed. Values-only. Serializer-level blob dedup is the answer if size becomes a problem.
- **`MemoryStepCache` implementing `ISnapshotted`.** Rejected in favour of the audit-derived approach (walk Calls, ask the cache for current state of touched keys). Cleaner abstraction boundary.
- **Memory channel buffers carried across resume.** Channels are I/O, not inter-step state. If a developer treats a memory channel as state, that's smell.
- **Capturing `Identity` object instead of name.** Names + referent integrity is the contract; objects are never carried. Same for every selection-shaped piece of state (provider, datasource, encryption, identity).
- **"Header" outside the signed envelope.** Whole `Data<Callback>` is signed; nothing meaningful sits outside the signature. Tampering is an all-or-nothing rejection.
- **Resumer's identity replacing captured identity.** The signed envelope authorises; the captured `ActorName` + `Identity.Name` dictate what the resumed code runs as. Who triggers `- run %callback%` is independent of what privilege the resumed code holds.
