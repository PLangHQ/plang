# born-native lift — architect answer (approved, reframed relocation → defork)

Answer to `coder/born-native-lift-placement.md`, settled with Ingi 2026-07-14. Your placement instinct is right, but the finding is bigger than a wrong address: `type.@this.Create` and `type.list.@this.Create` are a **fork** — two rung-ladders for one operation ("make this raw thing a plang value"), mutually recursive (entity `type/this.cs:313` → registry; registry `type/list/this.cs:338` → entity), each re-implementing rungs of the other (scalar lift, item pass-through, null, containers). Moving the body without killing the second door would preserve the fork under a better address. The detection method that should have caught it is now in `Documentation/v0.2/obp-scan.md` → "Cross-file forks — the census sweeps".

> **You own this.** Sketches below are traced against `020270fa1`; the code wins over any sketch — flag contradictions rather than following them.

## End state — three doors, three questions, no overlap

| Door | Question | Fate |
|---|---|---|
| `T.Create(raw, …)` (each type's ICreate faces) | "build a **T** from this" | stays, untouched |
| `App.Type[name].Create(raw, ctx)` (entity door, `type/this.cs:246`) | "build the **declared** type from this" | stays; ONE line changes (the fallback retarget, below) |
| `item.@this.Create(raw, ctx)` | "build **whatever this is**" — the inferred lift | the registry body moves here |
| `App.Type.Create(raw, ctx)` (registry static, `type/list/this.cs:299`) | — | **deleted. No forwarder** — a forwarder is the middleman smell and the fork survives as an alias |

Key insight that makes the move small: `item.@this` already declares `ICreate<@this>` (`item/this.cs:29`) — it already HAS the static `Create(raw, ctx)` face, currently the trivial default. This move makes the apex's own face do the real work: "create an *item* from raw" IS the inferred lift. Same law as every other type — the produced type owns its construction; for the inferred case, `item` is the produced type.

## The work

1. **Move the body** of `type.list.@this.Create` (`type/list/this.cs:299-348`) onto `item.@this` as its ICreate context face: `public static @this Create(object? raw, global::app.actor.context.@this? context)`. The body is unchanged EXCEPT the ownership-index rung: it currently reads the registry's private `_clr` field (`:337`) — from `item.@this` it goes through the public selection door instead: `context.App.Type[raw.GetType()]` (the CLR-type indexer, `type/list/this.cs:277-289`, "the conversion-ownership door") → non-null → `.Create(raw, context)` on that entity. Selection stays on the collection; behavior moves to the element.
2. **Retarget the ~35 call sites** — one mechanical substitution, `context.App.Type.Create(raw, ctx)` → `global::app.type.item.@this.Create(raw, ctx)` (accessor spellings vary — `Context.App…`, `ctx.App…` — same shape). No per-site decisions, no logic change.
3. **Retarget the entity door's fallback** (`type/this.cs:313`): `Create(context.App.Type.Create(raw, context), context)` → `Create(item.@this.Create(raw, context), context)`.
4. **Delete** `type.list.@this.Create` and its `<summary>` (the registry's "born-native lift" paragraph). The registry keeps the `_clr` index, both indexers, `of<T>`, and every selection door — it loses only the factory.
5. **Fix the prose claims** (part of the defork — three members currently claim the same responsibility): ICreate's pure core keeps "the ONE runtime boundary" (the per-type CLR crossing); `item.@this.Create` gets the inferred-lift doc ("the born door for an *undeclared* raw — infers the owner via the type collection's ownership door"); the entity door keeps "THE born-native door" for the *declared* ask. One claim per responsibility.

## Entity-door rungs — traced disposition (why they STAY)

I told Ingi initially the entity door "sheds duplicated rungs" — the trace says otherwise; every rung carries declaration policy the apex lift can't:

| Rung (`type/this.cs`) | Looks like a dup of apex… | Actually |
|---|---|---|
| `:254` null → `new @null(Name, Kind)` | apex `:301` null → bare citizen | NOT a dup — typed absence (declaration survives) vs untyped. Both stay. |
| `:267` non-leaf item held | apex `:302` pass-through | NOT a dup — this is the "containers never retype/downgrade at a declaration" POLICY (a dict in a `list`-declared slot is held, not converted). Deleting it would change behavior through the leaf-retype path. Stays. |
| `:263` string/bytes → source | (apex has no string arm) | declaration-specific deferral. Stays. |
| `:273` source re-declared | — | declaration-specific. Stays. |
| `:277-305` built-leaf refine/history/retype | — | declaration-specific. Stays. |
| `:311` own-family `_byContext` fast-path | apex's index rung | NOT a dup — the entity KNOWS the owner (itself); the apex INFERS it. Stays. |
| `:313` fallback | — | the one edit: retarget to `item.@this.Create`. |

Post-move the recursion is one-directional and bounded: entity → apex (fallback) → a DIFFERENT owner's entity → its `_byContext`, terminal; the apex never calls back into the same entity.

## Verify before closing

- **Self-bind check**: `type.@this.Creatable` (`type/this.cs:355-361`) binds `_byContext` for any entity whose ClrType implements `ICreate<self>`. If any entity's ClrType resolves to `typeof(item.@this)` (the `item`/`object` names), its thunk would now bind to the apex lift → possible recursion through `:311`. Trace what `App.Type["item"]`/`["object"]` report as ClrType and confirm no cycle (expected: `typeof(object)`, no bind — but verify, don't assume).
- Error semantics preserved: the apex lift ALWAYS returns a value (never null; Data guard throws — `type/list/this.cs:303-306` rides along); the entity door remains the throw boundary.
- The `context!` uses inside the moved body (`:310` etc.) — unchanged contract (containers need context), but you're now on the apex: consider whether the context-never-null throw the entity door has (`:250`) belongs at the apex too, or nullability stays as-is. Your call; note it either way.
- Zero new reds; the ~35-site sed compiles clean.

## Demolition

- `type.list.@this.Create(object?, actor.context.@this?)` — the method, its summary, and the "born-native lift" claim. Gone after the retarget, same commit.
- The `_clr.TryGetValue` read inside the moved body — replaced by the `App.Type[System.Type]` selection door; `_clr` stays private to the registry.
- No other member of `type/list/this.cs` is touched by this finding.
