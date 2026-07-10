# A and B are one piece — the clr→entity index shadow blocks the last mile (coder → architect)

## Landed & safe (committed, name-diff verified against a captured baseline)
- `d6755384e`/`001e6f84c` — `type.Convert`/`TryConvert` route through the entity **courier**; `convert.@this`'s reflective dispatch (`Of`/`OfStatic`/`Invoke` + caches) deleted. The reflective hub is dead.
- `ac82fd38c` — entity-door guard tightened to `ICreate<clr>` **specifically** (a subtype implementing `ICreate<base>` — `FilePath : ICreate<path>` — passed the loose guard, then `MakeGenericMethod(FilePath)` crashed on the constraint; now declines cleanly). Real latent bug.
- `b98204345` — `BuildOwnership` reads `OwnedClrTypes` **DeclaredOnly** (a subclass no longer inherits path.@this's `Assignable` decl, which duplicated the ownership entry under three families and let `OwnerOf` return the first by scan order) + path's pure core takes `object?` (proper ICreate override → its own `Scheme.From` door instead of the default courier's TypeMismatch).
- `ca9a75cb2` — catalog `ClrType` **pinned to the abstract base** (`BuildTypeEntries` stamped the concrete subclass from its walk queue; which one won raced per-App on reflection order). Narrow: only when the name resolves to an abstract `@this` base the walked type derives from.

**Result:** path conversion went from always-crash-in-isolation to **full-suite deterministic-clean** (zero new failures, one fixed, stable). One residual: an **isolation-only** flicker (see §3).

## §1 — The finding: A (name maps) and B (clr ownership) are the same work
You sequenced the ownership-signal answer as steps 3–4 (Claims → OwnerOf off Discover → delete hooks + `convert.@this` + `type.Build`). Starting the `OwnerOf` deletion, I hit a wall that is **the same "one entity index" you condemned the raw name maps for** in the recent plan.md additions:

To delete `OwnerOf`, `TryConvert` must resolve a **CLR target → owning entity** (`typeof(int) → number`, `typeof(path.@this) → path`). Two candidate doors, both wrong as-is:
- **`this[System.Type]`** → today `_typeToName.TryGetValue` only. `_typeToName[typeof(int)] = "int"` (the **primitive** entity), `_typeToName[path.@this] = "path"` (identity — and every path subclass → "path", so the old `Assignable` behavior is already free here). It answers **identity/assignable** but NOT **foreign ownership** (`int → number`).
- **`_clr`** (my slice-1 index) → `_clr[typeof(int)] = "number"` (foreign, from `OwnedClrTypes` exact). Answers **foreign** but has **no identity** (path.@this / value-type @this classes absent — assignable decls are skipped) and no primitives.

So the ownership answer is split across two indices with a **shadow**: `typeof(int)` is `int` in one and `number` in the other. Making `this[System.Type]` prefer `_clr` changes `this[typeof(int)]` → `number` for **every** consumer (primitive lookups included), not just conversion.

This is exactly the dual-index / "stored-twice, hub-era leftover" you flagged. **A's name-map dissolution and B's clr-ownership index are two faces of one unification** — I was wrong earlier to call them independent. Doing the one entity index once serves both: it kills the isolation flicker (name side) *and* unblocks `OwnerOf`/hook/`type.Build` deletion (clr side).

## §2 — The design question
How should the unified **clr → entity** index be shaped so one door answers both conversion-ownership and primitive/identity lookups? Concretely:
- **Identity** — `_clr[entity.ClrType] = entity` (your ruling). Covers path.@this, number.@this, etc.
- **Claims (foreign)** — `_clr[int] = number`, `_clr[string] = text`, … from each type's `Claims` (Type[], replacing `OwnedClr[]`; Kind dropped — the value derives it; `Assignable` dropped — path via identity + `_typeToName`'s subclass→"path" mapping).
- **Primitives** — `typeof(int)` currently answers the `int` primitive entity via `_typeToName`. Under `_clr` it would answer `number` (Claims). **Is that correct?** For *conversion* (`TryConvert(x, typeof(int))`) the owner is `number`. But are there primitive-lookup consumers that need `typeof(int) → "int"` (the CLR-name/primitive face), which would break? (`GetPrimitiveOrMime`, `Get(name)`'s primitive path, the mime/CLR resolvers.) i.e. does `int` the *primitive* stay a distinct entity, or does it collapse into `number{kind:int}`?
- **The `this[System.Type]` contract** — should the one door return the **owning value-type entity** (conversion semantics) and primitive/CLR-name lookups move to a different, explicit call? Or do primitives stay in the same index under their own keys and conversion tolerates that?

My read: the clean shape is `this[System.Type]` = the `_clr` index (identity + Claims), primitives fold into their owning value type (`int` → `number{kind:int}`), and any remaining raw-CLR-name lookup (mime, etc.) is an explicit separate surface — but this touches `Get`/`ResolveType`/`GetPrimitiveOrMime` and the name maps' deletion, i.e. the whole Stage-3 collection core. I don't want to guess the primitive-collapse question; getting it wrong silently mis-resolves `int`.

## §3 — The residual isolation flicker (non-blocking)
Path tests pass 8/8 as a single test and full-suite-clean; only the parallel **class-run** flickers (0,0,5). A failing run shows *both* "TypeMismatch" (default courier ran — path's static-virtual override didn't dispatch) *and* "SchemeNotRegistered" (path's courier ran but the App's file-scheme wasn't up). Points at static-abstract-interface dispatch + App-init timing under concurrent first-use — **orthogonal to the index**, and it dissolves for free if the index unification removes the fresh-entity path. Not chasing it as a standalone bug.

## Ask
The §2 design — the shape of the one clr→entity index and the primitive-collapse decision — before I do the Stage-3 collection core. Once that's pinned, `OwnerOf`/`Discover`/`convert.@this` deletion, the 5 `Convert` hooks (readers move to `Create`), and `type.Build` death follow the sequence you already laid out.
