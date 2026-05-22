# Tester v5 — result

**Branch:** path-polymorphism · **Tested:** 2026-05-22 · **Commit:** `6d6853293`
**Reviewing:** coder v5 (codeanalyzer v2 N1–N3 response).

## Test run (clean rebuild — stale-binary trap honoured)

| Suite | Result |
|---|---|
| C# `dotnet run --project PLang.Tests` | **2882 / 2882 pass**, 0 fail, 0 skip |
| plang `cd Tests && plang --test` | **203 / 203 pass**, 0 fail, **0 stale** |
| Build | clean — 0 errors, 447 warnings (pre-existing nullable noise) |

No regressions vs the coder's v5 baseline (C# 2881→2882, plang 203/203). Both
numbers reproduce exactly. The suite is **green** — but green is where I start,
not where I stop.

## Verdict: NEEDS-FIXES

The path-polymorphism C# suite is, on the whole, strong: the contract base
(`PathSchemeContractTests`) asserts verb round-trips, lifecycle, and permission
refusal with `Error.Key`/`StatusCode` — real oracles applied uniformly to both
schemes. The F3 fix has a proper two-branch oracle in `DefaultEvaluatorTests`.
N1 got a dedicated, genuine test.

But four gaps let subtly-wrong code through. Three of them sit on exactly the
v5 review-driven changes — the highest-risk place for a missing test.

---

## Finding 1 — vacuous test: `Assert.That(true).IsTrue()`  *(major, false-green)*

`PLang.Tests/App/Types/PathTests/HandlerShapeTests.cs:153`

```csharp
[Test] public async Task FileModule_PlangBehaviour_UnchangedFromProgramPerspective()
{
    await Assert.That(true).IsTrue();
}
```

A `[Test]` named as if it verifies the file module's program-visible behaviour
is unchanged. It verifies nothing — it asserts a literal. It contributes `+1` to
the green count and `0` to actual coverage. Deletion test: delete the whole
method, nothing is lost.

**Impact:** any regression in "file module behaviour from the program's
perspective" — the exact thing the name claims to guard — sails through. It is a
named placebo in the headline test file of the branch.

**Fix:** either delete it, or make it real — drive a `file save`/`file read`
round-trip through the `Read`/`Save` handlers and assert the bytes, the way
`HandlerShapeTests.ReadHandler_Delegates_To_PathReadText` already does.

---

## Finding 2 — N2 (`path.Equals` / `GetHashCode`) has zero coverage  *(major, missing-coverage)*

`PLang/app/types/path/this.cs:167-175` — codeanalyzer v2 N2 switched
`Equals`/`GetHashCode` from hard-coded `OrdinalIgnoreCase` to `RootComparison`
(Ordinal on Linux). The whole point: `/srv/x` and `/SRV/x` are distinct files
on Linux and must compare unequal / not hash-collide.

`grep -rn "Equals\|GetHashCode" PLang.Tests/App/Types/PathTests/` → **nothing.**
No test constructs two paths and compares them. Deletion test: revert line 169
to `OrdinalIgnoreCase`, or delete the `Equals` override entirely (back to
reference equality) — **the whole suite stays green.**

**Impact:** the N2 fix is unguarded. A future refactor that re-hard-codes
`OrdinalIgnoreCase`, or drops the override, silently reintroduces the
Linux case-collision N2 was filed to kill.

**Fix:** add to `PathAbstractTests` (or a new `PathEqualityTests`):
- two `FilePath`s with the same absolute path → `Equals` true, equal hash;
- two differing only in case → on Linux **not** equal, **distinct** hash; on
  Windows equal (gate on `OperatingSystem.IsWindows()`, mirroring
  `RootComparison`);
- `path.Equals(string)` overload — both the matching and the case-variant case.

---

## Finding 3 — N3 (`assert` truthiness of a path) has zero coverage  *(major, missing-coverage)*

`PLang/app/modules/assert/code/Default.cs:144-150` — N3's `ResolveTruthy` added
a branch: an `IBooleanResolvable` value (a path) routes through
`data.ToBooleanAsync()`. The source comment claims outright:
*"`assert %path% is true` is thus correct."*

`AssertTests.cs` was **not touched** since the merge base. Its `IsTrue`/`IsFalse`
tests cover `bool`, number, `null`, string — all plain values that hit
`IsTruthy`. **None passes a path** (or any `IBooleanResolvable`) as the asserted
value.

Deletion test: delete the `if (data.Value is IBooleanResolvable)` branch. A path
then falls through to `IsTruthy(object)`, whose final line is `return true`
(`Default.cs:159`) for any non-null object. So `assert %missing_path% is true`
**wrongly passes** — the precise F3 bug class ("non-null object `== true` →
always true"). No test goes red.

**Impact:** the assert-side of the F3/N3 fix is unproven. The condition-side
(`if %path% exists`) has its two-branch oracle in `DefaultEvaluatorTests`; the
assert-side has nothing. The source comment asserting correctness has no test
behind it.

**Fix:** add two `AssertTests`: `IsTrue` of a `Data` wrapping a `FilePath` to an
**existing** in-root file → `result.Success` true; `IsTrue` of a `FilePath` to a
**missing** file → `result.Success` false with `Error is AssertionError`. Mirror
for `IsFalse`.

---

## Finding 4 — F3 negative branch has no plang `.goal` test  *(major, missing-plang-test)*

The headline semantic of this branch is the F3 fix: `if %path% exists` reflects
*actual* existence. At the plang level it is exercised only by
`Tests/Modules/Condition/Files/FileExistsSubSteps/ConditionFileExistsSubSteps.test.goal`:

```
- file save "testfile.txt", content "hello"
- if testfile.txt exists
    - set %innerRan% = "yes"
- assert %innerRan% equals "yes"
```

This tests the **true** branch only — the file is created first, so the inner
block runs. Deletion test: revert the F3 fix so `if X exists` is always-true —
**this test still passes** (the file does exist; the inner block runs either
way). It cannot catch a regression of the bug it appears to cover.

The missing case: a file that does **not** exist → `if X exists` → inner block
must **not** run. That is the side F3 actually broke. C# covers it
(`DefaultEvaluatorTests.IfExists_PathToMissingFile_IsFalse`), so this is a
plang-layer gap, not a total blind spot — but the branch's headline fix deserves
a plang regression guard for its failing side, and no plang `.goal` files were
added for the whole feature.

**Fix:** add a sibling `.goal` — `if not-here.txt exists` with an inner
`set %innerRan%="yes"`, then `assert %innerRan% equals "no"`. One file; closes
the plang-layer hole.

---

## Finding 5 — `File.test.goal` `/ exists` is a weak assertion  *(minor, weak-assertion)*

`Tests/Modules/File/File.test.goal`:

```
- check if file 'test_output.txt' exists, write to %info%
- assert %info% is not null
```

Post-F3, `file.exists` returns a path object that is **always** non-null
regardless of whether the file is there. `assert %info% is not null` therefore
passes for a present *and* an absent file — it verifies "exists returned an
object", not "exists reported the right answer". Pre-existing test, but the F3
change makes its weakness actively misleading. Tighten to `assert %info% is
true` (or read existence semantically).

---

## Finding 6 — `PLangFileSystem_AbsentFromProductionAssembly`: stale comment + single-namespace probe  *(minor, weak-assertion)*

`HandlerShapeTests.cs:35-45`. The test body asserts
`AppAssembly.GetType("app.types.path.Default.PLangFileSystem")` is null. Its
comment says the assertion *"fails by design until that migration lands — an
honest red, not a stub."*

The comment is stale. The wrapper layer **is** gone — `grep` finds no
`PLangFileSystem` class and no `System.IO.Abstractions` reference anywhere under
`PLang/`; the name survives only in code comments. The test is **green**, not the
"honest red" the comment claims.

Separately, the assertion checks one hard-coded `namespace.Name` pair. The sister
test `NoProductionType_References_IFile` does a real assembly-wide scan; this one
does not — if a wrapper class were reintroduced under any other namespace, this
test would still pass. Low impact today (wrapper genuinely absent), but the
comment must be corrected so the next reader is not misled, and the assertion
should scan by simple name like its sibling.

---

## Process note (not a finding)

The coder recorded the v5 baseline inline in `coder/v5/plan.md` rather than as a
`coder/v5/baseline-tests.md`. The baseline data is present and accurate, so this
is not raised as a finding — noting it so the next tester knows where to look.

## What is genuinely solid (so the coder knows what not to touch)

- `PathSchemeContractTests` — one contract, both schemes, real oracles
  (round-trip equality, lifecycle, `Error.Key=="PermissionDenied"` +
  `StatusCode==403`). This is the model for the rest.
- `DefaultEvaluatorTests.IfExists_*` — both branches, real filesystem, the F3
  oracle done right.
- `HandlerShapeTests.FilePath_AsBooleanAsync_OutOfRoot_DeniedPermission_AnswersFalse`
  — the N1 oracle: a file that genuinely exists out-of-root answers `false`
  under a denying channel. Exactly the right test.
- `FileHandlerTests.Read_UnregisteredSchemePath_SurfacesTypedError_NotNre` —
  asserts `Error.Key=="SchemeNotRegistered"`, not just `Success==false`.
- `SchemeRegistryTests` — typed-exception assertions, multi-app isolation,
  case-insensitive scheme match, Windows-drive-letter disambiguation.
