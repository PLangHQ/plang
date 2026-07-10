# Question — where do the sub-registries live when they reparent off the collection? (coder → architect)

Slice 1 landed (`df15fb112`): the born-native lift now runs off the clr **ownership** index (`_clr`, from `OwnedClrTypes`: `int→number`, `string→text`) + the entity `Create` door, replacing `convert.OwnerOf` in the perimeter. Mutable index (not Frozen — Ingi: the registry mutates at runtime via `code.load`), populated inline as each type is indexed. The stale `[Obsolete]` came off `type.list.@this` — it IS the collection home, not moving.

## The open call — slice 2 placement

Plan line 90: *"Rehome `Kinds`/`Readers` to `app.type.*`; mechanical tail: `Renderers`/`KindHooks`/`Choices` reparent … `Scheme` reparents location-only."* But `app.type.*` doesn't pin the concrete **owner**.

Today the sub-registries hang off the **collection** as properties, reached through `app.Type`:

| registry            | type                          | reached as        | call sites |
|---------------------|-------------------------------|-------------------|-----------|
| `Kind`              | `app.type.kind.list.@this`    | `app.Type.Kind`      | 10 |
| `Scheme`            | `app.type.item.path.scheme.@this` | `app.Type.Scheme` | 8 (location-only per plan) |
| `Readers`           | `app.type.reader.@this`       | `app.Type.Readers`   | 7 |
| `Renderers`         | `app.type.renderer.@this`     | `app.Type.Renderers` | 6 |
| `KindHooks`         | `app.type.kind.Hooks`         | `app.Type.KindHooks` | 5 |
| `Conversions`       | `app.type.convert.@this`      | `app.Type.Conversions` | 2 (dies with the hub) |
| `Choices`           | `app.type.item.choice.list.@this` | `app.Type.Choices` | 2 |

The stage3 answer said the clean collection owns **only** name/clr indices + perimeter + lifecycle — so these move OFF it. But to where?

### The three shapes I see

1. **`app`-level siblings** — own them on `app.@this` beside `app.Type` and `app.Format` (their *types* already live at `app.type.<name>`). `app.Type.Readers → app.Readers`. ~30 mechanical call sites; matches the `app.Format` precedent. Risk: flattens type-machinery onto `app` (is `renderer`/`reader` a top-level concept, or type-system internals that shouldn't sit beside `goal`/`channel`?).

2. **A lean `app.type` machinery owner** — a small object at `app.type` (separate from the `list` collection) that owns the sub-registries; keeps them grouped under `type`. More structure; access `app.Type.Readers → app.type.Readers` (or similar). Risk: inventing a grouping the model doesn't otherwise have.

3. **Defer to Stage-3-core** — leave them on the collection this pass (`app.Type.X`), reparent when the collection's internals are cleaned in place. Slice 1 already landed the valuable part; the reparent is explicitly "zero-logic tail, may trail as own commits."

My lean read: option 1 is the honest OBP move **if** these registries are genuinely `app`-owned concerns (each answers "the app's reader table / renderer table"), matching `app.Format`. But `reader`/`renderer`/`kindHooks` feel like type-system *internals*, not app-level concepts — which argues for keeping them under a `type` owner (option 2) or deferring (option 3). I don't want to guess the concept boundary — it's a model call, not a mechanical one.

**Ask:** which owner? And is `Kind` (the per-App `kind.list`, reached `value.Kind.Navigate(...)` through the token — INTERNAL plumbing) even in scope for the reparent, or does it stay wherever it's cheapest to reach from a value's kind token?
