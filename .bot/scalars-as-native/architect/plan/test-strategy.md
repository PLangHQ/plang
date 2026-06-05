# Test strategy — `scalars-as-native`

For the test-designer. Maps each stage to its test layer, names the load-bearing proof, and gives the per-stage integration cut.

> **You own the final shape.** Test names, fixture files, assertion style, and exact counts are yours. Keep the layer split, the load-bearing proof, and the per-stage green bars; everything else is suggestion.

## Two layers

- **C# unit** (`PLang.Tests`, `dotnet run --project PLang.Tests` — recompiles in-place, immune to the stale-binary trap). The wrappers in isolation: each type's compare / equality / truthiness / parts / ops / bare-serializer; `item` as the base contract; value-equality as a dict key + in a `HashSet`; the coercion mediator over two wrappers; the `where T : item` constraint (a `Data<int>` should fail to compile — assert via a negative-compilation note or a guarded reflection test).
- **PLang integration** (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` — rebuild from clean first per CLAUDE.md's stale-binary trap). Developer-visible behavior end to end: `set`/read a scalar → it's a wrapper; `is <type>` queries; `foreach` atomicity on text; sort/compare across types; `.plang` vs `.json` serialization; `→ returns <clr>` reconstruction.

Put the type-distinctness and round-trip behavior at the integration layer — that's where the date≠datetime bug actually bites.

## The load-bearing proof (Stage 3) — `date` ≠ `datetime`

Today a `date` value silently becomes a `datetime`: `ScalarComparer` coerces `DateOnly → DateTimeOffset` and classes it `"datetime"`. The proof:

```
- set %d% = 2026-01-01        / a date value
- if %d% is date              / must be true
    - write to %isDate%
- if %d% is datetime          / must be false
    - write to %isDatetime%
- assert %isDate% == true
- assert %isDatetime% == false
```

This **must fail before Stage 3 and pass after** — it's the regression anchor that proves date/time stopped collapsing into datetime, not just that a wrapper exists. Land it failing first. Mutation-test it after Stage 3: revert the `ScalarComparer` `Name()`/`IsDateTime` fix and confirm it goes red (announce per CLAUDE.md).

Secondary load-bearing proof (whole-branch): **the constraint compiles.** After Stage 7, `Data<T> where T : item` is on and the tree builds with no `Data<rawCLR>` slot — that single fact is the strongest evidence every value flows native. A C# negative test (a `Data<int>` field that must not compile) pins it.

## Per-stage integration cuts

- **Stage 1 (`item` base)** — `number` behaves identically (arithmetic, compare, `if`, `→ returns int`); a C# test treats a `number` as `item` and gets compare/equality/truthiness through the base; `item` is abstract (cannot instantiate).
- **Stage 2 (`text`)** — `set %s%="Hello"` → `%s.length%`/upper/contains; `→ returns string` reconstructs; two `text("a")` equal as dict keys; **`foreach %s%` does not char-iterate**; empty text falsy; signed text survives `.plang`, bare on `.json`.
- **Stage 3 (`datetime`/`date`/`time`)** — the load-bearing `date is date` / `not datetime` proof; `time` value is a `time`, not unhandled; date/datetime/time each sort within their own type; `→ returns DateOnly`/`DateTimeOffset`/`TimeOnly` reconstruct; ISO round-trip `.plang`, bare `.json`; a `DateTime` input lands as `datetime`.
- **Stage 4 (`duration`)** — duration parts + compare; equal durations value-equal; `→ returns TimeSpan`; documented zero-duration truthiness; `.plang`/`.json`.
- **Stage 5 (`bool`)** — `set %b%=true` → `if %b%`/`if !%b%`; a condition action reads a `bool` result via `IBooleanResolvable`; `→ returns bool`; bare `true`/`false` on `.json`.
- **Stage 6 (`null`)** — `set %x%=null` → falsy, `%x%==null`; a **missing** variable reads as absent/`NotFound`, **not** `null.@this` (the guard test — this one catches the worst regression); nulls sort last; bare `null` on `.json`.
- **Stage 7 (lock)** — constraint compiles; `"5" == 5` and numeric widening still resolve via the mediator; date-vs-datetime comparison is a clean coercion outcome, not a silent equal; grep-clean of `is <scalar>` outside the two legal sites.

## Rough test budget (estimate, yours to set)

- **C# unit:** ~6–9 per wrapper × 7 wrappers + item base (~5) + mediator (~6) + constraint (~3) ≈ **55–70**. (Each wrapper: compare, equality, hashcode/dict-key, truthiness, parts/ops, bare-serialize, round-trip.)
- **PLang integration:** the load-bearing proof + ~3–5 per type (set→wrapper, `is` query, returns-reconstruct, serialize, the type-specific edge) + atomicity + null-vs-absent guard + mediator cross-type ≈ **30–40**.

These are the integration tests that catch what unit tests miss (born-native + sweep). One concern per `.test.goal`.
