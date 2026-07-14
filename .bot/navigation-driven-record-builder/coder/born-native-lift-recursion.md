# Defork recursion — the apex selection door breaks the "never re-selects the same entity" invariant

From coder, for architect. Follows your born-native-lift answer (`8c379a4e5`). I implemented the
defork per the spec; it **stack-overflows**. The root is the selection door the spec picked for the
apex lift, and it invalidates the spec's own termination argument. Sending it up for a ruling before
I redo the move — Ingi has already rejected the obvious band-aid (a `_clr`-only door beside the
indexer) as an obpv ("taping over a bug created somewhere else"), and he's right.

## What I built (per the spec)

- `item.@this.Create(object? raw, context)` — the moved lift body, its ownership rung reading the
  selection door the spec named: `context.App.Type[raw.GetType()]` (the CLR-type indexer).
- `type.list.@this.Create` deleted; the ~35 sites retargeted to `item.@this.Create`; the entity
  door's fallback (`type/this.cs:313`) retargeted to `item.@this.Create`.

It compiled clean. On the first test run it **stack-overflowed**, repeated 5345×:

```
   at app.type.this.Create(System.Object, app.actor.context.this)        // the entity door
   at app.type.item.this.Create(System.Object, app.actor.context.this)   // the apex lift
   … (repeat) …
```

## The root — the selection door conflates two questions

Your termination argument: *"Post-move the recursion is one-directional and bounded: entity → apex
(fallback) → a DIFFERENT owner's entity → its `_byContext`, terminal; the apex never calls back into
the same entity."* That holds **only if the apex selects a different, terminating owner.** It doesn't,
because `App.Type[System.Type]` (`type/list/this.cs:277-289`) is **two doors in one**:

```csharp
if (_clr.TryGetValue(clrType, out var owner)) return this[owner];        // CONVERSION ownership (int → number)
return _typeToName.TryGetValue(clrType, out var name) ? this[name] : null; // IDENTITY  (this CLR type IS plang type X)
```

- **`_clr` picks terminate.** A conversion-owner (number owns int) is Creatable — `Creatable`
  (`type/this.cs:355`) binds `_byContext` only when the entity's `ClrType` implements `ICreate<self>`.
  number.Create(int) succeeds → no fallback → terminal. (The OLD registry lift used **`_clr` only**,
  which is why it never looped.)
- **`_typeToName` picks can loop.** An identity-owner whose `ClrType` is a non-`ICreate` shape has
  `_byContext = (_,_) => null` — it **declines** → entity fallback → apex → the indexer picks the
  **same** identity-owner again (its `_typeToName` entry is unchanged) → infinite recursion. That is
  the loop above.

So the spec's `App.Type[raw.GetType()]` asks "who CONVERTS from this shape **or**, failing that, what
type IS this shape by identity." The lift wants only the first. The identity fallback is a different
question that, for a non-Creatable owner, has no terminating build — it just bounces back to the apex.

## The obpv Ingi is pointing at

Not "the apex needs a `_clr`-only helper." The smell is that **`App.Type[System.Type]` is one door
answering two different questions** (conversion-ownership vs. identity), and the lift is forced to
take both because that's the only door. A `_clr`-only method beside it would just be a second door
past the conflated one — the fork survives under a better address (exactly the shape you defork'd the
lift for).

## For your ruling — where the fix belongs

1. **Un-conflate the selection door.** `App.Type[clrType]` becomes conversion-ownership only (`_clr`);
   identity-lookup ("what plang type is this CLR type") is a separate, named door for whoever needs
   it. The lift then honestly asks conversion-ownership; no recursion; no second door. Cost: the ~14
   other callers of `App.Type[System.Type]` must be triaged — which want conversion, which want
   identity. (Its own doc says "conversion-ownership door (replaces convert.OwnerOf)" — so the
   `_typeToName` fallback may already be the wrong half living in the wrong door.)
2. **The entity fallback shouldn't re-enter the apex for an identity-owner.** Harder to make clean —
   the entity door can't easily know "I was reached via identity, don't bounce back."
3. Something I'm missing — you traced this ladder for the answer; if the `_typeToName` fallback is
   load-bearing for the lift in a way I don't see, name the raw type it's meant to catch and I'll
   trace from there.

My read is **(1)** — the conflation is the actual bug, upstream of the defork, and un-splitting it
makes the lift's selection honest. But it widens the blast radius to the indexer's other callers, so
it's your call, not something I'll guess at. Once you rule, the defork itself is mechanical (I have it
staged in my head; nothing else about the move is in question).

## Nothing committed

The defork is not committed (an uncommitted attempt was lost to a machine restart anyway). The branch
is clean at `8c379a4e5`. I redo the move once the selection-door question is settled.
