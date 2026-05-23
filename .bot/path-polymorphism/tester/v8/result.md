# Tester v8 — path-polymorphism

**Verdict: PASS.** All five tester-v7 NEEDS-FIXES are honestly closed; one
follow-up production fix landed (identitydata alias drop) and is also covered.

## Baseline (clean rebuild — stale-binary trap avoided)

- `rm -rf */bin */obj && dotnet build PlangConsole` → 0 errors, 454 warnings
  (unchanged from v7; pre-existing generator nullable warnings).
- `dotnet run --project PLang.Tests` → **2906 / 2906**, 0 failed.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` →
  **Test summary: 204 total, 204 pass, 0 fail, 0 timeout, 0 stale, 0 skipped.**

Deltas from v7: C# +17, plang +1. Matches coder's claimed delta exactly
(four N1 + four N2 + two N3 + seven N4 = 17 C#; rename = +1 plang).

## v7 findings — closed honestly (mutation-tested)

Each finding got a real test, and each test was confirmed to actually guard
the surface by temporarily breaking the production code path and re-running.
All reverts left `git status` clean.

### F4-CARRY — `.test.goal2` renamed → discovered

`Tests/Modules/Condition/Files/FileNotExists/ConditionFileNotExists.test.goal`
now lives on disk under the canonical extension and is in the runner's pass
list (line 1422 of the test log: `[Pass] …/ConditionFileNotExists.test.goal`).
Builds and passes alongside its two callees (`WhenExists.goal`,
`WhenMissing.goal`) which now have generated `.pr` files.

### N1 — `GoalCall.GetGoalAsync` slash-qualified resolution

`PLang.Tests/App/Goals/GoalCallResolutionTests.cs` — four cases on real .pr
files under a temp root.

**Mutation 1**: in `GoalCall.cs:86`, replaced the subPath construction with
the pre-v6 bare-name shape (`.build/{name.ToLowerInvariant()}.pr`).
→ 2 of 4 N1 tests went red (caller-ancestor walk + root-relative). 1.3 (bare
name) and 1.4 (LoadFromFile with pre-set PrPath) correctly stayed green
because they don't depend on the slash branch.

**Mutation 2**: in `GoalCall.cs:154-156`, removed the leaf-match for
slash-qualified Names. → 3 of 4 N1 tests went red (every test that hits
LoadFromFile after a slash-name resolve). N1.3 (bare name) stayed green as
expected.

Both mutations reverted; verified clean.

### N2 — `builder.actions` `Actions` filter parameter

Four new tests in `GetActionsTests.cs` (restrict-to-named, empty list =
no-filter, unknown → empty, case-insensitive).

**Mutation**: in `code/Default.cs:30`, made the filter branch unreachable
(`if (false && …)`) so every call returns the full catalog.
→ 3 of 4 new tests went red (restrict-to-named, unknown→empty,
case-insensitive). The "empty list = no-filter" test correctly stayed green,
which is the desired symmetry. Reverted clean.

### N3 — `Builder.RunAsync` inverted `File.Exists`

`BuilderRunAsyncTests.cs` — two tests; uses `dotnet`'s redirected stdin so
`Console.IsInputRedirected == true` lands on the headless branch
deterministically (no channel mocking).

**Mutation**: in `app/modules/builder/this.cs:113`, dropped the `!` to
re-invert. → both N3 tests went red (one tripped the wrong-branch assertion,
the other got NoAppFound when it shouldn't have). Reverted clean.

### N4 — `Action.ReturnTypeName` coverage

`ModulesDescribeReturnTypeTests.cs` — pins seven canonical rows
(data, bool, path, list&lt;path&gt;, identity, list&lt;identity&gt;) + a sanity
sweep that no row carries a null/empty name.

**Mutation**: in `app/modules/this.cs:397`, returned `"WRONG"` for bare
`Data`. → 1 of 7 went red (`ReturnTypeName_BareData_IsData` — the only test
exercising the bare branch). Reverted clean.

### N5 (process) — `baseline-tests.md`

Present at `.bot/path-polymorphism/coder/v8/baseline-tests.md`. Closed.

## Bonus production fix (commit 02eb2e1ca)

While writing N4, coder discovered Identity was surfacing as `identitydata`
because `RegisterDomainTypes()` was shadowing the class-level
`[PlangType("identity")]` attribute. Coder removed the shadowing register
call and left the method as a no-op hook.

**Audit:** searched all live source for `"identitydata"` —

```
grep -rln "identitydata" --include="*.cs" --include="*.goal" PLang/ PLang.Tests/ Tests/ system/
(no hits)
```

The only remaining occurrences are in frozen LLM-prompt trace JSONs under
`Tests/Error/Multilingual/.build/traces/` — those are test fixtures, not
consumers. The persistence claim ("no persistence depended on the alias")
holds. Pinned by `ReturnTypeName_DataOfIdentity_IsIdentity` in N4 — if
something re-introduces the alias, that test goes red.

## Minor observation — v8-O1 (informational, not a fix request)

`RegisterDomainTypes()` is now an empty body. Nothing fails if a future
contributor re-introduces a `Register("xxx", typeof(X))` line that shadows a
`[PlangType]` attribute on `X` — the regression would only surface
incidentally if X happened to be among N4's seven pinned types. A targeted
unit test ("every `[PlangType]`-marked class resolves to its attribute name,
not to a runtime override of the same type") would close that, but it's
speculative — no concrete bug today. Not blocking.

## Process

Mutations announced once up front in the live session; each revert verified;
final `git status` clean. No commits during testing.

Total wall time: ~6 min (clean rebuild + two suites + five mutation cycles
+ this writeup).
