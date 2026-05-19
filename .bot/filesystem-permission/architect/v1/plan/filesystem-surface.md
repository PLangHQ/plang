# IPLangFileSystem v2 — Surface Rewrite With Permission Baked In

## Why bundled

Two changes have to land together:

1. **Drop `System.IO.Abstractions.IFileSystem` inheritance.** The current PLangFileSystem extends the BCL abstraction wholesale. We want a Path-shaped, Data-returning surface — not File/Directory/Path with string parameters that throw on misuse.

2. **Bake permission check into every operation.** Every method on `IPLangFileSystem` v2 includes the permission check internally. There is no public `ValidatePath` or `CheckPermission` method that callers must remember to invoke — enforcement is structural.

If we did (1) without (2), every action handler would still call a separate `CheckPermission` before each FS operation, easy to forget. If we did (2) without (1), the current `string`-returning surface couldn't carry the Ask-marked Fail cleanly.

Together: one refactor, one branch.

## The new surface — shape

Every method on `IPLangFileSystem` v2:

- **Takes a `Path` object**, not `string`. Path carries absolute/raw/calling-goal context.
- **Returns `Data<T>` or `Data`.** Permission misses come back as `Data.Fail` whose Error implements the `Ask` marker (specifically `FilePermissionAsk` carrying the FilePermission record(s) needed).
- **Has its verb baked into the method.** `ReadText` and `Stat` and `Exists` check `Verb.Read` internally. `WriteText` / `Append` / `Mkdir` check `Verb.Write`. `Delete` checks `Verb.Delete`. `Move` and `Copy` check Read on source plus Write on dest. Callers don't pass the verb.

Method inventory (final list settled by coder during the inventory pass):

- **Reads** — `ReadText`, `ReadBytes`, `Exists`, `List`, `Stat`.
- **Writes** — `WriteText`, `WriteBytes`, `Append`, `Mkdir`.
- **Destructive** — `Delete`.
- **Multi-path** — `Move`, `Copy`.

That's roughly 11 methods. Some may collapse or split during implementation; the shape is what matters.

## Permission check — delegated to Path

Each operation method asks the Path object whether it has permission, via `path.CheckPermission(Verb.X)`. Path owns its own question — it knows its absolute form, its Properties cache, and how to reach `path.Context.Actor.Permission` (the calling actor's permission view). The FS method's job is to propagate Path's answer.

`path.CheckPermission(Verb.Read)` returns:
- `Data.Ok` if the Path is in-root, or a matching signed grant exists in cache or store.
- `Data.Fail(FilePermissionAsk(new FilePermission(...)))` if not.

The FS operation method propagates this Data directly. If `Ok`, it proceeds to the actual BCL IO. If `Fail-with-Ask`, it returns the Fail unchanged for `error.handle`'s built-in path to pick up.

Enforcement is structural at two levels:
- **Operation method** must ask the Path. There's no public "skip check" hook on Path.
- **Path** owns the check internally — every operation that takes a Path goes through it.

If a future operation forgets to call `path.CheckPermission` before doing IO, that's a coder bug catchable in review. Building a "you must call CheckPermission" enforcement at the compiler level is over-engineering — convention + test fixture (assert every IPLangFileSystem operation calls the check) is enough.

## Batched check for multi-path operations

`Move` and `Copy` ask each Path for its respective verb's permission. If both return `Ok`, the operation proceeds. If either returns `Fail-with-Ask`, the FS method bundles the missing Asks into one `Data.Fail` covering both — the user sees one consent prompt.

The bundling is the only piece of choreography that belongs at the FS layer: it knows about the operation (Move = Read+Write across two Paths) that ties the two checks together. Each Path knows about itself; the operation knows about the pair.

Fail-fast: no partial work happens. Either all permissions are present (operation runs) or any missing surfaces in one bundled Ask.

## Inventory pass

`System.IO.Abstractions.IFileSystem` exposes the full BCL FS surface. Today PLangFileSystem inherits it and consumers reach `fs.File.ReadAllText`, `fs.Directory.GetFiles`, `fs.Path.GetFullPath`, etc.

v2 collapses the surface to the ~11 methods listed above (plus a small set of helpers we discover during the inventory). Every existing call site needs to be touched. The inventory:

- **`PLang/App/modules/file/code/Default.cs`** — the primary consumer. Every method here gets rewritten against the new surface. Action handlers above it shrink to one-liners.
- **`PLang/App/Builder/`** — the builder reads `.goal` and `.pr` files from disk. Heavy FS user. Switching to Data returns means every read becomes a Data flow.
- **`PLang/App/Snapshot/`** — snapshot persistence. Reads/writes `.snapshot` files. Touch.
- **`PLang/App/Settings/Sqlite.cs`** — sqlite-backed settings. Opens DB files via the FS layer. Touch.
- **`PLang/App/Channels/Serializers/`** — file-extension-based content serialization. Indirectly uses FS.
- **`PLang/App/Cache/`** — disk-backed cache. Touch.
- **Runtime infra** — any place that does `fs.File.X` from internal C#. Grep `fs\.File\.|fs\.Directory\.|fs\.Path\.` across the codebase.

Expected scope: ~50–100 call sites. Most are read-side (reading content); fewer are write-side. The mechanical conversion is "wrap the call in await, await the Data, branch on Success."

## What stays the same

- **`Path` class** — the `App.FileSystem.Path` introduced in earlier work continues unchanged. Carries absolute, raw, calling-goal context.
- **`Code/Default`** — the implementation class still lives at `PLang/App/modules/file/code/Default.cs`, just with a different interface above it. Goal-mapped variants (parked) slot in alongside.
- **The signing pipeline** — `signing.sign` action is what `Permission.Add` uses to sign grants. Unchanged.
- **`signing.Signature` type** — what populates `Data.Signature`. Unchanged.

## Migration sequencing inside the bundle

Within stage 4 itself (the bundle), the work breaks down:

1. **Define `IPLangFileSystem` v2 interface.** Pure declaration, no implementations yet.
2. **Implement `Default` against v2.** Each operation calls `path.CheckPermission(Verb.X)` (single) or asks each Path then bundles missing asks (batched). The `Default` implementation doesn't see permission internals — Path's CheckPermission owns the consultation chain.
3. **Rewrite each FS action handler against v2.** Action handler bodies shrink to one or two lines — call the FS method, return its Data.
4. **Rewrite each non-action call site against v2.** Builder, Snapshot, Settings, Channels, Cache, Runtime infra. One pass, mechanical.
5. **Delete the old surface.** Old `IPLangFileSystem` (inheriting `System.IO.Abstractions.IFileSystem`), old `ValidatePath` returning string, old `FileAccessControl`. All gone.

Sub-stages can be commits inside one stage 4 PR. Bisectable per sub-stage.

## What this does NOT do

- **Doesn't introduce a separate `CheckPermission` public method.** The check is internal to each operation.
- **Doesn't expose `ValidatePath` to callers.** Whatever validation existed there is now embedded in the operation methods.
- **Doesn't change the action surface (PLang side).** PLang developers writing `- read file.txt` still get the same builder mapping. Only the C# layer changes.
- **Doesn't add Code routing for non-disk FS** (e.g. goal-mapped FS). Parked from original plan; stays parked.
