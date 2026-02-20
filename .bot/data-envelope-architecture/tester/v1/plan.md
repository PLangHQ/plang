# Tester v1 Plan — Engine.Types Phase 1

## What I'm testing

Coder v1 added `Engine.Types` — a new class consolidating PLang type knowledge. 62 TUnit tests were written. My job: verify those tests actually catch bugs, not just confirm the implementation.

## Steps

1. **Run C# test suite** — `dotnet run --project PLang.Tests` — verify all pass, no regressions
2. **Analyze test quality** — For each test group (Clr, Name, Kind, Mime, Compressible, Add/Remove, Engine integration):
   - Does the test verify intent or just mirror the implementation?
   - Would a subtle bug (flipped condition, wrong return, off-by-one) be caught?
   - Are edge cases covered?
3. **Check coverage gaps** — Are all public methods tested? Which code paths have no test?
4. **Check PLang test existence** — Phase 1 is additive with no runtime behavior change, so PLang tests may not be needed yet. Verify this is correct.
5. **Write test-report.json** — Structured findings for coder and auditor

## Key test quality questions

- `Clr()`: Do tests verify the returned CLR type is semantically correct, or just matching a hardcoded expected value? (Both are equivalent here — the spec IS the mapping.)
- `Name()`: What happens with types not in the reverse dictionary? Generic types with backtick arity?
- `Kind()`/`Mime()`: Are the edge cases tested — null input, empty string, paths vs extensions?
- `Compressible()`: Is the boundary between compressible and not-compressible fully tested?
- `Add()`/`Remove()`: Do tests verify the side effect (the mapping actually changed), not just no-exception?
- `BuilderNames()`/`ComplexSchemas()`: Are these tested at all?

## Deliverables

- `test-report.json` at branch root
- `v1/summary.md`
- `summary.md` at bot root
- Updated `report.json`
