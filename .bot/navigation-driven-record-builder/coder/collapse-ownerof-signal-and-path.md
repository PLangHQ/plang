# Blocker — the OwnerOf ownership signal isn't a clean swap, and path's abstract-base courier races (coder → architect)

## Landed & safe (committed, name-diff verified: zero new failures, one fixed)
- **`d6755384e` step 1** — `type.Convert` → the entity **courier** door (typed-`@null` carrier supplies the declared kind), off the reflective hub.
- **`001e6f84c` step 2** — `TryConvert` → the courier; **the reflective convert dispatch is deleted** (`convert.@this` lost `Of`/`OfStatic`/`Invoke` + caches; `Conversions` property gone). `convert.@this` shrank to `OwnerOf` + the ownership map.

The reflective hub is dead. What remains: delete the 5 per-type `Convert` hooks and `type.Build`.

## The blocker (precisely characterized)
Deleting the `Convert` hooks needs `OwnerOf` off its last dependency on them: **`OwnerOf` uses Convert-hook *presence* (`Discover`) as the "this type self-owns" signal** (`convert/this.cs:39`). The natural replacement is "self-owns iff it implements `ICreate<itself>`". **But the two signals are NOT equivalent**, and the gap breaks path:

- `path.@this` **implements `ICreate<path.@this>`** (so the ICreate signal says self-owns) but has **no static `Convert` hook** (so `Discover` said not-self-owns).
- Under the ICreate signal, `OwnerOf(path.@this)` returns `(path.@this)` → `TryConvert` enters the courier arm → `ctx.App.Type[path.@this]` → the courier binds `Courier<entity.ClrType>`.
- **`entity.ClrType` for "path" resolves to a concrete subclass (`path.file`/`FilePath`) — and races** (sometimes `path.@this`, sometimes `path.file`, run to run). `FilePath` implements `ICreate<path>` (base), not `ICreate<FilePath>`, so `Courier<FilePath>` throws the generic-constraint `ArgumentException` (or, with my `ICreate<clr>` guard fix, declines → falls through — but the fallthrough doesn't rebuild the path in isolation).

Evidence: with the OwnerOf→ICreate + guard-fix change, the path tests (`PathParameter_*`, `FileReadStep_StringPathParameter`, `Is_Facet_ImageIsPath`) **fail 5/5 deterministically in isolation** but **pass in the full suite** (some earlier test warms the shared entity's thunk). They were **stable-pass at step-2** — so this is a real regression I reverted rather than commit.

## Two real sub-issues for a design call
1. **The ownership signal.** "Self-owns via Convert-hook" ≠ "self-owns via `ICreate<self>`". `path.@this` is the counterexample (abstract base, `ICreate<self>`, no hook, but its *concrete* subclasses are what actually build). What is the correct signal for `OwnerOf` once the hooks are gone? Options I see: (a) keep an explicit `OwnedClrTypes` decl for the self-owning containers (list/dict/goal.call) so `OwnerOf` never needs a hook/interface probe; (b) exclude abstract families from the self-own check (path routes via its `Assignable` decl only); (c) the courier handles abstract-base→concrete (path→FilePath) itself.
2. **The path entity's `ClrType` races** — `app.Type["path"]` resolves to `path.@this` vs `path.file` nondeterministically (registration order under parallelism). This is a **pre-existing** registry race my routing merely exposes; it should be pinned regardless (an abstract family's entity should have a stable `ClrType`, probably the abstract base).

## My guard fix (independent, correct, currently reverted)
The entity door's `ICreate<>` guard was **too loose** — it accepted a type implementing *any* `ICreate<>` (e.g. `FilePath : ICreate<path>`), then `MakeGenericMethod(FilePath)` violated the `ICreate<FilePath>` constraint. The fix requires `ICreate<clr>` *specifically* (`i.GenericTypeArguments[0] == clr`). It's a real latent bug (only fires when a subtype reaches the courier). Reverted only because it's entangled with the OwnerOf change above; worth landing on its own.

## Ask
Which ownership signal for `OwnerOf` (1a/1b/1c or other)? And should the abstract-family `ClrType` race (2) be pinned first (it's pre-existing)? Once (1) is settled, the hook deletion + `type.Build` death follow.
