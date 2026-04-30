# Codeanalyzer v2 plan — verify coder v2 response to v1 review

## Context

v1 verdict was NEEDS WORK with 38 findings (10 MAJOR / 19 MINOR / 9 NIT). Coder v2 (commits `01aa150c` → `4018f26b`) addresses a focused subset:

- **Phase A** — cycle detection in `Data.AsT_Impl` (Finding 27, was crashing `plang test`)
- **Phase B** — `ActionClassInfo` → record + `EquatableArray<T>`; delete `__variables` + `__paramData` + `ParamData()` (Findings 1, 11, 12)
- **Phase C** — comment App.Run OCE catch; comment non-generic collection contract (Findings 28, 33)
- **Phase D** — trivial cleanups (Findings 2, 3, 6, 9, 21)
- **Phase E** — raw string literals for emission (Finding 19, requested mid-session by Ingi)

Plus new tests: `IncrementalCacheTests`, `NoDeadEmissionTests`, cycle tests in `DataAsTResolutionTests`, OCE behavioral test in `AppRunScaffoldingTests`, `ArrayList`/`Hashtable` non-generic tests.

Findings explicitly NOT taken in v2: 4, 5, 13, 14, 15, 16, 17, 18, 20, 22, 23, 24, 25, 26, 29, 30, 31, 32, 34, 35, 36, 37, 38. Coder logged each with rationale.

## Goal

Verify v2 actually fixes what was claimed. Apply all 5 passes against the new code:
- Pass 1 (OBP): mainly the new `EquatableArray` and the cycle-detection threadlocal.
- Pass 2 (Simplification): did the raw-string refactor add complexity? Are tests honest?
- Pass 3 (Readability): is the post-rename `@this` generator legible?
- Pass 4 (Behavioral): cycle key choice (raw string), thread-static lifecycle, output drift.
- Pass 5 (Deletion test): every new test line — does any test fail without it?

## Files to read

**Modified:**
- `PLang/App/Data/this.cs` — cycle detection in `AsT_Impl`; non-generic shape comment
- `PLang/App/this.cs` — OCE catch documentation
- `PLang.Generators/this.cs` — class rename (`LazyParamsGenerator` → `@this`)
- `PLang.Generators/Discovery/this.cs` — `ActionClassInfo` record; F2/F3/F6/F21 cleanups
- `PLang.Generators/Emission/Action/this.cs` — drop dead emission; raw string literals

**Created:**
- `PLang.Generators/EquatableArray.cs` — value-equal struct wrapper
- `PLang.Tests/Generator/IncrementalCacheTests.cs` — Roslyn driver test
- `PLang.Tests/Generator/NoDeadEmissionTests.cs` — regex test for dead fields

**Test additions (existing files):**
- `DataAsTResolutionTests` — cycle + non-generic
- `AppRunScaffoldingTests` — OCE behavioral
- `SnapshotParamsTests`, `GeneratorValidationTests` — generator folder rename

## Verification questions per phase

### Phase A — cycle detection
1. Is the cycle key safe? Coder uses raw `%`-containing string. What if two different deep-chain steps share the same outer string but resolve via different contexts?
2. Is `try/finally` complete? Does an exception inside the recursion clean up the set?
3. Is `_resolvingValues = null` only when the cycle root frame exits?
4. Do the tests actually detect StackOverflow without crashing the runner? (TUnit has assertions for this; let me verify they're correct.)
5. Was `plang test` re-run? Coder claims 165/42/5 — verify that's the real number.

### Phase B — record + EquatableArray
1. Does the new test actually drive `CSharpGeneratorDriver`, or is it just structural equality on the input record? The summary says "9 entries verifying value equality of `ActionClassInfo` and `EquatableArray<T>`" — that sounds like equality unit tests, NOT the Roslyn driver test the plan promised. **This was the gap I flagged in v1; check whether it's actually filled.**
2. `EquatableArray<T>` correctness — `IEnumerable<T>` impl, hash code distribution, default(EquatableArray<T>) behavior.
3. NoDeadEmissionTests — does it actually scan the generated file tree? Does it report all the fields? Are exclusions documented?
4. The plan flagged a Decision Point: dead-emission test will flag `__resolutionError` for v4-shape handlers. Coder went with option (b) — exclude until Phase 5. Is the exclusion scoped well?

### Phase C — behavioral comments + tests
1. OCE comment: does it explain the contract well enough to prevent a future "fix"?
2. OCE test: `OceThrowingHandler` fixture — read it. Does it pin both directions of the asymmetry (App.Run swallows; Step.RunAsync would propagate)? Plan promised both; summary says only one was added.
3. Non-generic comment: is it visible at the point that a future maintainer would touch?
4. Non-generic tests: do they actually assert the pass-through behavior?

### Phase D — trivial cleanups
1. F2 (drop `@this` disjunct) — verify both lines (134, 192) updated.
2. F3 (extract local) — readable?
3. F6 (`internal`) — verify external users (in `PLang/`, `os/`, `PLang.Tests/`) didn't reach in.
4. F9 (`@this` rename) — generator folder name change validated by 3 tests; verify the changes.
5. F21 (drop double cast for enum) — Discovery only; spot-check a generated `.g.cs` for an enum-default property.

### Phase E — raw string literals
1. Is output byte-equivalent to v1? Coder admits 5 trivial blank-line differences in a sampled handler. Acceptable, but verify the diff doesn't hide a behavior change.
2. Did any emission method get harder to read? The whole point of raw strings is to make it easier.
3. Is the snapshot test (`SnapshotParamsTests`) updated to match new output?

### Pass 4 — Behavioral reasoning (full session)
1. **Trace data origins for the cycle key**: `_resolvingValues.Add(strVal)` — `strVal` is the input to `AsT_Impl`. What if the same cycle string occurs in two unrelated steps in the same thread? (Step A finishes resolution, doesn't clean up, Step B starts.) The thread-static is shared. Cycle root = "_resolvingValues was null at entry" — so if Step A correctly cleans up, Step B starts fresh. But: what if Step A throws mid-resolution? `try/finally` should clean up — verify.
2. **`%var%` partial-match path**: does it also need cycle protection? Variables.Resolve already protects partial matches via `_resolvingVars`. Coder added `_resolvingValues` to `Data` for the full-match path. If both paths share the same thread-static, are there cross-protocol cycles? E.g. partial match calls `Variables.Resolve("%a% + %b%")` → resolves a → returns `%c%` → As<string> → As<T> ... does that trip the wrong protector?
3. **Clone/copy family audit**: did anything new get added to `Data.@this` that needs to flow through `Clone`/`ShallowClone`/`ConvertAndWrap`?
4. **Generic catches**: did any new throw site get added that an existing generic catch would mask?

### Pass 5 — Deletion test
1. For every new test line: would removing it make a wrong-fix pass?
2. For every new comment: is there a test that pins the contract so the comment can't drift?

## Validation

- `dotnet build PLang.sln` — clean? (Coder claims 2444/2444 green.)
- Generator output: spot-check one generated `.g.cs` for missing `__variables`, missing `__paramData`, missing `ParamData()`.

## Output

- `v2/result.md` — findings list (only NEW findings or follow-ups; reference v1 finding numbers when reaffirming).
- `v2/summary.md` — top-of-mind for the next bot.
- `v2/verdict.json` — pass/fail.
