# For architect — number's kind dissolution is a MODEL redesign, not a Convert→Create relocation

**From:** coder. **2026-07-09.** Started `number` (last of the Stage-2 relocation sweep, 12/13 done);
tracing it shows it's a different animal from the other 12. Surfacing before writing, per Ingi's
guardrail (bigger than the plan defines → stop, ask, use the architect).

## The other 12 were relocations. number is a redesign.

For bool/text/date/…/dict/list/path/image, "relocate Convert→Create" was mechanical: the Convert body
is a self-contained parse/coerce, ported to `Create(item)` + courier. `NumberKind` is **not**
self-contained — it's the spine of number's whole value + arithmetic model:

```
NumberKind (15: SByte…Decimal) is woven through:
 • this.Tower.cs   — IntegerLadder (11 rungs), NarrowInteger/NarrowStrict, SignedClimb,
                     WiderInteger, Category (Integer/BinaryFloat/Decimal), ClrToKind/KindToClrType
 • this.Convert.cs — CoerceToKind (the 15-arm build switch), KindFromName, FromObject
 • this.Unary.cs   — FromDoubleAsKind
 • serializer/Default.cs — the 15-arm read/write switch
 • this.Arithmetic / this.Equality / this.Operators / this.Axes — promote-then-narrow keyed on Kind
```

Plan line 71 targets a **new model**, not a port:
- **Kind = storage type** (declaration → source → default `long`, a setting)
- **Precision = decimal places, edge-only** (max in every calculation; round at output/explicit)
- Overflow/mix policy settings-carried — stays
- Each precision owns its build at `type/number/kind/<k>`; CoerceToKind + serializer switches +
  FromDoubleAsKind dissolve.

## The underspecified parts (what I'd have to invent — so I'm not)

1. **Does "Precision = decimal places, edge-only" REPLACE the promote-then-narrow integer ladder, or
   sit beside it?** Today number's arithmetic is 15-kind promote/narrow (int+int→long on overflow,
   the SignedClimb, NarrowInteger). If precision becomes "decimal places, max-in-calculation, round at
   edges," that's a *different arithmetic* — does the IntegerLadder/narrowing survive at all, or is the
   whole promote/narrow machinery replaced by "compute wide, round at output"?
2. **Do the 15 CLR-storage kinds stay** as kind classes at `type/number/kind/<k>` (selected via a door,
   like item kinds), with arithmetic still promoting/narrowing across them — OR does "Kind = storage
   type, default long" collapse them to a small set (long + decimal + BigInteger) and the rest become
   just I/O storage hints?
3. **The arithmetic files (Arithmetic/Equality/Operators/Axes)** — are they in scope for this move, or
   only construction (Convert/serializer/Unary)? The plan lists the *construction* switches to dissolve
   but the ladder lives in arithmetic; "no two patterns coexisting" implies arithmetic moves too, which
   is far larger than the construction port.

## Two ways forward (your call)

- **(A) Architect specs the number-model redesign** — the concrete shape of kind-as-storage +
  precision-as-decimal-places + what happens to the ladder/narrowing/arithmetic. Then I implement it as
  its own focused piece (it's comparable in size to the whole 12-type sweep). This is the honest scope.
- **(B) Thin interim now, full dissolution after the model is specced.** Give `number` the same
  static-factory `Create(item)` + courier as the other 12 — delegating to the *existing*
  CoerceToKind/FromObject internally (one thin door, no model change) — so the relocation sweep + compare
  pass + hub deletion can complete for all 13 types. The 15-kind→per-kind-class dissolution then lands on
  the architect's number-model design. This technically defers the "same move" coupling (golden rule),
  but keeps number's `Create` consistent with the other 12 and unblocks the hub deletion.

**Coder lean: (B) for immediate consistency + (A) queued.** The `Create` door is the Stage-2 unification;
number's *internal* kind model is a separate axis, and improvising a 15-kind arithmetic redesign at the
tail of the sweep is where number bugs get born. But the "in the same move" line is yours to relax or hold.
