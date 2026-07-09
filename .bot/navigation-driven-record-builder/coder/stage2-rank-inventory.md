# Rank inventory — for Ingi to rule on the form (compare pass, Stage 2)

**From:** coder. **2026-07-09.** The architect's task (`stage2-compare-pass-answer.md` §3): inventory
the existing `CompareRank` values + locate any existing named tiers, so Ingi picks the form of the
new virtual `Rank` with real numbers in hand.

## All 13 `CompareRank` values (sorted)

| Rank | Type | Apparent tier | Notes |
|---|---|---|---|
| 10 | text | **scalar-textual** | lowest — most coercible-INTO (`"true"`→bool, `"5"`→number) |
| 15 | choice | scalar-textual | |
| 20 | bool | scalar-textual | |
| 25 | binary | scalar-textual | |
| 30 | number | **numeric** | |
| 40 | duration | **temporal** | |
| 45 | time | temporal | |
| 45 | guid | temporal? | **COLLISION with time** — guid isn't temporal; likely wants its own slot |
| 50 | date | temporal | |
| 55 | datetime | temporal | |
| 70 | dict | **container** | |
| 75 | list | container | highest — least coercible |

## What Rank means

Precedence — "who drives / coerces." The higher-ranked type of a pair drives the comparison and the
lower one coerces INTO it. Used only relationally (`a.Rank >= b.Rank ? a.Compare(b) : b.Compare(a).Invert()`),
never a result, never on the wire. text is lowest because everything coerces out of a string; containers
are highest because they don't coerce into anything.

## Observations

- **The values DO cluster into 4 tiers** — scalar-textual (10–25), numeric (30), temporal (40–55),
  container (70–75) — with deliberate gaps between tiers.
- **BUT intra-tier order is load-bearing**, not incidental: text(10) < bool(20) is WHY `"true"` coerces
  into bool and not vice versa; duration(40) < date(50) orders within temporal. A pure "named tier"
  form (all scalars equal) would lose that and make `"true" == someBool` tie on caller order.
- **One real collision to resolve:** `guid == time == 45`. A tie means caller order drives; guid
  probably wants its own slot (it isn't temporal).
- **No existing named-tier enum found.** Searched `enum *Rank` / `Tier` / `Precedence` / `CoerceRank` /
  `RankTier` — only an unrelated "precedence" doc comment in `app/type/reader`. The architect's note
  that "named tiers already exist somewhere (Ingi)" didn't resolve to code I could find — Ingi, if
  they exist under another name, point me at it and I'll align to it.

## Coder lean (Ingi rules)

Because the intra-tier order carries meaning and there's a collision to fix, my lean is **keep an
ordered scale but name it** — e.g. a `Rank` enum whose members are the types in precedence order
(`Text < Choice < Bool < Binary < Number < Duration < Time < Guid < Date < DateTime < Dict < List`),
so the ordering is explicit and named (no magic ints) while preserving the total order the current
numbers encode. That removes the "magic numbers" smell AND the collision (guid gets its own member),
without collapsing meaningful intra-tier order the way coarse tiers would.

If you'd rather keep ints, each keeps its value + a naming comment and guid moves off 45.
