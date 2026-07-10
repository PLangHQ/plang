# Decision — the sub-registries are already home: `app.Type` is the type system's root node

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage3-subregistry-home.md`. None of your three shapes is the model's answer — the registries don't move.

## The ruling

`app.Type` is not "the collection with two indices" — it's the **type system's root node**, and the model already places sub-collections on their natural owner, not on `app`: the precedents are `actor` owning `Channels` and `callStack.Error`. Decisive: the kind ruling pinned the selection door as `ctx.App.Type.Kind[...]` — a sub-collection hanging off the type collection IS the ruled shape. Reader, renderer, choice are the same species: type-system concepts, owned by the type system's node.

- **(1) app-level siblings — rejected.** `reader`/`renderer` aren't PLang vocabulary; a PLang developer never says "reader". Serialization machinery beside `goal`/`channel` pollutes the app surface. Your hesitation was correct.
- **(2) a machinery grouper — rejected.** Two roots for one concept; the collection IS the type node.
- **(3) as "defer" — reframed.** Nothing is deferred; staying on `app.Type` is final, by the model.

The stage3 answer's "owns exactly" list was about what the collection class *implements* (indices, perimeter, lifecycle). Exposing owned sub-collection nodes is a different thing and the correct pattern — the middleman smell fires only if the collection *proxies their methods*. Expose the node; each registry behaves for itself.

## Per registry

| registry | disposition |
|---|---|
| `Kind` | **stays, untouched** — `app.Type.Kind` is the ruled door (kind model). Your sub-question: not in reparent scope; the value's kind token keeps reaching it as-is. |
| `Readers` | stays; **rename `Readers` → `Reader`** (see below) |
| `Renderers` | stays; **rename → `Renderer`** |
| `Choices` | stays; **rename → `Choice`** |
| `KindHooks` | belongs to the *kind* concept, not the type root — honest home is off the kind collection (`app.Type.Kind.Hook`), but that move is beyond a zero-logic tail. **Rename-only now** (`KindHooks` → stays put with its current shape); the relocation is noted for the kind redesign. Don't polish. |
| `Scheme` | **location-only**, per the standing plan note — its real home is the path type when scheme-follows-kind-pattern lands (`todos.md` 2026-07-09). Don't polish. |
| `Conversions` | dies with the hub — nothing to place. |

## The one real tail: singular renames

The convention exposes a collection as a **singular property naming the concept** (`app.Goal`, never `AppGoals`/plurals — the deleted-alias smell). So the mechanical slice-2 work is: `app.Type.Readers → app.Type.Reader`, `Renderers → Renderer`, `Choices → Choice` (~15 call sites by your table). Zero logic.

## Plan amendment riding along — the mutable index

Your slice 1 landed the clr ownership index **mutable** on Ingi's direct call (the registry mutates at runtime via `code.load`). That overrides the plan's `FrozenDictionary` + re-freeze pin — mine, now amended to match the landed truth. Same for the name index if it shares the shape.

## Acceptance

- `app.Type.Reader/Renderer/Choice` (singular) compile-clean across the ~15 sites; no `app.Readers`-style flat property anywhere.
- `Kind`/`Scheme` untouched beyond what already landed; `Conversions` gone with the hub deletion.
- Grep-zero: `app.Type.Readers|Renderers|Choices` (plural forms).
