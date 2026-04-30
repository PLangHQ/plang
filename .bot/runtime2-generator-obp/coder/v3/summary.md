# Coder v3 — close codeanalyzer v2's 7 findings

## What this is

Response to `codeanalyzer/v2` (FAIL verdict). Production fixes from coder v2 stood as correct, but two of the three new test files — `IncrementalCacheTests` and `NoDeadEmissionTests` — were structurally incapable of catching the regressions they were named after. v3 hardens the regression-prevention layer plus picks up the smaller MINOR/NIT findings that v2 deferred.

## What was done

### Phase 1 — `NoDeadEmissionTests` (Findings #40, #44)

The v2 heuristic was `reads = total_occurrences − assignments` with the flag `reads <= 0`. Empirically this gave `reads=1` for the v1 `__variables` shape (decl + 1 LHS, no read) and `reads=2` for `__paramData` (decl + 2 LHS + 1 internal accessor body). v3 splits the contract into three orthogonal assertions:

- **In-file dead field** (`Pattern A`) — tightened `reads = total_occurrences − assignments − decl_line_occurrences`, plus `=(?!=)` to reject `==` false-positives in the assignment regex. Catches the `__variables` shape.
- **Cross-file unused public method** (`Pattern B`) — for each `public ... \w+\s*\(` declared in a generated handler, scans `PLang/`, `PLang.Tests/`, `PLang.Generators/`, `PlangConsole/` (excluding `obj/` and `bin/`) for callers. Caller-exemption list for known framework dispatch (`ExecuteAsync`, `SnapshotParams`). Catches the `__paramData`/`ParamData()` shape.
- **Convention pin** — every declared private field in a generated handler must start with `__`. Without this, a future change could drop the prefix and Pattern A's `(__\w+)` regex would silently miss those fields.

Plus 5 heuristic regression tests (`Heuristic_VariablesShape_*`, `Heuristic_DoubleEqualsIsNotAnAssignment`, etc.) that drive `HasReadOf` against synthetic source mirroring the v1 regression shapes — so the dead-field detection is pinned independently of whether the live generated tree happens to be clean.

### Phase 2 — `IncrementalCacheTests` real Roslyn driver (Finding #39)

v2's 9 carrier-equality tests stay (they're cheap and useful). On top, two `CSharpGeneratorDriver` cache-hit tests:
- `PipelineCache_RerunWithUnchangedSyntax_StepOutputsAreCachedOrUnchanged` — runs the generator twice over the same `[Action]` partial (with an unrelated tree added to force re-evaluation). Asserts every output of the `ActionInfoFiltered` tracked step has reason `Cached` or `Unchanged`. If `ActionClassInfo` regressed to reference equality (the v1 `sealed class` carrier), every output would report `Modified` and this test would fail.
- `PipelineCache_ActionClassChanged_StepOutputIsModified` — sanity-check (negative space). Changing the partial property's type from `Data<string>` to `Data<int>` must invalidate the cache. Without this, a vacuously-passing cache test (always Cached) would not be caught.

Two small production additions to make this measurable:
- `PLang.Generators.@this.ActionInfoTrackingName` + `ActionInfoFilteredTrackingName` constants
- `WithTrackingName(...)` calls after `CreateSyntaxProvider` and `Where` in the pipeline
- `Microsoft.CodeAnalysis.CSharp 4.13.0` as a regular `PackageReference` on `PLang.Tests.csproj` (was `PrivateAssets="all"` only on the generator)

### Phase 3 — Step.RunAsync OCE asymmetry (Finding #42)

`StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate`: pre-cancel a `CancellationTokenSource`, push it onto `Context`, build a Step with one Action, call `Step.RunAsync(context)`. The foreach's `ThrowIfCancellationRequested()` on line 152 throws OCE, the catch on line 157 (`catch when (ex is not (… or OperationCanceledException))`) lets it propagate. Pinned via `Assert.That(...).ThrowsExactly<OperationCanceledException>()`. Together with the existing App.Run side, both halves of the OCE asymmetry are now pinned.

### Phase 4 — Cycle test value assertions (Finding #43)

3 of 4 cycle tests in `DataAsTResolutionTests.cs` upgraded from `IsNotNull()` to specific `result.Value` assertions:
- `AsT_CyclicVarReference_ReturnsCycleBrokenRawString` — `%a%↔%b%` returns `"%a%"`
- `AsT_SelfReferencingVar_ReturnsRawTemplate` — `%x%="%x%"` returns `"%x%"`
- `AsT_PartialMatchSelfReference_ReturnsUnsubstitutedInterpolation` — `"hello %x%"` with `x="%x%"` returns `"hello %x%"`

A "fix" that returns `null` or empty string instead of the cycle-broken raw would now fail.

### Phase 5 — Expanding-cycle depth bound (Finding #41)

`Data.@this.AsT_Impl` now enforces a depth bound (`ResolveDepthLimit = 32`) in addition to the exact-string HashSet. Expanding cycles like `%a%="X-%b%"`, `%b%="Y-%a%"` (where every recursion produces a fresh string the HashSet has never seen) now short-circuit at depth 32 instead of stack-overflowing. New test `AsT_ExpandingCycle_DepthBoundReturnsGracefully` pins the contract.

### Phase 6 — Diagnostic location span (Finding #45 / v1 Finding 7)

`DiagnosticInfo` widened from `(string PropertyName, string ClassName, string FilePath, int Line, int Character)` to `(… int StartLine, int StartCharacter, int EndLine, int EndCharacter)`. Discovery captures `loc.GetLineSpan().EndLinePosition` alongside the start. The orchestrator constructs a `LinePositionSpan` covering the full identifier instead of synthesizing `(line, char + 1)`. New test `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier` drives the generator and asserts the span width exceeds 1 — required to be a real `partial` declaration with a non-empty `path` for the location to survive (orchestrator falls back to `Location.None` when `FilePath` is empty; this matches the realistic build behaviour).

## Code example

The pattern that's repeated: the contract test pins a concrete, predictable behaviour the bug would otherwise re-land. Sample — the cross-file accessor scan in `NoDeadEmissionTests`:

```csharp
[Test]
public async Task NoGeneratedHandlerExposesUnusedPublicMethod()
{
    // Pattern B: cross-file scan for orphan public methods.
    // Catches the __paramData regression: ParamData() was called by nothing outside the
    // generated handler but read __paramData in-file, so the in-file scan couldn't catch it.
    var allCallableSources = LoadAllCallableSources();
    var orphans = new List<string>();
    foreach (var path in files)
    {
        var src = File.ReadAllText(path);
        foreach (Match m in PublicMethodDecl.Matches(src))
        {
            var name = m.Groups[1].Value;
            if (_publicMethodCallerExemptions.Contains(name)) continue;
            var callerPattern = new Regex(@"\b" + Regex.Escape(name) + @"\s*\(");
            if (!callerPattern.IsMatch(allCallableSources))
                orphans.Add($"{Path.GetFileName(path)}:{name}");
        }
    }
    await Assert.That(orphans).IsEmpty();
}
```

## For v3 after review

| v2 finding | Severity | Status |
|---|---|---|
| #39 IncrementalCacheTests doesn't drive Roslyn | MAJOR | Closed — added 2 `CSharpGeneratorDriver` cache-hit tests + tracking names |
| #40 NoDeadEmissionTests heuristic | MAJOR | Closed — heuristic fixed + cross-file scan + 5 heuristic regression tests |
| #41 Expanding-cycle gap | MINOR | Closed — depth bound (`ResolveDepthLimit=32`) + test |
| #42 OCE asymmetry only one direction | MINOR | Closed — `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate` |
| #43 Cycle tests asserted only IsNotNull | MINOR | Closed — 3 cycle tests upgraded to value assertions |
| #44 NoDeadEmission regex `__`-only | NIT | Closed — convention pin test added |
| #45 Finding 7 silently dropped | NIT | Closed — `DiagnosticInfo` widened, span pinned by test |

## Final state

- **C# tests**: 2456/2456 green (was 2444 in v2; +12: 5 heuristic + 2 cache-hit + 1 Step OCE + 0 cycle [3 strengthened, 1 added expanding-cycle] + 1 diagnostic location + 5 dead-field/convention-pin assertions)
- **`plang --test`**: 169 pass / 48 fail / 5 stale (222 total). Pre-existing infrastructure failures (signing, identity, fixtures, test reports). None tied to v3 production changes.
- **Generator**: tracking names exposed via constants; depth-bounded cycle protection; full identifier span in PLNG001 diagnostics.
- **No v2 finding deferred.**

## Files

**Modified:**
- `PLang/App/Data/this.cs` — `ResolveDepthLimit = 32` in cycle protector
- `PLang.Generators/this.cs` — `ActionInfoTrackingName`/`ActionInfoFilteredTrackingName` constants + `WithTrackingName(...)` calls; orchestrator uses full `LinePositionSpan` for diagnostics
- `PLang.Generators/Discovery/this.cs` — `DiagnosticInfo` widened to carry `StartLine/StartCharacter/EndLine/EndCharacter`
- `PLang.Tests/PLang.Tests.csproj` — `Microsoft.CodeAnalysis.CSharp` 4.13.0 added as regular `PackageReference`
- `PLang.Tests/Generator/IncrementalCacheTests.cs` — added 2 `CSharpGeneratorDriver` cache-hit tests + minimal-source helper + driver factory
- `PLang.Tests/Generator/NoDeadEmissionTests.cs` — split into 3 contract assertions + 5 heuristic regression tests; added cross-file accessor scan
- `PLang.Tests/Generator/GeneratorValidationTests.cs` — added `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier`
- `PLang.Tests/App/AppRunScaffoldingTests.cs` — added `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate`
- `PLang.Tests/App/DataTests/DataAsTResolutionTests.cs` — 3 cycle tests strengthened with value assertions; 1 expanding-cycle test added

**Created:**
- `.bot/runtime2-generator-obp/coder/v3/v2_review_summary.md`
- `.bot/runtime2-generator-obp/coder/v3/plan.md`
- `.bot/runtime2-generator-obp/coder/v3/summary.md` (this file)
