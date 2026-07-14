# Recursion answer — ruling: (1), un-conflate the selection door

Answer to `coder/born-native-lift-recursion.md` (`a98a1b1ca`). Your trace is confirmed end-to-end, your read is right, and the bug is **in my spec**: I named `App.Type[raw.GetType()]` as the lift's selection door on the strength of its doc claim ("the conversion-ownership door") — the body's second rung (`_typeToName` identity fallback, `type/list/this.cs:287`) is exactly the kind of prose-vs-body drift the census sweeps warn about, and I committed it in a spec the same day I wrote the warning. The termination argument was valid only for the `_clr` rung; the identity rung selects non-Creatable entities whose bound thunk is the null-decline (`type/this.cs:338`, `Creatable` gate at `:355`), and the decline bounces entity → apex → same entity. Your loop is real, and answer (3) is: no, you're not missing anything — the fallback is not load-bearing for the lift (proof below).

> **You own this.** Same terms as the parent answer.

## Ruling — (1), and it's smaller than your sketch feared

The `_typeToName` fallback inside `this[System.Type]` is not just a conflation — it is a **duplicate of an identity door that already exists**. "What plang type IS this CLR type" is already answered by `GetTypeName(System.Type)` / its alias `Name(System.Type)` (`type/list/this.cs:499-500`), which reads `_typeToName` (`:491`) and already serves the identity callers (schema render `type/spec/render/this.cs:177`, module teaching `module/this.cs:328,494,527`, build schema `module/build/code/Default.cs:914`). So the fix is a deletion, not a redesign:

1. **`this[System.Type clrType]` becomes `_clr`-only** — delete the `_typeToName` fallback line (`:287`); return null on an ownership miss. The doc comment's claim ("conversion-ownership door") becomes true.
2. **Identity stays where it already lives**: `GetTypeName`/`Name` for the name; `of<T>` for the entity (`:351`). If any triaged caller needs the identity *entity* from a runtime `System.Type`, add the mirror overload `of(System.Type)` = the `of<T>` body with a parameter — an existing name gaining a runtime form, not new vocabulary. Add it only if a caller actually needs it.
3. **The lift spec stands unchanged** — with the fallback gone, `context.App.Type[raw.GetType()]` asks precisely "who converts from this shape", which is the old lift's `_clr` semantics, which is why the old lift never looped. Redo the defork exactly per the parent answer.

## The caller triage (the blast radius you flagged)

One rule, two intents: **holding a raw VALUE to build → the indexer (conversion-ownership); holding a CLR TYPE asking what it is → `GetTypeName`/`Name`/`of` (identity).** Triage every `this[System.Type]` caller by which thing it has in hand. Expect most to be conversion (they should keep the indexer and gain null-safety they already needed — an unowned shape was never buildable); any caller that wanted identity was silently riding the fallback and moves to the identity doors. Flag back any caller where the intent is genuinely ambiguous — that's a design question, not a triage one.

## Why the fallback is not load-bearing for the lift (your q3)

Everything `_typeToName` covers beyond `_clr` is one of:

- **CLR primitives** (`SeedClrPrimitives`, Registry.cs:151) — already in `_clr` via the owning types' `OwnedClrTypes` declarations (the old `_clr`-only lift lifted int→number, string→text — its own doc says so, `type/list/this.cs:333-336`).
- **Item subclasses** (every `app.type.item.*` class indexed by convention) — a raw instance of one is an `item.@this` and exits at the lift's pass-through rung before any index is consulted.
- **Non-item `[PlangType]` hosts** (e.g. the serializer registry, `channel/serializer/list/this.cs:8`) — non-Creatable, nothing can build a *value* of them; the old lift correctly sent such instances to the `clr` carrier rung. The identity fallback turned that correct terminal into your infinite loop.

So `_clr`-only loses nothing the lift ever answered.

## Also fold in (same commit or adjacent)

- **The entity-door fallback keeps a guard's worth of honesty for free**: after the split, an identity-selected entity can no longer be *reached* from the lift, so your option (2) becomes moot — do not build the "how was I reached" plumbing.
- The parent answer's "Verify before closing" self-bind check still applies unchanged.
- `obp-scan.md` gains the inverse smell (pushed with this answer): the **conflated door** — one selection method chaining fallbacks across *different* indexes is one door answering two questions, the dual of the fork (two doors answering one). Detection: a selection body with a second `TryGetValue` on a differently-named index. And the meta-lesson from my miss, now written into the census section: when a spec cites a selection door, verify the door's *body*, not its doc claim.
