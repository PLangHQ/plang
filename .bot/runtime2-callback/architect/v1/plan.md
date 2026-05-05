# Callback — State-Machine Restoration (Phase 1)

This is the **foundation phase**: the resume mechanism itself, end-to-end in-process. No signing, no storage, no wire. One App throws, one App resumes from the captured Callback, control lands at the failed action with throw-time variables. Phase 2 (`v2/plan.md`) layers signing + developer-chosen storage on top. Phase 3 (future `v3/plan.md`) layers encryption + HTTP wire transport for ask-user.

## Why this exists

PLang executes goals as stateless flows. Two real situations need to interrupt that flow and continue it later, in a *different* process:

1. **`ask user`** — the goal needs a value the user has not provided yet. The web request that triggered the goal returns immediately with a form (or an external system holds the request). Hours or days later, the user submits, a fresh process picks up the response, and execution must continue at the `ask user` line with the user's answer bound to the declared variable.
2. **Errors that need durable retry** — a step fails, the developer wants to capture the failure and retry it later (different shift, different infrastructure state, different inputs). The original process is gone by the time retry happens.

Both cases share the same underlying mechanic: **construct a fresh App, jump to a specific position in a goal, hydrate the variable bag, and continue running from there as if the gap never happened.** No replay of earlier steps. No threads parked in memory. No coroutine state. Just *bind state, jump to position, run*.

This branch designs that mechanic.

## The shape

One **resume mechanism** at the engine level — `App.Run` accepts a Callback as its entry point, instead of a goal name. Two **issuers** that produce Callbacks consumed by it. Each issuer follows its own capture policy because the size and source of the carried state differ, but the resume contract is identical.

```
                   ┌──────────────┐
   ask user ────►  │              │
                   │  Callback    │  ──►  - run %callback%  ──►  App.Run(callback)
   error.handler   │  (record)    │
   ────►  %!error.callback%  ────►│              │
                   └──────────────┘
```

A `Callback` is a record (full schema in "Resume — engine-side" below). Issuers produce a `Data<Callback>`; the whole envelope is signed. The engine consumes it through `App.Run`. Storage and transport (wire, DB, file) are developer-driven PLang code — neither issuer nor engine cares where the envelope lives between issue and resume.

## Settled design

### Bind, jump, run — never replay

Resume jumps directly to `(goal X, step Y, action Z)` and continues from there. No prior step is re-executed. No actions before Z within step Y are re-executed. The engine, when constructed with a Callback, lands at Z with the carried Variables and ISnapshotted state already bound, and continues from there.

This is the fundamental difference from event-sourcing-flavoured durable execution: we *don't* require producer steps to be deterministic. We snapshot enough state that the world *looks the same* at resume.

### Two issuers, one mechanism

| Aspect | `ask user` | error retry |
|---|---|---|
| Capture policy | Developer-declared minimal slice (`vars: %x%, %y%`) | Full app state via `ISnapshotted` |
| Storage medium | Wire (hidden form field, encrypted, signed) | Server-side, developer's choice (DB, file, queue) |
| Size constraint | Tight (HTTP form fields, tokens) | Effectively none |
| Post-resume contract | Lossy — developer reloads `%order%` from `%orderId%` | Transparent — state is as it was |
| Materialisation | Action returns `Callback` as its `Data` value | Lazily on read of `%!error.callback%` |
| Variable timing | Values at issuance | Values at *throw time* (handler scope is isolated) |
| Resume position | At the `ask` action — handler distinguishes fresh vs resumed | At the failed action — re-executed |

The handler-end vs. throw-time asymmetry is deliberate: `ask user` exists because the developer chose to issue a callback, so handler-end-state is the issuance moment. Error-retry is automatic, and the developer writing the error handler should not have to worry about whether their convenience-variable assignments collide with names from the failed code. Frozen view of the moment things broke.

### Per-type `ISnapshotted` — three buckets

Each OBP `@this` type declares its own snapshot discipline. No central registry, no classifier table.

```csharp
public interface ISnapshotted
{
    void Capture(Snapshot.@this s);
    static abstract void Restore(Snapshot.@this s, Context.@this ctx);
}
```

`Snapshot.@this` owns the read/write surface of the captured payload — appending typed entries on Capture, retrieving them on Restore. `Context.@this` is the existing actor context the App already passes around. No invented `Reader`/`Writer`/`RestoreContext` types — we use the OBP types we already have.

Three buckets emerge naturally:

1. **Snapshot-and-restore** — type implements `ISnapshotted`. Variables (per-actor), `Errors.Trail`, `App.Providers` registry-layer state, `App._statics`, plus any third-party `IProvider` that opts in.
2. **Reconstruct-on-build** — type does *not* implement `ISnapshotted`. Modules, Goals, Catalog, Types, Navigators, Config, Settings (sqlite-backed), FileSystem, Events, Channels, Cache, Debug, all built-in `IProvider` instances. Normal App construction rebuilds them; resume just runs construction.
3. **Drop** — runtime-only state with no resume relevance. Live `App.CallStack` tree, Timing tier, Children-as-history, in-flight network state. Just gone.

The classifier *is* the type system. A new subsystem's author makes the bucket choice by deciding whether to implement the interface. No coordination across the codebase.

### Islands rule — values only, no graph identity

Each `ISnapshotted` captures **values**, not graph identity. Each subsystem rehydrates from its own bytes. References across types are by *name* (the same way PLang already works at runtime), resolved at lookup time, never restored as object pointers.

After `Restore`, no pointer fix-up phase, no inter-type ordering dependency. Two independent islands per `ISnapshotted` type.

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

Providers do **not** have diff tracking. The callback captures their registry-layer selection state at materialisation time. Convention: error handlers should not mutate provider selections. If they do, the callback reflects the post-handler selection — the runtime will not catch this. Honest asymmetry: vars get rich treatment because we have rich tooling for them; providers get the pragmatic one.

### Cache is not snapshotted

Cache is a **performance hint**, not state. The line between Variables and Cache is the line between "must survive resume" and "can be lost on resume." If a developer needs a value to be there on resume, the right tool is Variables. Capturing cache would blur the distinction and create two flavours of state with subtly different ergonomics — a clutter that compounds over time.

Walked use cases that survived examination:

- **Memoisation of an expensive call** — resume cache-misses, recomputes. Same as eviction. Slower, correct.
- **Dedup of a fetched object across steps** — same shape, re-fetches on resume.
- **Nonce replay prevention via `TryAddAsync`** — correctness-sensitive, but `MemoryStepCache` is the wrong backend. Use a durable cache (Redis, sqlite) for nonces.
- **"I want this on resume"** — wrong tool. Use Variables.

`MemoryStepCache` does **not** implement `ISnapshotted`. Resumed App gets a fresh empty cache. Pluggable durable backends (Redis) don't participate in snapshot/restore either — they're already durable, the entries are still there on resume by virtue of the backend's nature. ICache stays a pure performance abstraction.

This sharpens the rule for developers: **if it must survive resume, it's a Variable.** One tool per concern.

### Position semantics

Resume always lands **at the action that's the focus** — same rule for both modes:

- **Error-retry**: at the failed action. Re-executed with the prepared state. If the cause was addressed (network restored, file recreated, %name% set to a valid value), it succeeds. If it fails again, a new `%!error.callback%` materialises in that error's handler. Self-similar.
- **Ask-user**: at the `ask` action. The handler distinguishes fresh-call from resumed-call (the bound input from the directive is the signal — coder figures out the exact mechanism), and on resume returns `Ok(boundValue)` instead of issuing a Callback. Same handler, two modes.

The simplification: there is no "before the action" or "after the action" position — the action *is* the resume point. Whatever the action's logic is, it runs with the state the directive provides.

### Names vs values — the synthesis

After walking every subsystem in `App/`, one principle organises the whole snapshot story:

> **Variables are the values that survive resume. Everything else is a name.**

- **Variables** are the only piece of state captured as full payloads. Their values can't be re-derived from anywhere else — that's why they're Variables.
- **Everything else** that needs to persist across resume — provider selections, identity, datasource, encryption choice, mode flags, runtime-loaded DLLs — is captured as a *name*. The provider/registry/store knows how to resolve the name to its current value. Names may carry small qualifications (a runtime-registered provider's name is paired with its DLL path so the loader knows how to find it) but they're not full values.
- **`Errors.Trail`** and **`App._statics`** are the small messy exceptions where there's no clean name to dereference, but their content is constrained — error records are small and known-shape; `_statics` is provisional until the TODO closes.

**Two separate trust layers gate a resume:**

1. **Signature integrity** — the signed `Data<Callback>` envelope guarantees the captured contents weren't tampered with. Without a valid signature, the resume is rejected wholesale.
2. **Referent integrity** — names in the snapshot assume the system state they reference still exists. If `myidentity2` was deleted between issue and resume, the resume fails. Same shape as goal_hash mismatch (redeployed goal → invalid). The signature does *not* guarantee referent integrity, by design.

Both must hold. Edge cases ("what if X was deleted between issue and retry?") get answered by these two layers: signature integrity fails loud as auth rejection; referent integrity fails loud as resolve-by-name failure. No silent degradation.

## PLang surfaces

### `%!error.callback%`

Synthetic, read-only, lazy. Available inside any error handler scope. Materialises a `Callback` record on read. Multiple reads in one handler with var mutations between them produce different callbacks — convention: read once, in one place.

```plang
- insert into users, name=%name%
   on error call goal HandleError

HandleError
- write %!error.callback% to file callbacks/%!error.id%.bin
```

`on error call goal X` is the right shape — it cleanly separates the failure path into its own goal where steps can do whatever they need (write, log, queue, dispatch). Inline `on error` with steps underneath is not how PLang error handlers compose.

### `- run %callback%`

Consumes a callback. Verifies signature, decrypts vars (sensitive ones only, in v1), confirms `goal_hash` against current build (mismatch = signed-but-stale → hard error), constructs an App with the callback as entry point, runs.

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

## Resume — engine-side

The wire/storage shape is a signed `Data<Callback>` envelope — single blob, one signature covering the whole content. Tampering with any field (actor name, goal_hash, position, captured values, expiry) breaks the signature; the envelope is rejected wholesale. Leans on `Data` already being the universal envelope type — no separate "header" sitting outside the signature.

```csharp
record Callback(
    string GoalPrPath,                             // relative path to .pr file (App.Goals loads goals lazily by path)
    string GoalHash,                               // = goal.Hash (SHA-256 of name + step text); drift = hard error on resume
    int StepIndex,
    int ActionIndex,
    string ActorName,                              // System / Service / User
    Dictionary<string, object?> VariablesByActor,  // per-actor name → value bag
    Dictionary<string, object?> Selections,        // App.Providers + identity choices, by name
    Dictionary<string, object?> Statics,           // App._statics until that TODO closes
    List<IError> ErrorTrail,                       // read-only at restore
    bool BuildEnabled,
    bool TestingEnabled
    // Signature lives on the Data<Callback> envelope (Data.Signature), not a Callback field.
    // Validity / expiry, when wanted, also lives in Data.Signature — populated by explicit
    // `- sign %callback% expires in N`. Default signing is integrity-only, no expiry.
);
```

`GoalPrPath` is required because `App.Goals` loads goals lazily by path — without it the resumer can't locate the goal to load. `Goal.Hash` (already on `PLang/App/Goals/Goal/this.cs:121`, SHA-256 of name + concatenated step text) is what `GoalHash` carries; no new hashing infrastructure.

When a `Callback` is consumed by `App.Run(callback)`:

1. App construction runs normally for reconstruct-on-build types (Modules, Goals, Catalog, Settings, FileSystem, Events, Channels, Cache, Debug, built-in Providers).
2. `App.Providers` registry replays runtime-registered (non-default) providers and applies the captured default selections.
3. Per-actor `Variables` instances are populated from `VariablesByActor`. `App._statics` is populated from `Statics`. `Errors.Trail` is populated from `ErrorTrail`.
4. The engine's main loop, on first tick, navigates to `(GoalPrPath → goal, StepIndex, ActionIndex)` and runs from there. `goal.Hash` is checked against `callback.GoalHash` at load — mismatch is a hard referent-integrity failure (signed but stale; goal was redeployed).

That's the entire engine-side surface. The `Callback` record is the shared shape between issuance, wire/storage, and engine consumption. Everything else (signing, encryption, transport, UI, store/load actions) is layers on top.

There is **no `Seek` verb**. The engine has one main loop; what differs between a normal start and a resume start is *where the loop's first tick lands*, not the existence of a different verb. `App.Run(goal)` and `App.Run(callback)` are two ways of configuring the entry point.

There is **no cross-process causal trace** in the runtime data model. Telemetry stitching between the original run and the resumed run happens at the log layer by correlating callback identity (signature digest, expiry timestamp). `Call.Cause` stays same-process only — its invariant ("live ref, same process only") is preserved.

## Subsystem inventory — final buckets after the walk

Walked type-by-type through `App/`.

| Subsystem | Bucket | Notes |
|---|---|---|
| `App.Variables` (per actor — System, Service, User) | snapshot-and-restore | Existing `Snapshot()` partition is exactly the boundary: skip `!`-prefixed system vars, skip `DynamicData` (Now/GUID/`!app`/`MyIdentity`), skip `SettingsVariable` (sqlite-backed). Capture `(name, value, Type, Properties)` for everything else. |
| `App.Errors.Trail` | snapshot-and-restore | Read-only after restore. Resumed run needs `%!error.trail%` to read naturally. |
| `App.Providers` | snapshot-and-restore (registry layer only) | See "Providers — two layers" below. |
| `App._statics` | snapshot-and-restore (with caveat) | App-scoped mutable dict. Snapshot until `TODO: Replace with goal-backed dynamic property` closes. Flagged as a known fragility. |
| `App.Modules` | reconstruct | Deterministic from assembly scan + DLL discovery. |
| `App.Goals` | reconstruct | `.pr.json` on disk; `goal_hash` invariant gates any drift. |
| `App.Catalog` | reconstruct | Pure derivation from Modules. |
| `App.Types` | reconstruct | Static registry. |
| `App.Navigators` | reconstruct | Computed from Types. |
| `App.Config` | reconstruct | Loaded from `.pr.json`. |
| `App.Settings` (`SettingsVariable` + `SqliteSettingsStore`) | reconstruct | Sqlite file persists; reopen on resume. |
| `App.FileSystem` | reconstruct | Handles don't survive process death. |
| `App.Channels` | reconstruct | Std streams reconstruct trivially. Memory channels with buffered data are dropped — channels are I/O, not inter-step state. |
| `App.Cache` (`MemoryStepCache`) | reconstruct (empty) | Cache is a hint, not state. See "Cache is not snapshotted" above. |
| `App.Events` (lifecycle) | reconstruct | Re-attach during App boot. |
| `App.Debug` | reconstruct | Event handlers re-register from CLI flags. |
| `App.Testing` / `App.Build` mode flags | inside the Callback record | `BuildEnabled` / `TestingEnabled` fields on `Callback`. |
| `App.Actor` instances | reconstruct | Three actors constructed normally. Per-actor Variables are restored as a separate snapshot step. |
| Actor identity | name-only, in the Callback record | `Identity.Name` carried via `Selections`; provider's `GetOrCreateDefaultAsync(name)` resolves on resume. The `Identity` object itself is never carried — referent integrity gates the resume. |
| `App.Test` runner | reconstruct | No live state. |
| Built-in `IProvider` instances | reconstruct | None implements `ISnapshotted` today. The interface is a future-facing hook for third parties that hold inter-action mutable state. |
| Live `App.CallStack` tree | drop (live tree) — Caller chain captured as positional context | See "CallStack as positional context" below. |

### Providers — two layers

The `App.Providers` registry holds two kinds of mutable state, both load-bearing:

1. **Default selections per type.** `SetDefault(IIdentityProvider, "myidentity2")` flips which named provider is returned by `Get<IIdentityProvider>()`. The snapshot captures, per type, the current default name.
2. **Runtime registrations.** Anything registered after `RegisterDefaults()` — typically by PLang `- use 'mycrypto.dll' for encryption` style actions. The snapshot captures `(type, name, source)` tuples where `source` is the DLL path or whatever identifier the loader needs. Built-in registrations from `RegisterDefaults` are *not* in the snapshot — they reconstruct on App boot.

On restore: `RegisterDefaults()` runs as normal, then runtime registrations are loaded and registered, then default selections are applied.

The provider *instances* themselves remain reconstruct-on-build. None of the built-ins hold inter-action mutable state. Third-party providers that do can opt in to `ISnapshotted` separately — that's a per-instance concern, orthogonal to the registry-layer snapshot.

### CallStack as positional context

Not history (drop), not timing (drop). The *Caller chain* of the throwing Call is captured as a sequence of `(goal, step, action)` resume points so when the resumed action finishes and unwinds, control returns to the caller correctly. Concretely: if goal A's step 3 called goal B and B's step 2 errored, resume needs to know "after this resumed B step 2 finishes, return to A step 3 action N+1." A small list of positions, not a tree.

## Smallest meaningful first cut

One in-process test, no wire, no storage, no UI:

```csharp
[Fact]
public async Task Error_callback_resumes_at_failed_action_with_throwtime_vars()
{
    var app = new App(/* goal with: set %x%=1; throw; set %x%=2 */);
    await app.Run();  // throws

    var callback = /* read %!error.callback% on the failed app */;

    var resumed = new App();
    await resumed.Run(callback);

    Assert.Equal(2, resumed.Variables["x"]);  // step 3 ran, %x% is 2
    Assert.Equal(1, callback.VariablesByActor["x"]); // throw-time view, never 2
}
```

If that passes, every remaining piece (signing, encryption, wire format, storage backends, ask-user web flow) is mechanical. The resume mechanism is the keystone — get it green first, layer the rest.

## Open threads — for future sessions

None blocks the resume mechanism (smallest first cut).

1. **Wire format and key management for ask-user.** Signing scheme over `Data<Callback>`, encryption (AES-GCM probably), key rotation, expiry semantics, how `goal_hash` is computed and stored.
2. **Storage shape for error-retry.** Recommended patterns — file, DB provider, queue. Probably no runtime opinion; developer chooses via PLang. But might want a built-in `callback.store` / `callback.load` action pair for ergonomics.
3. **`goal_hash` mismatch handling.** Signed but stale → hard error. Settled in principle; surface design (error code, `%!callback.error%` shape) is open.
4. **PLang surface for `- run %callback%`.** Is it a normal action in a `callback` module, or a special infrastructure verb? Lean: normal action.
5. **Ask-user builder annotation** — exact `.pr.json` shape for `vars: %x%, %y%` on a step.
6. **`App._statics` TODO closure.** When that's replaced with goal-backed dynamic property, drop `Statics` from the `Callback` schema.
7. **Resumed-call signal for action handlers.** The exact mechanism by which an `ask user` handler distinguishes "fresh call, issue Callback" from "resumed call, return bound value." Likely a flag on the directive readable through `Context`. Coder-level decision.

## Settled rejections (context for future sessions)

- **`Data.Pause` lane.** Callback is a successful `Data` value whose payload happens to be a Callback record. Engine has one extra branch ("is the value a Callback? unwind without continuing"). No new Ok/Fail/Pause trichotomy.
- **Step-level `vars:` capture annotation.** Only on ask-family actions. Error-retry doesn't need declared vars — it captures everything.
- **`Cause` extension to cross-process.** Stays same-process only.
- **Replay-style resume.** Never. Bind + jump only.
- **Re-running producer steps for stateful provider re-acquisition.** Not in v1. Built-in providers are pure or wrap external state (no inter-action mutable state). Third parties opt in via `ISnapshotted`. Event-sourcing is a separate, larger conversation.
- **Ref-capture across islands.** Brainstormed, no real use case, closed. Values-only. Serializer-level blob dedup is the answer if size becomes a problem.
- **Cache in the snapshot.** Rejected. Cache is a hint, not state. If it must survive resume, it's a Variable. `MemoryStepCache` does not implement `ISnapshotted`; resumed App gets a fresh empty cache.
- **Memory channel buffers carried across resume.** Channels are I/O, not inter-step state. If a developer treats a memory channel as state, that's smell.
- **Capturing `Identity` object instead of name.** Names + referent integrity is the contract; objects are never carried. Same for every selection-shaped piece of state (provider, datasource, encryption, identity).
- **"Header" outside the signed envelope.** Whole `Data<Callback>` is signed; nothing meaningful sits outside the signature. Tampering is an all-or-nothing rejection.
- **Resumer's identity replacing captured identity.** The signed envelope authorises; the captured `ActorName` + `Identity.Name` dictate what the resumed code runs as. Who triggers `- run %callback%` is independent of what privilege the resumed code holds.
- **A `Seek` verb on the engine.** No new verb. `App.Run(callback)` is the same main loop as `App.Run(goal)`; only the entry point differs.
- **`CallbackOrigin` typed item on the resumed root Call.** Rejected — telemetry stitching is a log-layer concern, correlated by callback identity. No structured runtime field needed.
- **Callstack-walking as the cache audit substrate.** Rejected on two grounds: Calls pop on completion (forcing `Flags.History` on universally would be too expensive), and cache snapshotting is rejected entirely as a feature. The pattern of "subsystem owns its own audit-after-pop tracking" remains valid for any future subsystem that needs it.
