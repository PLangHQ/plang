# codeanalyzer v3 — review of coder v3

## What I'm reviewing

Coder v3 (commit `c9314c5e`) addresses my v2 review's 7 findings. v2 verdict was FAIL — production fixes were clean but two MAJOR findings sat on test toothlessness:

- **#39 (MAJOR)** — `IncrementalCacheTests` did unit equality on records, not pipeline-driven `CSharpGeneratorDriver` cache assertions
- **#40 (MAJOR)** — `NoDeadEmissionTests` heuristic empirically could not catch the `__variables` / `__paramData` regressions it was named after
- **#41 (MINOR)** — expanding-cycle gap (HashSet of exact strings can't trap `%a%="X-%b%"`, `%b%="Y-%a%"`)
- **#42 (MINOR)** — OCE asymmetry only one direction (App.Run pinned, Step.RunAsync wasn't)
- **#43 (MINOR)** — 3 of 4 cycle tests asserted only `IsNotNull`
- **#44 (NIT)** — NoDeadEmission regex restricts to `__`-prefix without pinning the convention
- **#45 (NIT)** — Finding 7 (synthetic 1-char diagnostic location) was silently dropped from v2 not-taken

Coder v3 says all 7 are closed. My job is to verify each closure is honest — that the test would actually fail if the regression reappeared, and that the production code is clean.

## Approach (5 passes, scoped to v3 deltas)

The v3 changes are tightly scoped: 4 test files, 3 production files. I'll go finding-by-finding rather than file-by-file.

### Per-finding verification

**#40/#44** — read `NoDeadEmissionTests.cs`. For each of the three orthogonal contracts (in-file dead field, cross-file unused public method, convention pin), simulate-mentally what `__variables` and `__paramData` would compute under the new heuristic. Verify the 5 heuristic regression tests cover the v1 regression shapes. Confirm the cross-file scan would catch `ParamData()` as having no callers.

**#39** — read `IncrementalCacheTests.cs`. Verify the `CSharpGeneratorDriver` is actually constructed with `trackIncrementalGeneratorSteps:true`, the generator is run twice, and the assertion reads `IncrementalGeneratorRunResult.TrackedSteps[name]` looking for `Cached`/`Unchanged`. Verify the negative-space test (`PipelineCache_ActionClassChanged`) actually flips to `Modified` when the source changes. Read the new `WithTrackingName` calls in `PLang.Generators/this.cs` to confirm they're real, not just constants.

**#41** — read `Data.@this.AsT_Impl`. Confirm `ResolveDepthLimit = 32` is checked before recursion. Read the new `AsT_ExpandingCycle_DepthBoundReturnsGracefully` test. Confirm the legitimate `AsT_DeepChain_5Levels` case still passes (it's well below 32). Watch for: is the depth counter incremented/decremented correctly across try/finally? Off-by-one on the limit?

**#42** — read `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate` in `AppRunScaffoldingTests.cs`. Trace which line throws (Step.cs:152 `ThrowIfCancellationRequested`) and which catch lets it propagate (Step.cs:157 `catch when (ex is not (… or OperationCanceledException))`). Confirm the test name + assertion match the contract.

**#43** — read the 3 strengthened cycle tests. Each must assert a specific `result.Value` value, not `IsNotNull`. Cross-check the assertions match what `AsT_Impl`'s "return raw on cycle" path actually produces.

**#45** — read `DiagnosticInfo` in `Discovery/this.cs`. Confirm `EndLine`/`EndCharacter` are real (not just placeholders), come from `loc.GetLineSpan().EndLinePosition`, and the orchestrator builds a real `LinePositionSpan`. Read `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier` and confirm the assertion catches the synthetic-`+1` regression.

### Pass 5 (Deletion test) on the production deltas

The new production code is small but real:
- `ResolveDepthLimit = 32` + depth tracking in `Data.@this`
- `WithTrackingName(...)` calls + 2 const tracking-name strings on `PLang.Generators/this.cs`
- `DiagnosticInfo` field widening + orchestrator `LinePositionSpan` reconstruction in `Discovery/this.cs`

For each: if I deleted lines X-Y, would a test fail? If yes, the line earns its place. If no, it's a finding.

### Sanity

- Build clean
- Test count matches coder's claim (2456/2456)
- No new `plang --test` regressions in cycle/depth/diagnostic family

## What I'm not reviewing

- The 22 v1 findings that stayed deferred in v2 — those have logged rationale and stay deferred.
- The 12 v1 findings already fixed in v2 (1, 2, 3, 6, 9, 11, 12, 19, 21, 27, 28, 33) — already verified in v2.

## Output

- `v3/result.md` — finding-by-finding verification with pass/fail per item
- `v3/summary.md` — this version's work summary
- `v3/verdict.json` — pass/fail
- root `summary.md` — v3 entry appended
- `report.json` — session entry with `before`, `plan`, `actions`, `after`
