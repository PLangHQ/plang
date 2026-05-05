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

## The three buckets

1. **Snapshot-and-restore** — type implements `ISnapshotted`. Variables (per-actor), `Errors.Trail`, `App.Providers` registry-layer state, `App._statics`, plus any third-party `IProvider` that opts in.
2. **Reconstruct-on-build** — type does *not* implement `ISnapshotted`. Modules, Goals, Catalog, Types, Navigators, Config, Settings (sqlite-backed), FileSystem, Events, Channels, Cache, Debug, all built-in `IProvider` instances. Normal App construction rebuilds them; resume just runs construction.
3. **Drop** — runtime-only state with no resume relevance. Live `App.CallStack` tree, Timing tier, Children-as-history, in-flight network state. Just gone.

## Islands rule — values only, no graph identity

Each `ISnapshotted` captures **values**, not graph identity. Each subsystem rehydrates from its own bytes. References across types are by *name* (the same way PLang already works at runtime), resolved at lookup time, never restored as object pointers.

After `Restore`, no pointer fix-up phase, no inter-type ordering dependency. Two independent islands per `ISnapshotted` type.

**Limitation accepted:** intra-island graph identity (two list entries pointing to the same object) is not preserved across resume — JSON loses it. PLang is value-based; this rarely bites. Future serializer work can add `PreserveReferencesHandling`-style ID/IDREF if a real case appears.

## Subsystem inventory — final buckets after the walk

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
| `App.Cache` (`MemoryStepCache`) | reconstruct (empty) | Cache is a hint, not state. See "Cache is not snapshotted" below. |
| `App.Events` (lifecycle) | reconstruct | Re-attach during App boot. |
| `App.Debug` | reconstruct | Event handlers re-register from CLI flags. |
| `App.Testing` / `App.Build` mode flags | inside the Callback record | `BuildEnabled` / `TestingEnabled` fields on `Callback`. |
| `App.Actor` instances | reconstruct | Three actors constructed normally. Per-actor Variables are restored as a separate snapshot step. |
| Actor identity | name-only, in the Callback record | `Identity.Name` carried via `Selections`; provider's `GetOrCreateDefaultAsync(name)` resolves on resume. The `Identity` object itself is never carried — referent integrity gates the resume. |
| `App.Test` runner | reconstruct | No live state. |
| Built-in `IProvider` instances | reconstruct | None implements `ISnapshotted` today. The interface is a future-facing hook for third parties that hold inter-action mutable state. |
| Live `App.CallStack` tree | drop (live tree) — Caller chain captured as positional context | See "CallStack as positional context" below. |

## Providers — two layers

The `App.Providers` registry holds two kinds of mutable state, both load-bearing:

1. **Default selections per type.** `SetDefault(IIdentityProvider, "myidentity2")` flips which named provider is returned by `Get<IIdentityProvider>()`. The snapshot captures, per type, the current default name.
2. **Runtime registrations.** Anything registered after `RegisterDefaults()` — typically by PLang `- use 'mycrypto.dll' for encryption` style actions. The snapshot captures `(type, name, source)` tuples where `source` is the DLL path or whatever identifier the loader needs. Built-in registrations from `RegisterDefaults` are *not* in the snapshot — they reconstruct on App boot.

On restore: `RegisterDefaults()` runs as normal, then runtime registrations are loaded and registered, then default selections are applied.

The provider *instances* themselves remain reconstruct-on-build. None of the built-ins hold inter-action mutable state. Third-party providers that do can opt in to `ISnapshotted` separately — that's a per-instance concern, orthogonal to the registry-layer snapshot.

## CallStack as positional context

Not history (drop), not timing (drop). The *Caller chain* of the throwing Call is captured as a sequence of `(goal, step, action)` resume points so when the resumed action finishes and unwinds, control returns to the caller correctly. Concretely: if goal A's step 3 called goal B and B's step 2 errored, resume needs to know "after this resumed B step 2 finishes, return to A step 3 action N+1." A small list of positions, not a tree.

## Cache is not snapshotted

Cache is a **performance hint**, not state. The line between Variables and Cache is the line between "must survive resume" and "can be lost on resume." If a developer needs a value to be there on resume, the right tool is Variables. Capturing cache would blur the distinction and create two flavours of state with subtly different ergonomics — a clutter that compounds over time.

Walked use cases that survived examination:

- **Memoisation of an expensive call** — resume cache-misses, recomputes. Same as eviction. Slower, correct.
- **Dedup of a fetched object across steps** — same shape, re-fetches on resume.
- **Nonce replay prevention via `TryAddAsync`** — correctness-sensitive, but `MemoryStepCache` is the wrong backend. Use a durable cache (Redis, sqlite) for nonces.
- **"I want this on resume"** — wrong tool. Use Variables.

`MemoryStepCache` does **not** implement `ISnapshotted`. Resumed App gets a fresh empty cache. Pluggable durable backends (Redis) don't participate in snapshot/restore either — they're already durable, the entries are still there on resume by virtue of the backend's nature. ICache stays a pure performance abstraction.

This sharpens the rule for developers: **if it must survive resume, it's a Variable.** One tool per concern.
