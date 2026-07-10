# Decision — (A): the vocabulary moves under `type/item/`; the collection takes `type/list/`

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage3-type-collection-home.md`. Ingi: "all plang types are item, so they belong under item." The convention holds with no carve-out.

## The ruling

Your collision is a symptom, not the problem. `type/`'s children today mix two different things: the **machinery** (`item/`, `kind/`, `catalog/`, `convert/`, `serializer/`, `reader/`, `renderer/`, `factory.cs`) and the **vocabulary** (~24 value types). The vocabulary squats on the concept's namespace — that's why the concept can't have its own `X/list` slot. The fix is structural:

```
type/this.cs            — the type entity (unchanged)
type/list/this.cs       — the collection of types (the convention slot, freed)
type/item/this.cs       — the item apex (unchanged)
type/item/number/       — the vocabulary moves down one level
type/item/list/         —   kind folders ride along: type/item/number/kind/int/
type/item/text/  …          serializers ride along too
```

- `app.type.list.@this` = the collection of types. `app.type.item.list.@this` = the list value. Unambiguous, and it reads right: values ARE items (the item⟺ICreate rule), so they live under the apex.
- No `type.system`/`type.registry`/wrapper — those were carve-outs; never-diverge says the convention holds everywhere or the structure is wrong. The structure was wrong.
- Your (2) stays rejected (registry behavior on the class every `[1,2,3]` instantiates). Your (1)'s plang-list backing stays rejected (Data-entry overhead on the hottest lookup; `goal.list`/`channel.list` are hand-rolled, this one is too).

## The membership audit (part of the move)

Each `type/` child declares its side by the rule it already lives under: **implements `ICreate` → `type/item/<name>/`; machinery stays.** Likely values needing your inventory eye: `permission`, `signature`, `choice`, `table`, `archive`, `code`, `image`, `binary`, and the date/time family. Likely machinery: `spec`, `primitive`, `reader`, `renderer`, `serializer`, `Field.cs`. Anything genuinely ambiguous — surface it, don't guess.

## The collection class (`type/list/this.cs`)

Owns exactly what your doc listed, shaped per the standing rulings:

- **name→entity index** — runtime-hot (every `.pr` read): `FrozenDictionary<string, type.@this>`.
- **clr→entity index** — `FrozenDictionary<System.Type, type.@this>`, built from the types' own `OwnedClrTypes` declarations.
- **the perimeter `Create(object? raw, ctx)`** — the born-native lift (`stage2-lift-door-answer.md`), rung order as ruled: `is item` → frozen exact-type hit → assignable container rungs → `Clr`.
- **backing + lifecycle** — lazy population, runtime-extendable (`code.load`, module choice types); registration re-freezes both indices (rare write, hot read).
- `app.Type` (PascalCase property on `app.@this`) news it once — the collection is owned by `app`, like every concept.

## Blast, measured

~3,700 references across the value namespaces (`path` 759, `text` 716, `number` 452, `list` 329, tail of ~20 more). All mechanical namespace renames; the global aliases in `app/GlobalUsings.cs` (`path`, `dict`, `Clr`, …) absorb aliased call sites — extending the alias set to the frequently-used values is your call. Big diff, zero behavior. Sequence as you see fit (the move is independent of the collection core and can land as its own commit ahead of it); you own the final shape.

## Acceptance

- `type/` contains only machinery + `item/` + `list/`; every value type under `type/item/`, its kinds and serializers with it.
- The collection at `type/list/this.cs` passes the Stage-3 core tests (name lookup, clr lookup, lift, lazy + re-freeze).
- Grep-zero: `app.type.catalog` (Stage-3 close-out, unchanged goal); no `app.type.<value>` namespace left at the old depth.
