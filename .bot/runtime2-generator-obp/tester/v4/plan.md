# Tester v4 — verify coder v4 honestly closes tester v3's 5 findings

## What I'm reviewing

`coder/v4` (commit `bf66c25c`) closes 5 findings from `tester/v3`:

| # | Sev | Coder's fix | Pinned by |
|---|---|---|---|
| 1 | MAJOR | Widen `PublicMethodDecl` → `PublicOrProtectedMethodDecl` (`public\|protected`) | 3 regex assertions (`PublicOrProtectedMethodDecl_*`) |
| 2 | minor | Extract `IsOrphanMethod(name, sources, exemptions)` helper | 3 `Heuristic_*` synthetic tests |
| 3 | minor | Add `StripCommentsAndStrings`, apply in `LoadAllCallableSources` | 3 `Strip_*` tests |
| 4 | minor | Add `--coverage` caveat comment block at top of file | comment block (no code change) |
| 5 | NIT | Add `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` | new test |

**No production code modified in v4.** All changes confined to two test files.

## Hunt strategy — false-green hunting on the v4 fixes

The v3 toothlessness pattern was: **the regex looked correct in isolation but couldn't match the regression shape it was named after**. The author wrote it, eyeballed it, and never empirically demonstrated that it caught the regression. So my v4 hunt focuses on: **does each new test actually fail when its claimed contract breaks?**

### Pass 1 — read code, look for structural toothlessness

Things to check by inspection before running any deletion test:

1. **`PublicOrProtectedMethodDecl_MatchesProtectedDeclaration`** — does this regex assertion actually require `protected` matching? If the regex were narrowed back to `public`-only, would it fail? `Matches(src).Count == 1` against input `protected static Data ParamData(...)` — narrow regex returns 0 → test fails. ✓ structurally honest.

2. **`Heuristic_OrphanProtectedMethod_IsFlagged`** — does this drive `IsOrphanMethod` in a way that wouldn't pass vacuously? Input is `// nothing references ParamData() here`, after strip: ` ` (or empty), `IsOrphanMethod("ParamData", "", empty-set)` → callerPattern doesn't match empty string → returns true. Asserts true. If `IsOrphanMethod` always returned false, the test fails. ✓ structurally honest.

3. **`Heuristic_CalledMethod_IsNotFlagged`** — input `void Caller() { var x = MyHelper(arg); }`, no comments/strings to strip, `IsOrphanMethod("MyHelper", ...)` finds `MyHelper(` → returns false. Asserts false. ✓ structurally honest.

4. **`Heuristic_ExemptedMethod_IsNotFlagged`** — exemption set `{ "ExecuteAsync" }`, no callers in source, but exemption short-circuits → returns false. If exemption check were removed, test fails. ✓ structurally honest.

5. **`Strip_MethodNameInsideLineComment_DoesNotCountAsCaller`** — input `// Data() is a helper provided by the generator\n`. After strip: ` ` (line comment regex zaps everything). `IsOrphanMethod("Data", " ", empty-set)` → no `Data(` in stripped → returns true. If `StripCommentsAndStrings` were identity, stripped would still contain `Data()`, `IsOrphanMethod` returns false, test fails. ✓ structurally honest.

6. **`Strip_MethodNameInsideStringLiteral_DoesNotCountAsCaller`** — input `var template = "Data()";`. After regular-string strip: `var template = ` `;`. No `Data(` → orphan true. ✓ structurally honest.

7. **`Strip_MethodNameInsideRawStringLiteral_DoesNotCountAsCaller`** — input `var emitter = """protected static Data() => Data.Ok();""";`. After raw-string strip: `var emitter = ` `;`. No `Data(` → orphan true. ✓ structurally honest.

8. **`PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged`** — pin on the unfiltered (`ActionInfoTrackingName`) step. Mirrors the post-Where test; same structure, different tracking name. The contract: each step output must report `Cached` or `Unchanged`. **Vacuous-pass risk:** if `TrackedSteps[ActionInfoTrackingName]` were empty (`Length == 0`), the inner `foreach` would iterate zero times and the test would pass without making any assertion. The author guards this with `Assert.That(infoSteps.Length).IsGreaterThan(0)`. ✓ checked.

### Pass 2 — empirical deletion tests

Mutate, build, run targeted test, expect failure, revert. The 4 deletion tests I'll execute:

| # | Mutation | Expected to fail |
|---|---|---|
| A | Narrow `PublicOrProtectedMethodDecl` regex back to `public`-only | `PublicOrProtectedMethodDecl_MatchesProtectedDeclaration` |
| B | Make `IsOrphanMethod` always return false | `Heuristic_OrphanProtectedMethod_IsFlagged` |
| C | Make `StripCommentsAndStrings` an identity function (`return src;`) | All 3 `Strip_*` tests |
| D | Change `WithTrackingName(ActionInfoTrackingName)` in `PLang.Generators/this.cs:29` to a literal disagreeing string | `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` |

If any mutation does NOT cause its expected test to fail, that's a false-green finding.

### Pass 3 — secondary toothlessness gaps

Even if all 4 deletion tests pass, look for second-order false greens:

1. **Integration of `StripCommentsAndStrings` into `LoadAllCallableSources` is not pinned.** The 3 `Strip_*` tests pin the helper. The live cross-file test calls `LoadAllCallableSources`, which calls `StripCommentsAndStrings`. If a future change removed the strip call from `LoadAllCallableSources`, would any test fail? The strip tests still pass (they call the helper directly). The live test would receive unstripped source — but `Data()` and `Error()` have real callers in user partials, so the live test still finds them as called → still green. **Possible finding.**

2. **`Heuristic_OrphanProtectedMethod_IsFlagged` doesn't actually drive the regex.** It calls `IsOrphanMethod` directly with a synthetic source string. The 3 regex assertions pin the regex separately. So the docstring claim "the regex would catch a protected ParamData()" is split across two test pairs. That's reasonable, but worth verifying: the cross-product is **not** tested — i.e., a fully end-to-end test that takes synthetic .Action.g.cs source containing `protected ParamData()`, scans with `PublicOrProtectedMethodDecl` AND drives `IsOrphanMethod`, asserts it's flagged. Pattern A's `Heuristic_*` tests have the same gap — they call `HasReadOf` directly, not the cross-file scan. So precedent supports this layering, but worth flagging if the gap matters.

3. **The widened regex now matches `Data` and `Error` in the live tree.** Coder claims user partials call them. Verify: grep for `Data(` and `Error(` calls in `PLang/App/modules/`. If the only callers are in the same generated file (which the cross-file scan would still find since LoadAllCallableSources includes generated files... wait, does it?). Check: `LoadAllCallableSources` walks `PLang`, `PLang.Tests`, `PLang.Generators`, `PlangConsole`. The generated `*.Action.g.cs` files live under `PLang.Tests/obj/Debug/net10.0/generated/...` — `obj/` is excluded. So generated files are NOT in the scan corpus. User partials in `PLang/App/modules/` ARE in the scan corpus. **If user partials don't call `Data(`/`Error(`, the live test would now flag the framework helpers as orphans.** Need to verify empirically.

4. **`Heuristic_ExemptedMethod_IsNotFlagged` could short-circuit a buggy IsOrphanMethod.** If `IsOrphanMethod` had a bug where it always returned `false` (regardless of input), `Heuristic_OrphanProtectedMethod_IsFlagged` would catch it. But if the bug were "always returns true," then `Heuristic_CalledMethod_IsNotFlagged` would catch it. Both paths are pinned. ✓

5. **The unfiltered cache-hit test (#5) — does it require both caching AND non-empty steps?** Yes: `Assert.That(infoSteps.Length).IsGreaterThan(0)` blocks vacuous pass when `TrackedSteps[ActionInfoTrackingName]` returns an empty array. ✓

## What I'll deliver

1. Run the full C# test suite with and without coverage.
2. Execute the 4 deletion tests (A, B, C, D) and record outcomes.
3. Verify the live cross-file test passes (i.e., the widened Pattern B doesn't surface false orphans for `Data`/`Error` due to insufficient callers in non-generated source).
4. Investigate the secondary gaps (1) and (2) above. If gap (1) is real, file as minor.
5. Write `test-report.json`, `verdict.json`, `coverage.json`, and `summary.md`.

## Questions / risks

- If gap (1) (integration of strip into LoadAllCallableSources is not pinned) is real, do I file it as minor or accept it? Precedent: Pattern A's `Heuristic_*` tests pin `HasReadOf` only, not the integration into `NoGeneratedHandlerDeclaresAnUnreadPrivateField` either. Same architecture, same gap. So if it's a finding, it applies to both. Will lean toward NOT filing — the precedent is clean and the helper is the actual contract surface; the cross-file test is just a corpus scan that exercises the helper.

- If `Data`/`Error` get flagged as orphans by the live cross-file test, that's a real production-like failure shape. Will need to verify empirically — grep the user-partial corpus for `Data(`/`Error(` calls.

## Out of scope

- PLang test infrastructure failures (169/48/5) — pre-existing, not v4-related.
- Re-validating the 6 v3-honestly-closed findings — already verified in tester/v3.
- `Heuristic_*` tests for Pattern A — already verified honest in tester/v3.
- Production-code review — v4 changed only test code.

## Workflow

1. Write this plan, read back to user, await approval.
2. Run baseline test suite, record pass/fail.
3. Execute Pass 1 inspection (already done in this plan).
4. Execute Pass 2 deletion tests A-D.
5. Execute Pass 3 — secondary toothlessness investigation, especially Data/Error caller verification.
6. Write outputs (`test-report.json`, `verdict.json`, `coverage.json`, `summary.md`, update `summary.md` at bot root).
7. Commit and push.
