# For architect — the compare pass's materialization gate (my flag was an obpv; need the OBP-clean home)

**From:** coder. **2026-07-10.** Implementing the async compare pass (`stage2-compare-async-answer.md`).
Hit a gap between "no eager `Value()` of the whole graph" (container short-circuit) and "items can't
self-render." My first shape was a bool flag a dispatcher branches on — Ingi flagged it obpv (a fork). So
the gate needs a real home; writing it up before touching the ~20 files.

## The tension

`data.Compare` must hand `item.Compare` two **comparable** items. What "comparable" requires differs by
type, and the differences don't line up with any existing marker:

```
scalar / template ("cache: %off%")  →  needs RENDER (await Value) to compare its VALUE
source (%jsonFile%, unparsed)        →  needs PARSE  (await Value); its shallow Peek is source.@this,
                                        Rank 0 — the flag-#1 trap (must rank off the declared type)
real container (%listA%)             →  must NOT be pre-materialized: list.Value() deep-renders all N,
                                        which kills the element-wise short-circuit the ruling REQUIRES
```

Two facts make this hard:

1. **`Order(item other)` has no `Data`** — so an item cannot render itself inside `Order` (rendering is a
   `Data.Value()` operation; the context-free item value doesn't carry its Data). So whatever rendering a
   scalar needs has to happen in `data.Compare` (which holds the Data), *before* `item.Compare`.
2. **`IsFinal` conflates two things** — it's `false` for BOTH `list`/`dict` (container whose *elements* may
   be lazy — but the container compares fine element-by-element) AND a template `text` (a *scalar* whose
   whole value needs rendering). So `IsFinal` can't be the gate: a list must stay shallow, a template text
   must materialize, yet both are `IsFinal == false`.

Net: `data.Compare` must materialize scalars/templates/sources but leave real containers shallow — a
per-type split. The split is real; the question is where it lives without a fork.

## What I tried (the obpv)

```csharp
// data.Compare — reads a flag and branches. THIS IS THE FORK Ingi rejected:
var a = Peek().MaterializeForCompare ? await Value() : Peek();
// item base: virtual bool MaterializeForCompare => true;  list/dict => false;
```

A bool on the element that the dispatcher switches on = behavioral fork. Correct to reject.

## Candidate OBP-clean homes (need your ruling)

- **(A) Polymorphic "give me my comparable form" on the element.** No branch in the caller — the behavior
  IS the element's:
  ```csharp
  // data.Compare:
  return await (await Peek().ComparableForm(this)).Compare(await other.Peek().ComparableForm(other));
  // item base:      ComparableForm(data d) => d.Value();     // scalars/templates/sources render/parse
  // list/dict:      ComparableForm(data d) => this;          // stay shallow; Order walks element Data lazily
  ```
  Behavior on the element, no flag, no switch. Cost: a new virtual whose name is a noun-phrase but still
  reads a bit verb-y; and it takes the `Data` (the item reaching back for its own door).

- **(B) The reconcile lives on `Data`, not `item`.** Since element recursion already goes Data→Data
  (`list.Order` walks element `Data`, each `await element.Compare(...)`), make `Compare`/`Order` a **`Data`**
  behavior throughout: `data.Compare` picks the driver off `data.Type` (declared, no materialization),
  then dispatches to the driver's per-type compare which reads the OTHER `Data` and renders lazily via its
  own door. Containers never pre-materialize because the walk is Data-level. Cost: `Order` becomes a
  `Data`-keyed dispatch rather than an `item` virtual — closer to today's `CompareValues` shape, which the
  ruling wanted gone; but it dissolves the "item can't self-render" problem entirely.

- **(C) `list.Value()` stops deep-rendering** — becomes lazy (renders elements on element access), so
  `data.Compare` can `await Value()` uniformly and containers still short-circuit. Cost: changes
  `list.Value` semantics for every consumer, not just compare — broad and risky.

## Coder lean

**(A)** — keeps the architect's ruled shape (per-type `Order` on the item, async reconcile) and turns the
gate into polymorphism instead of a flag. The item reaching for its `Data` in `ComparableForm` is the one
wrinkle; if that's the smell, **(B)** is the honest alternative (accept that comparison is a `Data`
behavior because operand *resolution* is a `Data` behavior). **(C)** I'd avoid — it moves a broad,
unrelated semantic.

## Everything else is ready

`Rank` ×10 table, the 13 `Order` bodies (coerce via the pure `Create` core), `@null.Rank=1000`, async sort,
`Invert()`/`AsSign()` (landed), and the `Compares`/statics/`CoerceOwn`/`CompareValues`/`type.Rank`+`Compare`
deletions are all shape-clear. Only this materialization gate blocks the sweep.
