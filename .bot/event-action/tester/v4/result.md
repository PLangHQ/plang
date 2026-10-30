# tester v4 on event-action (tip fe4167c02) — PASS

Pattern first: one door / the value answers (12); a birth fact, not a search (11).

## Run (clean tree, build first)
- C#: Modules 1496, Types 1042, Wire 641, Data 877+probes, Generator 191, Runtime 1450 — green. plang `--test`: 321 total, 320 pass, 0 fail, 0 stale, 1 skipped (baseline). Tree clean after. No new red names.

## v3 findings re-checked
1. Plain-Add variable event never fired (12): probe P1 now passes — one store (`_on` keyed by the variable), one bind door. P2 (nested), P3 (case), P5 (remove), P6, P7 (50 concurrent) all pass.
2. After-load join false green: the coder's `ABindAfterAVariablesListWasMade_JoinsIt` goes red when Join skips variable lists (mutation, reverted); my P4 red too. Caught.
3. First asks: goal start, sub app, restore tests present (SystemEventsTests).
5. Failed load: two tests (not kept; drops what it joined).

## Bytes per set (old table → now; 1,000 sets, my harness `TesterMeasure`)
| case | v3 old | now |
|---|---|---|
| nothing bound, one name | 5,009 | 3,464 |
| nothing bound, 1,000 names | 5,230 | 3,681 |
| watch count, hit | 34,998 | 4,424 |
| watch count, miss | 34,363 | 3,809 |
| type event after set | 34,964 | 4,296 |
Times 29–50 µs, noise. Finding 4 (Named parse) is closed by the numbers.

## Minor (not blocking)
- `Moments(variable)` scans `_events` linearly (name compare) on every set that has no list: a search per set, not a birth fact (pattern 11). Cheap now (3.7 KB total); grows with the number of events bound. A set of the names bound could be a hash lookup.
- Not run by me: the new plang test `SetRunsItsEvent` beyond the suite (it passes in `plang --test`).

Verdict: PASS.
