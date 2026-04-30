# Coder v4 — close tester v3's 5 findings

## What this is

Response to `tester/v3` (FAIL verdict). 6 of v2's 7 findings stayed honestly closed (tester verified via 4 empirical deletion tests). Finding #1 was the same toothlessness shape Ingi flagged on v2: `NoGeneratedHandlerExposesUnusedPublicMethod` (Pattern B) used a `^\s*public\s+...` regex, but the v1 regression was `protected ParamData()` — the test was structurally incapable of catching the regression named in its docstring. v4 closes that plus the 4 minor/nit findings, all in test code. **No production code modified.**

## What was done

### Phase 1 — Widen Pattern B + extract `IsOrphanMethod` + comment/string stripping (Findings #1, #2, #3)

`PLang.Tests/Generator/NoDeadEmissionTests.cs`:

- **Regex widened.** `PublicMethodDecl` → `PublicOrProtectedMethodDecl`, `^\s*public\s+...` → `^\s*(?:public|protected)\s+...`. The v1 `protected ParamData()` shape now matches.
- **`IsOrphanMethod(name, callableSources, exemptions)` extracted.** Pure-string helper; mirrors `HasReadOf` for Pattern A. Synthetic tests can drive it directly.
- **`StripCommentsAndStrings(src)` added.** Pragmatic regex pass — strips raw strings, verbatim strings, regular strings, char literals, block comments, then line comments (in that order, so single-line constructs strip before multi-line ones can run away across the file). Critical once Pattern B widens to `protected`: `Data` and `Error` are common substrings — they appear in raw-string emission text inside `PLang.Generators/Emission/Action/this.cs` and in countless docstrings. Without stripping, those false-greened the orphan check.
- **`LoadAllCallableSources` updated** to apply `StripCommentsAndStrings` per file before concatenation.
- **6 synthetic regression tests added** for Pattern B, mirroring the 5 `Heuristic_*` tests Pattern A already had:
  - `Heuristic_OrphanProtectedMethod_IsFlagged` — feeds `IsOrphanMethod` synthetic source, asserts orphan name returns true.
  - `Heuristic_CalledMethod_IsNotFlagged` — caller present → returns false.
  - `Heuristic_ExemptedMethod_IsNotFlagged` — exemption set short-circuits.
  - `Strip_MethodNameInsideLineComment_DoesNotCountAsCaller` — `// Data()` doesn't false-green.
  - `Strip_MethodNameInsideStringLiteral_DoesNotCountAsCaller` — `"Data()"` doesn't false-green.
  - `Strip_MethodNameInsideRawStringLiteral_DoesNotCountAsCaller` — `"""...Data()..."""` doesn't false-green.
- **3 regex assertions added** to pin the regex itself, decoupled from `IsOrphanMethod`. Without these, narrowing the regex back to `public`-only would not be caught by the helper-driven synthetic tests:
  - `PublicOrProtectedMethodDecl_MatchesProtectedDeclaration`
  - `PublicOrProtectedMethodDecl_MatchesPublicDeclaration`
  - `PublicOrProtectedMethodDecl_DoesNotMatchPrivate`

**Empirical deletion test:** narrowed the regex to `(?:public)` only, ran `PublicOrProtectedMethodDecl_MatchesProtectedDeclaration` — failed with `Expected to be 1 but found 0`. Reverted. Confirms the regex assertion is honest.

**Risk addressed empirically:** verified `Data(...)` and `Error(...)` are called from many user partials (`PLang/App/modules/list/*.cs`, `PLang/App/modules/event/skipAction.cs`, etc.). After stripping, those calls remain in the scanned source. The widened Pattern B does not flag the framework helpers as orphans — confirmed by the test suite running green against the live tree.

### Phase 2 — `--coverage` caveat (Finding #4)

`PLang.Tests/Generator/IncrementalCacheTests.cs`. Added a comment block at the top of the file documenting that `dotnet run -- --coverage` strips/relabels tracked steps so `runResult.TrackedSteps` does not contain the `ActionInfo`/`ActionInfoFiltered` keys. No code change. Lowest-cost option per tester suggestion.

### Phase 3 — Cache-hit test for the unfiltered `ActionInfo` step (Finding #5)

`PLang.Tests/Generator/IncrementalCacheTests.cs`. Added `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` mirroring the existing post-Where test but on the pre-Where step. Tester recommended option (b) over deletion — strictly stronger contract: the post-Where step's value-equality on `ActionClassInfo` can mask transform-step instability that the pre-Where step would surface. The dead `ActionInfoTrackingName` constant is now load-bearing.

**Empirical deletion test:** changed `.WithTrackingName(ActionInfoTrackingName)` to `.WithTrackingName("DisagreeingName")` in `PLang.Generators/this.cs`, ran the new test — failed with `Expected to contain key ActionInfo but key ActionInfo not found`. Reverted. Confirms the test catches producer/consumer drift on the constant.

## Code example

The shape that's repeated across the new tests: a pure-string contract helper plus synthetic regression tests that pin the helper's behavior independently of the live tree.

```csharp
internal static bool IsOrphanMethod(
    string methodName,
    string allCallableSources,
    ISet<string> exemptions)
{
    if (exemptions.Contains(methodName)) return false;
    var callerPattern = new Regex(@"\b" + Regex.Escape(methodName) + @"\s*\(");
    return !callerPattern.IsMatch(allCallableSources);
}

[Test]
public async Task Heuristic_OrphanProtectedMethod_IsFlagged()
{
    var allCallers = "// nothing references ParamData() here";
    var stripped = StripCommentsAndStrings(allCallers);
    await Assert.That(IsOrphanMethod("ParamData", stripped, new HashSet<string>())).IsTrue();
}
```

## For v4 after review

| tester finding | Severity | Status |
|---|---|---|
| #1 Pattern B regex restricts to `public` | MAJOR | Closed — regex widened to `public\|protected`, 3 regex assertions added, deletion-tested |
| #2 No synthetic regression tests for Pattern B | minor | Closed — `IsOrphanMethod` extracted, 3 `Heuristic_*` tests added |
| #3 Caller scan matches inside comments/strings | minor | Closed — `StripCommentsAndStrings` added, 3 strip tests added |
| #4 PipelineCache tests fail under `--coverage` | minor | Closed — caveat comment block at top of `IncrementalCacheTests.cs` |
| #5 `ActionInfoTrackingName` (unfiltered) is dead | NIT | Closed — option (b): added pre-Where cache-hit test, constant now load-bearing, deletion-tested |

## Final state

- **C# tests:** 2466/2466 green (was 2456 in v3; +10 new tests in v4: 3 IsOrphan synthetic + 3 strip + 3 regex + 1 unfiltered cache-hit).
- **Empirical deletion tests applied to the new tests' contracts:**
  - Regex → narrow to `public`-only → `PublicOrProtectedMethodDecl_MatchesProtectedDeclaration` fails. ✓
  - `WithTrackingName` constant → swap to literal disagreeing string → `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` fails. ✓
- **No production code modified in v4.** All 5 findings closed via test-quality changes plus activating the previously-dead `ActionInfoTrackingName` constant via the new test.
- **No tester finding deferred.**

## Files

**Modified:**
- `PLang.Tests/Generator/NoDeadEmissionTests.cs` — Pattern B widened, `IsOrphanMethod` + `StripCommentsAndStrings` extracted, 9 new tests (6 helper-driven + 3 regex assertions), live cross-file test renamed to `NoGeneratedHandlerExposesUnusedPublicOrProtectedMethod`, header comment updated to record v3→v4 toothlessness fix and the v4 contract structure.
- `PLang.Tests/Generator/IncrementalCacheTests.cs` — `--coverage` caveat comment block + new `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` test.

**Created:**
- `.bot/runtime2-generator-obp/coder/v4/v3_review_summary.md`
- `.bot/runtime2-generator-obp/coder/v4/plan.md`
- `.bot/runtime2-generator-obp/coder/v4/summary.md` (this file)
