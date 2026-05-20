# Stage 4: `IPLangFileSystem` v2

**Goal:** Drop `System.IO.Abstractions.IFileSystem` inheritance. Redesign `IPLangFileSystem` around `Path` parameters and `Data<T>` returns, with verb baked into every method. Permission check happens inside each FS operation via `path.Authorize(verb)` (stage 2b); the result propagates as-is (engine handles short-circuit + Snapshot via stage 2a). `Move`/`Copy` use a batched check.

**This is the largest stage by volume** — ~50-100 call sites across modules, builder, snapshot, settings, channels, cache, runtime infra. Mostly mechanical once the interface lands.

## Scope

- New `IPLangFileSystem` v2 interface (`Path` in, `Data` out, verb-baked).
- `Default` implementation against v2 with permission check baked in.
- Rewrite every FS action handler against v2 (handlers shrink to one-liners).
- Rewrite every non-action call site against v2.
- Delete the old surface: `ValidatePath(string) → string`, `FileAccessControl`, `IFileSystem` inheritance.

## Out of scope

- Goal-mapped FS provider (Code routing for virtual filesystem) — parked.
- New Read sub-options like `Content` (stat-vs-content distinction) — deferred.

## Deliverables

### 1. The v2 interface

Roughly 11 methods grouped by verb:

- **Reads** — `ReadText`, `ReadBytes`, `Exists`, `List`, `Stat`. Each calls `path.Authorize(new Read())` internally.
- **Writes** — `WriteText`, `WriteBytes`, `Append`, `Mkdir`. Each calls `path.Authorize(new Write())`.
- **Destructive** — `Delete`. Calls `path.Authorize(new Delete())`.
- **Multi-path** — `Move`, `Copy`. Each calls Read on source plus Write on dest, batched.

Every method takes a `Path`, not `string`. Every method returns `Data<T>` or `Data`. Permission miss surfaces as `Data<Ask>` (stage 2a's Exit-typed kind) — engine captures Snapshot, short-circuits the goal. No `Data.Fail`, no per-kind callback class.

Final method list and exact signatures settled by the coder during the inventory pass.

### 2. Permission check delegated to Path

Each operation's body is two delegations: ask Path, then do IO via BCL.

```csharp
public async Task<Data<byte[]>> ReadBytes(Path path)
{
    var auth = await path.Authorize(new Verb.Read());
    if (auth.Type?.ClrType?.Exit() == true) return auth;
    if (!auth.Success) return auth;
    // BCL read…
}
```

`Data.Ok` from Path → proceed with the IO. `Data<Ask>` → return unchanged; engine short-circuits via stage 2a's step-loop check.

### 3. Batched check for `Move` / `Copy`

`Move`/`Copy` ask each Path its respective verb. Both `Ok` → operation proceeds. Either returns `Data<Ask>` → FS method combines the two questions into a single bundled `Ask` (one question string covers both paths). User sees one consent prompt for the whole operation.

The bundling is the only choreography that belongs at the FS layer — it knows the operation that ties the two Paths together. Each Path knows about itself; the operation knows about the pair. Fail-fast: no partial work.

### 4. Action handler rewrites

Every `PLang/App/modules/file/*.cs` action handler shrinks to a thin shell — calls the corresponding FS method, returns the Data. Read may apply its `ResolveVariables` post-processing on the Success branch; others are essentially one-liners. The handler never inspects whether the result is Exit-typed — it returns it as-is.

### 5. Non-action call sites

Inventory and rewrite. Each site that does `fs.File.X`, `fs.Directory.X`, `fs.Path.X` becomes a `Data`-returning call on the new surface. The cognitive change for the caller: handle `Data` returns (Ok, Ok-with-Exit-bubble, Fail-with-other) instead of catching exceptions.

### 6. Delete the old surface

After all call sites are rewritten:
- Remove `System.IO.Abstractions.IFileSystem` inheritance.
- Delete the old `ValidatePath(string) → string` method.
- Delete `FileAccessControl` record and its `fileAccesses` list management API.

## Sub-staging (commit boundaries)

The work breaks into bisectable commits:

1. **Define the v2 interface.** Pure declaration. Compiles; tests don't reference it yet.
2. **Implement `Default` against v2.** Each operation asks `path.Authorize(new Verb.X())` (single) or asks each path then bundles (batched). Tests: operations succeed for in-root paths, return `Data<Ask>` for ungranted out-of-root paths against Message-like channel; complete synchronously against Stream.
3. **Rewrite each action handler** — one commit per action (read, save, delete, copy, move, list, exists). `Tests/App/modules/file/` stays green.
4. **Rewrite non-action call sites** — commit per consumer (builder, snapshot, settings, channels, cache, runtime infra).
5. **Delete the old surface.** Final commit removes `ValidatePath`, `FileAccessControl`, `IFileSystem` inheritance.

Each sub-stage compiles and tests green.

## Watch-outs

- **Path resolution semantics must match the old `ValidatePath`.** The `/system/` fallback to `OsDirectory`, the `//` OS-rooted form, the resolved-against-`RootDirectory` default — preserve all of it. The replacement is the permission story, not path resolution.
- **Build-time `.pr` snapshotting** in today's `Default.Read` — preserve. The builder relies on it to read `.pr` content when the disk file is mid-overwrite.
- **Stream operations.** The current FS surface supports streams via `System.IO.Abstractions`. v2 either exposes its own stream-returning methods or routes all stream consumers through bytes/text. Coder picks based on the few real stream consumers (uploads, large reads).

## Dependencies

- Stage 1 — `Permission`, `Verb.@this`, `Match`.
- Stage 2a — `Type.Exit()`, step-loop short-circuit, `Snapshot.Resume`, `output.ask` delegating to `Channel.Ask`.
- Stage 2b — `Path.Authorize(verb)` exists.
- Stage 3 — `Actor.@this.Permission` view exists (used internally by Authorize, not by FS code).

## Acceptance

- All FS unit tests pass against v2.
- All FS integration tests pass.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` zero regressions.
- Code search confirms no remaining references to `FileAccessControl`, the old `ValidatePath` signature, or `System.IO.Abstractions.IFileSystem` outside the `Default` code's BCL usage.
