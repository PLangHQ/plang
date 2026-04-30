# v1 review summary — codeanalyzer findings + Ingi's test-gap concern

## Source

- `.bot/runtime2-generator-obp/codeanalyzer/v1/result.md` — 38 findings (10 MAJOR, 19 MINOR, 9 NIT)
- `.bot/runtime2-generator-obp/codeanalyzer/v1/summary.md` — top-3 fix list + behavioral concerns
- Ingi's question: "unit tests should have caught some of those — did you write all the tests and run them?"
- **`plang test` runs StackOverflowException** mid-run (confirmed by direct invocation; trace shows `Data.AsT_Impl` recursing via `Variables.Resolve`). Finding 27 is no longer theoretical — it's blocking the test suite.

## Verdict applied to v1

`fail` (NEEDS WORK). The v4 design is sound; the issues are concrete and surgical.

## Top-3 mechanical fixes (codeanalyzer)

1. **Finding 1** — `ActionClassInfo` is `sealed class`, not `record`. The `IIncrementalGenerator` cache always misses on this carrier. `List<T>` collections compound the problem (no value equality). My `ActionPropertyRecord_NoSymbolLeaks_IncrementalSafe` test only checks for symbol *leaks* — it never verifies cache *hits*, so the test passes despite the structural defect.
2. **Finding 11** — `__variables` field declared, set in ExecuteAsync, never read anywhere across `PLang/`, `PLang.Tests/`, `os/`. Dead emission.
3. **Finding 12** — `__paramData` dict + `ParamData()` accessor: dict is filled by legacy `__Resolve<T>`, accessor has zero callers across the entire repo. Dead emission.

## Top behavioral concerns

- **Finding 27** — `AsT_Impl` recursion has no cycle detection. **Currently crashing `plang test` with StackOverflowException.** `Variables.Resolve` has cycle protection via thread-static `_resolvingVars`, but `As<T>`'s full-match path bypasses `Resolve` and recurses on the variable's value. Need a thread-static visited-set in `Data` mirroring `Variables._resolvingVars`, or route both paths through `Resolve`.
- **Finding 28** — `WalkList`/`WalkDict`/`SubstitutePrimitive` only match `IList<object?>` / `IDictionary<string, object?>`. Non-generic `IList`/`IDictionary` (e.g. `ArrayList`, `Hashtable`) silently pass through. In practice everything's normalized via `UnwrapJsonElement`, but the contract isn't pinned by tests.
- **Finding 29** — `As<T>` ignores `_type.Convert` capability. JSON-typed Data parameters won't honor JSON deserialization through the resolution path. Pre-existing — flagged for awareness.
- **Finding 33** — `App.Run` deliberately catches `OperationCanceledException` (load-bearing for `timeout.after`), unlike `Step.RunAsync` which excludes OCE. No comment documenting the dependency. No test pinning the behavior.

## Test-set gaps Ingi correctly identified

| Gap | What test was missing |
|-----|----------------------|
| Cache hits | No real Roslyn `IIncrementalGenerator` driver test — only structural symbol-leak check |
| Dead emission | No regex/contract test asserting every emitted private field has at least one reader in the same generated file |
| Cycle in `AsT_Impl` | `DataAsTResolutionTests` has 17 happy-path tests, zero cycle tests |
| Non-generic collections | `WalkList`/`WalkDict` tests only feed typed-generic shapes |
| OCE in App.Run | `AppRunScaffoldingTests` has zero cancellation tests |
| Direct-init composition (Finding 20) | No test constructs a generated handler via `init { Backing = …, SetFlag = true }` to verify the four getter shapes' `SetFlag` vs `Backing == null` consistency |

## Findings deferred (transitional, MINOR/NIT cleanup)

The codeanalyzer flagged several findings (2-5, 8, 13, 15, 16, 17, 19, 22-26, 30-32, 34-38) that are either:
- Transitional — Phase 5 cleanup will sweep them anyway (legacy helpers, dual validation blocks)
- Pre-existing — v4 didn't introduce, out of scope (ToBoolean hand-roll, partial-class split, IsActionDestination coupling)
- Pure readability — `sb.AppendLine` cascades, named local helpers

I'll take a small subset (Findings 2, 3, 6, 9, 21) which are trivial and high-readability-value. Everything else is logged for a future cleanup pass once Phase 5 lands.
