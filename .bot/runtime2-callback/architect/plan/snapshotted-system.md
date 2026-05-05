# `ISnapshotted` system — interface, buckets, inventory

## The interface

Each OBP `@this` type declares its own snapshot discipline. No central registry, no classifier table.

```csharp
public interface ISnapshotted
{
    void Capture(Snapshot.@this s);
    static abstract void Restore(Snapshot.@this s, Context.@this ctx);
}
```

`Snapshot.@this` owns the read/write surface of the captured payload — appending typed entries on Capture, retrieving them on Restore. `Context.@this` is the existing actor context the App already passes around. No invented `Reader`/`Writer`/`RestoreContext` types — we use the OBP types we already have.

The classifier *is* the type system. A new subsystem's author makes the bucket choice by deciding whether to implement the interface. No coordination across the codebase.

## `App.Snapshot()` is the entry point

`App` has its own `Snapshot()` method — it walks its `@this` properties and asks each one that implements `ISnapshotted` for its capture. The result is a `Snapshot.@this` tree mirroring the App's structure (`App.Variables` → `Variables` subtree, `App.Errors` → `Errors` subtree, etc.).

`Snapshot.@this` is *the* snapshot type — the same type `ISnapshotted.Capture` writes to and `ISnapshotted.Restore` reads from. There's no separate `AppSnapshot`; `app.Snapshot()` returns `Snapshot.@this` and that's what `ErrorCallback` carries — see [callback-schema.md](callback-schema.md#errorcallback). One concept, one type. The wire shape *is* the App tree. No translator class converting between "App state" and "callback fields."

Restore is the dual: `App.Restore(snapshot, ctx)` boots a fresh App normally, then walks the snapshot subtrees and hands each one to the matching `@this.Restore(subsnap, ctx)`.

## The three buckets

1. **Snapshot-and-restore** — type implements `ISnapshotted`. Variables (per-actor), `Errors.Trail`, `App.Providers` registry-layer state, `App._statics`, plus any third-party `IProvider` that opts in.
2. **Reconstruct-on-build** — type does *not* implement `ISnapshotted`. Modules, Goals, Catalog, Types, Navigators, Config, Settings (sqlite-backed), FileSystem, Events, Channels, Cache, Debug, all built-in `IProvider` instances. Normal App construction rebuilds them; resume just runs construction.
3. **Drop** — runtime-only state with no resume relevance. Children-as-history (Calls that already completed under any active Call), Timing tier, in-flight network state. Just gone. The active CallStack chain is *not* in this bucket — see "CallStack: position lives in frames" below.

## Islands rule — values only, no graph identity

Each `ISnapshotted` captures **values**, not graph identity. Each subsystem rehydrates from its own bytes. References across types are by *name* (the same way PLang already works at runtime), resolved at lookup time, never restored as object pointers.

After `Restore`, no pointer fix-up phase, no inter-type ordering dependency. Two independent islands per `ISnapshotted` type.

**Limitation accepted:** intra-island graph identity (two list entries pointing to the same object) is not preserved across resume — JSON loses it. PLang is value-based; this rarely bites. Future serializer work can add `PreserveReferencesHandling`-style ID/IDREF if a real case appears.

## Subsystem inventory — final buckets after the walk

| Subsystem | Bucket | Notes |
|---|---|---|
| `App.Variables` (per actor — System, Service, User) | snapshot-and-restore | Existing `Snapshot()` partition is exactly the boundary: skip `!`-prefixed system vars, skip `DynamicData` (Now/GUID/`!app`/`MyIdentity`), skip `SettingsVariable` (sqlite-backed). Capture `(name, value, Type, Properties)` for everything else. Variables also owns `SnapshotAt(error)` for the throw-time projection — see "Variables owns its own time-travel" below. |
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
| `App.Cache` (`MemoryStepCache`) | reconstruct (empty) | Cache is a hint, not state. See "Cache is not snapshotted" below. |
| `App.Events` (lifecycle) | reconstruct | Re-attach during App boot. |
| `App.Debug` | reconstruct | Event handlers re-register from CLI flags. |
| `App.Build` (`@this`, has `IsEnabled`) | snapshot-and-restore | Build mode materially affects step semantics. `Build.Snapshot()` emits `IsEnabled`. AskCallback doesn't capture `App` at all — it's a clean pause; this is an ErrorCallback concern. |
| `App.Testing` (`@this`, has `IsEnabled`) | snapshot-and-restore | Same logic as Build. |
| `App.Actor` instances | reconstruct | Three actors constructed normally. Per-actor Variables are restored as a separate snapshot step. |
| Actor identity | name-only, inside `App.Providers` snapshot | `Identity.Name` is one of the named selections captured under Providers; provider's `GetOrCreateDefaultAsync(name)` resolves on resume. The `Identity` object itself is never carried — referent integrity gates the resume. |
| `App.Test` runner | reconstruct | No live state. |
| Built-in `IProvider` instances | reconstruct | None implements `ISnapshotted` today. The interface is a future-facing hook for third parties that hold inter-action mutable state. |
| `App.CallStack` | snapshot-and-restore (frames as positional context) | See "CallStack: position lives in frames" below. |

## Providers — two layers

The `App.Providers` registry holds two kinds of mutable state, both load-bearing:

1. **Default selections per type.** `SetDefault(IIdentityProvider, "myidentity2")` flips which named provider is returned by `Get<IIdentityProvider>()`. The snapshot captures, per type, the current default name.
2. **Runtime registrations.** Anything registered after `RegisterDefaults()` — typically by PLang `- use 'mycrypto.dll' for encryption` style actions. The snapshot captures `(type, name, source)` tuples where `source` is the DLL path or whatever identifier the loader needs. Built-in registrations from `RegisterDefaults` are *not* in the snapshot — they reconstruct on App boot.

On restore: `RegisterDefaults()` runs as normal, then runtime registrations are loaded and registered, then default selections are applied.

**Unresolvable name on restore = hard error.** If the captured runtime registration can't be loaded (DLL missing, loader fails) or a captured default-selection name doesn't exist after registration, the resume fails with a referent-integrity error — same shape as the goal-not-found case. No silent fallback to the system default. The contract is: names + referent integrity. If the named thing isn't there, the resume isn't safe.

The provider *instances* themselves remain reconstruct-on-build. None of the built-ins hold inter-action mutable state. Third-party providers that do can opt in to `ISnapshotted` separately — that's a per-instance concern, orthogonal to the registry-layer snapshot.

## Variables owns its own time-travel

The throw-time projection — "give me what `App.Variables` looked like at `error.throwTime`" — lives on `App.Variables`, not on a synthetic helper or on CallStack:

```csharp
// On App.Variables
public Variables.@this SnapshotAt(Error error) { … }
```

The split is principled:

- **CallStack owns the diff stream** — each Call records the variable mutations that happened during it as part of its own audit trail. That's where time-ordered events naturally live.
- **Variables owns the projection** — *how* to compute "what I looked like at T" is Variables' concern. Internally, `SnapshotAt(error)` asks CallStack for the diff events with `timestamp > error.throwTime` and reverse-applies them to its own current state.

The seam reads as: *Variables knows how to project itself; CallStack knows what happened in time.* Neither type knows the other's internals — Variables asks CallStack a small question (events-since-T) and answers the bigger one (what did I look like at T).

Lazy materialization of `%!error.callback%` calls `app.Variables.SnapshotAt(error)` as part of `app.Snapshot()`'s walk — see [variable-capture.md](variable-capture.md).

## CallStack: position lives in frames

The CallStack *is* the position. There's no separate "where do we resume" field on ErrorCallback — that question is already answered by the live App at any moment, and the snapshot just captures the answer.

`App.CallStack.Snapshot()` walks the active frames (live Call chain up to the throwing Call) and emits each as a positional record: `(Goal-stub, StepIndex, ActionIndex, …)`. The **bottom-most frame** is the resume point — that's where the throw happened. Outer frames are the Caller chain, used so unwinding after the resumed action completes returns control through the right outer Calls.

`Call` is itself an `@this` and owns its own snapshot/restore. It emits its `Goal` as a stub (`{ PrPath, Hash }`), restores by resolving the path against the live registry. Goal-hash mismatch on restore = hard error.

Children-as-history (Calls that already completed under this Call), timing tier, in-flight network state — those still drop. The snapshot captures the active chain, not the history.

Concretely: if goal A's step 3 called goal B and B's step 2 errored, the snapshot has two frames — A@step3 and B@step2. Resume rebuilds both, lands at B@step2, runs the action, and on completion unwinds to A@step3 + 1.

## Cache is not snapshotted

Cache is a **performance hint**, not state. The line between Variables and Cache is the line between "must survive resume" and "can be lost on resume." If a developer needs a value to be there on resume, the right tool is Variables. Capturing cache would blur the distinction and create two flavours of state with subtly different ergonomics — a clutter that compounds over time.

Walked use cases that survived examination:

- **Memoisation of an expensive call** — resume cache-misses, recomputes. Same as eviction. Slower, correct.
- **Dedup of a fetched object across steps** — same shape, re-fetches on resume.
- **Nonce replay prevention via `TryAddAsync`** — correctness-sensitive, but `MemoryStepCache` is the wrong backend. Use a durable cache (Redis, sqlite) for nonces.
- **"I want this on resume"** — wrong tool. Use Variables.

`MemoryStepCache` does **not** implement `ISnapshotted`. Resumed App gets a fresh empty cache. Pluggable durable backends (Redis) don't participate in snapshot/restore either — they're already durable, the entries are still there on resume by virtue of the backend's nature. ICache stays a pure performance abstraction.

This sharpens the rule for developers: **if it must survive resume, it's a Variable.** One tool per concern.
