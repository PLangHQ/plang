# Tester v7 — result

**Branch:** path-polymorphism · **Tested:** 2026-05-23 · **Commit:** `d30f84c77`
**Reviewing:** coder v6 (slash `goal.call` resolution, `builder.actions` Actions
filter, inverted `File.Exists` fix) + v7 (doc-only). Also covers the
intervening typed-returns sweep (~25 commits, 69 handlers, 9 provider
interfaces) which codeanalyzer v4 already gave CLEAN-modulo-docs.

## Test run (clean rebuild — stale-binary trap honoured)

| Suite | Result |
|---|---|
| C# `dotnet run --project PLang.Tests` | **2889 / 2889 pass**, 0 fail, 0 skip |
| plang `cd Tests && plang --test` | **203 / 203 pass**, 0 fail, **0 stale** |
| Build | clean — 0 errors, 454 warnings (pre-existing nullable noise) |

C# went 2882 → 2889 since v5 (+7 — `PathEqualityTests.cs` (4) + `AssertTests`
N3 tests (3) + miscellaneous from the typed-returns sweep). plang held at
203/203. Better than codeanalyzer v4's snapshot (their lone plang fail was an
external LLM 503; not reproduced here).

No regressions. The suite is green — but green is where I start.

---

## Status of tester v5's six findings

| ID | v5 finding | v7 status | Evidence |
|---|---|---|---|
| F1 | vacuous `Assert.That(true).IsTrue()` in `HandlerShapeTests` | **resolved** | grep across the suite finds zero `Assert.That(true).IsTrue()`. |
| F2 | `path.Equals` / `GetHashCode` zero coverage | **resolved** | `PLang.Tests/App/Types/PathTests/PathEqualityTests.cs` — 4 tests including the case-variant Linux-vs-Windows oracle. Mutation test: reverted `RootComparison` → hard-coded `OrdinalIgnoreCase` at `PLang/app/types/path/this.cs:169-175`; `FilePath_CaseVariant_HonoursRootComparison` and `FilePath_EqualsString_ComparesAbsolute` went red. **Honest red.** Reverted. |
| F3 | `assert` path-truthiness branch zero coverage | **resolved** | `AssertTests.cs:193-260` — 4 tests over `IsTrue`/`IsFalse` × existing/missing paths. Mutation test: deleted the `IBooleanResolvable` branch at `Default.cs:147-148`; `IsTrue_PathToMissingFile_Fails` and `IsFalse_PathToExistingFile_Fails` (and 2 others) went red. **Honest red.** Reverted. |
| F4 | negative-branch plang `.goal` for `if X exists` | **NOT resolved — and disguised** | A negative-branch `.test.goal2` exists at `Tests/Modules/Condition/Files/FileNotExists/ConditionFileNotExists.test.goal2` with exactly the right shape (`if missing_file_abc123.txt exists, call WhenExists, else call WhenMissing` → asserts `%result%=="missing"`). The `.goal2` extension is **not picked up by `plang --test`** (the runner discovers `.test.goal`, not `.test.goal2`). The file is parked. The original gap stands. See finding F4-CARRY below. |
| F5 | `File.test.goal` `/ exists` weak `is not null` | **resolved** | now `assert %info% is true` (`Tests/Modules/File/File.test.goal:7`). |
| F6 | `PLangFileSystem_AbsentFromProductionAssembly` stale comment | **resolved** | the test is gone from `HandlerShapeTests.cs` (grep confirms). |

**Net:** five of six closed honestly. F4 is the only carry-forward.

---

## Verdict: NEEDS-FIXES

Five new findings. None are false-greens of the v5 variety (the suite is
genuinely stronger). All five are *missing-coverage* on surfaces coder v6
created and the typed-returns sweep introduced — exactly the high-risk spots
because they're brand-new and changed in this cycle.

---

### F4-CARRY (major, missing-plang-test) — negative-branch `if X exists` parked as `.goal2`

`Tests/Modules/Condition/Files/FileNotExists/ConditionFileNotExists.test.goal2`

```
Start
/ Test: multi-action condition — file doesn't exist, else branch runs
- if missing_file_abc123.txt exists, call WhenExists, else call WhenMissing
- assert %result% equals "missing", "GoalIfFalse should run when file doesn't exist"
```

This is exactly the test v5 F4 asked for. It is **not discovered** by
`plang --test`: the runner scans for `*.test.goal` and ignores `.goal2`. The
`.build/` folder beside it contains only one `.pr` (`whenexists.pr` — the
callee), never the harness goal. So we have a test file that looks tested but
isn't.

There are ~40 `.test.goal2` files across the suite — `.goal2` is a recognised
"parked" convention. Once a coder writes one, it visually counts as coverage in
a directory listing while contributing zero to the actual green count. That is
the structural shape of a false green at the filesystem level.

**Impact:** the headline plang-layer regression test for the F3 fix's failing
branch is inert. A revert of F3 (where `if X exists` always recurses) is still
not caught by any plang `.test.goal` — only C#
`DefaultEvaluatorTests.IfExists_PathToMissingFile_IsFalse`.

**Fix (one-line for the coder):**
`mv Tests/Modules/Condition/Files/FileNotExists/ConditionFileNotExists.test.goal2 \
    Tests/Modules/Condition/Files/FileNotExists/ConditionFileNotExists.test.goal`,
delete the stale `whenexists.pr`, rebuild from project root. (Separate
discussion for Ingi/docs: prune or convert all 40 `.goal2` files so this trap
goes away — out of scope here.)

---

### N1 (major, missing-coverage) — `GoalCall.GetGoalAsync` slash-qualified resolution has no unit test

`PLang/app/goals/goal/GoalCall.cs` — coder v6's core fix. Slash-qualified
`Folder/Leaf` names now resolve as `{folder}/.build/{leaf}.pr`, walking the
caller's ancestor folders, then root, then context. `LoadFromFile` was also
changed to leaf-match a slash-qualified `Name` against the loaded goal's own
unqualified `Name`.

`grep -rn "GetGoalAsync\|GoalCall" PLang.Tests/` finds plenty of `new GoalCall
{ Name = ... }` constructions but **none** with a slash, and zero direct tests
of the four-tier resolution order (caller-ancestor → context → root → leaf
match). The fix's only oracle is "self-rebuild of the system/builder tree
succeeded" — a smoke test, not a unit test.

**Impact:** the resolution order is the kind of code that gets refactored once
the dispatcher grows another caller pattern. The tier walk could be reordered
or a tier silently dropped; tests stay green; the next bot does a self-rebuild
and discovers it the hard way.

**Fix:** `PLang.Tests/App/Goals/GoalCallResolutionTests.cs` with the four
canonical cases:
1. slash name resolved by caller-ancestor walk (caller in `BuildGoal/`, target `BuildStep/Start` → finds `BuildStep/.build/start.pr`);
2. slash name resolved by root-relative when the caller has no matching ancestor;
3. bare name unchanged from prior behaviour (regression guard);
4. `LoadFromFile` leaf-match — pre-resolved `prPath` + `Name="BuildGoal/Start"` loads against a `.pr` whose own `Name` is just `"Start"` (the bug coder v6 fixed in step).

---

### N2 (major, missing-coverage) — `builder.actions`'s new `Actions` filter parameter is unguarded

`PLang/app/modules/builder/actions.cs` (coder v6) added
`Data<List<string>>? Actions` so the LLM can restrict the returned catalog to a
specific set of `module.action` names. The plang goal
`os/system/builder/BuildStep/Start.goal:16` was updated to pass it.

`PLang.Tests/App/Modules/builder/GetActionsTests.cs` has 6 tests; **none sets
`Actions = …`**. The filter path in `Builder.Actions` (apply filter when set,
return full catalog when null/empty) has zero test coverage.

**Impact:** the filter could no-op silently (e.g. case-insensitive match
broken, the filter accidentally treating an empty list as "filter to nothing"
instead of "no filter"). The plang side wouldn't notice — the LLM gets a
larger catalog than asked for, picks something, and the build still passes
because the catalog is a superset.

**Fix:** two test cases in `GetActionsTests` —
1. `Actions = ["file.read", "file.save"]` → result contains exactly those two and nothing else;
2. `Actions = []` (empty list) → result is the full catalog (semantic = "no filter");
3. `Actions = ["nonexistent.action"]` → result is empty, no error.

---

### N3 (minor, missing-coverage) — inverted `File.Exists` fix has no regression guard

`PLang/app/modules/builder/this.cs:113` — coder v6 flipped
`File.Exists(appPrPath) && !_app.Create` → `!File.Exists(appPrPath) &&
!_app.Create`. Validated only by "self-rebuild now runs without
`--app={"create":true}`".

`PLang.Tests/App/Modules/builder/AppTests.cs` exercises `_app.Load()` and
`_app.Save()` but **never goes through `Builder.@this.RunAsync()`**. Flip the
`!` back: the suite stays green; existing apps once again refuse to build
without the workaround flag.

**Impact:** the exact bug coder v6 fixed comes back silently. Low blast radius
(human notices the next time they run a build), but the fix earned a guard.

**Fix:** in `AppTests.cs` or a new `BuilderRunAsyncTests.cs`:
1. existing app marker (`.build/app.pr` present) + `Create=false` →
   `RunAsync()` proceeds (doesn't hit the `NoAppFound` / prompt branch);
2. missing app marker + `Create=false` + stdin redirected → returns
   `NoAppFound` `ServiceError`.

---

### N4 (minor, missing-coverage) — `Action.ReturnTypeName` (typed-returns sweep) has zero tests

`PLang/app/goals/goal/steps/step/actions/action/this.cs:301-308` — new public
`string? ReturnTypeName` property. Per the docstring, it's "PLang name of the
action's return type T (when `Run()` returns `Task<Data<T>>`)" and "Compile.llm
uses this to choose the Type for a trailing `variable.set` after a `write to
%x%`."

`grep -rn "ReturnTypeName" PLang.Tests/` → **zero hits.**

**Impact:** this is the LLM's signal for which CLR type a `write to %x%`
result will produce. A wrong/null value here cascades into the exact build-time
mis-compile the builder bot reported in Class 2/3 (the LLM picks `string`
because `ReturnTypeName` was null where it should have been `path` or
`List<path>`). The whole typed-returns sweep was justified by this signal —
and the signal isn't tested.

**Fix:** a `DescribeReturnTypeTests` (sibling of `GetActionsTests`) over a
representative slice:
- `Task<Data>` (bare) → `ReturnTypeName == null`;
- `Task<Data<bool>>` → `"bool"`;
- `Task<Data<path>>` → `"path"`;
- `Task<Data<List<path>>>` → `"list<path>"` (or whatever the PLang mapping is — verify against `TypeMapping`).

---

### N5 (process, not a test-quality finding) — coder skipped `baseline-tests.md` again

No `baseline-tests.md` in `coder/v6/` or `coder/v7/`. The character file
requires it; v5 had the same shortcut (recorded inline in `plan.md`). v6/v7
embed baselines in `plan.md` / `result.md`, so the data is recoverable, but
the convention exists so the tester can mechanically diff without parsing
prose. **Raising once more as a friendly nudge — not as a coder finding to
fix.**

---

## What is genuinely solid (so the coder knows what not to touch)

- `PathSchemeContractTests` — both schemes, real oracles (round-trip,
  lifecycle, `Error.Key=="PermissionDenied"` + `StatusCode==403`). Still the
  model.
- `PathEqualityTests` (new) — including the Linux-vs-Windows case-variant
  oracle gated on `OperatingSystem.IsWindows()`. Mutation-verified red.
- `AssertTests` N3 block (new, `:193-260`) — the `IBooleanResolvable` branch
  is now real: existing-vs-missing × `IsTrue`/`IsFalse`. Mutation-verified red.
- `DefaultEvaluatorTests.IfExists_*` — both branches, real filesystem.
- `HandlerShapeTests.FilePath_AsBooleanAsync_OutOfRoot_DeniedPermission_AnswersFalse`
  — N1 oracle from v5; still the right shape.
- `FileHandlerTests.Read_UnregisteredSchemePath_SurfacesTypedError_NotNre` —
  asserts `Error.Key`, not just `Success==false`.

## Mutation log (announced + reverted, `git status` clean)

1. `PLang/app/types/path/this.cs:169-175` → reverted `RootComparison` to
   `OrdinalIgnoreCase`. 2 tests in `PathEqualityTests` went red. Reverted.
2. `PLang/app/modules/assert/code/Default.cs:147-148` → deleted the
   `IBooleanResolvable` branch. 4 tests in `AssertTests` went red. Reverted.

`git status` clean after revert (only `.bot/` untracked).
