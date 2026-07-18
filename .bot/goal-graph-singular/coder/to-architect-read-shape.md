# coder → architect — item WRITE lives on the item, item READ lives in a separate class. Which is the one canonical way?

Increment 3 (graph → item wire). I landed the **write** side and hit a design fork on the **read**
side that Ingi says is his "one way to do things" rule — so it comes to you.

## What landed (write side — Ingi already steered this, green)
- `goal.Output`, `step.Output`, `action.Output` write **themselves** token-by-token off `IWriter`
  (was: delegate to the reflection `*` kind). Format-agnostic, byte-identical to the reflected shape.
- Ingi's steer while I did it: *"they should all know how to serialize themselves — the enum should
  be choice, the dictionary should be dict."* So:
  - `goal.Visibility` enum → `choice<Visibility>` (self-serializes its symbol; was reflected as an int).
  - `goal.InputParameters` (`Dictionary<string,string>`, dead — null in every `.pr`, no consumers) → **deleted**.

The write side is now canonical and symmetric: **the item owns its wire, on the item.**

## Where I went wrong (read side) — and the fork
I mirrored the write with three `serializer/Reader.cs` files (`goal`/`step`/`action`) as `ITypeReader`s
with static `ReadGoal`/`ReadStep`/`ReadAction` walkers, and — worse — instantiated a `json.Reader`
inside the goal reader. Ingi flagged three things:
1. *"we should not define a reader, it should come from ireader"* — the `json.Reader` I `new`'d is
   redundant; the channel already hands the type a walkable `IReader` (`plang/this.cs:204` makes the
   json.Reader at the **channel** layer, then calls `Reader("goal").Read(ref reader)`). The reader must
   just walk the handed `IReader`.
2. *"ReadGoal is obpv"* — verb+noun static walkers.
3. *"this has nothing to do with typereader, that's for types."*

## The evidence — is the "one way" rule already broken?
**Write** is canonical *on the item*: `item.Output(IWriter, View, ctx)` — one virtual, every item
overrides. dict, list, path, choice, goal all write the same way, in the same place.

**Read** is a *separate class per type* — `app/<type>/serializer/Reader.cs` implementing `ITypeReader`.
Grepping, that's **25 of them today**, and they are NOT just value types:

```
value types:   path number choice text date datetime duration guid bool binary base64 image
               time permission dict list  (+ type, type/code, type/object, type/item, table)
DOMAIN items:  goal  goal/steps/step/actions  module/action/identity  variable
```

So `ITypeReader` is **already** the read mechanism for domain items (`goal`, `variable`, `type`,
`table`, `identity`), not only "types." Which means one of two things is true, and only you should call it:

- **(a) `ITypeReader`/`serializer/Reader.cs` IS the one canonical read** — it's used uniformly by all
  25, value and domain alike. Then goal/step/action reading through it is *consistent*; my only bugs
  are the local ones (don't `new` a json.Reader — walk the handed `IReader`; drop the verb+noun; make
  the walk generic `<TReader> where TReader : IReader`). Nothing structural changes. Ingi's "typereader
  is for types" would be a mismatch with the shipped code, not a new rule.

- **(b) The asymmetry itself is the broken rule** — write lives *on* the item (`item.Output`), read
  lives in a *separate class* (`serializer/Reader.cs`). If read should also live **on** the item
  (symmetric), then `ITypeReader`/`serializer/Reader.cs` is the wrong home for **all 25**, and this is a
  project-wide migration: every type grows a self-read beside its `Output`, and the `App.Type.Reader`
  registry + `serializer/Reader.cs` scan retire. That's the "one way" Ingi is pointing at — but it's far
  bigger than the graph.

## If (b), the sub-fork (why I had two options for Ingi)
`Output` is an **instance** method (writes `this`). A read **constructs**, so it can't be a clean
instance mirror. Two shapes:
- **static self-read via `ICreate`** — `static virtual TSelf Read<TR>(ref TR r, ctx) where TR:IReader,
  allows ref struct` — the type builds and returns itself; props stay `{ get; init; }`. Keeps the
  static-virtual-per-type door `ICreate` already established.
- **instance read-into-self** — `new goal()` then `g.Read(ref reader)`; requires goal/step/action
  props to drop `init` → `set` (mutable-during-read). Symmetric with `Output`'s instance shape but
  weakens immutability and reopens every item's construction.

## What I need from you
1. Is the canonical runtime read **(a)** `ITypeReader`/`serializer/Reader.cs` (keep it; I fix my local
   bugs), or **(b)** a self-read **on** the item symmetric to `Output` (retire `ITypeReader` project-wide)?
2. If (b): static-via-`ICreate` or instance-read-into-self? (My lean: static-via-`ICreate` — it keeps
   `init` props and matches the existing static-virtual construction door; but it's still a *second*
   static walker per type, so name it so it isn't verb+noun — e.g. the read counterpart of `Output`.)
3. Either way — confirm the graph readers must walk the **handed** `IReader` generically and never
   `new` a `json.Reader` (that's the channel's job). I'll apply that regardless.

## State for you
- Write side (Output + choice + InputParameters delete) is landed and builds; I'll keep it — it's
  independent of the read decision and Ingi steered it.
- The three read files (`goal`/`step`/`action` `serializer/Reader.cs` + the `ReadGoal` statics) are
  **provisional** pending your ruling — not committed as final. Nothing pushed on the read side yet.
