# Decision — the kind IS the behavior; one selection door; kinds live under their owning type

**From:** architect. **Settled with Ingi (2026-07-09).** Supersedes the open list-kind-collision question (`coder/stage1-navigation-listkind-collision.md`) — `sequence` is rejected and dead; the collision dissolves structurally. Rides WITH the in-flight A+C navigation work (same seam, unblocks your list-kind home).

## 1. `behavior` dies — the kind IS the behavior

The current split is the anemic-object pattern: `kind.@this` is a hollow token (six one-line proxy verbs, each a registry hop `ctx.App.Type.Kinds[this].<Verb>`), `behavior.@this` is behavior with no identity, and the registry marries them **by name on every verb call**. It inverts the repo's own rule (*"Registry = selection + lifecycle; behavior lives on the element"*) — the element is empty, the behavior lives in a shadow tree.

**The merge:**
- `kind.@this` = the base class. **Owns the verb defaults directly** (move `behavior.@this`'s bodies in): `Navigate` (per-hop re-derive, per A+C), `Descend` (throws not-navigable), `Enumerate`, `Set`, `Load` (→ text), `Convert`, `Output`.
- Each kind is a **subclass** overriding what it does differently — the classes formerly under `behavior/`.
- **An unknown kind ("md", "csv", a host's class name) is just a base instance carrying the name.** Today's "unclaimed falls to defaults" mechanism becomes plain object-orientation.
- `value.Kind.Navigate(...)` becomes a direct virtual call — the per-verb registry hop is gone (matters now that navigation re-derives the kind per hop).
- The `behavior/` tree deletes in this stage (not the end-sweep).

## 2. One selection door — the implicit AND `kind.Of` both die (option b: do it correctly)

`kind.Of(name, ctx)` was a static factory doing the collection's job — selection belongs to the collection, reached by navigation. Two ways to get a kind = divergence; a static = broken OBP. Same verdict for the implicit `(kind)"json"` (mints context-less half-objects).

**The one door:** `ctx.App.Type.Kind[name]` and `ctx.App.Type.Kind[clrType]` — the collection (today's plural `App.Type.Kinds` renames to the singular concept node `Kind`, per the `app.X["name"]` convention). The collection owns **selection + lifecycle**:
- known name / claimed ClrForm → the subclass instance (exact ClrForm wins, then assignable — `JsonElement`→json, `IList`→list);
- unknown → the collection **mints a base instance carrying the name**. The indexer never returns null.
- Instances are born WITH context (context-at-birth; the collection is per-App).

The call-site changes, concretely:

```csharp
// SITE 1 — a kind building its own children (json.cs Data — the most common "json" literal):
// today:  new clr.@this(e, ctx, "json")
// after:  new clr.@this(e, ctx, this)            ← the kind hands ITSELF; the literal disappears

// SITE 2 — the wire reading a type descriptor (kind:"md"):
// today:  type.Kind = kind.Of(s)                 ← context-less token, stamped later
// after:  type.Kind = ctx.App.Type.Kind[s]       ← the reader already holds ctx

// SITE 3 — the clr ctor's born-kind resolution:
// today:  Kind = kind ?? Context.App.Type.Kinds[value.GetType()] ?? (kind)(fallbackName)
// after:  Kind = kind ?? Context.App.Type.Kind[value.GetType()]  ← one call: claimed → subclass;
//                                                                   unclaimed → base named for the host
```

Equality (by `Name`, case-insensitive) and wire form (the name string) are unchanged — only the doors change.

## 3. Structure — `type/<owner>/kind/<k>/this.cs` (the `Type[t].Kind[k]` ruling, finally applied)

A kind specializes a type (`kind.Type` already declares it: json→item, md→text, int→number), so it **lives under the type it specializes**. The flat `kind/behavior/` folder was a v1 shortcut past the clr-navigators `Type[t].Kind[k]` ruling.

```
app/type/kind/this.cs                   — kind.@this, the BASE (owns the verb defaults)
app/type/kind/list/this.cs              — the collection of kinds (selection + lifecycle; X.list convention)

app/type/item/kind/json/this.cs         — the json kind        (JsonElement host   → {item, json})
app/type/item/kind/list/this.cs         — the LIST kind        (raw IList host     → {item, list})
app/type/item/kind/dict/this.cs         — the dict kind        (raw IDictionary    → {item, dict})
app/type/item/kind/reflection/this.cs   — the * kind           (any other POCO     → {item, *})

app/type/number/kind/int/this.cs        — number's precisions land here in Stage 2 (already settled)
app/type/text/kind/md/this.cs           — md, when it grows behavior — same one predictable path
```

**Every kind folder's class is `@this`** — so the word `list` is fully usable (`app.type.item.kind.list.@this`), no collision with the collection (`app.type.kind.list.@this`) or the list type (`app.type.list.@this`). Discoverability is absolute: the json navigator is at `type/item/kind/json`; the int parser will be at `type/number/kind/int`.

## 4. Interaction with the in-flight A+C work

Nothing in `stage1-navigation-answer.md` changes semantically — per-hop re-derivation, `Descend`, `Set` symmetry, assignable ClrForm all stand. What moves:
- the list/dict descend kinds you're adding land at `type/item/kind/list/` and `type/item/kind/dict/` (class `@this`), not `behavior/sequence.cs`;
- the base walk lives on `kind.@this` itself;
- `Kinds[...]` call sites become `Kind[...]` (the rename rides the same sweep);
- add `kind.Of`, the string implicit, and the `behavior/` tree to the `[Obsolete]`-then-delete set for this stage.

## Why this is worth the churn (the one-breath version)

One class per kind, owning its behavior, at one predictable path, selected through one navigated door, with unknown kinds as plain base instances. The token/behavior split, the per-verb registry hop, the static factory, the context-less half-tokens, and the `sequence` compromise all cease to exist.
