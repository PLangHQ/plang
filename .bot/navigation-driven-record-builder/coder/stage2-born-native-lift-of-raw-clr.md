# For architect — raw CLR primitives don't born-native-lift; the lift rides two dying paths

**From:** coder. **2026-07-10.** The compare pass landed (`db1bf3396`) — correct, green, zero regressions.
But its acceptance tests (`CompareRedesign/Stage4_PerTypeCompareTests`, etc.) stay red, and Ingi's two
questions ("should `json.Parse(9)` be there?", "isn't `convert.OfStatic` dead code?") pinpointed why: the
born-native lift of a raw CLR primitive runs through the two mechanisms this branch is deleting.

## The trace

```
new Data("a", 9)                          // a raw C# int (the Cmp test helper; also any C# handler return)
  → json.Parse(9)                         // item/serializer/json.cs — the DOM WALKER (dies, Json-sweep read side)
                                          //   a raw int isn't json; Parse passes it through unchanged → 9
  → type.Create(9)                        // the natural lift
      → convert.OwnerOf(typeof(int))       // → number family   [the CLR-type→plang-type map, on the DYING hub]
      → convert.OfStatic(number, 9)        // convert.Of* — DIES in the Stage-2 hub deletion
          → number.Convert(9)              // the transient adapter now REQUIRES item.@this
                                          //   `value is not item.@this` → error
      → falls through → new Clr(9)         // ← the bug: a raw int becomes a Clr carrier, Rank 0, base Order
```

So `da.Value()` yields a `Clr(9)`, not `number.@this(9)`; `9.Compare(10)` runs base `Order` → `NotEqual`,
not `Less`. Compare is correct for born-native values — the values just never became native.

## Why patching is wrong

- Making `number.Convert` accept raw CLR props up the **dying hub** (`convert.Of*` is on the Stage-2
  deletion list). Wrong layer.
- The `json.Parse` step is the **DOM walker** already ruled to die in the Json-sweep read side. A raw int
  should never touch it.

Both are dead-code-to-be. The lift shouldn't route through either.

## The real gap

A raw CLR primitive (`int`, `long`, `double`, `DateOnly`, `Guid`, …) reaching `type.Create` (or the `Data`
ctor) should **born-native-lift to its plang item type directly** — `(number.@this)9`, `(date.@this)d` —
via each type's implicit operator / pure `Create`. Two pieces are missing a home once the hub + walker die:

1. **The CLR-type → plang-type map** (`int→number`, `DateOnly→date`, `Guid→guid`, `byte[]→binary`, …).
   Today it's `convert.OwnerOf`, on the dying hub. Where does it live after? (A per-type registration? A
   switch on the perimeter? The `ICreate<T>` surface?)
2. **The lift call itself** — `type.Create`, seeing a raw CLR scalar, should hand it to the owning type's
   born-native lift (implicit operator / `Create`), not `convert.OfStatic` and not `json.Parse`.

## Questions

1. Is this yours to spec as part of the **hub deletion** (the natural place — the hub's `OwnerOf`/`OfStatic`
   are exactly what's dying), or the **Json-sweep read side** (where `json.Parse` dies), or its own step?
2. Where does the CLR-type→plang-type map live post-hub? (It's the one piece of `convert` that isn't
   "construction" — it's "which type owns this CLR shape.")
3. Interim: should `new Data(name, rawCLR)` even be a supported entry, or must callers pass born-native
   values (the test helper switches to `(number.@this)9`)? I.e. is raw-CLR-in-a-Data a real perimeter or a
   test artifact to fix at the test? (In real flow, values arrive as JsonElements → `NumberLeaf` →
   `number.@this`, so the raw-CLR path may be genuinely rare — but `type.Create`'s `Clr(raw)` fallback
   means it fails silently rather than lifting.)

## Status

Compare pass is committed and correct; these acceptance tests were red at baseline too (they've never
passed — they're forward-looking for exactly this lift). They go green the moment a raw CLR primitive lifts
to its plang type. No compare rework needed — this is upstream of compare.
