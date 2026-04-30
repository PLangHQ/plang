# codeanalyzer v3 — review of coder v3

## What this is

Verification pass on coder v3, which was tasked with closing the 7 findings codeanalyzer v2 raised. v2's verdict was NEEDS WORK: production fixes (cycle detection, EquatableArray record carrier, dead emission removal, OCE/non-generic comments, raw-string emission) were all clean, but two MAJOR findings sat on test toothlessness — `IncrementalCacheTests` did unit equality on records (didn't drive Roslyn) and `NoDeadEmissionTests` empirically couldn't catch the `__variables`/`__paramData` regressions it was named after. Five smaller findings rounded out the round (#41 expanding-cycle gap, #42 OCE asymmetry one direction only, #43 cycle tests asserted only IsNotNull, #44 NoDeadEmission regex __-only restriction, #45 Finding 7 silently dropped).

## What was done

I went finding-by-finding, simulated the test arithmetic, traced the code paths, and ran the deletion test on every production delta.

- **#40 / #44** — heuristic: simulated `__variables` shape (`reads = 2 − 1 − 1 = 0 → flagged`), `__paramData` shape (`reads = 4 − 2 − 1 = 1 → not flagged in-file → caught by cross-file scan`). 5 heuristic regression tests (`Heuristic_*`) pin the patterns. Convention pin (`__` prefix) prevents drift. **Closed honestly.**
- **#39** — `IncrementalCacheTests` now constructs a real `CSharpGeneratorDriver` with `trackIncrementalGeneratorSteps:true`, runs twice, asserts `Cached`/`Unchanged` on the `ActionInfoFiltered` step. Negative-space `PipelineCache_ActionClassChanged_StepOutputIsModified` catches always-Cached vacuous passes. **Closed honestly.**
- **#41** — `ResolveDepthLimit = 32` constant + `Count > Limit` clause in `Data.AsT_Impl`. `isCycleRoot`-clears-set logic is load-bearing under depth-bound trip — verified the unwind. **Closed honestly.**
- **#42** — `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate` traces lines 152→157 of `Step/this.cs` (`ThrowIfCancellationRequested` + the OCE-excluding catch). **Closed honestly.**
- **#43** — 3 cycle tests upgraded from `IsNotNull` to specific value assertions (`%a%`, `%x%`, `hello %x%`). **Closed honestly.**
- **#45** — `DiagnosticInfo` widened with `Start/End` Line/Character, orchestrator builds real `LinePositionSpan`, test pinning `spanWidth > 1`. The `path: "BadHandler.cs"` argument in the test is load-bearing (orchestrator falls back to `Location.None` on empty FilePath). **Closed honestly.**

One new finding:
- **Finding 46 (NIT)** — `ActionInfoTrackingName` (unfiltered) constant + its `.WithTrackingName(...)` call earn no test. Either delete or upgrade into an additional cache assertion on the pre-filter step.

## Code example — the pattern that repeats

The new tests pin the *behavior* the bug would otherwise re-land. The clearest example is the heuristic regression set in `NoDeadEmissionTests` — they don't depend on the live generated tree being clean:

```csharp
[Test]
public async Task Heuristic_VariablesShape_DeclAndOneLhs_NoRead_IsDead()
{
    var src = """
        partial class H {
            private readonly Dictionary<string, object?>? __variables;
            public void M() { __variables = new Dictionary<string, object?>(); }
        }
        """;
    await Assert.That(HasReadOf(src, "__variables")).IsFalse();
}
```

The synthetic source mirrors the v1 regression shape exactly. If a future change loosens `HasReadOf` (e.g., drops the `decl_line_occurrences` subtraction), this test fails immediately — independent of whether any handler currently has the regression.

## Verdict

**CLEAN.** All 7 v2 findings honestly closed. The test-gap concern Ingi flagged in v1 is genuinely resolved — the regression-prevention layer is now real, not toothless. C# tests 2456/2456 green (locally re-run). One NIT (Finding 46) — non-blocking. Recommend a tester pass on `plang --test` for the 48 failing tests (pre-existing infra per coder; worth confirming).

## Files

**Read (no code modified):**
- `PLang/App/Data/this.cs` — depth bound + cycle protector
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — OCE-excluding catch (verifying #42)
- `PLang.Generators/this.cs` — tracking names + orchestrator span widening
- `PLang.Generators/Discovery/this.cs` — DiagnosticInfo widening
- `PLang.Tests/PLang.Tests.csproj` — Microsoft.CodeAnalysis.CSharp PackageReference
- `PLang.Tests/Generator/NoDeadEmissionTests.cs` — three contracts + 5 heuristic regression tests
- `PLang.Tests/Generator/IncrementalCacheTests.cs` — 9 carrier tests + 2 pipeline-driven tests
- `PLang.Tests/Generator/GeneratorValidationTests.cs` — `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier`
- `PLang.Tests/App/AppRunScaffoldingTests.cs` — `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate`
- `PLang.Tests/App/DataTests/DataAsTResolutionTests.cs` — strengthened cycle tests + expanding-cycle test

**Created:**
- `.bot/runtime2-generator-obp/codeanalyzer/v3/plan.md`
- `.bot/runtime2-generator-obp/codeanalyzer/v3/result.md`
- `.bot/runtime2-generator-obp/codeanalyzer/v3/summary.md` (this file)
- `.bot/runtime2-generator-obp/codeanalyzer/v3/verdict.json`
