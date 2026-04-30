# codeanalyzer v2 — review of coder's response to v1 review

## What this is

Second review pass on the v4 generator restructure branch. Coder pushed v2 (`01aa150c..4018f26b`) addressing 12 of v1's 38 findings, deferring 25 with rationale, and silently missing 1. New tests (`IncrementalCacheTests`, `NoDeadEmissionTests`, plus cycle/OCE/non-generic entries) were added to close the test-gap concern Ingi raised in v1.

## What was done

Reviewed all 5 phases (A–E) of coder v2 plus the new test files. Built the project and ran the full TUnit suite (2444/2444 green) plus targeted runs on the new tests. Empirically validated `NoDeadEmissionTests`'s regex by simulating the v1 dead-field regressions in Python.

**Verdict: NEEDS WORK** — production fixes are correct and `plang test` is unblocked, but two of the three new test files are toothless: they pass for the post-fix code but cannot catch the bugs they were named after.

### v1 findings closed (verified correct)

| # | Topic | Where |
|---|---|---|
| 1 | `ActionClassInfo` → `record` + `EquatableArray<T>` | `Discovery/this.cs:282-296`, new `EquatableArray.cs` |
| 2, 3, 6, 9, 21 | Discovery cleanups | `Discovery/this.cs` + class rename |
| 11, 12 | `__variables` / `__paramData` deletion | `Emission/Action/this.cs` (verified absent in generated output) |
| 19 | Raw string literals for emission | `Emission/Action/this.cs` (cleaner top-to-bottom shape) |
| 27 | Cycle detection in `Data.AsT_Impl` | `Data/this.cs:390-437` (try/finally + thread-static) |
| 28 | Non-generic shape contract documented | `Data/this.cs:508-512` |
| 33 | App.Run OCE catch documented | `App/this.cs:411-414` |

### New v2 findings (7 total: 2 MAJOR, 3 MINOR, 2 NIT)

- **MAJOR 39** — `IncrementalCacheTests` is 9 unit-equality tests on the carrier records. The plan promised a `CSharpGeneratorDriver`-driven cache-hit test using `TrackedSteps`/`IncrementalStepRunReason.Cached`. That test was not delivered. The IIncrementalGenerator pipeline contract is not exercised.
- **MAJOR 40** — `NoDeadEmissionTests` cannot catch the regressions it was named after. Empirically: the v1 `__variables` pattern (declared + 1 assignment, no read) computes `reads=1`, the test flags only `reads<=0`. The author flagged the right heuristic in their own comment but adopted the wrong one. `__paramData` requires cross-file analysis the test doesn't perform. The test passes today because there are no dead fields, not because the test detects them.
- MINOR 41 — Cycle protector keys on raw input string; expanding cycles (`%a%="X-%b%"`, `%b%="Y-%a%"`) still recurse infinitely.
- MINOR 42 — OCE asymmetry pinned only on App.Run side. Plan promised a paired Step.RunAsync test; it wasn't delivered.
- MINOR 43 — Cycle tests assert only `IsNotNull`; value contract not pinned.
- NIT 44 — `NoDeadEmissionTests` regex restricts to `__`-prefixed fields without enforcing the convention.
- NIT 45 — Finding 7 (synthetic 1-char diagnostic span) silently dropped from coder's not-taken list.

## Code example

The pattern that's repeated for both major findings — present-but-toothless test:

`NoDeadEmissionTests.cs:60-73`:
```csharp
// Count occurrences of the identifier elsewhere (anywhere it appears).
// Subtract 1 for the declaration line. A field with only the declaration
// (count == 1) and an assignment on the same identifier is still "set, not read";
// distinguish read by counting non-LHS occurrences.
var allOccurrences = Regex.Matches(src, @"\b" + Regex.Escape(fieldName) + @"\b").Count;
var assignments = Regex.Matches(src, @"\b" + Regex.Escape(fieldName) + @"\b\s*(\??\[[^\]]*\])?\s*=").Count;
var reads = allOccurrences - assignments;
// The declaration itself counts as a "use" by the regex above (just the bare name)
// but isn't a read — adjust by subtracting 1 if the line has no `=`.
// Simpler heuristic: a field is dead if reads <= 0.
if (reads <= 0)
```

The author *describes* the right approach in the comment but implements the wrong one. For `__variables`-shape (decl + LHS only) `reads = 2 - 1 = 1`, never flagged. The "simpler heuristic" only triggers when the field has an inline initializer AND no other mention — a case neither v1 dead field exhibited.

## What to do next

If coder treats this as a v3 round:
1. Replace/supplement `IncrementalCacheTests` with a real `CSharpGeneratorDriver` cache-hit assertion (one test is enough — drive twice, assert second run's `TrackedSteps.Outputs[0].Reason == Cached`).
2. Fix `NoDeadEmissionTests` heuristic (subtract declaration line from reads) AND add a cross-file scan for accessor callers.
3. Add `StepRunAsync_HandlerThrowsOCE_LetsItPropagate` to pin the asymmetry.
4. Strengthen cycle tests with value assertions.
5. Optional: variable-name-keyed cycle protection or depth bound for expanding-cycle case.
6. Address Finding 7 (full location span in `DiagnosticInfo`).

If Ingi accepts the production fixes as-is and defers test hardening, that's also a defensible call — production code is correct, only the regression-prevention layer is incomplete.

## Files

**Read in this review:**
- `PLang/App/Data/this.cs` (cycle detection, non-generic comment)
- `PLang/App/this.cs` (OCE comment)
- `PLang/App/Variables/this.cs` (verified Resolve cycle interaction)
- `PLang.Generators/this.cs` (orchestrator + Finding 7 still standing)
- `PLang.Generators/Discovery/this.cs` (record conversion + cleanups)
- `PLang.Generators/Emission/Action/this.cs` (raw string literals + dead emission removed)
- `PLang.Generators/EquatableArray.cs` (new)
- `PLang.Tests/Generator/IncrementalCacheTests.cs` (new — Finding 39)
- `PLang.Tests/Generator/NoDeadEmissionTests.cs` (new — Finding 40)
- `PLang.Tests/Generator/SnapshotParamsTests.cs` (path rename)
- `PLang.Tests/App/AppRunScaffoldingTests.cs` (OCE test + fixture)
- `PLang.Tests/App/DataTests/DataAsTResolutionTests.cs` (cycle + non-generic tests)
- One sample `App.modules.matrix.plain.BoolPlain.Action.g.cs` (verified dead emission absent)

**Written by this session:**
- `.bot/runtime2-generator-obp/codeanalyzer/v2/plan.md`
- `.bot/runtime2-generator-obp/codeanalyzer/v2/result.md`
- `.bot/runtime2-generator-obp/codeanalyzer/v2/verdict.json`
- `.bot/runtime2-generator-obp/codeanalyzer/v2/summary.md`
