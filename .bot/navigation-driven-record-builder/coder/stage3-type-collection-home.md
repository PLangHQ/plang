# For architect — Stage 3 core: `app.type.list.@this` is the list VALUE type, so where does the `list<type>` type-collection class live?

**From:** coder. **2026-07-10.** Starting Stage 3 (catalog-removal). Chose "new `list<type>`, migrate
incrementally" with Ingi. Hit a naming collision before building the runtime-hot core — foundational, so
surfacing it.

## The collision

The plan says: **"Registry = `list<type>` (`app.type.list`), keyed name→entity index ON THE COLLECTION."**

But `app.type.list.@this` is **already taken** — it's the generic list VALUE type (the `[1,2,3]` native
plang list):

```csharp
// PLang/app/type/list/this.cs
public partial class @this : item.@this, item.ICreate<@this>, module.IContext, IListLeaf, IEnumerable<Data>
```

So the type-collection class (holding the name→entity index + the perimeter `Create`) can't literally be
`app.type.list.@this` — that's the list value. The `X.list.@this = the collection` convention
(`channel.list.@this`, `goal.list.@this`) works for every concept EXCEPT `type`, because for `type`,
`type/list/this.cs` is the list value type itself, not a free collection slot.

## What the core needs a home for

- The **name→entity index** (`this[string] → type.@this`) — **runtime-hot**, hit on every `.pr` read.
- The **clr→entity index** (`this[System.Type]`) — built from `OwnedClrTypes`.
- The **perimeter `Create(object? raw, ctx)`** — the born-native lift (the reverted-from-catalog work).
- The backing collection of type entities (`list<type>`), lazy + runtime-extendable (`code.load`, module
  choice types).

## Candidates

1. **`app.Type` = `list.@this<type.@this>` (the generic typed list holds the entities); the index +
   perimeter live on a dedicated WRAPPER collection class** at a home that isn't `type.list.@this`. Names
   under `type/` that aren't `list`: `type.registry.@this`? (but "registry" is the smell-word the branch is
   removing) — `type.collection.@this`? `type.index.@this`? The wrapper HAS-A `list<type>` + the two
   indices + `Create`. `app.Type` is this wrapper; `app.Type.list` (or an enumerable face) is the
   `list<type>`.

2. **Load the index + perimeter directly onto `app.type.list.@this`** (the list value type), so a
   `list<type>` INSTANCE literally IS the registry. One class, the plan's "on the collection" reads
   literally — but it loads registry behavior (name index, clr index, perimeter, lazy population) onto the
   generic list value type that every `[1,2,3]` shares. Feels like the naked-collection/stored-twice smell
   inverted (behavior for ONE special instance on the type EVERY instance uses).

3. **Something else** — e.g. `app.Type` stays a dedicated non-list class (like `goal.list.@this`, which is
   a hand-rolled collection, NOT a `list.@this`), just named to reflect it's the type collection, and
   "`list<type>`" is the conceptual model (it enumerates as type entities) rather than a literal
   `list.@this<type>`. Note: `goal.list.@this` / `channel.list.@this` are **hand-rolled** collection
   classes, not instances of `list.@this<goal>` — so the `X.list.@this` convention is already "a dedicated
   collection class," not "a typed native list." By that reading, `type`'s collection should also be a
   dedicated class — but its natural name `type.list.@this` is occupied.

## Coder lean

**(3)** reframed: the `X.list.@this` collections are already dedicated hand-rolled classes (goal/channel),
not native `list.@this<T>` instances — so "list<type>" is the *model*, and the type collection wants a
dedicated class too. The only real problem is the NAME (`type.list.@this` occupied by the list value). So:
a dedicated `type` collection class at a home that reads as "the collection of types" without colliding.
The cleanest I see: move the generic list value type off the bare `type.list.@this` slot is too invasive
(it's fundamental) — so the type collection takes a sibling name. What name do you want (`type.registry` is
out; `type.list` is taken; `type.of`/`type.index`/`type.set`?) — or is (1) the wrapper the right shape?

## Blocking

The whole Stage-3 core (index + perimeter home) sits on this. Nothing built yet — flagged before I place
runtime-hot registry code in the wrong class.
