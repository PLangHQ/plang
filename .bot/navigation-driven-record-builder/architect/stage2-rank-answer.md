# Decision — Rank: plain ints, declared per type, ×10 scale (the defined table)

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage2-rank-inventory.md`. Good inventory — the intra-tier-order-is-load-bearing observation and the guid collision were both right and both shaped the ruling.

## The form — your enum lean is rejected; plain virtual int wins

- **Enum-of-all-types rejected:** a central, closed catalog of every type (the registry-knows-all-elements inversion), and a plugin type via `code.load` can't insert itself into an enum. Precedence is declared **by the type, on the type**, like every other behavior.
- **No tier names either** (we considered `Textual`/`Numeric` constants and Ingi killed them): they'd be new words for things that already have names — text, number. A tier whose name is just its anchor type's name adds nothing.
- So: **`public virtual int Rank`** on `item.@this` (base `0` — the apex/unranked), each type overriding with its value from the table. The contract lives ONCE, on `item.Rank`'s doc comment: *higher drives; the lower operand coerces into the higher via its `Create`; used only relationally, never a result, never on the wire.* Each type's override carries a one-line why relative to its neighbors.

## The table (×10 of the inventory — Ingi wants big gaps; guid gets its own slot)

| Rank | type | why it sits here |
|---|---|---|
| 100 | text | lowest — everything coerces out of a string |
| 150 | choice | a named set; coerces from text |
| 200 | bool | |
| 250 | binary | |
| 300 | number | |
| 400 | duration | |
| 450 | time | |
| 500 | date | |
| 550 | datetime | |
| **600** | **guid** | **own slot — off the old `45` collision with time; it was never temporal** |
| 700 | dict | |
| 750 | list | highest — containers coerce into nothing |

Gaps are deliberate — insertion room for future types (the same idea as number's `IntegerLadder` rungs, which is the ordered-precedence precedent already in the tree; found at `number/this.Tower.cs:116` — that's the "named tiers" Ingi remembered. Don't reuse the `Rung` name here; it's number's word and the value-model's "rung-2" is yet another axis).

## Rides along

- Everything else in the compare pass is as ruled (`stage2-compare-pass-answer.md`): instance `a.Compare(b)`, dispatch inlined into `data.Compare`, `Invert()` on right-operand-drives with its named test, the `Compares` registry + statics + `CoerceOwn` deleted.
- Acceptance additions for the rank change specifically: the guid↔time pair now has a deterministic driver (guid) — pin it with a test; the ×10 rescale must be invisible (relational use only — a test asserting no absolute rank value leaks anywhere).
