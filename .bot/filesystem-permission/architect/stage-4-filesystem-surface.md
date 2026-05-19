# Stage 4: IPLangFileSystem v2 — The Surface Bundle

**Goal:** The big bundle. Drop `System.IO.Abstractions.IFileSystem` inheritance. Redesign `IPLangFileSystem` v2 around `Path` parameters and `Data<T>` returns, with verb baked into every method. Permission check happens inside each operation; on miss, the method returns `Data.Fail` with a `FilePermissionAsk`-marked error. Move/Copy use the batched check (one bundled Ask covers both required permissions). Inventory every call site across the codebase and rewrite.

**This is the largest stage by far.** ~50–100 call sites touched across modules, builder, snapshot, settings, channels, cache, runtime infra.

**Scope:**
- New `IPLangFileSystem` v2 interface (`Path` in, `Data` out, verb-baked methods).
- `Default` implementation against v2 with permission check baked in.
- Rewrite every FS action handler against v2 (action handlers shrink to one-liners).
- Rewrite every non-action call site against v2.
- Delete the old surface (`ValidatePath` returning string, `FileAccessControl`, `IFileSystem` inheritance).

**Excluded:**
- Goal-mapped FS provider (Code routing for virtual filesystem). Parked.
- New Read sub-options like `Content` (stat-vs-content distinction). Deferred.

## Deliverables

### The v2 interface

Roughly 11 methods grouped as:
- **Reads** — `ReadText`, `ReadBytes`, `Exists`, `List`, `Stat`. Each checks `Verb.Read` internally.
- **Writes** — `WriteText`, `WriteBytes`, `Append`, `Mkdir`. Each checks `Verb.Write` internally.
- **Destructive** — `Delete`. Checks `Verb.Delete` internally.
- **Multi-path** — `Move`, `Copy`. Each checks Read on source plus Write on dest, batched into one Ask if either is missing.

Every method takes a `Path` object, not `string`. Every method returns `Data<T>` or `Data`. Permission miss = `Data.Fail` whose Error implements the `Ask` marker (concretely a `FilePermissionAsk` carrying the FilePermission record).

Final method list and exact signatures settled by coder during the inventory pass. The shape is what matters: typed Path in, Data out, verb baked in.

### Permission check, delegated to Path

Every operation method asks the Path: `await path.CheckPermission(Verb.X)`. Path owns the question — it knows its absolute form, its Properties cache, and how to reach `actor.Permission` via its Context. The FS method propagates whatever Data Path returns:

- `Data.Ok` from Path → FS method proceeds with the BCL IO.
- `Data.Fail` (with a `FilePermissionAsk`-marked error) from Path → FS method returns it unchanged. `error.handle`'s built-in path catches it from there.

The FS method body is two delegations: ask Path, then do IO via BCL. No private helpers, no transaction script. `path.CheckPermission` was wired in stage 3.

### Batched check for multi-path operations

`Move` and `Copy` ask each Path for its respective verb. If both return `Ok`, the operation proceeds. If either returns `Fail-with-Ask`, the FS method merges any missing Asks into one bundled `Data.Fail` covering both paths. User sees one consent prompt for the whole operation.

The bundling is the only choreography that belongs at the FS layer — it knows the operation (Move/Copy) that ties the two Paths together. Each Path knows about itself; the operation knows about the pair.

Fail-fast: no partial work happens. Either both grants present (operation runs) or any missing surfaces in one bundled Ask.

### Action handler rewrites

Every `PLang/App/modules/file/*.cs` action handler shrinks to a thin shell — calls the corresponding FS method, returns the Data. Read may apply its `ResolveVariables` post-processing on the Success branch; other actions are essentially one-liners. The action handler never inspects an Ask-marked Fail — it returns it as-is and `error.handle` does the rest.

### Non-action call sites

Inventory and rewrite — see [v1/plan/filesystem-surface.md](v1/plan/filesystem-surface.md#inventory-pass). Each call site that currently does `fs.File.X`, `fs.Directory.X`, `fs.Path.X` becomes a `Data`-returning call on the new surface. Cognitive change: handle Data return (Ok or Fail-with-Ask or Fail-with-other) instead of catching exceptions.

### Delete the old surface

After all call sites are rewritten: remove `System.IO.Abstractions.IFileSystem` inheritance; delete the old `ValidatePath(string) → string` method; delete `FileAccessControl` record and its `fileAccesses` list management API.

## Dependencies

- Stage 1 (types — `FilePermission`, `Verb.@this`, `Match`).
- Stage 2 (`Ask` marker exists; `error.handle` built-in path recognises it; engine retry loop in place).
- Stage 3 (`path.CheckPermission(Verb.X)` exists on the Path class; `Actor.@this.Permission` view is reachable via `path.Context.Actor.Permission` but only used internally by Path).

## Design

Full surface design lives in [v1/plan/filesystem-surface.md](v1/plan/filesystem-surface.md). Coder reads that before starting.

### Sub-staging within this stage

This is one stage on the plan, but the work breaks into commits for bisectability:

1. **Define the v2 interface.** Pure declaration. Code compiles; tests don't reference it yet.
2. **Implement `Default` against v2.** Each operation asks `path.CheckPermission(Verb.X)` (single) or asks each path then merges (batched). Tests against `Default`: operations succeed for in-root paths, return Ask-marked Fail for out-of-root without grants.
3. **Rewrite each action handler.** One commit per action (read, save, delete, copy, move, list, exists). Tests in `Tests/App/modules/file/` continue to pass against the new shape.
4. **Rewrite non-action call sites.** Commit per consumer (builder, snapshot, settings, channels, cache, runtime infra).
5. **Delete the old surface.** Final commit removes `ValidatePath`, `FileAccessControl`, `IFileSystem` inheritance.

Each sub-stage compiles and tests green.

### What to be careful about

- **Path resolution semantics must match the old `ValidatePath`.** The /system/ fallback to OsDirectory, the // OS-rooted form, the resolved-against-RootDirectory default — preserve all of it. The replacement is the permission story, not the path-resolution story.
- **Build-time .pr snapshotting** in today's `Default.Read` — preserve. This is the builder's mechanism for reading a `.pr` content from snapshot when the disk file is mid-overwrite.
- **Stream operations.** The current FS surface supports streams via `System.IO.Abstractions`. v2 either exposes its own stream-returning methods or routes all stream consumers through bytes/text. Coder picks based on the few real stream consumers (uploads, large reads).

### What stage 4 does NOT do

- Doesn't change PLang-side action surface. Builder mapping is unchanged.
- Doesn't introduce Code routing for virtual FS. Parked.
- Doesn't add new `Read.Content` sub-option. Deferred.

## Acceptance

- All FS unit tests pass against v2.
- All FS integration tests pass.
- `plang --test` from `Tests/` runs all goal-based tests — full suite green.
- Existing `runtime2` test counts hold or improve (no permission-related regressions).
- Code search confirms no remaining references to `FileAccessControl`, the old `ValidatePath` signature, or `System.IO.Abstractions.IFileSystem` outside the Default code's BCL usage.
