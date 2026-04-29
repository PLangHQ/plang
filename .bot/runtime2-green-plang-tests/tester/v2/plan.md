# Tester Plan v2 — Re-baseline + Quality Review of Coder v1 Waves 1–4

## Context

Coder v1 (commit `ce0de138` "Waves 1-4" + `0cbbeb1f` bot artifacts) shipped architect's Waves 1–4 on
`runtime2-green-plang-tests`. Coder reports **161 tests: 122 pass / 35 fail / 4 stale** (+13 passes vs my
v1 baseline of 109/48/4). Also +1 C# test (2273/2274, same pre-existing LLM flake).

User instruction: "pull, new from coder, we will not code analyzer yet" — go straight to tester.

This is tester v2 — I am re-baselining AND quality-reviewing the code that landed.

## What I'll do

### 1. Environment check
- `git status`, confirm branch, confirm I'm on `ce0de138`+`0cbbeb1f` parent.
- `dotnet build PlangConsole/PLangConsole.csproj` — fresh rebuild (stale-binary burn twice cost me 10 min on the last branch; not repeating it).
- Sanity check: does `plang.exe` launch cleanly.

### 2. Full C# suite
- `dotnet run --project PLang.Tests` — TUnit on .NET 10.
- Record pass/fail/skipped counts. Note any new flakes.
- Confirm coder's claim of 2273/2274 passing and the single LLM flake.

### 3. PLang test re-baseline
- `plang --test={"format":"junit","timeout":5}` from project root.
- Record counts. Confirm coder's 122/35/4 vs new reality.
- **Compare pass-set delta with my v1 baseline** (`tester/v1/baseline.md`). Three questions per changed test:
  - Which tests flipped `fail → pass`? (expected wins)
  - Did any test flip `pass → fail`? (regressions — critical)
  - Did stale-set shift?

### 4. Coverage on changed C# files
Run coverage scoped to these files coder modified:
- `PLang/App/Actor/this.cs` (W1)
- `PLang/App/Variables/this.cs` (Variables.Set unification)
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` (`%__data__%` no-mutation)
- `PLang/App/modules/event/on.cs` (W2 enum)
- `PLang/App/modules/variable/set.cs` (W3 return-value)
- `PLang/App/modules/http/download.cs` (W4b bytes-only)
- `PLang/App/modules/http/providers/DefaultHttpProvider.cs` (W4b)
- `PLang/App/modules/ui/providers/FluidProvider.cs` (GetAll KVP)
- Cascade call sites: `App/Actor/Context/this.cs`, `App/Debug/this.cs`, `App/Errors/Error.cs`,
  `App/modules/cache/wrap.cs`, `App/modules/loop/foreach.cs`, `App/this.cs`.

Save to `v2/coverage.json`. Per my memory: hunt for 0% lines on the new paths specifically, not just
deltas on whole files. Default/auto-detect paths are least tested (memory `default_paths_least_tested`).

### 5. Quality analysis — this is the main job

For each wave, I apply the deletion test and intent-verification:

**Wave 1 (per-test in-memory System db)**
- Is there a C# test that would catch it if `CreateSettingsStore` silently reverted to the file-backed
  path? Check `PLang.Tests/App/Context/ActorSettingsStoreTests.cs` — coder says the assertion was
  "inverted" (System now also uses in-memory when `Testing.IsEnabled`). Flip: what does the test now
  require that the old test didn't?
- The real proof-of-isolation is integration — the 9 Identity + 9 Signing tests that used to fail with
  "Identity 'testSigner' already exists" should now pass. That's only a valid probe if those 18 tests
  were actually previously failing with that specific error.
- Check: `App.Id` scoping ("`Cache=Shared` merges by DataSource name" per coder's summary) — is there a
  test that would catch it if somebody simplified the code by dropping the `App.Id` from the connection
  string? The latent bug the coder caught is exactly the kind of thing that should be guarded.

**Wave 2 (`event.on.Type` enum)**
- Coder says they "removed a C# test that exercised the runtime error path" because compile-time
  enforcement replaces it. Confirm — does `EventHandlerTests.cs` still exercise something meaningful
  after the removal, or is it now testing a tautology? Read the diff, apply deletion test.
- Compile-time enforcement is only meaningful if the builder actually emits the enum value correctly.
  Is there any test that goes .goal → `plang build` → .pr → run and verifies event firing? If no, the
  runtime removal may have handed off to an untested path.

**Wave 3 (Variables.Set unification + variable.set returns stored Data)**
This is the riskiest change — three methods collapsed to one, and `Action.RunAsync` stopped mutating
`result.Name`. Test questions:
- `VariablesTests.cs` — coder renamed Put → Set. But does the new Set path test **aliasing without
  clone** specifically? The previous `Put(Data)` stored by `value.Name`; the previous `Set(string,
  object)` cloned if names differ. The new behavior: `Set(string, object)` where object is Data stores
  the Data as-is under the name. If I delete the "store-as-is" assertion, does any test fail?
- `%__data__%` no-rename-mutation — this is the subtle W3 fix. Is there a test asserting that when
  `handler` returns `Data{Name="foo"}` and `Action.RunAsync` publishes to `%__data__%`, the Data's
  `Name` is STILL "foo"? Without that, a future refactor reintroducing `result.Name = "__data__"`
  goes silent.
- `variable.set` now returns the Value instead of empty `Data.Ok()`. Is there a C# test for `set.cs`
  that checks `Data.Value` on return, not just `Success`? Coder's own writeup says "ReturnMapping,
  GoalCallReturn" integration tests cover this — but integration tests only cover the end-to-end; they
  don't localize regressions.
- `GetAll()` now returns `IEnumerable<KeyValuePair<string, Data>>`. Any test asserting a specific KVP
  under a specific key? `FluidProvider` was the one real call site — does a test verify FluidProvider
  resolves `%name%` against the dictionary key vs `data.Name` after the contract change?

**Wave 4b (http.download split)**
- Coder removed `SaveTo` and `IfExists` from `download.cs`. `DownloadActionTests.cs` dropped 99 lines —
  confirm what stayed is a real bytes-in-MemoryStream assertion, not just `Data.Ok()`.
- Edge cases for download: 4xx/5xx response, redirect, empty body, TimeoutInSec honored. Were any
  preserved?

**Wave 4c (builder prompt — 5 rules)**
- No direct C# test for prompt rules. Observability is at the build → .pr level only. I'll check a
  handful of .pr files regenerated post-wave for evidence the rules took hold:
  - `Signing/Expired/` and `Signing/TimedOut/` — coder's v1 triage blamed `timeout.after.after` double
    suffix. Did the .pr regenerate without it? (Note: coder reverted the full rebuild — so most .pr
    files are NOT regenerated. This is load-bearing — the prompt rules are effectively dormant until
    someone rebuilds.)
  - `Http/DownloadFile/` — if not rebuilt, it still has the old `SaveTo` form and can't pass. But W4
    rebuild was reverted...

**This is the big red flag I want to surface explicitly.** Coder summary says:
> Rebuild regressed 38 previously-green tests... Reverted all `.pr` changes (`git checkout -- Tests/`).

So the +13 win comes entirely from the C# changes (W1 + W3). The prompt rules (W4c) and the
`http.download` API change (W4b) are in the code but have ZERO observable effect on the test suite
right now, because every .pr in the repo still contains old `SaveTo` / old modifier shape / old
arithmetic-on-RHS patterns. The builder landed; nothing uses it.

Status check with user: is this an acceptable state for "Wave 4 done", or does it demand another pass?
(I'll flag but not decide.)

### 6. PLang .test.goal existence check
New module or action behavior usually wants a PLang integration test. W1/W2/W3/W4 are mostly
behavioral changes to existing modules — extensively covered already by Tests/ .test.goal files. I'll
note which Tests/ folders DIDN'T rebuild post-W4 and therefore can't exercise the new contracts.

### 7. Verdict + test-report.json
- Write findings to `test-report.json` at branch root (shared).
- Write `verdict.json` with `pass` or `needs-fixes`.
- Summary + session files.

## What I'll NOT do

- **No code review of correctness of the C# changes.** I only review test quality. If I think a
  handler has a bug that the tests don't cover, I report "test does not cover X" — I don't report
  "handler is wrong". (Memory `feedback_tester_role.md`.)
- **No architectural opinions on Option A vs B for Variables.Set.** That's Ingi's call + coder's
  implementation. I verify tests, not design.
- **No rebuild of Tests/.** If coder's rebuild reverted, the state is what it is. I report the gap.

## Stop-and-escalate conditions

- Regression: any previously-green test (either in my v1 pass-set or the C# suite) flipped to fail
  → stop, surface to user before continuing.
- Critical false-green: any new test that would pass if the code were deleted → surface in findings.
- Coverage cliff on new logic paths (e.g. `App.Id`-scoping branch is 0%) → finding.

## Output

Under `.bot/runtime2-green-plang-tests/tester/v2/`:
- `plan.md` — this file
- `coverage.json` — scoped coverage on modified files
- `result.md` — quality findings in detail (one per wave)
- `summary.md` — session summary
- `verdict.json` — pass / needs-fixes
- `changes.patch` — diff excluding .bot/

Shared at branch root: `test-report.json`.

## Awaiting approval

Approve this plan and I'll execute.
