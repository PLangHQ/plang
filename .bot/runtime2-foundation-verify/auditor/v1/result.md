# Auditor result — runtime2-foundation-verify v1

## Summary

Pass. Branch is narrow (architect docs + three regression-pin `.test.goal` files) and the work matches its brief. Clean rebuild + `plang --test` from `Tests/` reports **202 total, 202 pass, 0 fail, 0 timeout** — the three new tests included. No production C# was touched. No cross-file contracts to break here.

One minor finding (below) about Test 3's pin strength. Not a blocker.

## What I verified

### 1. Branch scope

`git log runtime2..HEAD` shows two commits — architect + coder. Diff against `runtime2..HEAD` includes apparent "deletions" of `Documentation/v0.2/app-tree.md` and `reminisce-2026-05-11.md`; these are **not** deletions performed by this branch — they were added on the sibling `runtime2-channels` branch (commits 199b4997 and c04cb551) **after** the merge-base (200e8e93). Confirmed via `git log --all` per-file. This is a base-divergence artifact, not branch work.

Actual production additions on this branch:
- `Tests/Errors/GoalFirstReturnsRecoveryValue.test.goal`
- `Tests/Errors/RetryFirstReturnsRecoveryValue.test.goal`
- `Tests/Errors/MultiActionRecoveryLastActionPropagates.test.goal`
- Three matching `.build/*.test.pr` files
- `Documentation/Runtime2/todos.md` edits (audit header + resolutions + 2 new entries)

No `PLang/` or `PLang.Tests/` source files modified. Zero C# diff.

### 2. Code-vs-test correspondence

Re-read `PLang/App/modules/error/handle.cs:90-185`:

- **Lines 109-114** — `Order=GoalFirst` branch: runs recovery, on success sets `erroredCall.Handled = true` and returns `recoveryResult`. Test 1 (`GoalFirstReturnsRecoveryValue`) pins this with `Order=GoalFirst` + assertion on the recovery's side-effect + assertion that `%!error%` is null. The two assertions sit in the same code branch as the architect's brief intends.

- **Lines 120-131** — `Order=RetryFirst` branch: retry runs first, on its failure recovery runs, on recovery success sets Handled=true and returns `recoveryResult`. This is the path the 2026-04-27 symmetry fix repaired (previously returned `Ok()`). Test 2 (`RetryFirstReturnsRecoveryValue`) uses `Order=RetryFirst, retry=1` and asserts the same two layers. If a regression replaces this branch with `return Ok()` (the old bug), `%!error%` would surface because `Handled` wouldn't be set. Symmetry pinned correctly.

- **Lines 167-185** — `RunRecovery` foreach: walks `actions`, captures `last = await action.RunAsync(...)`, returns `last`. Test 3 (`MultiActionRecoveryLastActionPropagates`) drives this with three sequential `variable.set` actions. All three side-effects are asserted plus `%!error%` is null.

### 3. Builder output

Spot-checked `Tests/Errors/.build/goalfirstreturnsrecoveryvalue.test.pr`:
- Step 0: `error.throw` with `Message=boom`, modifier `error.handle` with `Order=GoalFirst` and `Actions=[variable.set Name=%content% Value=from-recovery]`.

Counted `"action": "set"` occurrences in `multiactionrecoverylastactionpropagates.test.pr` — exactly 3, matching the chain. Builder mapped the .goal text to the brief's intended shape.

### 4. Test execution

Clean rebuild: `rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj PLang.Tests/bin PLang.Tests/obj PLang.Generators/bin PLang.Generators/obj && dotnet build PlangConsole`. Then `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

Result on this branch (no `Tests/Code/HelloPlain.test.goal` — that file lives on `runtime2-channels`, not on this branch base):

```
Test summary: 202 total, 202 pass, 0 fail, 0 timeout, 0 stale, 0 skipped
  [Pass] Errors/RetryFirstReturnsRecoveryValue.test.goal (144ms)
  [Pass] Errors/MultiActionRecoveryLastActionPropagates.test.goal (20ms)
  [Pass] Errors/GoalFirstReturnsRecoveryValue.test.goal (15ms)
```

Stale-binary trap avoided by full clean rebuild. Coder's reported numbers (203/202pass/1fail HelloPlain) reflect their working tree at the time; on the clean branch state I see, the count is 202/202pass/0fail.

## Findings

### F1 — Test 3 pin is weaker than intended (minor)

**File:** `Tests/Errors/MultiActionRecoveryLastActionPropagates.test.goal`
**Severity:** minor
**Category:** contract / test
**Missed by:** none (no prior reviewer)

The architect's brief said this test should pin "the chain's final returned Data is what `RunRecovery` returns" (line 184 of `handle.cs` — `return last`). The implementation uses three sequential `variable.set` actions on `%first%`, `%second%`, `%content%`. All three assertions verify side-effects landed (proving the chain executed in order) and `%!error%` is null (proving the success-return branch was reached).

What the test does **not** distinguish: whether `RunRecovery` returns `last` (the third action's Data) vs returns `Ok()` after the loop. Since `variable.set` returns `Ok()` with no Value, both behaviors produce identical observable outcomes — the side-effects land either way, `%!error%` is suppressed either way (the suppression depends on `recoveryResult.Success`, not on its Value). A future refactor that replaces `return last` with `return global::App.Data.@this.Ok()` would still pass this test.

The architect's original recipe used `read 'fallback.txt', write to %content%` — `file.read` returns Data with a populated Value, so `%content%` would only carry the recovery's value if `RunRecovery` actually returned `last`. That recipe pins the propagation; the implemented chain pins only execution.

**Impact:** Low. The two regressions a coder would realistically introduce here (drop the `Handled=true` set, swap `recoveryResult` for `Ok()` in the outer if-branch) are pinned by tests 1 and 2. A regression specifically inside `RunRecovery` that returns `Ok()` instead of `last` would slip past Test 3 — but only mattered if a *downstream consumer* observes the recovery's terminal Value, and no such PLang surface currently exists (no `read 'x', on error read 'y', write to %v%, returns %v%` use site is asserted by these tests).

**Suggestion (don't block on this):** Either accept as a known-limitation regression pin (assertion of chain ordering, not Value propagation) — and note it in the test file's `/` comment so a future reader doesn't assume more pin than is there — or augment the test with one step like `set %step% = (the outer step's return)` once a `step returns` surface is available, to genuinely lock the Value propagation. Folding this into a future module-port test (when an actual recovery-Value consumer lands) would be the cheapest moment to upgrade.

### Other notes (not findings)

- **Trigger substitution from `read 'missing.txt'` to `throw error "boom"`** — coder defended this and I agree. Removes filesystem dependency, same recovery semantic, matches `Tests/App/CallStack/*.test.goal` idiom.
- **Placement under `Tests/Errors/`** flat (rather than `Tests/Errors/RecoveryValue/` subfolder) — consistent with sibling `Tests/App/CallStack/HandledFlagSetWhenRecoverySucceeds.test.goal`. Architect left the choice to test-designer/coder; coder's call holds up.
- **`Tests/Errors/SimpleRecovery/Start.goal` shape** (one Start.goal + one .test.goal asserting post-state) was correctly identified as not actually carving post-state in the existing fixture, so coder's self-contained `.test.goal` files are an upgrade, not a deviation.

## Reviews assessed

- **codeanalyzer** — none on branch; n/a.
- **tester** — none on branch; n/a. (I ran the test suite myself.)
- **security** — none on branch; n/a. (Test-only branch, no attack surface.)

## Verdict

PASS. One minor finding (F1) for future awareness; not a blocker for hand-off to docs.
