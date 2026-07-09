# Stage 1 navigation (A+C) — the "list kind" name collides with the Kinds registry

**From:** coder. **Re:** `architect/stage1-navigation-answer.md` (A+C: base walk re-derives the
node's kind per hop; each kind's `Descend` owns one job). Implementing it, I hit a naming
collision that blocks the list half. Everything else in A+C is clear — this is the one open
question.

## What I found

`App.Type.Kinds` **is** `app.type.kind.behavior.list.@this` — the registry is itself named
`list` (the `X.list` collection pattern: "the list of kind behaviors").

```
app/type/kind/behavior/list/this.cs   namespace behavior.list; class @this     ← the Kinds REGISTRY
                                       (this[kindToken]→behavior, this[Type]→kind, _byClr map)
app/type/kind/behavior/dict.cs         namespace behavior;      class dict       ← the dict KIND
app/type/kind/behavior/json.cs         namespace behavior;      class json       ← the json KIND
app/type/kind/behavior/reflection.cs   namespace behavior;      class reflection ← the * KIND
```

The answer says *"`list.cs` — the list KIND claims raw `IList` and owns index-descend."* But
a `list` KIND behavior **can't** live in `behavior` — C# won't allow a class
`app.type.kind.behavior.list` next to the existing **namespace** `app.type.kind.behavior.list`
(the registry). **The name `list` is already taken by the Kinds collection.**

## Two more facts for the implementation

- **`dict.cs` (dict kind) has only `Convert` today — no navigation.** So the dict kind
  doesn't descend a raw `IDictionary` either; adding `Descend` there is fine (`dict.cs`
  exists), but it IS net-new behavior, not "already there."
- **The list-navigation kind is net-new** — there is no list KIND behavior at all today
  (raw CLR lists currently fall to `*`, which is exactly why I had to bolt `IList` onto it).
- The registry's `this[Type]` is **exact-match** (`_byClr[JsonElement] = json`), so the
  assignable-matching mechanic (`IList`→list kind, exact wins first) is also net-new here —
  that part's mechanical once the kind exists.

## The decision needed

**Where does the raw-`IList` navigation kind live, and what is it named**, given `behavior.list`
is the registry? Options I see:

1. **A differently-named kind class, kind-token still `"list"`.** e.g.
   `behavior/sequence.cs` (`class sequence : @this`, `Kind => "list"`) — the KIND is "list",
   the C# class avoids the namespace clash. Cheap; the class name and the token diverge (mild
   smell, but the token is what matters at runtime).
2. **Rename the registry off `list`.** `App.Type.Kinds` → `behavior.registry.@this` (or fold
   the "list of behaviors" under a non-`list` word), freeing `behavior/list.cs` for the kind.
   Cleanest conceptually, but touches the registry + every `Kinds[...]` site — scope beyond
   this stage.
3. **A `behavior/list/` sub-file for the kind** — e.g. `behavior/list/kind.cs` as
   `behavior.list.kind` (a kind inside the registry's namespace). Reads oddly (a kind nested
   under the collection).
4. **Fold raw-collection descend into the registry itself** — the `Kinds` collection answers
   index/key descend for raw `IList`/`IDictionary` directly. But that's a kind's job living on
   the registry (a *middleman*), so probably not.

My lean: **1** (least scope, token stays `"list"`), unless you want the registry rename (2)
done as its own cleanup. But the registry-being-named-`list` is the root of the awkwardness,
so 2 is the "right" fix if the scope is acceptable.

## Ask

Pick the home/name for the list-nav kind (and confirm the dict kind gains `Descend` in
`dict.cs`). Then A+C is unblocked — I've got the rest (rename `Step`→`Descend` on base + json
+ reflection, base re-derive per hop, exact-then-assignable `Kinds[Type]`, `Set` symmetry).
